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
import json
import mimetypes
import os
import tempfile
import threading
import time
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit


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


def log_change(log: Path, kind, key, value):
    """바꾼 값을 한 줄씩 쌓는다 — 백업·되돌리기용. 지우지 않는다."""
    line = json.dumps({"at": datetime.now(timezone.utc).isoformat(), "kind": kind, "key": key,
                       "value": value}, ensure_ascii=False)
    with open(log, "a", encoding="utf-8") as stream:
        stream.write(line + "\n")


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

    def write(self, name, value):
        if name not in self.NAMES:
            raise InvalidRequest("없는 저장 이름입니다.")
        if not isinstance(value, dict) or any(
                not isinstance(k, str) or isinstance(v, bool) or not isinstance(v, (str, int))
                for k, v in value.items()):
            raise InvalidRequest("값은 {글자: 글자|정수} 꼴이어야 합니다.")
        with self.lock:
            before = self.read(name)
            atomic_write(self.folder / f"{name}.json", value)
            # 기록에는 바뀐 칸만(지운 칸은 null) — 통째로 적으면 입력할 때마다 커진다.
            changed = {k: value.get(k) for k in set(before) | set(value) if before.get(k) != value.get(k)}
            if changed:
                log_change(self.log, "state", name, changed)
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
            self._write(data)
            if self.log:
                log_change(self.log, "ability", key, entry)
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
    return hmac.compare_digest(signature, wanted)


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
    root = store = credential = states = throttle = password_file = None
    server_version = "LODAbilityOps/1"

    def do_GET(self):
        # 보기는 누구나, 고치기(PUT)만 로그인한 사람 — 사용자 2026-10-02. 페이지는 /api/session 으로 편집 단추를 켠다.
        path = urlsplit(self.path).path
        if path == "/api/session":
            self._json(200, {"signedIn": self._signed_in()})
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
        user = self.credential.split(":", 1)[0]
        supplied = f"{user}:{body.get('password') or ''}"
        if not hmac.compare_digest(hashlib.sha256(supplied.encode()).digest(),
                                   hashlib.sha256(self.credential.encode()).digest()):
            self.throttle.failed(who)
            self._json(401, {"error": "비밀번호가 맞지 않습니다."})
            return
        remember = body.get("remember") is True
        token = make_session(self.credential, REMEMBER_SECONDS if remember else SESSION_SECONDS)
        cookie = f"{SESSION_COOKIE}={token}; Path=/; HttpOnly; Secure; SameSite=Strict"
        if remember:
            cookie += f"; Max-Age={REMEMBER_SECONDS}"
        self._json(200, {"ok": True}, cookie=cookie)

    def do_PUT(self):
        if not self._signed_in():
            self._json(401, {"error": "로그인이 필요합니다."})
            return
        prefix = "/api/ability-overrides/"
        path = urlsplit(self.path).path
        if path.startswith("/api/state/") and self.states:
            try:
                body = self._body(StateStore.LIMIT)
                self._json(200, self.states.write(unquote(path[len("/api/state/"):]), body.get("value")))
            except (InvalidRequest, ValueError, UnicodeDecodeError, json.JSONDecodeError) as error:
                self._json(400, {"error": str(error)})
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


def handler_for(root, store, credential, states=None, throttle=None, password_file=None):
    return type("Handler", (OpsHandler,), {"root": root, "store": store, "credential": credential,
                                           "states": states, "throttle": throttle or LoginThrottle(),
                                           "password_file": password_file})


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--overrides", type=Path, required=True)
    parser.add_argument("--password-file", type=Path, required=True)
    parser.add_argument("--bind", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8787)
    args = parser.parse_args()
    credential = args.password_file.read_text(encoding="utf-8").strip()
    if ":" not in credential:
        raise SystemExit("password file must contain user:password")
    data = args.overrides.parent
    log = data / "changes.jsonl"
    stores = (OverrideStore(args.catalog, args.overrides, log), StateStore(data / "state", log))
    server = ThreadingHTTPServer((args.bind, args.port), handler_for(args.root, stores[0], credential, stores[1],
                                                                     password_file=args.password_file))
    print(f"LOD ability operations: http://{args.bind}:{args.port}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
