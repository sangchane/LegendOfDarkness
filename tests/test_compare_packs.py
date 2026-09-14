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
