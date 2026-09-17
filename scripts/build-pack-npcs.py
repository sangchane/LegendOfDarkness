#!/usr/bin/env python3
"""5.99 서버팩의 NPC 스크립트와 아이템 사용 스크립트를 하데스에서 그대로 돌게 옮긴다.

NPC: 먼저 기술·마법을 가르치는 사범(`Npc/Npc_Skill.txt`). 아이템: `Item/*.txt` 의 블록 — 아이템의 `사용펄숫` 칸이 부르는
이름이다(염색약 `set_haircolor 14; item_del …`). 아이템 블록은 NPC 가 없어 창을 띄울 곳이 없으므로, **기다리지 않는
것만** 옮기고(`[Script("ITEM_이름")]` 아이템 스크립트, 쓰면 곧장 돈다) `mes`·`menu`·`input` 이 있는 것은 목록만 남긴다.

서버 NPC 95명 중 기술을 가르치는 스크립트가 붙은 NPC 가 하나도 없어, 운영자 명령 없이는 기술을 배울 수 없었다.
5.99 사범은 표가 아니라 대화 스크립트다(`menu` → 직업·레벨 확인 → `skill_add`). 기술·마법처럼 **문장을 C# 으로
그대로 옮긴다** — 변환기는 `build-pack-abilities.py` 의 것을 그대로 쓰고, 플레이어를 기다리는 세 곳만 다르게 옮긴다:

  mes 1, "글";           → yield return Mes(1, "글");          ("다음"을 누를 때까지 멈춘다)
  set @고름, menu(…);    → yield return Menu(…); v_고름 = reply.Choice;
  input @글$, 1, "물음"…;  → yield return Input("물음"); v_글_s = reply.Words;
  end;                   → yield break;

NPC·아이템 스크립트의 `#이름`·`$이름` 은 캐릭터에 남는다(퀘스트 진행 `#gragas` 따위) — `p["#이름"]` 으로 옮겨
`Aisling.PackVariables` 에 둔다. 기술 변환기는 그대로 블록 안에서만 쓴다.

기다렸다 이어 가는 일은 `scripts/Pack599/PackNpc.cs` 가 한다. 명령(`skill_add`·`get_level` …)은 기술과 같은
`Pack599.Call` 로 간다. 스크립트 이름마다 `[Script("NPC_이름")]` 클래스 하나 — NPC 템플릿의 `ScriptKey` 가 이것을
가리킨다(`tools/pack-import/import.py`, 팩 `npc/Spawn.txt` 여섯째 칸이 스크립트 이름).

  쓰는 법: python3 scripts/build-pack-npcs.py [--쓰기]
  산출물:  sources/wren11/Dark-Ages-Private-Server/database/server/scripts/Pack599/Npcs/<스크립트 이름>.cs
"""
import importlib.util
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
_spec = importlib.util.spec_from_file_location("abilities", ROOT / "scripts" / "build-pack-abilities.py")
abilities = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(abilities)

NPC_SCRIPTS = abilities.PACK / "script" / "Npc"
OUT = abilities.OUT / "Npcs"
#: 상점(`Npc_Shop.txt`)은 아직 뺀다 — `shop` 명령이 없고, 상점 NPC 는 하데스 shop1 로 이미 돈다.
FILES = ["Npc_Skill.txt", "Npc_Script.txt", "Npc_Quest.txt", "Npc_Making.txt", "Npc_Warp.txt"]
ITEM_SCRIPTS = abilities.PACK / "script" / "Item"
ITEM_OUT = abilities.OUT / "Items"
# 1초마다 도는 스크립트(개인 던전 사본 안의 사람마다 — 서버 PackRoutine). 대화가 없어야 한다.
ROUTINE_FILES = ["Dungeon.txt"]
ROUTINE_SCRIPTS = abilities.PACK / "script"
ROUTINE_OUT = abilities.OUT / "Routines"
ITEM_FILES = ["E.T.C.txt", "Quest.txt", "Potion.txt", "Blessing.txt", "CashI.txt"]
#: 플레이어를 기다리는 명령 — 아이템 스크립트에서는 아직 띄울 창이 없다.
WAITS = {"mes", "menu", "input"}

