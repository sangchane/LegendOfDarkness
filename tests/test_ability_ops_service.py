import base64
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SPEC = importlib.util.spec_from_file_location(
    "ability_ops_service", ROOT / "scripts" / "ability-ops-service.py")
SERVICE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SERVICE)


class AbilityOpsStoreTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        self.root = Path(self.scratch.name)
        self.catalog = self.root / "catalog.json"
        self.overrides = self.root / "overrides.json"
        self.catalog.write_text(json.dumps({"목록": [
            {"운영키": "skill:단각"}, {"운영키": "spell:쿠로토"},
        ]}), encoding="utf-8")
        self.store = SERVICE.OverrideStore(self.catalog, self.overrides)

    def tearDown(self):
        self.scratch.cleanup()

    def test_update_is_atomic_revisioned_and_persists_only_allowed_fields(self):
        saved = self.store.update("skill:단각", {"effect": 42, "speed": 75, "sound": 16}, 0)
        self.assertEqual(saved["revision"], 1)
        self.assertEqual(saved["abilities"]["skill:단각"], {"effect": 42, "speed": 75, "sound": 16})
        self.assertEqual(json.loads(self.overrides.read_text(encoding="utf-8"))["revision"], 1)
        self.assertEqual(self.overrides.stat().st_mode & 0o777, 0o600)

    def test_unknown_keys_and_out_of_range_values_are_rejected(self):
        with self.assertRaises(SERVICE.InvalidRequest):
            self.store.update("skill:없는기술", {"effect": 42}, 0)
        for field, value in (("effect", 0), ("effect", 1000), ("speed", 0),
                             ("speed", 256), ("sound", -1), ("sound", 256)):
            with self.subTest(field=field, value=value):
                with self.assertRaises(SERVICE.InvalidRequest):
                    self.store.update("skill:단각", {field: value}, 0)

    def test_stale_revision_cannot_overwrite_a_newer_mobile_edit(self):
        self.store.update("skill:단각", {"effect": 42}, 0)
        with self.assertRaises(SERVICE.RevisionConflict):
            self.store.update("skill:단각", {"effect": 69}, 0)

    def test_null_fields_restore_the_server_default_and_remove_an_empty_entry(self):
        self.store.update("spell:쿠로토", {"effect": 4, "speed": 75, "sound": 8}, 0)
        saved = self.store.update("spell:쿠로토", {"effect": None, "speed": None, "sound": None}, 1)
        self.assertNotIn("spell:쿠로토", saved["abilities"])
        self.assertNotIn("spell:쿠로토", saved["changedAt"])

    def test_each_saved_entry_remembers_when_it_changed(self):
        saved = self.store.update("skill:단각", {"effect": 42}, 0)
        self.assertIn("skill:단각", saved["changedAt"])
        self.assertEqual(self.store.read()["changedAt"], saved["changedAt"])


class AbilityOpsSecurityTests(unittest.TestCase):
    def test_basic_auth_uses_the_exact_configured_secret(self):
        expected = "lod-admin:한 번만 쓰는 긴 비밀번호"
        header = "Basic " + base64.b64encode(expected.encode()).decode()
        self.assertTrue(SERVICE.authorized(header, expected))
        self.assertFalse(SERVICE.authorized(header, expected + "x"))
        self.assertFalse(SERVICE.authorized("Bearer nope", expected))

    def test_static_path_cannot_leave_the_document_root(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "index.html").write_text("ok", encoding="utf-8")
            self.assertEqual(SERVICE.safe_static_path(root, "/"), (root / "index.html").resolve())
            self.assertIsNone(SERVICE.safe_static_path(root, "/../secret"))
            self.assertIsNone(SERVICE.safe_static_path(root, "/%2e%2e/secret"))


