import importlib.util
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))
SPEC = importlib.util.spec_from_file_location(
    "compare_packs", ROOT / "scripts" / "compare-packs.py"
)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
grade = MODULE.grade


class GradeTest(unittest.TestCase):
    def test_no_candidate_is_not_a_finding(self):
        self.assertEqual(grade({}), "NONE")

    def test_one_pack_alone_is_not_consensus(self):
        self.assertEqual(grade({"honden-community": ["가죽각반"]}), "EXTRACTED_SINGLE")

    def test_two_independent_packs_agreeing_is_consensus(self):
        self.assertEqual(
            grade({"honden-community": ["가죽각반"], "novaonline": ["가죽각반"]}),
            "PACK_CONSENSUS",
        )

    def test_same_lineage_packs_do_not_count_as_two_witnesses(self):
        self.assertEqual(
            grade({"5.99-server": ["가죽각반"], "novaonline": ["가죽각반"]}),
            "SAME_LINEAGE",
        )

    def test_different_names_on_one_image_is_a_conflict(self):
        self.assertEqual(
            grade({"5.99-server": ["가죽각반"], "honden-community": ["가죽장갑"]}),
            "CONFLICT",
        )

    def test_one_pack_listing_several_names_is_also_a_conflict(self):
        self.assertEqual(grade({"novaonline": ["가죽각반", "가죽각반(Lev3)"]}), "CONFLICT")


if __name__ == "__main__":
    unittest.main()


class PlaceholderNameTest(unittest.TestCase):
    def test_number_only_name_is_an_image_reference_sheet_row(self):
        self.assertTrue(MODULE.is_placeholder("01"))
        self.assertTrue(MODULE.is_placeholder(" 238 "))

    def test_real_item_names_survive(self):
        self.assertFalse(MODULE.is_placeholder("가죽각반"))
        self.assertFalse(MODULE.is_placeholder("가죽각반(Lev3)"))


class AffixNarrowingTest(unittest.TestCase):
    def test_english_affix_vocabulary_comes_from_the_data(self):
        items = [{"Name": "Iron Greaves", "Image": 250},
                 {"Name": "Cail Iron Greaves", "Image": 250},
                 {"Name": "Deoch Iron Greaves", "Image": 250},
                 {"Name": "Cail Leather Greaves", "Image": 250},
                 {"Name": "Leather Greaves", "Image": 250},
                 {"Name": "Cail Bracer", "Image": 227},
                 {"Name": "Bracer", "Image": 227}]
        self.assertEqual(MODULE.english_affixes(items), {"Cail"})

    def test_korean_affix_needs_its_base_on_the_same_image(self):
        siblings = {"동각반", "칸의동각반", "정의의검"}
        self.assertEqual(MODULE.split_ko("칸의동각반", siblings), ("칸", "동각반"))
        self.assertEqual(MODULE.split_ko("정의의검", siblings), (None, "정의의검"))

    def test_unmapped_english_affix_gets_no_korean_name(self):
        # Deoch·Sgrios 는 아직 한글 짝을 모른다. \"접사 없는 이름\" 으로 떨어뜨리면 틀린 짝이 된다.
        cand = {"novaonline": ["동각반", "칸의동각반"]}
        self.assertEqual(MODULE.narrow_by_affix(cand, "Deoch Iron Greaves", {"Deoch", "Gramail"}), {})

    def test_plain_english_name_keeps_only_the_plain_korean_name(self):
        cand = {"novaonline": ["동각반", "칸의동각반"]}
        self.assertEqual(MODULE.narrow_by_affix(cand, "Iron Greaves", {"Gramail"}),
                         {"novaonline": ["동각반"]})

    def test_mapped_affix_keeps_only_that_variant(self):
        cand = {"novaonline": ["동각반", "칸의동각반"]}
        self.assertEqual(MODULE.narrow_by_affix(cand, "Gramail Iron Greaves", {"Gramail"}),
                         {"novaonline": ["칸의동각반"]})

    def test_durability_separates_gear_from_supplies(self):
        self.assertTrue(MODULE.wearable_pack({"내구력": "6000"}))
        self.assertFalse(MODULE.wearable_pack({}))              # 동전·설탕에는 내구력이 없다
        self.assertTrue(MODULE.wearable_hades({"MaxDurability": 3000}))
        self.assertFalse(MODULE.wearable_hades({"MaxDurability": 0}))