#: 블록 머리 — 줄 맨 앞의 `0,0,0,0,0,0,0` 다음 탭, 이름, `{`. 안쪽의 `if(…){` 줄은 탭으로 시작해 걸리지 않는다.
HEADER = re.compile(r"^\d[\d,]*\t([^\t{]+?)\s*\{", re.M)
#: 2026-09-17 에 센 블록 수. HEADER 는 줄 모양(숫자열 + 탭)에 기대므로 팩 파일 모양이 바뀌면 블록이 말없이 빠진다 — 적게 잡히면 알린다.
EXPECTED_NPC_BLOCKS = 84
EXPECTED_ITEM_BLOCKS = 26


class NpcTranslator(abilities.Translator):
    """기술 변환기에 기다리는 자리만 더한다. 나머지 문장·식은 그대로다."""

    def statement(self, depth):
        pad = "    " * depth
        kind, word = self.peek()
        if kind == "id" and word == "mes":
            self.take()
            args = [self.expr()]
            while self.at(","):
                self.take()
                args.append(self.expr())
            self.take(";")
            self.calls["mes"] += 1
            return f"{pad}yield return Mes({', '.join(args)});"
        if kind == "id" and word == "set" and self.peek(2)[1] == "," and self.peek(3)[1] == "menu":
            self.take("set")
            name = self.variable(self.take())
            self.take(",")
            self.take("menu")
            self.take("(")
            args = []
            while not self.at(")"):
                args.append(self.expr())
                if self.at(","):
                    self.take()
            self.take(")")
            self.take(";")
            self.calls["menu"] += 1
            return f"{pad}yield return Menu({', '.join(args)});\n{pad}{name} = reply.Choice;"
        if kind == "id" and word == "input":
            self.take()
            name = self.variable(self.take())
            self.take(",")
            self.expr()  # 입력 종류
            self.take(",")
            question = self.expr()
            while not self.at(";"):
                self.take()
            self.take(";")
            self.calls["input"] += 1
            return f"{pad}yield return Input({question});\n{pad}{name} = reply.Words;"
        if kind == "id" and word == "end":
            self.take()
            self.take(";")
            return f"{pad}yield break;"
        return super().statement(depth)

    def variable(self, raw):
        if raw[:1] in ("#", "$"):
            return f'p["{raw}"]'
        return super().variable(raw)


def blocks(path):
    """이름 → 본문. 괄호 짝으로 자른다(글자 안의 괄호는 세지 않는다)."""
    text = abilities.read(path)
    heads = list(HEADER.finditer(text))
    out = {}
    for n, head in enumerate(heads):
        limit = heads[n + 1].start() if n + 1 < len(heads) else len(text)
        depth, at, quoted = 1, head.end(), False
        while at < limit and depth:
            c = text[at]
            if c == '"' and text[at - 1] != "\\":
                quoted = not quoted
            elif not quoted:
                depth += c == "{"
                depth -= c == "}"
            at += 1
        out[head.group(1).strip()] = text[head.end():at - 1 if not depth else at]
    return out


def klass(name):
    return "Npc" + "".join(f"{ord(c):04X}" for c in name)


