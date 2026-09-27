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


if __name__ == "__main__":
    unittest.main()
