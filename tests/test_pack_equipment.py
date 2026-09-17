"""5.99 장비 생성기가 같은 이름을 말없이 덮지 않고, 뺀 것을 이유별로 세는 것을 지킨다."""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))      # graphify_runtime 이 거기 있다
SPEC = importlib.util.spec_from_file_location("build_pack_equipment", ROOT / "scripts" / "build-pack-equipment.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def item(name, **fields):
    return {"이름": name, "출처": "item/공통반지.txt", "fields": {"이름": name, **fields}}


class EquipmentTest(unittest.TestCase):
    def run_main(self, items):
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / "items.json"
            source.write_text(json.dumps(items, ensure_ascii=False), encoding="utf-8")
            with mock.patch.object(MODULE, "ITEMS", source), mock.patch.object(MODULE, "OUT", Path(tmp) / "out"), \
                    mock.patch.object(MODULE, "ROOT", Path(tmp)), mock.patch.object(sys, "argv", ["build-pack-equipment.py"]):
                return MODULE.main()

    def test_duplicate_name_stops_instead_of_dropping_one(self):
        with self.assertRaises(SystemExit) as stopped:
            self.run_main([item("은반지", 속성="6"), item("은반지", 속성="6", 방어력="-1")])
        self.assertIn("은반지", str(stopped.exception.code))

    def test_skip_reasons(self):
        self.assertEqual(MODULE.classify({"타입": "2", "속성": "3"}), (None, "타입이 0 이 아니다"))
        self.assertEqual(MODULE.classify({"속성": "0"})[1], "무기 자리인데 공격력 칸이 없다(재료)")
        self.assertEqual(MODULE.classify({"속성": "1"})[1], "갑옷인데 착용 그림이 없다")
        self.assertEqual(MODULE.classify({"속성": "99"})[1], "속성이 장비 자리가 아니다")
        self.assertEqual(MODULE.classify({"속성": "6"}), ("반지", None))

    def test_unique_names_still_run(self):
        self.assertEqual(self.run_main([item("은반지", 속성="6"), item("금반지", 속성="6"), item("물약", 타입="2")]), 0)


if __name__ == "__main__":
    unittest.main()