def csharp(name, source, code, variables, flags):
    declare = "".join(f"            V {v} = 0;\n" for v in variables)
    declare += "".join(f"            bool {f} = false;\n" for f in flags)
    where = klass(name)
    return f"""using System.Collections.Generic;
using Darkages.Network.Game;
using Darkages.Scripting;
using Darkages.Types;

namespace Darkages.Storage.locales.Scripts.Pack599
{{
    /// <summary>
    /// {name} — 5.99 `{source}` 의 NPC 스크립트를 그대로 옮긴 것.
    /// </summary>
    /// <remarks>
    /// 손으로 고치지 말 것. `scripts/build-pack-npcs.py` 가 다시 만든다.
    /// </remarks>
    [Script("NPC_{name}", "{abilities.MARK}")]
    public class {where} : PackNpc
    {{
        public {where}(GameServer server, Mundane mundane) : base(server, mundane)
        {{
        }}

        protected override IEnumerable<Prompt> Talk(Pack599 p, Reply reply)
        {{
{declare}
{code}
            // 말도 메뉴도 없는 스크립트(적룡의결계 …)도 이터레이터여야 한다.
            yield break;
        }}
    }}
}}
"""


def csharp_item(name, source, code, variables, flags):
    declare = "".join(f"            V {v} = 0;\n" for v in variables)
    declare += "".join(f"            bool {f} = false;\n" for f in flags)
    where = "Item" + "".join(f"{ord(c):04X}" for c in name)
    return f"""using Darkages.Scripting;
using Darkages.Types;

namespace Darkages.Storage.locales.Scripts.Pack599
{{
    /// <summary>
    /// {name} — 5.99 `{source}` 의 아이템 사용 스크립트를 그대로 옮긴 것. 아이템이 스스로 지운다(`item_del`).
    /// </summary>
    /// <remarks>
    /// 손으로 고치지 말 것. `scripts/build-pack-npcs.py` 가 다시 만든다.
    /// </remarks>
    [Script("ITEM_{name}", "{abilities.MARK}")]
    public class {where} : ItemScript
    {{
        public {where}(Item item) : base(item)
        {{
        }}

        public override void Equipped(Sprite sprite, byte displayslot)
        {{
        }}

        public override void UnEquipped(Sprite sprite, byte displayslot)
        {{
        }}

        public override void OnUse(Sprite sprite, byte slot)
        {{
            var p = new Pack599(sprite, null);
            if (!p.Ready)
                return;
{declare}
{code}
        }}
    }}
}}
"""


def csharp_routine(name, source, code, variables, flags):
    declare = "".join(f"            V {v} = 0;\n" for v in variables)
    declare += "".join(f"            bool {f} = false;\n" for f in flags)
    where = "Routine" + "".join(f"{ord(c):04X}" for c in name)
    return f"""using System.Collections.Generic;
using Darkages.Scripting;
using Darkages.Types;

namespace Darkages.Storage.locales.Scripts.Pack599
{{
    /// <summary>
    /// {name} — 5.99 `script/{source}` 를 그대로 옮긴 것. 개인 던전 사본 안의 사람마다 1초에 한 번 돈다(PackRoutine).
    /// </summary>
    /// <remarks>
    /// 손으로 고치지 말 것. `scripts/build-pack-npcs.py` 가 다시 만든다.
    /// </remarks>
    [Script("PACK_{name}", "{abilities.MARK}")]
    public class {where} : PackRoutine
    {{
        public {where}(Area area) : base(area)
        {{
        }}

        protected override IEnumerable<int> Run(Pack599 p)
        {{
{declare}
{code}
            yield break;
        }}
    }}
}}
"""


def routines(write):
    """1초마다 도는 스크립트. 대화(mes·menu·input)가 있으면 옮기지 않는다 — 돌리는 쪽에 창이 없다."""
    made, failed = [], []
    for file in ROUTINE_FILES:
        for name, body in blocks(ROUTINE_SCRIPTS / file).items():
            translator = NpcTranslator(body)
            try:
                code = translator.program()
            except abilities.Unsupported as why:
                failed.append(f"{name}({why})")
                continue
            if WAITS & set(translator.calls):
                failed.append(f"{name}(대화가 있다)")
                continue
            for label in sorted(translator.jumps - translator.labels - translator.entries):
                code += f"\n            L_{label}: ;"
            flags = [f"f_{label}" for label in sorted(translator.entries & translator.jumps)]
            if write:
                ROUTINE_OUT.mkdir(parents=True, exist_ok=True)
                (ROUTINE_OUT / f"{name}.cs").write_text(
                    csharp_routine(name, file, code, sorted(translator.vars), flags), encoding="utf-8-sig")
            made.append(name)
    return made, failed


