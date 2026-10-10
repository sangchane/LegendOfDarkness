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
    KAKAO = {"client_id": "rest-key", "client_secret": "kakao-secret",
             "redirect_uri": "https://lodgame.duckdns.org/api/kakao/callback"}
    JOIN = "4321"  # 시험용 초대 번호 — 진짜 번호는 서버 data/join-code 에만

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
        # 카카오 대신 — 코드 "good" 만 이 사람으로 바꿔 준다.
        self.kakao_person = ("777", "손님")
        def exchange(config, code):
            if config is not self.KAKAO or code != "good":
                raise ValueError("bad code")
            return self.kakao_person
        self.users = SERVICE.KakaoUsers(root / "data" / "kakao-users.json")
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), SERVICE.handler_for(
            root / "www", store, self.CREDENTIAL, states, password_file=self.password_file,
            kakao=self.KAKAO, kakao_users=self.users, exchange=exchange, join_code=self.JOIN))
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

        class Stay(urllib.request.HTTPRedirectHandler):  # 302 를 따라가지 않고 그대로 본다
            def redirect_request(self, *args):
                return None
        try:
            with urllib.request.build_opener(Stay).open(req) as response:
                return response.status, response.headers, response.read().decode()
        except urllib.error.HTTPError as error:
            return error.code, error.headers, error.read().decode()

    def login(self, remember=True, password="secret"):
        status, headers, _ = self.request("POST", "/api/login", {"password": password, "remember": remember})
        self.assertEqual(status, 200)
        return headers["Set-Cookie"]

    def kakao_login(self, next_path="/?view=download", code=JOIN):
        """카카오 단추 → 동의 → 돌아오기(→ 대기면 초대 번호). (돌아온 답, 세션 쿠키 「lod_ops=…」 또는 None)."""
        import urllib.parse
        status, headers, _ = self.request("GET", "/api/kakao/start?next=" + urllib.parse.quote(next_path, safe=""))
        self.assertEqual(status, 302)
        state_cookie = headers["Set-Cookie"].split(";")[0]
        state = state_cookie.split("=", 1)[1].split("|")[0]
        status, headers, _ = self.request("GET", f"/api/kakao/callback?code=good&state={state}", cookie=state_cookie)
        session = next((c.split(";")[0] for c in headers.get_all("Set-Cookie") or [] if c.startswith("lod_ops=")), None)
        if session and code and headers["Location"].startswith("/login.html"):
            self.request("POST", "/api/kakao/join", {"code": code}, cookie=session)
        return (status, headers), session

    def test_activity_is_admin_only_and_dates_are_validated(self):
        status, _, _ = self.request("GET", "/api/activity?from=2026-10-02&to=2026-10-03")
        self.assertEqual(status, 401)
        cookie = self.login().split(";")[0]
        status, _, body = self.request("GET", "/api/activity?from=2026-10-02&to=2026-10-03", cookie=cookie)
        self.assertEqual(status, 200)
        self.assertIn("summary", json.loads(body))
        status, _, _ = self.request("GET", "/api/activity?from=bad&to=2026-10-03", cookie=cookie)
        self.assertEqual(status, 400)

    def test_nobody_sees_anything_but_the_login_page_until_signed_in(self):
        # SC-1 — 사용자 2026-10-10 「아무나 접근 할 수 없게」.
        for path, wanted in (("/index.html", "%2Findex.html"), ("/?view=abilities", "%2F%3Fview%3Dabilities"),
                             ("/no-such-file.js", "%2Fno-such-file.js")):
            status, headers, _ = self.request("GET", path)
            self.assertEqual((status, headers["Location"]), (302, "/login.html?next=" + wanted))
        for path in ("/api/ability-overrides", "/api/state/item-names", "/api/kakao/users"):
            status, _, _ = self.request("GET", path)
            self.assertEqual(status, 401, path)
        status, _, body = self.request("GET", "/login.html")
        self.assertEqual((status, body), (200, "LOGIN"))
        status, _, body = self.request("GET", "/api/session")
        self.assertEqual(json.loads(body), {"signedIn": False, "role": None})
        status, headers, _ = self.request("PUT", "/api/state/item-names", {"value": {"Stick": "막대기"}})
        self.assertEqual(status, 401)
        self.assertIsNone(headers.get("WWW-Authenticate"))
        status, _, body = self.request("GET", "/api/session", cookie=self.login().split(";")[0])
        self.assertEqual(json.loads(body), {"signedIn": True, "role": "admin", "ota": SERVICE.ota_token(self.CREDENTIAL)})

    def test_download_gate_opens_for_admin_kakao_guest_or_the_install_token(self):
        status, headers, _ = self.request("GET", "/api/signed-in")
        self.assertEqual(status, 401)
        self.assertIsNone(headers.get("WWW-Authenticate"))
        for cookie in (self.login().split(";")[0], self.kakao_login()[1]):
            status, _, _ = self.request("GET", "/api/signed-in", cookie=cookie)
            self.assertEqual(status, 204)
        token = SERVICE.ota_token(self.CREDENTIAL)
        status, _, _ = self.request("GET", "/api/signed-in", extra={"X-Original-URI": f"/download/LodClient.ipa?ota={token}"})
        self.assertEqual(status, 204)
        for bad in ("/download/LodClient.ipa?ota=wrong", "/download/LodClient.ipa?ota=%EC%95%88", "/download/LodClient.ipa"):
            status, _, _ = self.request("GET", "/api/signed-in", extra={"X-Original-URI": bad})
            self.assertEqual(status, 401)

    def test_kakao_guest_looks_and_downloads_but_cannot_change(self):
        # SC-4 — 지금 손님과 같게(사용자 2026-10-10): 보기·내려받기, 고치기·접속 기록·사람 관리는 관리자만.
        (status, headers), guest = self.kakao_login()
        self.assertEqual((status, headers["Location"]), (302, "/login.html?next=%2F%3Fview%3Ddownload"))  # 처음엔 대기
        self.assertIn("SameSite=Lax", next(c for c in headers.get_all("Set-Cookie") if c.startswith("lod_ops=")))
        status, _, body = self.request("GET", "/index.html", cookie=guest)
        self.assertEqual((status, body), (200, "DASHBOARD"))
        status, _, body = self.request("GET", "/api/session", cookie=guest)
        self.assertEqual(json.loads(body), {"signedIn": False, "role": "member"})
        status, _, _ = self.request("GET", "/api/ability-overrides", cookie=guest)
        self.assertEqual(status, 200)
        for method, path, body in (("PUT", "/api/state/item-names", {"changes": {"Stick": "막대기"}}),
                                   ("GET", "/api/activity?from=2026-10-02&to=2026-10-03", None),
                                   ("POST", "/api/password", {"current": "x", "new": "longenough1"}),
                                   ("GET", "/api/kakao/users", None),
                                   ("PUT", "/api/kakao/users/777", {"status": "denied"})):
            status, _, _ = self.request(method, path, body, cookie=guest)
            self.assertEqual(status, 401, path)
        # 카카오 쿠키는 관리자 쿠키로 읽히지 않고, 관리자 쿠키도 카카오 쿠키로 읽히지 않는다.
        self.assertFalse(SERVICE.valid_session(guest.split("=", 1)[1], self.CREDENTIAL))
        self.assertIsNone(SERVICE.kakao_session_id(self.login().split(";")[0].split("=", 1)[1], self.CREDENTIAL))

    def test_the_old_guest_password_no_longer_opens_anything(self):
        # SC-6 — 손님 4자리는 없앴다. 관리자 비밀번호는 비상용으로 그대로.
        status, _, _ = self.request("POST", "/api/login", {"password": "1234", "remember": True})
        self.assertEqual(status, 401)
        status, _, body = self.request("POST", "/api/login", {"password": "secret"})
        self.assertEqual((status, json.loads(body)), (200, {"ok": True, "role": "admin"}))

    def test_kakao_start_goes_to_kakao_with_a_state_cookie(self):
        # SC-2
        import urllib.parse
        status, headers, _ = self.request("GET", "/api/kakao/start?next=%2F%3Fview%3Ditems")
        self.assertEqual(status, 302)
        place = urllib.parse.urlsplit(headers["Location"])
        query = urllib.parse.parse_qs(place.query)
        self.assertEqual((place.scheme, place.netloc, place.path), ("https", "kauth.kakao.com", "/oauth/authorize"))
        self.assertEqual((query["client_id"], query["redirect_uri"], query["response_type"]),
                         (["rest-key"], [self.KAKAO["redirect_uri"]], ["code"]))
        cookie = headers["Set-Cookie"]
        self.assertTrue(cookie.startswith(f"lod_kakao={query['state'][0]}|%2F%3Fview%3Ditems;"))
        for part in ("HttpOnly", "Secure", "SameSite=Lax", "Path=/api/kakao/", "Max-Age=600"):
            self.assertIn(part, cookie)
        self.assertNotIn("kakao-secret", headers["Location"])

    def test_first_kakao_visit_waits_and_is_remembered(self):
        # 처음 오면 대기, 초대 번호를 맞히면 허가(사용자 2026-10-10).
        self.kakao_login(code=None)
        self.assertEqual(self.users.listing()[0]["status"], "pending")
        self.kakao_person = ("777", "새이름")
        self.kakao_login()
        [person] = self.users.listing()
        self.assertEqual((person["id"], person["name"], person["status"]), ("777", "새이름", "allowed"))
        self.assertLessEqual(person["first"], person["last"])
        self.kakao_person = ("888", "나중사람")
        self.kakao_login(code=None)
        self.assertEqual([p["id"] for p in self.users.listing()], ["888", "777"])  # 마지막에 온 사람이 위

    def test_a_waiting_person_gets_in_with_the_invite_code_or_the_admin(self):
        # 사용자 2026-10-10 「번호를 입력하면 즉시 승인이고 입력 못하면 대기상태로」.
        (status, headers), waiting = self.kakao_login(code=None)
        self.assertEqual(headers["Location"], "/login.html?next=%2F%3Fview%3Ddownload")
        self.assertEqual(self.request("GET", "/index.html", cookie=waiting)[0], 302)
        self.assertEqual(self.request("GET", "/api/signed-in", cookie=waiting)[0], 401)
        status, _, body = self.request("GET", "/api/session", cookie=waiting)
        self.assertEqual(json.loads(body), {"signedIn": False, "role": None, "pending": True})
        status, _, body = self.request("POST", "/api/kakao/join", {"code": "0000"}, cookie=waiting)
        self.assertEqual((status, json.loads(body)["error"]), (403, "번호가 맞지 않습니다. 남은 기회 4번."))
        status, _, _ = self.request("POST", "/api/kakao/join", {"code": " 4321 "}, cookie=waiting)
        self.assertEqual(status, 200)
        self.assertEqual(self.request("GET", "/index.html", cookie=waiting)[0], 200)
        # 번호를 모르는 사람은 관리자가 허가한다.
        self.kakao_person = ("888", "모르는사람")
        stranger = self.kakao_login(code=None)[1]
        admin = self.login().split(";")[0]
        status, _, _ = self.request("PUT", "/api/kakao/users/888", {"status": "allowed"}, cookie=admin)
        self.assertEqual(status, 200)
        self.assertEqual(self.request("GET", "/index.html", cookie=stranger)[0], 200)
        # 카카오 쿠키 없이는 번호를 넣을 수 없다.
        self.assertEqual(self.request("POST", "/api/kakao/join", {"code": "4321"})[0], 401)

    def test_the_invite_code_stops_working_after_five_wrong_tries(self):
        # 4자리를 계정 하나로 다 맞혀 보지 못하게 — 다섯 번 틀리면 번호로는 못 들어오고 관리자를 기다린다.
        waiting = self.kakao_login(code=None)[1]
        for left in (4, 3, 2, 1):
            status, _, body = self.request("POST", "/api/kakao/join", {"code": "0000"}, cookie=waiting)
            self.assertEqual(json.loads(body)["error"], f"번호가 맞지 않습니다. 남은 기회 {left}번.")
        for code in ("0000", "4321"):
            status, _, body = self.request("POST", "/api/kakao/join", {"code": code}, cookie=waiting)
            self.assertEqual((status, json.loads(body)["error"]),
                             (403, "여러 번 틀려 번호로는 들어올 수 없습니다 — 관리자 승인을 기다려 주세요."))
        self.assertEqual(self.users.listing()[0]["status"], "pending")

    def test_without_an_invite_code_everyone_waits_for_the_admin(self):
        self.server.RequestHandlerClass.join_code = None
        waiting = self.kakao_login(code=None)[1]
        status, _, body = self.request("POST", "/api/kakao/join", {"code": "4321"}, cookie=waiting)
        self.assertEqual((status, json.loads(body)["error"]), (403, "초대 번호로는 들어올 수 없습니다 — 관리자 승인을 기다려 주세요."))

    def test_kakao_callback_refuses_a_wrong_state_a_cancel_or_a_bad_code(self):
        # SC-3 — 다른 브라우저에서 시작했거나 동의를 취소했거나 코드가 틀리면 세션 없이 로그인 화면으로.
        status, headers, _ = self.request("GET", "/api/kakao/start")
        state_cookie = headers["Set-Cookie"].split(";")[0]
        state = state_cookie.split("=", 1)[1].split("|")[0]
        for query, cookie in ((f"code=good&state=other", state_cookie), (f"code=good&state={state}", None),
                              (f"error=access_denied&state={state}", state_cookie), (f"code=bad&state={state}", state_cookie)):
            status, headers, _ = self.request("GET", "/api/kakao/callback?" + query, cookie=cookie)
            self.assertEqual((status, headers["Location"]), (302, "/login.html?error=kakao"), query)
            self.assertFalse(any(c.startswith("lod_ops=") for c in headers.get_all("Set-Cookie") or []), query)
        self.assertEqual(self.users.listing(), [])

    def test_a_denied_person_is_cut_off_at_once_and_cannot_come_back(self):
        # SC-5 — 관리자가 거부로 바꾸면 그 쿠키는 바로 막히고, 카카오로 다시 와도 못 들어온다.
        guest = self.kakao_login()[1]
        admin = self.login().split(";")[0]
        status, _, body = self.request("GET", "/api/kakao/users", cookie=admin)
        self.assertEqual([(p["id"], p["status"]) for p in json.loads(body)], [("777", "allowed")])
        status, _, body = self.request("PUT", "/api/kakao/users/777", {"status": "denied"}, cookie=admin)
        self.assertEqual((status, json.loads(body)[0]["status"]), (200, "denied"))
        status, headers, _ = self.request("GET", "/index.html", cookie=guest)
        self.assertEqual(status, 302)
        status, _, _ = self.request("GET", "/api/signed-in", cookie=guest)
        self.assertEqual(status, 401)
        (status, headers), again = self.kakao_login()
        self.assertEqual((headers["Location"], again), ("/login.html?error=denied", None))
        self.assertEqual(self.request("POST", "/api/kakao/join", {"code": "4321"}, cookie=guest)[0], 403)  # 번호로 거부를 못 푼다
        for body in ({"status": "maybe"}, {"status": "pending"}, {"allowed": True}):
            status, _, _ = self.request("PUT", "/api/kakao/users/777", body, cookie=admin)
            self.assertEqual(status, 400, body)
        status, _, _ = self.request("PUT", "/api/kakao/users/999", {"status": "allowed"}, cookie=admin)
        self.assertEqual(status, 400)
        self.request("PUT", "/api/kakao/users/777", {"status": "allowed"}, cookie=admin)
        self.assertEqual(self.request("GET", "/index.html", cookie=guest)[0], 200)

    def test_after_login_the_person_only_goes_somewhere_on_this_site(self):
        # SC-7
        for outside in ("//evil.example/x", "/\\evil.example", "https://evil.example/", "/a\r\nSet-Cookie: x=1", "evil"):
            self.assertEqual(SERVICE.safe_next(outside), "/", outside)
        self.assertEqual(SERVICE.safe_next("/?view=items&x=1"), "/?view=items&x=1")
        (status, headers), _ = self.kakao_login("//evil.example/x")  # 처음(대기) — 번호 칸으로 가도 next 는 이 사이트만
        self.assertEqual(headers["Location"], "/login.html?next=%2F")
        (status, headers), _ = self.kakao_login("//evil.example/x", code=None)  # 허가된 뒤
        self.assertEqual(headers["Location"], "/")

    def test_kakao_session_is_signed_and_expires(self):
        token = SERVICE.kakao_session(self.CREDENTIAL, "777", 60, now=1000)
        self.assertEqual(SERVICE.kakao_session_id(token, self.CREDENTIAL, now=1030), "777")
        self.assertIsNone(SERVICE.kakao_session_id(token, self.CREDENTIAL, now=1061))
        self.assertIsNone(SERVICE.kakao_session_id(token, "another-secret", now=1030))
        self.assertIsNone(SERVICE.kakao_session_id(token.replace("k777.", "k778.", 1), self.CREDENTIAL, now=1030))
        self.assertIsNone(SERVICE.kakao_session_id("k²." + token.split(".", 1)[1], self.CREDENTIAL, now=1030))
        # 서명 자리에 비ASCII 가 오면 예외로 연결이 끊기지 않고 그냥 아니다(보안 리뷰 2026-10-10 낮음 4).
        self.assertIsNone(SERVICE.kakao_session_id("k1.9999999999.é", self.CREDENTIAL))
        self.assertFalse(SERVICE.valid_session("9999999999.é", self.CREDENTIAL))

    def test_changing_the_admin_password_keeps_kakao_guests_signed_in(self):
        # 사용자 2026-10-10 「관리자 비번 바꾼다고 다 다시 로그인하면 되나」 — 카카오 쿠키는 따로 둔 열쇠로 서명한다.
        guest = self.kakao_login()[1]
        admin = self.login().split(";")[0]
        status, _, _ = self.request("POST", "/api/password", {"current": "secret", "new": "longenough1"}, cookie=admin)
        self.assertEqual(status, 200)
        self.assertEqual(self.request("GET", "/index.html", cookie=guest)[0], 200)
        self.assertEqual(self.request("GET", "/index.html", cookie=admin)[0], 302)  # 옛 관리자 로그인만 풀린다

    def test_kakao_button_says_not_ready_without_keys(self):
        self.server.RequestHandlerClass.kakao = None
        status, headers, _ = self.request("GET", "/api/kakao/start")
        self.assertEqual((status, headers["Location"]), (302, "/login.html?error=kakao-off"))
        status, headers, _ = self.request("GET", "/api/kakao/callback?code=good&state=x", cookie="lod_kakao=x|%2F")
        self.assertEqual((status, headers["Location"]), (302, "/login.html?error=kakao-off"))

    def test_a_broken_people_file_lets_no_guest_in_and_is_not_overwritten(self):
        # 리뷰 2026-10-10 낮음 2 — 깨진 목록이면 손님은 막히고(502 아님), 덮어써 거부 목록을 잃지 않는다.
        guest = self.kakao_login()[1]
        self.users.path.write_text("[broken", encoding="utf-8")
        self.assertEqual(self.request("GET", "/index.html", cookie=guest)[0], 302)
        (status, headers), again = self.kakao_login()
        self.assertEqual((headers["Location"], again), ("/login.html?error=kakao", None))
        self.assertEqual(self.users.path.read_text(encoding="utf-8"), "[broken")
        self.assertEqual(self.request("GET", "/api/kakao/users", cookie=self.login().split(";")[0])[0], 500)

    def test_a_cut_off_kakao_answer_sends_back_to_login(self):
        # 리뷰 2026-10-10 낮음 1 — 카카오 응답이 중간에 끊겨도 처리기가 죽지 않는다.
        import http.client
        def cut(config, code):
            raise http.client.IncompleteRead(b"")
        self.server.RequestHandlerClass.kakao_exchange = staticmethod(cut)
        (status, headers), session = self.kakao_login()
        self.assertEqual((status, headers["Location"], session), (302, "/login.html?error=kakao", None))

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
        _, _, body = self.request("GET", "/api/state/item-names", cookie=phone)
        self.assertEqual(json.loads(body), {"Stick": "막대기", "Eppe": "에페"})
        self.request("PUT", "/api/state/item-names", {"changes": {"Stick": None}}, cookie=phone)  # null 은 그 칸만 지운다
        _, _, body = self.request("GET", "/api/state/item-names", cookie=phone)
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
        _, _, body = self.request("GET", "/api/state/item-names", cookie=cookie)
        self.assertEqual(json.loads(body), {"Stick": "막대기"})
        status, _, _ = self.request("PUT", "/api/ability-overrides/" + urllib.parse.quote("skill:단각"),
                                    {"values": {"effect": 5}, "revision": 0}, cookie=cookie)
        self.assertEqual(status, 500)
        _, _, body = self.request("GET", "/api/ability-overrides", cookie=cookie)
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
