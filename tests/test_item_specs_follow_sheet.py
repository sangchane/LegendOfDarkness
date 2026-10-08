"""입는 물건의 수치는 어둠템(도감)대로 — 사용자 2026-10-09 「아이템 스펙은 이 데이터에 있는걸 기준으로 해야해」·「어둠템 값에 맞춰 장비스펙은」.

정본 `docs/items/어둠템#1~5.xlsx` → `data/game-data/items-original-sheets.json`. 서버 템플릿에 같은 이름이 있으면 능력치·레벨·직업이
같아야 한다. 어긋나면 `python3 scripts/gen/items/build-gear-from-original.py --쓰기` 로 되돌린다(`autopilot/item-specs/SPEC.md`).
값(Value)은 보지 않는다 — 서클 상한이 정한다(`build-price-cap.py`).
"""
import importlib.util
import json
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))
from lib._gear_original import MODIFIERS, differs, wanted  # noqa: E402

SPEC = importlib.util.spec_from_file_location("gear_from_original", ROOT / "scripts" / "gen" / "items" / "build-gear-from-original.py")
assert SPEC and SPEC.loader
GENERATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GENERATOR)

ITEMS = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates" / "items"
SHEET = ROOT / "data" / "game-data" / "items-original-sheets.json"
SPEC_FIELDS = set(MODIFIERS.values()) | {"LevelRequired", "Class"}


class ItemSpecsFollowSheetTest(unittest.TestCase):
    def test_every_wearable_in_the_sheet_has_the_sheet_specs(self):
        rows = {}
        for row in json.loads(SHEET.read_text(encoding="utf-8"))["수치표"]:
            rows.setdefault(row["이름"], row)

        checked, wrong = 0, []
        for path in sorted(ITEMS.glob("*.json")):
            item = json.loads(path.read_text(encoding="utf-8-sig"))
            row = rows.get(item.get("Name"))
            if not item.get("EquipmentSlot") or row is None or item["Name"] in GENERATOR.KEEP:
                continue
            checked += 1
            off = [field for field, value in wanted(row).items() if field in SPEC_FIELDS and differs(item, field, value)]
            if off:
                wrong.append(f"{item['Name']} ({item.get('Group')}): {', '.join(off)}")

        self.assertGreater(checked, 1000, "어둠템과 견준 장비가 너무 적다 — 경로나 자료가 바뀌었나")
        self.assertEqual([], wrong, f"어둠템과 다른 장비 {len(wrong)}장")


if __name__ == "__main__":
    unittest.main()
