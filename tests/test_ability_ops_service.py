import base64
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SPEC = importlib.util.spec_from_file_location(
    "ability_ops_service", ROOT / "scripts" / "ops" / "ability-ops-service.py")
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

    def test_audit_failure_leaves_every_value_unchanged(self):
        # 리뷰 12 — 감사기록을 먼저 쓴다. 못 쓰면 값은 그대로다.
        log = self.root / "changes.jsonl"
        log.mkdir()
        with self.assertRaises(OSError):
            SERVICE.OverrideStore(self.catalog, self.overrides, log).update("skill:단각", {"effect": 42}, 0)
        self.assertFalse(self.overrides.exists())
        states = SERVICE.StateStore(self.root / "state", log)
        with self.assertRaises(OSError):
            states.write("item-names", {"Stick": "막대기"})
        self.assertEqual(states.read("item-names"), {})

    def test_value_write_failure_leaves_a_failure_line_after_the_audit_line(self):
        from unittest import mock
        log = self.root / "changes.jsonl"
        store = SERVICE.OverrideStore(self.catalog, self.overrides, log)
        states = SERVICE.StateStore(self.root / "state", log)
        with mock.patch.object(SERVICE, "atomic_write", side_effect=OSError("disk full")):
            with self.assertRaises(OSError):
                store.update("skill:단각", {"effect": 42}, 0)
            with self.assertRaises(OSError):
                states.write("item-names", {"Stick": "막대기"})
        lines = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()]
        self.assertEqual([(line["kind"], line.get("failed")) for line in lines],
                         [("ability", None), ("ability", "disk full"), ("state", None), ("state", "disk full")])
        self.assertFalse(self.overrides.exists())
        self.assertEqual(states.read("item-names"), {})


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
    MEMBER = "member:1234"

    def setUp(self):
        import threading
        from http.server import ThreadingHTTPServer
        self.scratch = tempfile.TemporaryDirectory()
        root = Path(self.scratch.name)
        (root / "www").mkdir()
        (root / "www" / "index.html").write_text("DASHBOARD", encoding="utf-8")
        (root / "www" / "login.html").write_text("LOGIN", encoding="utf-8")
        (root / "www" / "download").mkdir()
        (root / "www" / "download" / "manifest.plist").write_text(
            "<string>https://lodgame.duckdns.org/download/LodClient.ipa</string>", encoding="utf-8")
        catalog = root / "catalog.json"
        catalog.write_text(json.dumps({"목록": [{"운영키": "skill:단각"}]}), encoding="utf-8")
        self.log = root / "data" / "changes.jsonl"
        (root / "data").mkdir()
        store = SERVICE.OverrideStore(catalog, root / "data" / "overrides.json", self.log)
        states = SERVICE.StateStore(root / "data" / "state", self.log)
        self.password_file = root / "data" / "credential"
        self.password_file.write_text(self.CREDENTIAL + "\n", encoding="utf-8")
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), SERVICE.handler_for(
            root / "www", store, self.CREDENTIAL, states, password_file=self.password_file, member=self.MEMBER))
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.base = f"http://127.0.0.1:{self.server.server_address[1]}"

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.scratch.cleanup()

    def request(self, method, path, body=None, cookie=None, extra=None):
        import urllib.error
        import urllib.request
        headers = {"Content-Type": "application/json"} if body is not None else {}
        headers.update(extra or {})
        if cookie:
            headers["Cookie"] = cookie
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(self.base + path, data=data, method=method, headers=headers)
        try:
            with urllib.request.urlopen(req) as response:
                return response.status, response.headers, response.read().decode()
        except urllib.error.HTTPError as error:
            return error.code, error.headers, error.read().decode()

    def login(self, remember=True, password="secret"):
        status, headers, _ = self.request("POST", "/api/login", {"password": password, "remember": remember})
        self.assertEqual(status, 200)
        return headers["Set-Cookie"]

    def test_activity_is_admin_only_and_dates_are_validated(self):
        status, _, _ = self.request("GET", "/api/activity?from=2026-10-02&to=2026-10-03")
        self.assertEqual(status, 401)
        cookie = self.login().split(";")[0]
        status, _, body = self.request("GET", "/api/activity?from=2026-10-02&to=2026-10-03", cookie=cookie)
        self.assertEqual(status, 200)
        self.assertIn("summary", json.loads(body))
        status, _, _ = self.request("GET", "/api/activity?from=bad&to=2026-10-03", cookie=cookie)
        self.assertEqual(status, 400)

    def test_anyone_can_look_but_only_a_signed_in_person_can_change(self):
        status, _, body = self.request("GET", "/index.html")
        self.assertEqual((status, body), (200, "DASHBOARD"))
        status, _, body = self.request("GET", "/api/session")
        self.assertEqual(json.loads(body), {"signedIn": False, "role": None})
        status, _, _ = self.request("GET", "/api/ability-overrides")
        self.assertEqual(status, 200)
        status, headers, _ = self.request("PUT", "/api/state/item-names", {"value": {"Stick": "막대기"}})
        self.assertEqual(status, 401)
        self.assertIsNone(headers.get("WWW-Authenticate"))
        status, _, body = self.request("GET", "/api/session", cookie=self.login().split(";")[0])
        self.assertEqual(json.loads(body), {"signedIn": True, "role": "admin", "ota": SERVICE.ota_token(self.CREDENTIAL)})

    def test_download_gate_opens_for_admin_guest_or_the_install_token(self):
        status, headers, _ = self.request("GET", "/api/signed-in")
        self.assertEqual(status, 401)
        self.assertIsNone(headers.get("WWW-Authenticate"))
        for password in ("secret", "1234"):
            status, _, _ = self.request("GET", "/api/signed-in", cookie=self.login(password=password).split(";")[0])
            self.assertEqual(status, 204)
        token = SERVICE.ota_token(self.CREDENTIAL)
        status, _, _ = self.request("GET", "/api/signed-in", extra={"X-Original-URI": f"/download/LodClient.ipa?ota={token}"})
        self.assertEqual(status, 204)
        for bad in ("/download/LodClient.ipa?ota=wrong", "/download/LodClient.ipa?ota=%EC%95%88", "/download/LodClient.ipa"):
            status, _, _ = self.request("GET", "/api/signed-in", extra={"X-Original-URI": bad})
            self.assertEqual(status, 401)

    def test_guest_password_signs_in_without_admin_rights(self):
        status, _, body = self.request("POST", "/api/login", {"password": "1234", "remember": True})
        self.assertEqual((status, json.loads(body)["role"]), (200, "member"))
        guest = self.login(password="1234").split(";")[0]
        status, _, body = self.request("GET", "/api/session", cookie=guest)
        self.assertEqual(json.loads(body), {"signedIn": False, "role": "member"})
        status, _, _ = self.request("PUT", "/api/state/item-names", {"value": {"Stick": "막대기"}}, cookie=guest)
        self.assertEqual(status, 401)
        status, _, _ = self.request("GET", "/api/activity?from=2026-10-02&to=2026-10-03", cookie=guest)
        self.assertEqual(status, 401)
        status, _, _ = self.request("POST", "/api/password", {"current": "1234", "new": "longenough1"}, cookie=guest)
        self.assertEqual(status, 401)
        # 손님 쿠키는 관리자 서명으로 풀리지 않는다 — 열쇠가 서로의 비밀번호에서 나온다.
        self.assertFalse(SERVICE.valid_session(guest.split("=", 1)[1], self.CREDENTIAL))

    def test_install_manifest_needs_the_token_and_passes_it_to_the_ipa(self):
        token = SERVICE.ota_token(self.CREDENTIAL)
        status, _, _ = self.request("GET", "/api/ota-manifest?ota=wrong")
        self.assertEqual(status, 401)
        status, headers, body = self.request("GET", f"/api/ota-manifest?ota={token}")
        self.assertEqual(status, 200)
        self.assertIn("xml", headers["Content-Type"])
        self.assertIn(f"/download/LodClient.ipa?ota={token}</string>", body)

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
            req = urllib.request.Request(self.base + "/api/state/item-names", method="PUT", data=b'{"value": {}}',
                                         headers={"Authorization": header, "Content-Type": "application/json"})
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
        status, _, _ = self.request("PUT", "/api/state/item-names", {"changes": {"Stick": "막대기"}}, cookie=cookie)
        self.assertEqual(status, 200)
        status, _, body = self.request("GET", "/api/state/item-names", cookie=cookie)
        self.assertEqual(json.loads(body), {"Stick": "막대기"})
        self.request("PUT", "/api/ability-overrides/" + urllib.parse.quote("skill:단각"), {"values": {"effect": 5}, "revision": 0}, cookie=cookie)
        self.request("PUT", "/api/state/item-names", {"changes": {"Stick": "막대기", "Eppe": "에페"}}, cookie=cookie)
        lines = [json.loads(line) for line in self.log.read_text(encoding="utf-8").splitlines()]
        self.assertEqual([line["kind"] for line in lines], ["state", "ability", "state"])
        self.assertEqual(lines[2]["value"], {"Eppe": "에페"})  # 바뀐 칸만
        status, _, _ = self.request("PUT", "/api/state/other", {"changes": {}}, cookie=cookie)
        self.assertEqual(status, 400)
        status, _, _ = self.request("PUT", "/api/state/item-names", {"changes": {"a": True}}, cookie=cookie)
        self.assertEqual(status, 400)

    def test_two_devices_saving_different_names_keep_both(self):
        # 리뷰 11 — 두 기기가 같은 빈 목록을 읽고 서로 다른 칸을 고친다. 나중 저장이 앞 저장을 지우면 안 된다.
        phone, laptop = self.login().split(";")[0], self.login().split(";")[0]
        status, _, _ = self.request("PUT", "/api/state/item-names", {"changes": {"Stick": "막대기"}}, cookie=phone)
        self.assertEqual(status, 200)
        status, _, _ = self.request("PUT", "/api/state/item-names", {"changes": {"Eppe": "에페"}}, cookie=laptop)
        self.assertEqual(status, 200)
        _, _, body = self.request("GET", "/api/state/item-names")
        self.assertEqual(json.loads(body), {"Stick": "막대기", "Eppe": "에페"})
        self.request("PUT", "/api/state/item-names", {"changes": {"Stick": None}}, cookie=phone)  # null 은 그 칸만 지운다
        _, _, body = self.request("GET", "/api/state/item-names")
        self.assertEqual(json.loads(body), {"Eppe": "에페"})
        # 사전을 통째로 보내던 옛 꼴은 받지 않는다 — 다른 기기의 변경을 지우던 길이다.
        status, _, _ = self.request("PUT", "/api/state/item-names", {"value": {"Stick": "막대기"}}, cookie=phone)
        self.assertEqual(status, 400)

    def test_audit_failure_answers_500_and_keeps_the_values(self):
        import urllib.parse
        cookie = self.login().split(";")[0]
        self.request("PUT", "/api/state/item-names", {"changes": {"Stick": "막대기"}}, cookie=cookie)
        self.log.unlink()
        self.log.mkdir()  # 기록을 쓸 수 없게
        status, _, body = self.request("PUT", "/api/state/item-names", {"changes": {"Stick": "몽둥이"}}, cookie=cookie)
        self.assertEqual(status, 500)
        self.assertIn("error", json.loads(body))
        _, _, body = self.request("GET", "/api/state/item-names")
        self.assertEqual(json.loads(body), {"Stick": "막대기"})
        status, _, _ = self.request("PUT", "/api/ability-overrides/" + urllib.parse.quote("skill:단각"),
                                    {"values": {"effect": 5}, "revision": 0}, cookie=cookie)
        self.assertEqual(status, 500)
        _, _, body = self.request("GET", "/api/ability-overrides")
        self.assertEqual(json.loads(body)["revision"], 0)

    def test_signed_in_person_changes_the_password_and_old_logins_end(self):
        old = self.login().split(";")[0]
        status, _, _ = self.request("POST", "/api/password", {"current": "secret", "new": "newpass99"})
        self.assertEqual(status, 401)  # 로그인 없이는 못 바꾼다
        status, _, _ = self.request("POST", "/api/password", {"current": "wrong", "new": "newpass99"}, cookie=old)
        self.assertEqual(status, 403)
        status, _, _ = self.request("POST", "/api/password", {"current": "secret", "new": "short"}, cookie=old)
        self.assertEqual(status, 400)
        status, headers, _ = self.request("POST", "/api/password", {"current": "secret", "new": "newpass99"}, cookie=old)
        self.assertEqual(status, 200)
        fresh = headers["Set-Cookie"].split(";")[0]
        self.assertEqual(self.password_file.read_text(encoding="utf-8"), "lod-admin:newpass99\n")
        _, _, body = self.request("GET", "/api/session", cookie=old)
        self.assertEqual(json.loads(body), {"signedIn": False, "role": None})  # 다른 기기의 옛 로그인은 풀린다
        _, _, body = self.request("GET", "/api/session", cookie=fresh)
        self.assertEqual(json.loads(body)["signedIn"], True)
        status, _, _ = self.request("POST", "/api/login", {"password": "secret"})
        self.assertEqual(status, 401)
        status, _, _ = self.request("POST", "/api/login", {"password": "newpass99"})
        self.assertEqual(status, 200)


if __name__ == "__main__":
    unittest.main()