def items(write):
    """아이템 블록 중 기다리지 않는 것만. (옮긴 이름, 창이 필요해 남긴 이름)."""
    made, waiting = [], []
    for file in ITEM_FILES:
        for name, body in blocks(ITEM_SCRIPTS / file).items():
            try:
                translator = abilities.Translator(body)
                code = translator.program()
            except abilities.Unsupported as why:
                waiting.append(f"{name}(못 읽음: {why})")
                continue
            if WAITS & set(translator.calls):
                waiting.append(name)
                continue
            for label in sorted(translator.jumps - translator.labels - translator.entries):
                code += f"\n            L_{label}: ;"
            flags = [f"f_{label}" for label in sorted(translator.entries & translator.jumps)]
            if write:
                ITEM_OUT.mkdir(parents=True, exist_ok=True)
                (ITEM_OUT / f"{name}.cs").write_text(
                    csharp_item(name, file, code, sorted(translator.vars), flags), encoding="utf-8-sig")
            made.append(name)
    return made, waiting


def main():
    write = "--쓰기" in sys.argv or "--write" in sys.argv
    made, failed, calls = [], [], {}
    for file in FILES:
        for name, body in blocks(NPC_SCRIPTS / file).items():
            translator = NpcTranslator(body)
            try:
                code = translator.program()
            except abilities.Unsupported as why:
                failed.append(f"{name}({why})")
                continue
            # 없는 라벨로 뛰는 곳은 스크립트 끝으로 보낸다(기술 변환기와 같다).
            for label in sorted(translator.jumps - translator.labels - translator.entries):
                code += f"\n            L_{label}: ;"
            flags = [f"f_{label}" for label in sorted(translator.entries & translator.jumps)]
            for call, count in translator.calls.items():
                calls[call] = calls.get(call, 0) + count
            if write:
                OUT.mkdir(parents=True, exist_ok=True)
                (OUT / f"{name}.cs").write_text(
                    csharp(name, file, code, sorted(translator.vars), flags), encoding="utf-8-sig")
            made.append(name)

    print(f"NPC 스크립트 {len(made)}개 {'씀' if write else '(세어만 봄 — --쓰기 로 쓴다)'} → {OUT.relative_to(ROOT)}")
    if failed:
        print(f"  못 옮긴 것 {len(failed)}: {', '.join(failed)}")
    print("  부르는 명령: " + ", ".join(f"{k}×{v}" for k, v in sorted(calls.items(), key=lambda kv: -kv[1])))

    done, waiting = items(write)
    print(f"아이템 스크립트 {len(done)}개 {'씀' if write else '(세어만 봄)'} → {ITEM_OUT.relative_to(ROOT)}: {', '.join(done)}")
    print(f"  창이 필요해 남긴 것 {len(waiting)}: {', '.join(waiting)}")

    routine_made, routine_failed = routines(write)
    print(f"1초마다 도는 스크립트 {len(routine_made)}개 {'씀' if write else '(세어만 봄)'} → {ROUTINE_OUT.relative_to(ROOT)}: {', '.join(routine_made)}")
    if routine_failed:
        print(f"  못 옮긴 것 {len(routine_failed)}: {', '.join(routine_failed)}")

    for what, found, expected in (("NPC", len(made) + len(failed), EXPECTED_NPC_BLOCKS),
                                  ("아이템", len(done) + len(waiting), EXPECTED_ITEM_BLOCKS)):
        if found < expected:
            print(f"경고: {what} 블록이 {found}개뿐이다(전에는 {expected}개) — HEADER 가 블록 머리를 놓쳤는지 팩 파일을 확인하라.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
