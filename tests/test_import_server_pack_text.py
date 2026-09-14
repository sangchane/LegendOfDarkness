import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts" / "import-server-pack-text.py"
SPEC = importlib.util.spec_from_file_location("import_server_pack_text", SCRIPT)
importer = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(importer)


class ServerPackTextImporterTest(unittest.TestCase):
    def setUp(self):
        self.tempdir = tempfile.TemporaryDirectory()
        self.root = Path(self.tempdir.name)
        self.repo = self.root / "repo"
        self.source = self.repo / "sources" / "sample"
        (self.source / "db" / "monster").mkdir(parents=True)

    def tearDown(self):
        self.tempdir.cleanup()

    def test_converts_cp949_korean_and_preserves_relative_path(self):
        raw = "슬라임|체력=100\r\n".encode("cp949")
        (self.source / "db" / "monster" / "슬라임.txt").write_bytes(raw)

        result = importer.import_pack(self.source, "novaonline", self.repo, write=True)

        output = self.repo / "data" / "server-packs" / "novaonline"
        copied = output / "db" / "monster" / "슬라임.txt"
        self.assertEqual(copied.read_bytes(), "슬라임|체력=100\r\n".encode("utf-8"))
        metadata = json.loads((output / "source.json").read_text(encoding="utf-8"))
        self.assertEqual(metadata["source"], "sources/sample")
        self.assertEqual(metadata["encoding"]["input"], {"cp949": 1, "utf-8-sig": 0})
        self.assertEqual(metadata["encoding"]["output"], "utf-8")
        self.assertEqual(metadata["copied_text_files"], 1)
        self.assertEqual(result.copied, 1)

    def test_utf8_sig_is_preferred_and_bom_is_removed(self):
        payload = "이미 UTF-8\n".encode("utf-8-sig")
        source_file = self.source / "db" / "monster" / "utf8.txt"
        source_file.write_bytes(payload)

        importer.import_pack(self.source, "novaonline", self.repo, write=True)

        output_file = self.repo / "data" / "server-packs" / "novaonline" / "db" / "monster" / "utf8.txt"
        self.assertEqual(output_file.read_bytes(), "이미 UTF-8\n".encode("utf-8"))

    def test_dry_run_is_default_and_writes_nothing(self):
        (self.source / "db" / "monster" / "one.txt").write_text("one", encoding="utf-8")

        result = importer.import_pack(self.source, "novaonline", self.repo)

        self.assertFalse((self.repo / "data").exists())
        self.assertFalse(result.written)
        self.assertEqual(result.copied, 1)

    def test_rejects_unsafe_pack_ids_and_missing_db(self):
        for unsafe in ("../escape", "NovaOnline", "two words", "a/b", ""):
            with self.subTest(pack_id=unsafe):
                with self.assertRaises(ValueError):
                    importer.import_pack(self.source, unsafe, self.repo)

        missing = self.repo / "sources" / "missing"
        missing.mkdir(parents=True)
        with self.assertRaises(FileNotFoundError):
            importer.import_pack(missing, "missing", self.repo)

    def test_rejects_empty_db_without_clearing_an_existing_snapshot(self):
        target = self.repo / "data" / "server-packs" / "empty" / "db"
        target.mkdir(parents=True)
        marker = target / "keep.txt"
        marker.write_text("keep", encoding="utf-8")

        with self.assertRaises(ValueError):
            importer.import_pack(self.source, "empty", self.repo, write=True)

        self.assertTrue(marker.exists())

    def test_decode_failure_is_preflighted_before_existing_db_is_cleared(self):
        target = self.repo / "data" / "server-packs" / "novaonline" / "db"
        target.mkdir(parents=True)
        marker = target / "keep.txt"
        marker.write_text("keep", encoding="utf-8")
        (self.source / "db" / "monster" / "invalid.txt").write_bytes(b"\x81\x00")

        with self.assertRaises(UnicodeError):
            importer.import_pack(self.source, "novaonline", self.repo, write=True)

        self.assertTrue(marker.exists())

    def test_excludes_binaries_and_hashes_exactly_one_top_level_exe(self):
        (self.source / "db" / "monster" / "one.txt").write_text("one", encoding="utf-8")
        (self.source / "db" / "monster" / "map.map").write_bytes(b"map")
        (self.source / "db" / "native.dll").write_bytes(b"dll")
        (self.source / "save").mkdir()
        (self.source / "save" / "player.txt").write_text("save", encoding="utf-8")
        executable = self.source / "Legend.exe"
        executable.write_bytes(b"native executable")

        importer.import_pack(self.source, "novaonline", self.repo, write=True)

        output = self.repo / "data" / "server-packs" / "novaonline"
        self.assertTrue((output / "db" / "monster" / "one.txt").is_file())
        self.assertFalse((output / "db" / "monster" / "map.map").exists())
        self.assertFalse((output / "db" / "native.dll").exists())
        self.assertFalse((output / "save").exists())
        metadata = json.loads((output / "source.json").read_text(encoding="utf-8"))
        self.assertEqual(metadata["top_level_exe"]["name"], "Legend.exe")
        self.assertEqual(metadata["top_level_exe"]["sha256"], hashlib.sha256(b"native executable").hexdigest())

        (self.source / "Second.exe").write_bytes(b"second")
        importer.import_pack(self.source, "two-exes", self.repo, write=True)
        metadata = json.loads(
            (self.repo / "data" / "server-packs" / "two-exes" / "source.json").read_text(encoding="utf-8")
        )
        self.assertIsNone(metadata["top_level_exe"])

    def test_rejects_a_txt_symlink_that_escapes_source_db(self):
        outside = self.root / "outside.txt"
        outside.write_text("outside", encoding="utf-8")
        link = self.source / "db" / "monster" / "linked.txt"
        try:
            link.symlink_to(outside)
        except OSError as error:
            self.skipTest(f"symlinks unavailable: {error}")

        with self.assertRaises(ValueError):
            importer.import_pack(self.source, "novaonline", self.repo)

    def test_write_clears_only_stale_target_db(self):
        target = self.repo / "data" / "server-packs" / "novaonline"
        (target / "db").mkdir(parents=True)
        (target / "db" / "stale.txt").write_text("stale", encoding="utf-8")
        (target / "keep.txt").write_text("unrelated", encoding="utf-8")
        (self.source / "db" / "monster" / "fresh.txt").write_text("fresh", encoding="utf-8")

        importer.import_pack(self.source, "novaonline", self.repo, write=True)

        self.assertFalse((target / "db" / "stale.txt").exists())
        self.assertTrue((target / "db" / "monster" / "fresh.txt").is_file())
        self.assertEqual((target / "keep.txt").read_text(encoding="utf-8"), "unrelated")

    def test_clear_guard_rejects_a_db_outside_the_exact_pack_parent(self):
        pack_root = self.repo / "data" / "server-packs" / "novaonline"
        outside_db = self.repo / "unrelated" / "db"
        outside_db.mkdir(parents=True)
        marker = outside_db / "keep.txt"
        marker.write_text("keep", encoding="utf-8")

        with self.assertRaises(ValueError):
            importer.clear_target_db(outside_db, pack_root)

        self.assertTrue(marker.exists())

    def test_rejects_non_file_source_metadata_before_clearing_db(self):
        target = self.repo / "data" / "server-packs" / "novaonline"
        (target / "db").mkdir(parents=True)
        marker = target / "db" / "keep.txt"
        marker.write_text("keep", encoding="utf-8")
        (target / "source.json").mkdir()
        (self.source / "db" / "monster" / "fresh.txt").write_text("fresh", encoding="utf-8")

        with self.assertRaises(ValueError):
            importer.import_pack(self.source, "novaonline", self.repo, write=True)

        self.assertTrue(marker.exists())

    def test_restores_existing_db_if_final_metadata_replace_fails(self):
        target = self.repo / "data" / "server-packs" / "novaonline"
        (target / "db").mkdir(parents=True)
        marker = target / "db" / "old.txt"
        marker.write_text("old", encoding="utf-8")
        (target / "source.json").write_text('{"old": true}\n', encoding="utf-8")
        (self.source / "db" / "monster" / "fresh.txt").write_text("fresh", encoding="utf-8")
        real_replace = importer.os.replace

        def fail_metadata(source, destination):
            if Path(source).name == "source.json":
                raise OSError("simulated metadata failure")
            return real_replace(source, destination)

        with mock.patch.object(importer.os, "replace", side_effect=fail_metadata):
            with self.assertRaises(OSError):
                importer.import_pack(self.source, "novaonline", self.repo, write=True)

        self.assertEqual(marker.read_text(encoding="utf-8"), "old")
        self.assertFalse((target / "db" / "monster" / "fresh.txt").exists())
        self.assertEqual((target / "source.json").read_text(encoding="utf-8"), '{"old": true}\n')


if __name__ == "__main__":
    unittest.main()
