#!/usr/bin/env python3
"""인증된 기술·마법 연출 운영 API와 로컬 미리보기 정적 서버.

클라우드에서는 nginx가 HTTPS·Basic 인증·정적 파일을 맡고 이 프로세스의 `/api`만
127.0.0.1로 프록시한다. 프로세스도 같은 인증을 다시 확인한다.
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
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit


class InvalidRequest(ValueError):
    pass


class RevisionConflict(RuntimeError):
    pass


class OverrideStore:
    def __init__(self, catalog: Path, overrides: Path):
        source = json.loads(catalog.read_text(encoding="utf-8"))
        self.allowed = {row["운영키"] for row in source["목록"]}
        self.path = overrides
        self.lock = threading.Lock()

    def read(self):
        if not self.path.exists():
            return {"version": 1, "revision": 0, "updatedAt": None, "abilities": {}}
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
            if entry:
                data["abilities"][key] = entry
            else:
                data["abilities"].pop(key, None)
            data["revision"] += 1
            data["updatedAt"] = datetime.now(timezone.utc).isoformat()
            self._write(data)
            return data

    def _write(self, data):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        handle, temporary = tempfile.mkstemp(prefix=".ability-overrides-", dir=self.path.parent)
        try:
            with os.fdopen(handle, "w", encoding="utf-8") as stream:
                json.dump(data, stream, ensure_ascii=False, indent=2)
                stream.write("\n")
                stream.flush()
                os.fsync(stream.fileno())
            os.chmod(temporary, 0o600)
            os.replace(temporary, self.path)
        finally:
            if os.path.exists(temporary):
                os.unlink(temporary)


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


def handler_for(root, store, credential):
    class Handler(BaseHTTPRequestHandler):
        server_version = "LODAbilityOps/1"

        def do_GET(self):
            if not self._authenticate():
                return
            path = urlsplit(self.path).path
            if path == "/api/health":
                self._json(200, {"ok": True})
            elif path == "/api/ability-overrides":
                try:
                    self._json(200, store.read())
                except InvalidRequest as error:
                    self._json(500, {"error": str(error)})
            else:
                self._static()

        def do_PUT(self):
            if not self._authenticate():
                return
            prefix = "/api/ability-overrides/"
            path = urlsplit(self.path).path
            if not path.startswith(prefix):
                self._json(404, {"error": "없는 API입니다."})
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if length <= 0 or length > 4096:
                    raise InvalidRequest("요청 크기가 올바르지 않습니다.")
                body = json.loads(self.rfile.read(length).decode("utf-8"))
                saved = store.update(unquote(path[len(prefix):]), body.get("values"), body.get("revision"))
                self._json(200, saved)
            except RevisionConflict as error:
                self._json(409, {"error": str(error), "current": store.read()})
            except (InvalidRequest, ValueError, UnicodeDecodeError, json.JSONDecodeError) as error:
                self._json(400, {"error": str(error)})

        def _authenticate(self):
            if authorized(self.headers.get("Authorization"), credential):
                return True
            self.send_response(401)
            self.send_header("WWW-Authenticate", 'Basic realm="LOD operations", charset="UTF-8"')
            self.send_header("Content-Length", "0")
            self.end_headers()
            return False

        def _static(self):
            path = safe_static_path(root, self.path)
            if path is None:
                self._json(404, {"error": "파일이 없습니다."})
                return
            payload = path.read_bytes()
            content_type = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
            self.send_response(200)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(payload)))
            self.send_header("Cache-Control", "no-cache" if path.suffix in {".html", ".js"} else "public, max-age=86400")
            self._security_headers()
            self.end_headers()
            self.wfile.write(payload)

        def _json(self, status, value):
            payload = json.dumps(value, ensure_ascii=False).encode("utf-8")
            self.send_response(status)
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

    return Handler


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
    server = ThreadingHTTPServer((args.bind, args.port), handler_for(args.root, OverrideStore(args.catalog, args.overrides), credential))
    print(f"LOD ability operations: http://{args.bind}:{args.port}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
