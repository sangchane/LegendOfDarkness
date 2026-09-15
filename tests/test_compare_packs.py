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
        # 짝을 모르는 접사는 \"접사 없는 이름\" 으로 떨어뜨리면 안 된다 — 틀린 짝이 된다.
        # (Deoch·Sgrios 는 카페 520 으로 짝을 찾았다. 아직 모르는 것만 여기 남는다.)
        unknown = "Zzz"
        self.assertNotIn(unknown, MODULE.EN_TO_KO)
        cand = {"novaonline": ["동각반", "칸의동각반"]}
        self.assertEqual(MODULE.narrow_by_affix(cand, f"{unknown} Iron Greaves", {unknown, "Gramail"}), {})

    def test_cafe_520_affixes_are_mapped(self):
        # 카페 `【item】 520` 이 적어 둔 한↔영 접사. 수치 칸으로는 못 가리던 둘이 여기 있다.
        self.assertEqual(MODULE.AFFIX["세오"], "Deoch")
        self.assertEqual(MODULE.AFFIX["뮤레칸"], "Sgrios")
        self.assertEqual(MODULE.EN_TO_KO["Magic"], {"마법", "마력"})   # 한 영문에 한글 둘
        cand = {"novaonline": ["가죽각반", "세오의가죽각반"]}
        self.assertEqual(MODULE.narrow_by_affix(cand, "Deoch Leather Greaves", {"Deoch"}),
                         {"novaonline": ["세오의가죽각반"]})

    def test_display_image_is_the_key_not_image(self):
        # `Image` 는 인벤토리 아이콘이라 여럿이 나눠 쓴다. 갈리는 값은 `DisplayImage` 다.
        self.assertEqual(MODULE.hades_image({"Image": 210, "DisplayImage": 32974}), 206)
        self.assertEqual(MODULE.hades_image({"Image": 238}), 238)      # 없으면 Image 로 돌아간다

    def test_ko_base_strips_variant_shells(self):
        siblings = {"라비린스메일", "동각반"}
        self.assertEqual(MODULE.ko_base("로오의동각반", siblings), "동각반")
        self.assertEqual(MODULE.ko_base("[속]화염의목걸이", siblings), "목걸이")
        self.assertEqual(MODULE.ko_base("라비린스메일수", siblings), "라비린스메일")
        self.assertEqual(MODULE.ko_base("설단검(x)", siblings), "설단검")
        # `의` 가 없으면 접사가 아니다 — `뮤레칸가면` 은 뮤레칸을 본뜬 가면이다.
        self.assertEqual(MODULE.ko_base("뮤레칸글러브+1", siblings), "뮤레칸글러브")
        # 속성 꼬리는 벗긴 것이 실제로 있을 때만 벗긴다.
        self.assertEqual(MODULE.ko_base("유황화", siblings), "유황화")

    def test_collapse_keeps_body_part_words_out(self):
        # 후보가 전부 접사투성이면 밑말이 부위 이름만 남는다. 그것은 물건 이름이 아니다.
        rows = [{"영문": "Loures Signet Ring", "한글이름": None, "등급": "CONFLICT",
                 "한글후보": {"honden-community": ["로오의반지"]}}]
        MODULE.collapse_ko_base(rows, set())
        self.assertIsNone(rows[0]["한글이름"])

    def test_collapse_settles_when_variants_agree(self):
        rows = [{"영문": "Iron Greaves", "한글이름": None, "등급": "CONFLICT",
                 "한글후보": {"novaonline": ["로오의동각반", "칸의동각반", "동각반"]}}]
        self.assertEqual(MODULE.collapse_ko_base(rows, set()), 1)
        self.assertEqual(rows[0]["한글이름"], "동각반")

    def test_derive_uses_the_preferred_spelling_when_korean_has_two(self):
        # Magic 은 팩에 마법·마력이 다 있다. 지을 때는 `마력의가죽각반` 으로 적는다 (카페 520).
        rows = [{"영문": "Leather Greaves", "한글이름": "가죽각반", "등급": "PACK_CONSENSUS",
                 "영문접사": None, "영문밑말": "Leather Greaves"},
                {"영문": "Deoch Leather Greaves", "한글이름": None, "등급": "CONFLICT",
                 "영문접사": "Deoch", "영문밑말": "Leather Greaves"},
                {"영문": "Magic Leather Greaves", "한글이름": None, "등급": "CONFLICT",
                 "영문접사": "Magic", "영문밑말": "Leather Greaves"}]
        self.assertEqual(MODULE.derive_affixed(rows), 2)
        self.assertEqual(rows[1]["한글이름"], "세오의가죽각반")
        self.assertEqual(rows[2]["한글이름"], "마력의가죽각반")
        # 고를 근거가 없는 접사는 그대로 짓지 않는다.
        self.assertNotIn("Fire", MODULE.PREFERRED_KO)

    def test_both_magic_spellings_still_match(self):
        # 이름을 지을 때만 마력을 고른다. 짝을 찾을 때는 팩에 있는 둘 다 받는다.
        self.assertEqual(MODULE.EN_TO_KO["Magic"], {"마법", "마력"})
        cand = {"novaonline": ["마법의가죽각반"]}
        self.assertEqual(MODULE.narrow_by_affix(cand, "Magic Leather Greaves", {"Magic"}),
                         {"novaonline": ["마법의가죽각반"]})

    def test_weapon_tiers_line_up_by_rank(self):
        # 영문은 품질로, 한글은 속성 글자로 쪼갰지만 둘 다 데미지 사다리다 (자료에서 28 : 3 군).
        self.assertEqual(MODULE.EN_TIER_TO_KO["Good"], "수")
        self.assertEqual(MODULE.EN_TIER_TO_KO["Great"], "화")
        cand = {"novaonline": ["일단검", "일단검수", "일단검토", "일단검풍", "일단검화"]}
        rows = [{"영문": "Good Sun Dagger", "한글이름": None, "등급": "NAME_CLASH", "한글후보": cand},
                {"영문": "Great Sun Dagger", "한글이름": None, "등급": "NAME_CLASH", "한글후보": cand}]
        self.assertEqual(MODULE.match_weapon_tiers(rows), 2)
        self.assertEqual(rows[0]["한글이름"], "일단검수")
        self.assertEqual(rows[1]["한글이름"], "일단검화")

    def test_weapon_tier_never_invents_a_missing_rung(self):
        # 그 칸이 후보에 없으면 짓지 않는다.
        rows = [{"영문": "Great Sun Dagger", "한글이름": None, "등급": "NAME_CLASH",
                 "한글후보": {"novaonline": ["일단검", "일단검수"]}}]
        self.assertEqual(MODULE.match_weapon_tiers(rows), 0)
        self.assertIsNone(rows[0]["한글이름"])

    def test_name_clash_drops_every_claimant(self):
        # 템플릿은 이름이 열쇠다. 셋이 한 이름을 차지하면 둘이 조용히 사라진다.
        rows = [{"영문": "Pearl Necklace", "한글이름": "진주목걸이", "등급": "PACK_CONSENSUS"},
                {"영문": "Dark Pearl Necklace", "한글이름": "진주목걸이", "등급": "KO_BASE_COLLAPSED"},
                {"영문": "Wooden Shield", "한글이름": "나무방패", "등급": "PACK_CONSENSUS"}]
        self.assertEqual(MODULE.drop_name_clashes(rows), 2)
        self.assertIsNone(rows[0]["한글이름"])
        self.assertIsNone(rows[1]["한글이름"])
        self.assertEqual(rows[0]["등급"], "NAME_CLASH")
        self.assertEqual(rows[2]["한글이름"], "나무방패")   # 혼자 쓰는 이름은 그대로 둔다

    def test_compose_only_uses_names_the_packs_have(self):
        rows = [{"영문": "Leather Greaves", "한글이름": "가죽각반", "등급": "PACK_CONSENSUS", "접사맞춤": {}},
                {"영문": "Deoch Leather Greaves", "한글이름": None, "등급": "CONFLICT", "접사맞춤": {}}]
        MODULE.compose_affixed(rows, {"Deoch"}, {"세오의가죽각반"})
        self.assertEqual(rows[1]["한글이름"], "세오의가죽각반")
        rows[1]["한글이름"] = None
        MODULE.compose_affixed(rows, {"Deoch"}, set())          # 팩에 없으면 짓지 않는다
        self.assertIsNone(rows[1]["한글이름"])

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