class LoginAndStateTests(unittest.TestCase):
    """팝업 없는 로그인(쿠키)과 바꾼 값 서버 보관 — plans/ops-login-and-backup.md"""
    CREDENTIAL = "lod-admin:secret"

    def setUp(self):
        import threading
        from http.server import ThreadingHTTPServer
        self.scratch = tempfile.TemporaryDirectory()
        root = Path(self.scratch.name)
        (root / "www").mkdir()
        (root / "www" / "index.html").write_text("DASHBOARD", encoding="utf-8")
        (root / "www" / "login.html").write_text("LOGIN", encoding="utf-8")
        catalog = root / "catalog.json"
        catalog.write_text(json.dumps({"목록": [{"운영키": "skill:단각"}]}), encoding="utf-8")
        self.log = root / "data" / "changes.jsonl"
        (root / "data").mkdir()
        store = SERVICE.OverrideStore(catalog, root / "data" / "overrides.json", self.log)
        states = SERVICE.StateStore(root / "data" / "state", self.log)
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), SERVICE.handler_for(
            root / "www", store, self.CREDENTIAL, states))
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.base = f"http://127.0.0.1:{self.server.server_address[1]}"

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.scratch.cleanup()

    def request(self, method, path, body=None, cookie=None):
        import urllib.error
        import urllib.request
        headers = {"Content-Type": "application/json"} if body is not None else {}
        if cookie:
            headers["Cookie"] = cookie
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(self.base + path, data=data, method=method, headers=headers)
        try:
            with urllib.request.urlopen(req) as response:
                return response.status, response.headers, response.read().decode()
        except urllib.error.HTTPError as error:
            return error.code, error.headers, error.read().decode()

    def login(self, remember=True):
        status, headers, _ = self.request("POST", "/api/login", {"password": "secret", "remember": remember})
        self.assertEqual(status, 200)
        return headers["Set-Cookie"]

    def test_signed_out_page_shows_login_screen_without_browser_popup(self):
        status, headers, body = self.request("GET", "/index.html")
        self.assertEqual((status, body), (200, "LOGIN"))
        self.assertIsNone(headers.get("WWW-Authenticate"))
        status, headers, _ = self.request("GET", "/api/ability-overrides")
        self.assertEqual(status, 401)
        self.assertIsNone(headers.get("WWW-Authenticate"))

    def test_login_sets_a_cookie_that_opens_the_dashboard(self):
        cookie = self.login(remember=True)
        self.assertIn("HttpOnly", cookie)
        self.assertIn("SameSite=Strict", cookie)
        self.assertIn(f"Max-Age={SERVICE.REMEMBER_SECONDS}", cookie)
        self.assertNotIn("Max-Age", self.login(remember=False))
        status, _, body = self.request("GET", "/index.html", cookie=cookie.split(";")[0])
        self.assertEqual((status, body), (200, "DASHBOARD"))

    def test_wrong_password_is_refused_and_throttled(self):
        for _ in range(SERVICE.LoginThrottle.TRIES):
            status, _, _ = self.request("POST", "/api/login", {"password": "nope"})
            self.assertEqual(status, 401)
        status, _, _ = self.request("POST", "/api/login", {"password": "secret"})
        self.assertEqual(status, 429)

    def test_session_token_is_signed_and_expires(self):
        token = SERVICE.make_session(self.CREDENTIAL, 60, now=1000)
        self.assertTrue(SERVICE.valid_session(token, self.CREDENTIAL, now=1030))
        self.assertFalse(SERVICE.valid_session(token, self.CREDENTIAL, now=1061))
        self.assertFalse(SERVICE.valid_session(token, "lod-admin:changed", now=1030))
        self.assertFalse(SERVICE.valid_session("9999999999.forged", self.CREDENTIAL))
        self.assertFalse(SERVICE.valid_session("²." + "0" * 64, self.CREDENTIAL))

    def test_basic_header_guessing_is_throttled_too(self):
        import urllib.request
        wrong = "Basic " + base64.b64encode(b"lod-admin:nope").decode()
        right = "Basic " + base64.b64encode(self.CREDENTIAL.encode()).decode()
        def health(header):
            req = urllib.request.Request(self.base + "/api/health", headers={"Authorization": header})
            try:
                with urllib.request.urlopen(req) as response:
                    return response.status
            except urllib.error.HTTPError as error:
                return error.code
        import urllib.error
        for _ in range(SERVICE.LoginThrottle.TRIES):
            self.assertEqual(health(wrong), 401)
        self.assertEqual(health(right), 401)

    def test_changed_values_are_kept_on_the_server_and_logged(self):
        import urllib.parse
        cookie = self.login().split(";")[0]
        status, _, _ = self.request("PUT", "/api/state/item-names", {"value": {"Stick": "막대기"}}, cookie=cookie)
        self.assertEqual(status, 200)
        status, _, body = self.request("GET", "/api/state/item-names", cookie=cookie)
        self.assertEqual(json.loads(body), {"Stick": "막대기"})
        self.request("PUT", "/api/ability-overrides/" + urllib.parse.quote("skill:단각"), {"values": {"effect": 5}, "revision": 0}, cookie=cookie)
        self.request("PUT", "/api/state/item-names", {"value": {"Stick": "막대기", "Eppe": "에페"}}, cookie=cookie)
        lines = [json.loads(line) for line in self.log.read_text(encoding="utf-8").splitlines()]
        self.assertEqual([line["kind"] for line in lines], ["state", "ability", "state"])
        self.assertEqual(lines[2]["value"], {"Eppe": "에페"})  # 바뀐 칸만
        status, _, _ = self.request("PUT", "/api/state/other", {"value": {}}, cookie=cookie)
        self.assertEqual(status, 400)
        status, _, _ = self.request("PUT", "/api/state/item-names", {"value": {"a": True}}, cookie=cookie)
        self.assertEqual(status, 400)


if __name__ == "__main__":
    unittest.main()
