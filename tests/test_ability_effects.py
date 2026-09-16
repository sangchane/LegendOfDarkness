"""기술·마법의 아이콘·레벨·이펙트·사운드를 팩에서 뽑는 것을 지킨다."""
import importlib.util
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))      # graphify_runtime 이 거기 있다

SPEC = importlib.util.spec_from_file_location(
    "build_ability_effects", ROOT / "scripts" / "build-ability-effects.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class BlockFileTest(unittest.TestCase):
    """팩의 `{ 이름<탭>값 }` 덩어리 형식."""

    def test_reads_each_block_as_one_row(self):
        text = ("////////////new//////////\n"
                "{\n이름\t파워단련(Lev1)\n이미지\t99\n타입\t0\n딜레이\t0\n"
                "script_do\tSPELL_파워단련(Lev1)\n}\n"
                "////////////////////////\n"
                "{\n이름\t쿠로\n이미지\t28\n타입\t2\n}\n")
        rows = MODULE.parse_blocks(text)
        self.assertEqual([r["이름"] for r in rows], ["파워단련(Lev1)", "쿠로"])
        self.assertEqual(rows[0]["이미지"], "99")
        self.assertEqual(rows[0]["script_do"], "SPELL_파워단련(Lev1)")

    def test_ignores_comment_and_blank_lines(self):
        self.assertEqual(MODULE.parse_blocks("// 주석\n\n{\n이름\t가\n}\n"),
                         [{"이름": "가"}])


class LevelTest(unittest.TestCase):
    """기술은 아이템의 접사 자리에 레벨이 온다 — `홀리볼트(Lev3)`."""

    def test_splits_trailing_level(self):
        self.assertEqual(MODULE.split_level("홀리볼트(Lev3)"), ("홀리볼트", 3))
        self.assertEqual(MODULE.split_level("쿠로"), ("쿠로", None))

    def test_keeps_parentheses_that_are_not_levels(self):
        # `(여)` 같은 꼬리는 레벨이 아니다. 떼면 다른 물건이 된다.
        self.assertEqual(MODULE.split_level("적비화무투구(여)"), ("적비화무투구(여)", None))


class ScriptTest(unittest.TestCase):
    """스크립트 본문에 있다 — 소리는 `game_sound`(`sound` 가 아니다), 모션은 `motion N, M`."""

    SCRIPT = ("0,0,0,0,0,0,0\tSKILL_내려치기(Lev1)\t{\n"
              "\tset @myid, get_myid();\n"
              "\t\t\t\teffect @mob, 0, 48, 100;\n"
              "\t\t\t\tgame_sound 78, 0;\n"
              "\t\t\t\tmotion 141, 40;\n"
              "\t\t\t\tgame_sound 78, 0;\n"
              "}\n"
              "0,0,0,0,0,0,0\tSKILL_다른것\t{\n\tgame_sound 9, 0;\n}\n")

    def test_picks_only_the_named_script(self):
        got = MODULE.script_media(self.SCRIPT, "SKILL_내려치기(Lev1)")
        self.assertEqual(got["사운드"], [78])          # 두 번 나와도 한 번만 센다
        self.assertEqual(got["이펙트"], ["@mob, 0, 48, 100"])
        self.assertEqual(got["모션"], [[141, 40]])

    def test_sound_means_game_sound_only(self):
        # 팩에 `sound` 단독 지시는 없다 — `game_sound` 만 3,989 회 쓴다.
        # 세는 말을 틀리면 조용히 0 개가 나온다. 실제로 그렇게 나왔다.
        got = MODULE.script_media("x\tS\t{\n game_sound 35, 0;\n}\n", "S")
        self.assertEqual(got["사운드"], [35])

    def test_missing_script_is_empty_not_an_error(self):
        self.assertEqual(MODULE.script_media(self.SCRIPT, "SKILL_없는것"),
                         {"사운드": [], "이펙트": [], "모션": []})

    def test_reads_effect_with_numbers(self):
        got = MODULE.script_media("x\tS\t{\n effect @mob12, 0, 136, 75;\n}\n", "S")
        self.assertEqual(got["이펙트"], ["@mob12, 0, 136, 75"])


if __name__ == "__main__":
    unittest.main()
