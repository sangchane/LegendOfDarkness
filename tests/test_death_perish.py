"""죽으면 사라지는 장비 규칙(사용자 2026-10-11)과 Flags 꼴 지키기 — build-death-perish.py."""
import importlib.util
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))
SPEC = importlib.util.spec_from_file_location("build_death_perish", ROOT / "scripts" / "gen" / "items" / "build-death-perish.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class RuleTest(unittest.TestCase):
    def test_god_prefixes_perish_in_any_slot(self):
        for name in ("세토아의카이레", "칸의산호귀걸이", "체력의반지", "마력의목걸이", "뮤레칸의가죽장갑"):
            self.assertTrue(MODULE.perishes({"Name": name, "EquipmentSlot": 1}), name)

    def test_elemental_necklace_and_belt_perish_but_not_crystal(self):
        self.assertTrue(MODULE.perishes({"Name": "바람의진주목걸이", "EquipmentSlot": 6, "OffenseElement": 3}))
        self.assertTrue(MODULE.perishes({"Name": "화염의가죽벨트", "EquipmentSlot": 11, "DefenseElement": 1}))
        self.assertFalse(MODULE.perishes({"Name": "대지의크리스탈목걸이", "EquipmentSlot": 6, "OffenseElement": 4}))
        self.assertFalse(MODULE.perishes({"Name": "빛의목걸이", "EquipmentSlot": 6, "OffenseElement": 5}))  # 빛·어둠은 수토풍화 밖
        self.assertFalse(MODULE.perishes({"Name": "어둠의벨트", "EquipmentSlot": 11, "DefenseElement": 6}))

    def test_plain_gear_and_elemental_other_slots_stay(self):
        self.assertFalse(MODULE.perishes({"Name": "진주목걸이", "EquipmentSlot": 6, "OffenseElement": 0}))
        self.assertFalse(MODULE.perishes({"Name": "화염의검", "EquipmentSlot": 1, "OffenseElement": 1}))
        self.assertFalse(MODULE.perishes({"Name": "칸칸의옷", "EquipmentSlot": 2}))   # 「칸의」가 아니다


class FlagsShapeTest(unittest.TestCase):
    def test_number_flags_toggle_only_the_perish_bit(self):
        self.assertEqual(63, MODULE.toggled(61, True))
        self.assertEqual(61, MODULE.toggled(63, False))

    def test_word_flags_keep_their_shape(self):
        self.assertEqual("NormalEquipPerish", MODULE.toggled("NormalEquipment", True))
        self.assertEqual("NormalEquipment", MODULE.toggled("NormalEquipPerish", False))
        self.assertEqual("NormalEquipment, Elemental", MODULE.toggled("NormalEquipElementPerish", False))
        self.assertEqual("Equipable, Dropable, Perishable", MODULE.toggled("Equipable, Dropable", True))


class TemplatesFollowRuleTest(unittest.TestCase):
    """서버 템플릿의 사라짐 표시가 규칙 그대로인지(생성기를 --쓰기 한 뒤)."""

    def test_every_equipment_template_matches_the_rule(self):
        wrong = []
        for path in sorted(MODULE.ITEMS.glob("*.json")):
            body = json.loads(path.read_text(encoding="utf-8-sig"))
            flags = body.get("Flags")
            if flags is None or not MODULE.value(flags) & MODULE.FLAG["Equipable"]:
                continue
            if MODULE.perishes(body) != bool(MODULE.value(flags) & MODULE.PERISH):
                wrong.append(body["Name"])
        self.assertEqual([], wrong)


if __name__ == "__main__":
    unittest.main()
