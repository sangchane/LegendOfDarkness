"""5.99 팩 스크립트 → C# 변환기(기술·마법 Translator, NPC NpcTranslator)를 지킨다.

작은 조각을 옮겨 **중요한 조각만** 본다 — 들여쓰기·괄호 모양이 조금 바뀌어도 깨지지 않게, 뜻이 달라지면 깨지게.
"""
import importlib.util
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))      # graphify_runtime 이 거기 있다


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ABILITIES = load("build_pack_abilities", "build-pack-abilities.py")
NPCS = load("build_pack_npcs", "build-pack-npcs.py")


def compact(code):
    """공백을 모두 지운다 — 조각을 들여쓰기와 상관없이 찾으려고."""
    return "".join(code.split())


class AbilityTranslatorTest(unittest.TestCase):
    def translate(self, text):
        return ABILITIES.translate(text)

    def test_commands_variables_and_hex_literal(self):
        code, variables, calls, flags = self.translate(
            "set @hp, get_hp(@myid);\n"
            "if(@hp < 0x01) { goto go1; }\n"
            "damaged @target, @hp * 2;\n"
            "go1:\n"
            "effect 5;")
        self.assertIn('v_hp = p.Call("get_hp", v_myid);', code)
        self.assertIn("(V)0x01L", code)
        self.assertIn('p.Call("damaged", v_target, ((V)(v_hp) * (V)((V)2L)));', code)
        self.assertIn("goto L_go1;", code)
        self.assertIn("L_go1: ;", code)
        self.assertEqual(variables, ["v_hp", "v_myid", "v_target"])
        self.assertEqual(dict(calls), {"get_hp": 1, "damaged": 1, "effect": 1})
        self.assertEqual(flags, [])

    def test_label_order_is_kept(self):
        code, *_ = self.translate("goto go1; effect 1; go1: effect 2;")
        self.assertLess(code.index("goto L_go1;"), code.index('p.Call("effect", (V)1L)'))
        self.assertLess(code.index('p.Call("effect", (V)1L)'), code.index("L_go1: ;"))
        self.assertLess(code.index("L_go1: ;"), code.index('p.Call("effect", (V)2L)'))

    def test_jump_to_missing_label_goes_to_the_end(self):
        code, *_ = self.translate("goto go4; effect 1;")
        self.assertTrue(code.rstrip().endswith("L_go4: ;"))

    def test_label_inside_else_becomes_entry_flag(self):
        # C# 은 블록 안쪽 라벨로 뛰어들지 못한다 — if 앞으로 가서 곧장 else 로 들어가게 바꾼다.
        code, _, _, flags = self.translate('if(@a == 1){ goto go2; } else { go2: message "x"; }')
        self.assertEqual(flags, ["f_go2"])
        self.assertIn("L_go2_enter: ;", code)
        self.assertIn("V.B(!f_go2 &&", code)
        self.assertIn("{ f_go2 = true; goto L_go2_enter; }", code)
        self.assertIn("f_go2 = false;", code)
        self.assertLess(code.index("L_go2_enter: ;"), code.index("if (V.T("))

    def test_nested_if_else_and_persistent_and_string_vars(self):
        code, variables, _, _ = self.translate(
            'if(@a == 1 && #quest != 2) { if(@b) { end; } else { set #quest, 3; } } '
            'else { set @msg$, "안녕"; }')
        flat = compact(code)
        self.assertIn(compact("V.B(V.T(((V)(v_a) == (V)((V)1L))) && V.T(((V)(h_quest) != (V)((V)2L))))"), flat)
        self.assertIn(compact("if (V.T(v_b)) { return; } else { h_quest = (V)3L; }"), flat)
        self.assertIn(compact('else { v_msg_s = (V)"안녕"; }'), flat)
        # 기술 변환기는 `#`·`$` 도 블록 안에서만 쓴다. 글자 변수는 `_s` 로 갈린다.
        self.assertEqual(variables, ["h_quest", "v_a", "v_b", "v_msg_s"])

    def test_switch_adds_break_but_not_after_end(self):
        code, *_ = self.translate("switch(@x) { case -1: end; case 2: effect 1; default: effect 2; }")
        flat = compact(code)
        self.assertIn(compact("switch ((v_x).Num)"), flat)
        self.assertIn(compact("case -1: return; case 2:"), flat)
        self.assertIn(compact('p.Call("effect", (V)1L); break; default:'), flat)

    def test_unknown_statement_is_reported_not_guessed(self):
        with self.assertRaises(ABILITIES.Unsupported):
            self.translate("set 5, 1;")


class NpcTranslatorTest(unittest.TestCase):
    def translate(self, text):
        t = NPCS.NpcTranslator(text)
        return t.program(), t

    def test_waits_become_yields(self):
        code, t = self.translate(
            'mes 1, "안녕";\n'
            'set @sel, menu("배운다", "그만");\n'
            'if(@sel == 1) { input @name$, 1, "이름?", 0; }\n'
            'end;')
        self.assertIn('yield return Mes((V)1L, (V)"안녕");', code)
        self.assertIn('yield return Menu((V)"배운다", (V)"그만");', code)
        self.assertLess(code.index("yield return Menu("), code.index("v_sel = reply.Choice;"))
        self.assertIn('yield return Input((V)"이름?");', code)
        self.assertIn("v_name_s = reply.Words;", code)
        self.assertIn("yield break;", code)
        self.assertNotIn("return;", code.replace("yield return", "").replace("yield break;", ""))
        self.assertEqual({k: t.calls[k] for k in ("mes", "menu", "input")}, {"mes": 1, "menu": 1, "input": 1})

    def test_persistent_vars_live_on_the_character(self):
        code, t = self.translate("set #gragas, #gragas + 1; set $flag, 2; set @local, 3;")
        self.assertIn('p["#gragas"] = ((V)(p["#gragas"]) + (V)((V)1L));', code)
        self.assertIn('p["$flag"] = (V)2L;', code)
        self.assertIn("v_local = (V)3L;", code)
        # 캐릭터에 남는 값은 지역 변수로 선언하지 않는다.
        self.assertEqual(sorted(t.vars), ["v_local"])

    def test_generated_class_wraps_the_code(self):
        code, t = self.translate('mes 1, "안녕"; end;')
        source = NPCS.csharp("가렌2", "Npc_Script.txt", code, sorted(t.vars), [])
        self.assertIn('[Script("NPC_가렌2", "5.99표")]', source)
        self.assertIn("public class Npc" + "".join(f"{ord(c):04X}" for c in "가렌2") + " : PackNpc", source)
        self.assertIn("protected override IEnumerable<Prompt> Talk(Pack599 p, Reply reply)", source)
        self.assertIn('yield return Mes((V)1L, (V)"안녕");', source)


if __name__ == "__main__":
    unittest.main()
