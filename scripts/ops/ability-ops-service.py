#!/usr/bin/env python3
"""인증된 기술·마법 연출 운영 API와 로컬 미리보기 정적 서버.

클라우드에서는 nginx가 HTTPS만 맡고 모든 요청을 127.0.0.1의 이 프로세스로 넘긴다.
로그인은 여기서 본다 — 페이지 안 로그인 화면 → 서명 쿠키(브라우저 Basic 팝업 없음, 사용자 2026-09-30).
스크립트용 Basic 헤더도 받는다. 페이지에서 바꾼 값은 모두 data 폴더에 두고 changes.jsonl 에 쌓는다.
명세: plans/ops-login-and-backup.md
"""
import argparse
import base64
import hashlib
import hmac
import http.client
import json
import mimetypes
import os
import secrets
import sys
import sqlite3
import tempfile
import threading
import time
import urllib.request
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import quote, unquote, urlencode, urlsplit, parse_qs

sys.path.insert(0, str(Path(__file__).resolve().parent))
from activity_store import ActivityStore


class InvalidRequest(ValueError):
    pass


class RevisionConflict(RuntimeError):
    pass


def atomic_write(path: Path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary = tempfile.mkstemp(prefix="." + path.stem + "-", dir=path.parent)
    try:
        with os.fdopen(handle, "w", encoding="utf-8") as stream:
            json.dump(data, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(temporary, 0o600)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def log_change(log: Path, kind, key, value, failed=None):
    """바꾼 값을 한 줄씩 쌓는다 — 백업·되돌리기용. 지우지 않는다. failed 가 있으면 앞 줄의 값을 못 바꿨다는 줄."""
    record = {"at": datetime.now(timezone.utc).isoformat(), "kind": kind, "key": key, "value": value}
    if failed is not None:
        record["failed"] = failed
    with open(log, "a", encoding="utf-8") as stream:
        stream.write(json.dumps(record, ensure_ascii=False) + "\n")


def logged_write(log, kind, key, value, write):
    """감사기록을 먼저 쓰고 값을 바꾼다 — 기록을 못 쓰면 값은 그대로 둔 채 OSError 를 올린다(기록 없는 변경이 없게).
    값 교체가 실패하면 실패 줄을 남기고(최선) 다시 올린다."""
    if log:
        log_change(log, kind, key, value)
    try:
        write()
    except OSError as error:
        if log:
            try:
                log_change(log, kind, key, value, failed=str(error))
            except OSError:
                pass
        raise


class StateStore:
    """페이지에서 바꾸는 나머지 값(지금은 아이템 한글 이름). 이름 하나 = 파일 하나. 새 값이 생기면 NAMES 에 더한다."""
    NAMES = {"item-names"}
    LIMIT = 256 * 1024

    def __init__(self, folder: Path, log: Path):
        self.folder = folder
        self.log = log
        self.lock = threading.Lock()

    def read(self, name):
        if name not in self.NAMES:
            raise InvalidRequest("없는 저장 이름입니다.")
        path = self.folder / f"{name}.json"
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}

    def write(self, name, changes):
        """바꾼 칸만 받는다(null 은 그 칸을 지운다). 잠근 채 최신 파일에 그 칸만 합쳐, 다른 기기가 바꾼 다른 칸을 지우지 않는다."""
        if name not in self.NAMES:
            raise InvalidRequest("없는 저장 이름입니다.")
        if not isinstance(changes, dict) or len(changes) > 500 or any(
                not isinstance(k, str) or not 0 < len(k) <= 200 or isinstance(v, bool)
                or not isinstance(v, (str, int, type(None))) or (isinstance(v, str) and len(v) > 200)
                for k, v in changes.items()):
            raise InvalidRequest("값은 {글자(200자까지): 글자(200자까지)|정수|null} 꼴, 한 번에 500칸까지입니다.")
        with self.lock:
            value = self.read(name)
            # 기록에는 바뀐 칸만(지운 칸은 null) — 통째로 적으면 입력할 때마다 커진다.
            changed = {k: v for k, v in changes.items() if value.get(k) != v}
            for k, v in changed.items():
                if v is None:
                    value.pop(k, None)
                else:
                    value[k] = v
            if changed:
                logged_write(self.log, "state", name, changed, lambda: atomic_write(self.folder / f"{name}.json", value))
        return value


class OverrideStore:
    def __init__(self, catalog: Path, overrides: Path, log: Path = None):
        source = json.loads(catalog.read_text(encoding="utf-8"))
        self.allowed = {row["운영키"] for row in source["목록"]}
        self.path = overrides
        self.log = log
        self.lock = threading.Lock()

    def read(self):
        if not self.path.exists():
            return {"version": 1, "revision": 0, "updatedAt": None, "abilities": {}, "changedAt": {}}
        try:
            data = json.loads(self.path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            raise InvalidRequest("운영값 파일을 읽을 수 없습니다.")
        if not isinstance(data, dict) or not isinstance(data.get("abilities"), dict):
            raise InvalidRequest("운영값 파일 형식이 잘못됐습니다.")
        return {
            "version": 1,
            "revision": int(data.get("revision") or 0),
            "updatedAt": data.get("updatedAt"),
            "abilities": data["abilities"],
            # 항목별 마지막 저장 시각(화면의 「최근 바꾼 것」). 게임 서버는 abilities 만 읽는다.
            "changedAt": data["changedAt"] if isinstance(data.get("changedAt"), dict) else {},
        }

    def update(self, key, patch, revision):
        if key not in self.allowed:
            raise InvalidRequest("현재 서버에 없는 기술·마법입니다.")
        if not isinstance(patch, dict) or set(patch) - {"effect", "speed", "sound"}:
            raise InvalidRequest("이펙트·속도·사운드만 바꿀 수 있습니다.")
        limits = {"effect": (1, 999), "speed": (1, 255), "sound": (0, 255)}
        clean = {}
        for field, value in patch.items():
            if value is None:
                clean[field] = None
                continue
            if isinstance(value, bool) or not isinstance(value, int):
                raise InvalidRequest(f"{field} 값은 정수여야 합니다.")
            low, high = limits[field]
            if not low <= value <= high:
                raise InvalidRequest(f"{field} 값은 {low}~{high}여야 합니다.")
            clean[field] = value

        with self.lock:
            data = self.read()
            if not isinstance(revision, int) or revision != data["revision"]:
                raise RevisionConflict("다른 기기에서 먼저 저장했습니다. 새 값을 다시 불러오세요.")
            entry = dict(data["abilities"].get(key) or {})
            for field, value in clean.items():
                if value is None:
                    entry.pop(field, None)
                else:
                    entry[field] = value
            now = datetime.now(timezone.utc).isoformat()
            if entry:
                data["abilities"][key] = entry
                data["changedAt"][key] = now
            else:
                data["abilities"].pop(key, None)
                data["changedAt"].pop(key, None)
            data["revision"] += 1
            data["updatedAt"] = now
            logged_write(self.log, "ability", key, entry, lambda: self._write(data))
            return data

    def _write(self, data):
        atomic_write(self.path, data)


def authorized(header, expected):
    if not header or not header.startswith("Basic "):
        return False
    try:
        supplied = base64.b64decode(header[6:], validate=True).decode("utf-8")
    except (ValueError, UnicodeDecodeError):
        return False
    return hmac.compare_digest(
        hashlib.sha256(supplied.encode()).digest(),
        hashlib.sha256(expected.encode()).digest())


SAVE_FAILED = "저장하지 못했습니다 — 값은 바뀌지 않았습니다. 잠시 뒤 다시 해 주세요."
SESSION_COOKIE = "lod_ops"
REMEMBER_SECONDS = 30 * 24 * 3600
SESSION_SECONDS = 12 * 3600


def session_key(credential):
    return hashlib.sha256(b"lod-ops-session\0" + credential.encode()).digest()


def make_session(credential, lifetime, now=None):
    expiry = str(int((now or time.time()) + lifetime))
    return expiry + "." + hmac.new(session_key(credential), expiry.encode(), hashlib.sha256).hexdigest()


def valid_session(token, credential, now=None):
    expiry, _, signature = (token or "").partition(".")
    if not (expiry.isascii() and expiry.isdigit() and len(expiry) < 12) or int(expiry) < (now or time.time()):
        return False
    wanted = hmac.new(session_key(credential), expiry.encode(), hashlib.sha256).hexdigest()
    return signature.isascii() and hmac.compare_digest(signature, wanted)  # 비ASCII 는 compare_digest 가 TypeError


def ota_token(credential):
    # 「내 아이폰에 설치」는 사파리가 아니라 아이폰 시스템이 쿠키 없이 manifest·.ipa 를 받는다 — 주소에 다는 표.
    # 관리자 비밀번호에서 나와 비밀번호를 바꾸면 함께 바뀐다(사용자 2026-10-09).
    return hmac.new(session_key(credential), b"ota", hashlib.sha256).hexdigest()[:32]


def same_secret(supplied, expected):
    return hmac.compare_digest(hashlib.sha256(supplied.encode()).digest(), hashlib.sha256(expected.encode()).digest())


def save_credential(path: Path, credential):
    """비밀번호 파일을 통째로 바꾼다 — 반쯤 쓴 파일이 남지 않게 옆에 쓰고 이름을 바꾼다."""
    handle, temporary = tempfile.mkstemp(prefix=".credential-", dir=path.parent)
    try:
        with os.fdopen(handle, "w", encoding="utf-8") as stream:
            stream.write(credential + "\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(temporary, 0o600)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def cookie_value(header, name):
    for part in (header or "").split(";"):
        key, _, value = part.strip().partition("=")
        if key == name:
            return value
    return None


# 카카오 로그인(사용자 2026-10-10 「아무나 접근 할 수 없게 … 카카오톡 로그인」) — 명세 autopilot/kakao-login/SPEC.md.
KAKAO_COOKIE = "lod_kakao"
# 로그인하지 않은 사람이 받을 수 있는 파일 — 로그인 화면뿐.
PUBLIC_FILES = {"login.html", "login.js", "favicon.svg"}


def kakao_session(secret, kakao_id, lifetime, now=None):
    # 관리자 쿠키(만료.서명)와 꼴이 달라(k번호.만료.서명) 서로 풀리지 않는다. 열쇠는 관리자 비밀번호가 아니라 따로 둔
    # session-secret — 관리자 비밀번호를 바꿔도 카카오 손님은 그대로(사용자 2026-10-10). 모두 내보내려면 그 파일을 지우고 다시 켠다.
    expiry = str(int((now or time.time()) + lifetime))
    signature = hmac.new(session_key(secret), f"kakao:{kakao_id}:{expiry}".encode(), hashlib.sha256).hexdigest()
    return f"k{kakao_id}.{expiry}.{signature}"


def kakao_session_id(token, secret, now=None):
    """맞는 카카오 쿠키면 카카오 회원번호, 아니면 None."""
    head, _, rest = (token or "").partition(".")
    expiry, _, signature = rest.partition(".")
    kakao_id = head[1:]
    if not (head.startswith("k") and kakao_id.isascii() and kakao_id.isdigit() and len(kakao_id) < 20
            and expiry.isascii() and expiry.isdigit() and len(expiry) < 12) or int(expiry) < (now or time.time()):
        return None
    wanted = hmac.new(session_key(secret), f"kakao:{kakao_id}:{expiry}".encode(), hashlib.sha256).hexdigest()
    return kakao_id if signature.isascii() and hmac.compare_digest(signature, wanted) else None


def safe_next(value):
    # 로그인 뒤 돌아갈 곳 — 이 사이트 안 경로만. 「//evil」「/\evil」은 브라우저가 바깥 주소로 읽고, 줄바꿈은 머리글을 깬다.
    if value.startswith("/") and not value.startswith(("//", "/\\")) and all(32 < ord(c) < 127 for c in value):
        return value
    return "/"


def kakao_exchange(config, code):
    """인가 코드 → (회원번호, 닉네임). 카카오 REST — 토큰 받기 뒤 사용자 정보 가져오기."""
    form = {"grant_type": "authorization_code", "client_id": config["client_id"],
            "redirect_uri": config["redirect_uri"], "code": code}
    if config.get("client_secret"):
        form["client_secret"] = config["client_secret"]
    request = urllib.request.Request("https://kauth.kakao.com/oauth/token", data=urlencode(form).encode(),
                                     headers={"Content-Type": "application/x-www-form-urlencoded;charset=utf-8"})
    with urllib.request.urlopen(request, timeout=10) as response:
        access = json.load(response)["access_token"]
    request = urllib.request.Request("https://kapi.kakao.com/v2/user/me", headers={"Authorization": f"Bearer {access}"})
    with urllib.request.urlopen(request, timeout=10) as response:
        me = json.load(response)
    profile = (me.get("kakao_account") or {}).get("profile") or {}
    name = profile.get("nickname") or (me.get("properties") or {}).get("nickname") or ""
    return str(int(me["id"])), str(name)[:40]


class KakaoUsers:
    """카카오로 들어온 사람 — 처음 오면 허가, 관리자가 모르는 사람을 거부로 바꾼다(사용자 2026-10-10).
    파일 `{"회원번호": {"name", "allowed", "first", "last"}}` 를 요청마다 읽는다 — 거부하면 그 자리에서 끊긴다."""

    def __init__(self, path: Path):
        self.path = path
        self.lock = threading.Lock()

    def _read(self):
        try:
            users = json.loads(self.path.read_text(encoding="utf-8"))
        except FileNotFoundError:
            return {}
        if not isinstance(users, dict):
            raise ValueError("kakao-users.json 꼴이 깨졌습니다")
        return users

    def allowed(self, kakao_id):
        # 파일이 깨졌으면 아무도 손님이 아니다 — 덮어쓰면 거부 목록이 사라지니 고칠 때까지 막아 둔다.
        with self.lock:
            try:
                return self._read().get(kakao_id, {}).get("allowed") is True
            except ValueError:
                return False

    def arrive(self, kakao_id, name):
        """로그인할 때 — 처음이면 허가로 적고, 들어와도 되는지 돌려준다."""
        now = datetime.now(timezone.utc).isoformat()
        with self.lock:
            users = self._read()
            user = users.setdefault(kakao_id, {"name": name, "allowed": True, "first": now})
            user["name"] = name or user.get("name", "")
            user["last"] = now
            save_credential(self.path, json.dumps(users, ensure_ascii=False, indent=1))  # 옆에 써서 바꾸기(600)
            return user["allowed"] is True

    def set_allowed(self, kakao_id, allowed):
        with self.lock:
            users = self._read()
            if kakao_id not in users:
                raise InvalidRequest("없는 사람입니다.")
            users[kakao_id]["allowed"] = allowed
            save_credential(self.path, json.dumps(users, ensure_ascii=False, indent=1))

    def listing(self):
        with self.lock:
            users = self._read()
        return sorted(({"id": key} | value for key, value in users.items()), key=lambda user: user.get("last", ""), reverse=True)


class LoginThrottle:
    """같은 곳에서 10분에 5번 넘게 틀리면 10분 쉰다."""
    WINDOW, TRIES = 600, 5

    def __init__(self):
        self.failures = {}
        self.lock = threading.Lock()

    def blocked(self, who, now=None):
        now = now or time.time()
        with self.lock:
            recent = [t for t in self.failures.get(who, []) if now - t < self.WINDOW]
            self.failures[who] = recent
            return len(recent) >= self.TRIES

    def failed(self, who, now=None):
        with self.lock:
            self.failures.setdefault(who, []).append(now or time.time())


def safe_static_path(root: Path, request_path: str):
    decoded = unquote(urlsplit(request_path).path)
    relative = decoded.lstrip("/") or "index.html"
    candidate = (root / relative).resolve()
    resolved = root.resolve()
    if candidate != resolved and resolved not in candidate.parents:
        return None
    if candidate.is_dir():
        candidate = candidate / "index.html"
    return candidate if candidate.is_file() else None


class OpsHandler(BaseHTTPRequestHandler):
    """운영 API 와 정적 파일. 설정은 `handler_for` 가 하위 클래스의 클래스 속성으로 넣는다."""
    root = store = credential = states = throttle = password_file = activity = kakao = kakao_users = kakao_exchange = kakao_secret = None
    server_version = "LODAbilityOps/1"

    def do_GET(self):
        # 보기도 로그인한 사람만(카카오 손님·관리자 — 사용자 2026-10-10), 고치기(PUT)는 관리자만. 페이지는 /api/session 으로 편집 단추를 켠다.
        path = urlsplit(self.path).path
        if path in ("/api/ability-overrides", "/api/kakao/users") or path.startswith("/api/state/"):
            if not self._role() or (path == "/api/kakao/users" and self._role() != "admin"):
                self._json(401, {"error": "로그인이 필요합니다."})
                return
        if path == "/api/kakao/start":
            self._kakao_start()
        elif path == "/api/kakao/callback":
            self._kakao_callback()
        elif path == "/api/kakao/users":
            try:
                self._json(200, self.kakao_users.listing())
            except ValueError as error:
                self._json(500, {"error": str(error)})
        elif path == "/api/activity":
            if not self._signed_in():
                self._json(401, {"error": "관리자 로그인이 필요합니다."})
                return
            query = parse_qs(urlsplit(self.path).query)
            try:
                result = self.activity.report(query.get("from", [""])[0], query.get("to", [""])[0],
                                              query.get("player", [""])[0], query.get("bots", ["0"])[0] == "1")
                self._json(200, result)
            except ValueError:
                self._json(400, {"error": "올바른 날짜와 1~90일 기간을 입력하세요."})
            except (OSError, sqlite3.Error):
                self._json(503, {"error": "기록을 읽지 못했습니다. 잠시 후 다시 조회하세요."})
        elif path == "/api/session":
            role = self._role()
            self._json(200, {"signedIn": role == "admin", "role": role}
                       | ({"ota": ota_token(self.credential)} if role == "admin" else {}))
        elif path == "/api/signed-in":
            # nginx auth_request 가 내려받기 파일마다 묻는다(사용자 2026-10-09) — 본문 없이 204/401 만.
            # 관리자·손님 쿠키, 또는 원래 주소(X-Original-URI)에 붙은 「내 아이폰에 설치」 표.
            token = parse_qs(urlsplit(self.headers.get("X-Original-URI") or "").query).get("ota", [""])[0]
            self.send_response(204 if self._role() or same_secret(token, ota_token(self.credential)) else 401)
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
        elif path == "/api/ota-manifest":
            self._ota_manifest()
        elif path.startswith("/api/state/") and self.states:
            try:
                self._json(200, self.states.read(unquote(path[len("/api/state/"):])))
            except InvalidRequest as error:
                self._json(404, {"error": str(error)})
        elif path == "/api/health":
            self._json(200, {"ok": True})
        elif path == "/api/ability-overrides":
            try:
                self._json(200, self.store.read())
            except InvalidRequest as error:
                self._json(500, {"error": str(error)})
        else:
            self._static()

    def do_POST(self):
        path = urlsplit(self.path).path
        if path == "/api/logout":
            self._json(200, {"ok": True}, cookie=f"{SESSION_COOKIE}=; Max-Age=0; Path=/; HttpOnly; Secure; SameSite=Strict")
            return
        if path == "/api/password":
            self._change_password()
            return
        if path != "/api/login":
            self._json(404, {"error": "없는 API입니다."})
            return
        who = self._client()
        if self.throttle.blocked(who):
            self._json(429, {"error": "너무 여러 번 틀렸습니다. 10분 뒤에 다시 해 주세요."})
            return
        try:
            body = self._body(1024)
        except (InvalidRequest, ValueError, UnicodeDecodeError, json.JSONDecodeError) as error:
            self._json(400, {"error": str(error)})
            return
        # 비밀번호 칸은 관리자만(비상용) — 손님 비밀번호는 카카오 로그인으로 바꿨다(사용자 2026-10-10).
        password = body.get("password") or ""
        if not same_secret(f"{self.credential.split(':', 1)[0]}:{password}", self.credential):
            self.throttle.failed(who)
            self._json(401, {"error": "비밀번호가 맞지 않습니다."})
            return
        remember = body.get("remember") is True
        token = make_session(self.credential, REMEMBER_SECONDS if remember else SESSION_SECONDS)
        cookie = f"{SESSION_COOKIE}={token}; Path=/; HttpOnly; Secure; SameSite=Strict"
        if remember:
            cookie += f"; Max-Age={REMEMBER_SECONDS}"
        self._json(200, {"ok": True, "role": "admin"}, cookie=cookie)

    def do_PUT(self):
        if not self._signed_in():
            self._json(401, {"error": "로그인이 필요합니다."})
            return
        prefix = "/api/ability-overrides/"
        path = urlsplit(self.path).path
        if path.startswith("/api/kakao/users/"):
            # 허가·거부 바꾸기 — 관리자만(위에서 걸렀다).
            try:
                allowed = self._body(256).get("allowed")
                if not isinstance(allowed, bool):
                    raise InvalidRequest("allowed 는 true·false 여야 합니다.")
                self.kakao_users.set_allowed(unquote(path[len("/api/kakao/users/"):]), allowed)
                self._json(200, self.kakao_users.listing())
            except (InvalidRequest, ValueError, UnicodeDecodeError, json.JSONDecodeError) as error:
                self._json(400, {"error": str(error)})
            except OSError:
                self._json(500, {"error": SAVE_FAILED})
            return
        if path.startswith("/api/state/") and self.states:
            try:
                body = self._body(StateStore.LIMIT)
                self._json(200, self.states.write(unquote(path[len("/api/state/"):]), body.get("changes")))
            except InvalidRequest as error:
                self._json(400, {"error": str(error)})
            except (ValueError, UnicodeDecodeError, json.JSONDecodeError):
                self._json(400, {"error": "요청을 읽을 수 없습니다."})  # 해석기 문구는 내보내지 않는다
            except OSError:
                self._json(500, {"error": SAVE_FAILED})
            return
        if not path.startswith(prefix):
            self._json(404, {"error": "없는 API입니다."})
            return
        try:
            body = self._body(4096)
            saved = self.store.update(unquote(path[len(prefix):]), body.get("values"), body.get("revision"))
            self._json(200, saved)
        except RevisionConflict as error:
            self._json(409, {"error": str(error), "current": self.store.read()})
        except (InvalidRequest, ValueError, UnicodeDecodeError, json.JSONDecodeError) as error:
            self._json(400, {"error": str(error)})
        except OSError:
            self._json(500, {"error": SAVE_FAILED})

    # 계정 관리 — 로그인한 사람이 지금 비밀번호를 한 번 더 넣고 바꾼다(사용자 2026-10-02). 바꾸면 다른 기기의
    # 로그인은 모두 풀린다(세션 서명이 비밀번호에서 나온다). 바꾼 사람에게는 새 쿠키를 준다.
    def _change_password(self):
        if not self._signed_in():
            self._json(401, {"error": "로그인이 필요합니다."})
            return
        who = self._client()
        if self.throttle.blocked(who):
            self._json(429, {"error": "너무 여러 번 틀렸습니다. 10분 뒤에 다시 해 주세요."})
            return
        try:
            body = self._body(1024)
        except (InvalidRequest, ValueError, UnicodeDecodeError, json.JSONDecodeError) as error:
            self._json(400, {"error": str(error)})
            return
        user = self.credential.split(":", 1)[0]
        current = f"{user}:{body.get('current') or ''}"
        if not hmac.compare_digest(hashlib.sha256(current.encode()).digest(),
                                   hashlib.sha256(self.credential.encode()).digest()):
            self.throttle.failed(who)
            self._json(403, {"error": "지금 비밀번호가 맞지 않습니다."})
            return
        new = body.get("new")
        if not isinstance(new, str) or not 8 <= len(new) <= 64 or any(c in new for c in "\r\n"):
            self._json(400, {"error": "새 비밀번호는 8~64자로 해 주세요."})
            return
        credential = f"{user}:{new}"
        if self.password_file:
            save_credential(self.password_file, credential)
        type(self).credential = credential
        token = make_session(credential, REMEMBER_SECONDS)
        self._json(200, {"ok": True},
                   cookie=f"{SESSION_COOKIE}={token}; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age={REMEMBER_SECONDS}")

    def _ota_manifest(self):
        # 표가 맞으면 manifest 의 .ipa 주소에 같은 표를 붙여 준다 — 아이폰이 그 주소로 .ipa 를 받는다.
        token = parse_qs(urlsplit(self.path).query).get("ota", [""])[0]
        if not same_secret(token, ota_token(self.credential)):
            self._json(401, {"error": "설치 주소가 맞지 않습니다."})
            return
        try:
            plist = (Path(self.root) / "download" / "manifest.plist").read_text(encoding="utf-8")
        except OSError:
            self._json(404, {"error": "파일이 없습니다."})
            return
        payload = plist.replace("/download/LodClient.ipa<", f"/download/LodClient.ipa?ota={token}<").encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/xml; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.send_header("Cache-Control", "no-store")
        self._security_headers()
        self.end_headers()
        self.wfile.write(payload)

    def _role(self):
        # 관리자(비밀번호 쿠키·Basic) · 손님(허가된 카카오 쿠키). 거부된 카카오 사람은 쿠키가 맞아도 손님이 아니다.
        if self._signed_in():
            return "admin"
        kakao_id = kakao_session_id(cookie_value(self.headers.get("Cookie"), SESSION_COOKIE), self.kakao_secret)
        if kakao_id and self.kakao_users.allowed(kakao_id):
            return "member"
        return None

    def _kakao_start(self):
        # 카카오 동의 화면으로 — 돌아왔을 때 같은 사람인지 보려고 state 를 쿠키에(Lax: 카카오에서 돌아오는 길에도 실린다).
        if not self.kakao:
            self._redirect("/login.html?error=kakao-off")
            return
        target = safe_next(parse_qs(urlsplit(self.path).query).get("next", ["/"])[0])
        state = secrets.token_urlsafe(24)
        cookie = f"{KAKAO_COOKIE}={state}|{quote(target, safe='')}; Path=/api/kakao/; Max-Age=600; HttpOnly; Secure; SameSite=Lax"
        self._redirect("https://kauth.kakao.com/oauth/authorize?" + urlencode({
            "response_type": "code", "client_id": self.kakao["client_id"],
            "redirect_uri": self.kakao["redirect_uri"], "state": state}), cookie)

    def _kakao_callback(self):
        query = parse_qs(urlsplit(self.path).query)
        state, _, target = (cookie_value(self.headers.get("Cookie"), KAKAO_COOKIE) or "").partition("|")
        clear = f"{KAKAO_COOKIE}=; Path=/api/kakao/; Max-Age=0; HttpOnly; Secure; SameSite=Lax"
        code = query.get("code", [""])[0]
        if not self.kakao:
            self._redirect("/login.html?error=kakao-off", clear)
            return
        # 동의 취소(error=)·다른 브라우저에서 시작한 것·쿠키 없음은 모두 다시 하게.
        if "error" in query or not code or not state or not same_secret(query.get("state", [""])[0], state):
            self._redirect("/login.html?error=kakao", clear)
            return
        try:
            kakao_id, name = self.kakao_exchange(self.kakao, code)
        except (OSError, ValueError, KeyError, TypeError, http.client.HTTPException) as error:  # 응답이 중간에 끊긴 것까지
            self.log_message("kakao login failed: %s", type(error).__name__)  # 코드·토큰은 적지 않는다
            self._redirect("/login.html?error=kakao", clear)
            return
        try:
            allowed = self.kakao_users.arrive(kakao_id, name)
        except (OSError, ValueError):  # 목록 파일이 깨졌으면 덮어쓰지 않고 들이지 않는다
            self._redirect("/login.html?error=kakao", clear)
            return
        if not allowed:
            self._redirect("/login.html?error=denied", clear)
            return
        session = (f"{SESSION_COOKIE}={kakao_session(self.kakao_secret, kakao_id, REMEMBER_SECONDS)}; Path=/; HttpOnly; Secure;"
                   f" SameSite=Lax; Max-Age={REMEMBER_SECONDS}")
        self._redirect(safe_next(unquote(target)), clear, session)

    def _redirect(self, location, *cookies):
        self.send_response(302)
        self.send_header("Location", location)
        for cookie in cookies:
            self.send_header("Set-Cookie", cookie)
        self.send_header("Content-Length", "0")
        self.send_header("Cache-Control", "no-store")
        self._security_headers()
        self.end_headers()

    def _signed_in(self):
        # 쿠키(페이지) 또는 Basic 헤더(스크립트). 401 에 WWW-Authenticate 를 붙이지 않는다 — 붙이면 팝업이 뜬다.
        if valid_session(cookie_value(self.headers.get("Cookie"), SESSION_COOKIE), self.credential):
            return True
        header = self.headers.get("Authorization")
        if not header:
            return False
        # Basic 으로 비밀번호를 맞춰 보는 것도 로그인과 같은 횟수 제한을 받는다.
        who = self._client()
        if self.throttle.blocked(who):
            return False
        if authorized(header, self.credential):
            return True
        self.throttle.failed(who)
        return False

    def _client(self):
        # nginx 뒤에서는 모든 요청이 127.0.0.1 에서 온다 — 그때만 nginx 가 적은 X-Real-IP 를 믿는다.
        peer = self.client_address[0]
        return (self.headers.get("X-Real-IP") or peer) if peer == "127.0.0.1" else peer

    def _body(self, limit):
        # JSON 만 받는다 — 다른 사이트의 폼 제출(단순 요청)로는 쓸 수 없게.
        if not (self.headers.get("Content-Type") or "").startswith("application/json"):
            raise InvalidRequest("JSON 으로 보내야 합니다.")
        length = int(self.headers.get("Content-Length", "0"))
        if length <= 0 or length > limit:
            raise InvalidRequest("요청 크기가 올바르지 않습니다.")
        body = json.loads(self.rfile.read(length).decode("utf-8"))
        if not isinstance(body, dict):
            raise InvalidRequest("요청 꼴이 잘못됐습니다.")
        return body

    def _static(self):
        path = safe_static_path(self.root, self.path)
        # 로그인하지 않았으면 로그인 화면만 — 없는 파일도 먼저 로그인으로 보내 무엇이 있는지 드러내지 않는다.
        public = path is not None and path.parent == Path(self.root).resolve() and path.name in PUBLIC_FILES
        if not public and not self._role():
            self._redirect("/login.html?next=" + quote(safe_next(self.path), safe=""))
            return
        if path is None:
            self._json(404, {"error": "파일이 없습니다."})
            return
        payload = path.read_bytes()
        content_type = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
        self.send_response(200)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(payload)))
        self.send_header("Cache-Control", "no-cache" if path.suffix in {".html", ".js", ".css"} else "public, max-age=86400")
        self._security_headers()
        self.end_headers()
        self.wfile.write(payload)

    def _json(self, status, value, cookie=None):
        payload = json.dumps(value, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        if cookie:
            self.send_header("Set-Cookie", cookie)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.send_header("Cache-Control", "no-store")
        self._security_headers()
        self.end_headers()
        self.wfile.write(payload)

    def _security_headers(self):
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("X-Frame-Options", "DENY")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Content-Security-Policy", "default-src 'self'; img-src 'self' data:; "
                         "media-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self'; "
                         "connect-src 'self'; frame-src 'self'")

    def log_message(self, pattern, *args):
        print(f"{self.address_string()} {pattern % args}")


def handler_for(root, store, credential, states=None, throttle=None, password_file=None, activity=None,
                kakao=None, kakao_users=None, exchange=None, kakao_secret=None):
    return type("Handler", (OpsHandler,), {"root": root, "store": store, "credential": credential,
                                           "states": states, "throttle": throttle or LoginThrottle(),
                                           "password_file": password_file,
                                           "activity": activity or ActivityStore(store.path.parent / "activity.sqlite"),
                                           "kakao": kakao,
                                           "kakao_users": kakao_users or KakaoUsers(store.path.parent / "kakao-users.json"),
                                           "kakao_exchange": staticmethod(exchange or kakao_exchange),
                                           "kakao_secret": kakao_secret or secrets.token_hex(32)})


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--overrides", type=Path, required=True)
    parser.add_argument("--password-file", type=Path, required=True)
    parser.add_argument("--web-activity", default=os.environ.get("LOD_WEB_ACTIVITY", ""))
    parser.add_argument("--game-activity", default=os.environ.get("LOD_GAME_ACTIVITY", ""))
    parser.add_argument("--bind", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8787)
    args = parser.parse_args()
    credential = args.password_file.read_text(encoding="utf-8").strip()
    if ":" not in credential:
        raise SystemExit("password file must contain user:password")
    # 카카오 앱 키는 관리자 비밀번호 파일 옆 kakao.json(cloud-dashboard.sh kakao-keys) — 없으면 카카오 단추가 「준비 안 됨」.
    kakao_file = args.password_file.with_name("kakao.json")
    kakao = json.loads(kakao_file.read_text(encoding="utf-8")) if kakao_file.exists() else None
    if kakao is not None and not (kakao.get("client_id") and kakao.get("redirect_uri")):
        raise SystemExit("kakao.json must contain client_id and redirect_uri")
    # 카카오 쿠키 서명 열쇠 — 처음 켤 때 만들어 둔다(600). 관리자 비밀번호와 따로라 비밀번호를 바꿔도 카카오 손님은 안 풀린다.
    secret_file = args.password_file.with_name("session-secret")
    if not secret_file.exists():
        save_credential(secret_file, secrets.token_hex(32))
    kakao_secret = secret_file.read_text(encoding="utf-8").strip()
    data = args.overrides.parent
    log = data / "changes.jsonl"
    stores = (OverrideStore(args.catalog, args.overrides, log), StateStore(data / "state", log))
    activity = ActivityStore(data / "activity.sqlite", args.web_activity, args.game_activity, os.environ.get("LOD_CHARACTER_DIR", ""), os.environ.get("LOD_SERVER_CONFIG", ""))
    activity.start()
    server = ThreadingHTTPServer((args.bind, args.port), handler_for(args.root, stores[0], credential, stores[1],
                                                                     password_file=args.password_file, activity=activity,
                                                                     kakao=kakao, kakao_secret=kakao_secret))
    print(f"LOD ability operations: http://{args.bind}:{args.port}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
