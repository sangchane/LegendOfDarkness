#!/usr/bin/env python3
"""5.99 서버팩의 기술·마법 스크립트를 하데스에서 그대로 돌게 옮긴다.

5.99 는 기술·마법마다 `SKILL_이름 { … }`·`SPELL_이름 { … }` 블록 하나에 피해식·조건·범위·이펙트·모션·소리를
다 적어 둔다. 모양마다 손으로 옮기지 않고 **문장을 C# 으로 그대로 옮긴다** — `if`·`for`·`goto`·계산식은
스크립트에 적힌 대로 두고, 명령(`damaged`·`effect`·`get_att_damage` …)만 `Pack599.Call` 로 보낸다.
명령의 뜻은 `database/server/scripts/Pack599/Pack599.cs` 한 곳에 있다. 아직 옮기지 않은 명령은 서버를
멈추지 않고 0 을 돌려주므로, 명령을 채우는 만큼 기술이 살아난다.

템플릿은 이미 있으면 붙이는 칸만 바꾸고(기술 `ScriptName` · 마법 `ScriptKey` · 쿨다운), 없으면 만든다 —
직업은 가르치는 NPC 의 `get_class` 검사, 배우는 레벨은 `get_level` 검사에서 읽는다.

무도가 기술은 `build-monk-skills.py` 가 따로 만든 것(`Skills/Monk/`)을 그대로 둔다.

  쓰는 법: python3 scripts/gen/ability/build-pack-abilities.py [--쓰기] [--만 이름 …]

`--만` 을 붙이면 그 이름의 블록만 쓴다. 손본 스크립트(쿠로토·다라밀공의 무도가 몸동작 …)를 되돌리지 않고
한두 개만 새로 옮길 때 쓴다.

**노바에서 옮기는 것**(`FROM_NOVA`): 사용자 결정(2026-09-27) 「기술·마법 목록과 배우는 레벨을 노바처럼」으로 노바 1차
스킬상인이 가르치는데 5.99 팩에 블록이 없는 것. 노바 팩(`data/server-packs/novaonline/db`)의 같은 이름 블록을 같은 길로
옮긴다 — 두 팩은 같은 엔진의 같은 말이다. 템플릿 묶음은 `노바표/…`, 아이콘·설명·마법 대상은 노바 `skill/default.txt`·
`spell/spell.txt` 에서 읽는다. 통배권은 노바 정의가 `SKILL_통배권`을 부르지만 실제 블록 이름이 `SKILL_통배권1`인
오타라서 그 블록을 통배권으로 옮긴다. 혼든의 같은 이름 정의도 아이콘 4로 일치한다.
"""
import json
import re
import sys
from collections import Counter
from pathlib import Path

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from graphify_runtime import configure_utf8_stdio

from lib._paths import ROOT
from lib._pack_abilities import PACK, HADES, OUT, MARK, blocks, _cut, Unsupported, Translator
from lib._io import read_source as read
NOVA = ROOT / "data" / "server-packs" / "novaonline" / "db"
MONK = HADES / "scripts" / "Skills" / "Monk"
RUNTIME = OUT / "Pack599.cs"

configure_utf8_stdio(sys.stdout, sys.stderr)

NOVA_MARK = "노바표"
#: 노바 1차 스킬상인이 가르치는데 5.99 에 블록이 없는 것 — 노바 블록을 옮긴다(위 설명).
FROM_NOVA = {"두번찌르기", "마레네라", "엑스마레나", "디베노모", "벨라르모", "수페라벨라르모", "통배권"}
#: 사용자가 2026-09-16 에 뺐던 정권은 2026-09-25 에 다시 넣으라 했다("5.99 기준으로 완성") — 비어 있다.
EXCLUDED = set()

#: 옮기지 않고 하데스 스크립트를 그대로 붙이는 것. 기본공격은 하데스 `Assail` 이 바로 그것이다(사용자 확인).
ALIASES = {"기본공격": "Assail"}

#: 직업은 파일 이름 → 가르치는 NPC → 모션 순으로 정한다. 모션만으로는 못 정한다 — 파일로 직업이 나오는 130개 중
#: 모션과 맞는 것 67 · 어긋나는 것 6(마법은 여러 직업이 마법사 시전 모션 136 을 같이 쓰고, 2차 기술은 다른 직업
#: 모션을 빌린다) · 모션이 없는 것 57. `Jigja.txt` 는 NPC(성직자 7)와 모션(128·137)이 다 성직자다.
#: `공통스킬.txt` 는 공용이라 직업을 두지 않는다.
FILE_CLASS = [("전사", 1), ("Warrior", 1), ("도적", 2), ("Rogue", 2), ("법사", 3), ("Wizard", 3),
              ("성직자", 4), ("Jigja", 4), ("무도가", 5), ("Monk", 5)]

#: 원작 `skill.tbl` — 모션 번호는 0x80 + NO 이고 NO 의 그림 파일 글자가 직업이다
#: (b 성직자 · c 전사 · d 무도가 · e 도적 · f 마법사, `docs/original-sprite-animation.md` 3.2).
MOTION_CLASS = {0: 4, 9: 4, 10: 4, 1: 1, 2: 1, 11: 1, 12: 1, 13: 1, 3: 5, 4: 5, 5: 5,
                6: 2, 7: 2, 14: 2, 15: 2, 16: 2, 8: 3, 17: 3}


# ── 읽기 ─────────────────────────────────────────────────────────────────────

def nova_blocks():
    """`FROM_NOVA` 의 노바 블록. 노바가 읽는 순서(`script/script_db.txt`)대로 보고 같은 이름은 먼저 것. 출처는 `노바/파일`."""
    listing = read(NOVA / "script" / "script_db.txt")
    paths = [NOVA.parent / rel for rel in re.findall(r"^script:(\S.*?)\s*$", listing, re.M)]
    out = {}
    for key, (source, body) in _cut([p for p in paths if p.exists()], first=True).items():
        if key == ("SKILL", "통배권1"):
            key = ("SKILL", "통배권")
        if key[0] in ("SKILL", "SPELL") and key[1] in FROM_NOVA:
            out[key] = (f"노바/{source}", body)
    return out


def definitions(path):
    """`Skill.txt`·`spell.txt` 의 `{ 이름 … }` 묶음. 이름 → 칸."""
    out = {}
    for chunk in re.findall(r"\{(.*?)\}", read(path), re.S):
        fields = dict(line.split("\t", 1) for line in chunk.strip().splitlines() if "\t" in line)
        if "이름" in fields:
            out[fields["이름"].strip()] = {k.strip(): v.strip() for k, v in fields.items()}
    return out


def teachers():
    """NPC 가 가르치는 것마다 `(직업, 레벨)`. `skill_add`·`spell_add` 앞의 검사를 읽는다."""
    text = read(PACK / "script" / "Npc" / "Npc_Skill.txt")
    out = {}
    for found in re.finditer(r'(?:skill_add2?|spell_add)\s+"([^"]+)"', text):
        before = text[:found.start()]
        npc = before[max(before.rfind("\n0,0,0"), 0):]
        cls = re.findall(r"get_class\(@myid\)\s*!=\s*(\d+)", npc) or re.findall(r"get_class\(@myid\)\s*==\s*(\d+)", npc)
        branch = before[max(0, found.start() - 900):]
        branch = branch[branch.rfind("if(@select"):] if "if(@select" in branch else branch
        level = re.findall(r"get_level\(@myid\)\s*<\s*(\d+)", branch)
        out.setdefault(found.group(1), (int(cls[-1]) if cls else None, int(level[-1]) if level else None))
    return out


def class_of(name, source, body, taught):
    if "공통" in source:
        return None
    for key, cls in FILE_CLASS:
        if key in source:
            return cls
    if taught.get(name, (None, None))[0]:
        return taught[name][0]
    guessed = {MOTION_CLASS.get(int(m) - 0x80) for m in re.findall(r"\bmotion\s+(\d+)", body)} - {None}
    return guessed.pop() if len(guessed) == 1 else None


# ── 옮기기: 5.99 스크립트 → C# ────────────────────────────────────────────────

def translate(body):
    t = Translator(body)
    code = t.program()
    # 없는 라벨로 뛰는 곳이 있다(아무네지아 `go4`). 스크립트 끝으로 보낸다.
    for label in sorted(t.jumps - t.labels - t.entries):
        code += f"\n            L_{label}: ;"
    flags = [f"f_{label}" for label in sorted(t.entries & t.jumps)]
    return code, sorted(t.vars), t.calls, flags


# ── 쓰기 ─────────────────────────────────────────────────────────────────────

def klass(kind, name):
    return {"SKILL": "Skill", "SPELL": "Spell", "Monster": "Monster"}[kind] + "".join(f"{ord(c):04X}" for c in name)


def monster_spells():
    """5.99 괴물 정의의 `스킬 Monster_이름 N` — 괴물 이름 → 괴물 마법 이름."""
    out = {}
    for path in (PACK / "mob").rglob("*.txt"):
        for chunk in re.findall(r"\{(.*?)\}", read(path), re.S):
            fields = dict(line.split("\t", 1) for line in chunk.strip().splitlines() if "\t" in line)
            if "이름" in fields and "스킬" in fields:
                out[fields["이름"].strip()] = fields["스킬"].split("\t")[0].strip()
    return out


def csharp(kind, name, source, code, variables, flags):
    declare = "".join(f"            V {v} = 0;\n" for v in variables)
    declare += "".join(f"            bool {f} = false;\n" for f in flags)
    where = klass(kind, name)
    header = f"""using Darkages.Scripting;
using Darkages.Types;

namespace Darkages.Storage.locales.Scripts.Pack599
{{
    /// <summary>
    /// {name} — {"노바 `" + source[3:] if source.startswith("노바/") else "5.99 `" + source}` 의 {kind}_{name} 을 그대로 옮긴 것.
    /// </summary>
    /// <remarks>
    /// 손으로 고치지 말 것. `scripts/gen/ability/build-pack-abilities.py` 가 다시 만든다.
    /// </remarks>
    [Script("{'Monster_' + name if kind == 'Monster' else name}", "{MARK}")]
"""
    if kind == "Monster":
        return header + f"""    public class {where} : SpellScript
    {{
        public {where}(Spell spell) : base(spell)
        {{
        }}

        public override void OnFailed(Sprite sprite, Sprite target)
        {{
        }}

        public override void OnSuccess(Sprite sprite, Sprite target)
        {{
        }}

        public override void OnUse(Sprite sprite, Sprite target)
        {{
            var p = Pack599.ForMonster(sprite, target);
            if (!p.Ready)
                return;
{declare}
{code}
        }}
    }}
}}
"""
    if kind == "SKILL":
        return header + f"""    public class {where} : SkillScript
    {{
        public {where}(Skill skill) : base(skill)
        {{
        }}

        public override void OnFailed(Sprite sprite)
        {{
        }}

        public override void OnSuccess(Sprite sprite)
        {{
        }}

        public override void OnUse(Sprite sprite)
        {{
            if (!Skill.Ready)
                return;

            var p = new Pack599(sprite, null);
            if (!p.Ready)
                return;

            p.Train(Skill);
{declare}
{code}
        }}
    }}
}}
"""
    return header + f"""    public class {where} : SpellScript
    {{
        public {where}(Spell spell) : base(spell)
        {{
        }}

        public override void OnFailed(Sprite sprite, Sprite target)
        {{
        }}

        public override void OnSuccess(Sprite sprite, Sprite target)
        {{
        }}

        public override void OnUse(Sprite sprite, Sprite target)
        {{
            var p = new Pack599(sprite, target);
            if (!p.Ready)
                return;
{declare}
{code}
        }}
    }}
}}
"""


def implemented():
    return set(re.findall(r'case "([^"]+)":', read(RUNTIME)))


def translate_all(found, known, taught):
    """블록마다 C# 으로 옮긴다 — 옮긴 것, 못 옮긴 것, 아직 없는 명령, 하데스 스크립트를 붙일 것."""
    made, failed, missing, aliased = [], [], Counter(), []
    for (kind, name), (source, body) in sorted(found.items()):
        if name in EXCLUDED or (kind == "SKILL" and (MONK / f"{name}.cs").exists()):
            continue
        if name in ALIASES:
            aliased.append((kind, name))
            continue
        try:
            code, variables, calls, flags = translate(body)
        except Unsupported as error:
            failed.append((kind, name, str(error)))
            continue
        lacking = sorted(set(calls) - known)
        for call in lacking:
            missing[call] += 1
        delay = re.search(r"\bskill_delay\s+(\d+)", body)
        made.append((kind, name, source, code, variables, flags, lacking, int(delay.group(1)) if delay else 0,
                     class_of(name, source, body, taught)))
    return made, failed, missing, aliased


def write_made(made, nova_skills, nova_spells, skills, spells, taught):
    """옮긴 스크립트와 그 템플릿을 쓴다."""
    for kind, name, source, code, variables, flags, lacking, delay, cls in made:
        folder = OUT / {"SKILL": "Skills", "SPELL": "Spells", "Monster": "Monsters"}[kind]
        folder.mkdir(parents=True, exist_ok=True)
        (folder / f"{name}.cs").write_text(csharp(kind, name, source, code, variables, flags), encoding="utf-8-sig")
        if kind == "Monster":
            # 하데스 괴물 AI 는 같은 이름의 마법 템플릿이 있어야 스크립트를 불러온다(`CommonMonster.cs:292`) —
            # 없으면 말없이 건너뛴다. 가르치는 NPC 가 없으니 사람이 배울 길은 없다.
            path = HADES / "templates" / "spells" / f"Monster_{name}.json"
            path.write_text(json.dumps({"Name": f"Monster_{name}", "ScriptKey": f"Monster_{name}", "Prerequisites": {},
                                        "MaxLevel": 100, "ID": 0, "Description": None, "TargetType": 2,
                                        "Group": f"{MARK}/괴물마법"}, ensure_ascii=False, indent=2), encoding="utf-8-sig")
            continue

        nova = source.startswith("노바/")
        define = ((nova_skills if kind == "SKILL" else nova_spells) if nova
                  else (skills if kind == "SKILL" else spells)).get(name, {})
        level = None if nova else taught.get(name, (None, None))[1]
        path = HADES / "templates" / ("skills" if kind == "SKILL" else "spells") / f"{name}.json"
        if path.exists():
            template = json.loads(path.read_text(encoding="utf-8-sig"))
        else:
            template = {"Name": name, "Prerequisites": {}, "MaxLevel": 100, "ID": 0,
                        "Description": define.get("설명"),
                        "Group": f"{NOVA_MARK if nova else MARK}/{Path(source).stem}"}
            if cls:
                template["Prerequisites"]["Class_Required"] = cls
            if level:
                template["Prerequisites"]["ExpLevel_Required"] = level
            if define.get("이미지", "").isdigit():
                template["Icon"] = int(define["이미지"])
            if kind == "SPELL" and define.get("타입", "").isdigit():
                template["TargetType"] = int(define["타입"])
        # 이 생성기가 만든 템플릿은 직업을 다시 정한다(원작 템플릿은 건드리지 않는다).
        if str(template.get("Group", "")).startswith((MARK, NOVA_MARK)):
            template.setdefault("Prerequisites", {}).pop("Class_Required", None)
            if cls:
                template["Prerequisites"]["Class_Required"] = cls
        template["ScriptName" if kind == "SKILL" else "ScriptKey"] = name
        template["Cooldown"] = delay
        path.write_text(json.dumps(template, ensure_ascii=False, indent=2), encoding="utf-8-sig")


def write_aliased(aliased):
    """하데스 스크립트를 붙이는 것 — 템플릿이 그 스크립트를 가리키게."""
    for kind, name in aliased:
        folder = "skills" if kind == "SKILL" else "spells"
        (OUT / folder.capitalize() / f"{name}.cs").unlink(missing_ok=True)
        path = HADES / "templates" / folder / f"{name}.json"
        template = json.loads(path.read_text(encoding="utf-8-sig"))
        template["ScriptName" if kind == "SKILL" else "ScriptKey"] = ALIASES[name]
        path.write_text(json.dumps(template, ensure_ascii=False, indent=2), encoding="utf-8-sig")


def attach_monster_spells(made):
    """괴물 마법을 괴물 템플릿에 붙인다."""
    # 괴물 마법을 괴물 템플릿에 붙인다. 하데스 `CommonMonster` 가 `SpellScripts` 의 것을 표적에게 쓴다.
    defined = {name for kind, name, *_ in made if kind == "Monster"}
    wanted = monster_spells()
    attached, undefined = 0, Counter()
    for path in (HADES / "templates" / "monsters" / "5.99").glob("*.json"):
        template = json.loads(path.read_text(encoding="utf-8-sig"))
        spell = wanted.get(template.get("BaseName") or template.get("Name"))
        if not spell:
            continue
        if spell[len("Monster_"):] not in defined:
            undefined[spell] += 1
            continue
        template["SpellScripts"] = [spell]
        path.write_text(json.dumps(template, ensure_ascii=False, indent=2), encoding="utf-8-sig")
        attached += 1
    return attached, undefined


def main():
    writing = "--쓰기" in sys.argv or "--write" in sys.argv
    only = set(sys.argv[sys.argv.index("--만") + 1:]) if "--만" in sys.argv else None
    found = {**blocks(), **nova_blocks()}
    if only is not None:
        found = {key: value for key, value in found.items() if key[1] in only}
    skills, spells = definitions(PACK / "skill" / "Skill.txt"), definitions(PACK / "spell" / "spell.txt")
    nova_skills, nova_spells = definitions(NOVA / "skill" / "default.txt"), definitions(NOVA / "spell" / "spell.txt")
    taught = teachers()
    known = implemented()

    made, failed, missing, aliased = translate_all(found, known, taught)

    whole = [m for m in made if not m[6]]
    print(f"5.99 블록 {len(found)} · 옮김 {len(made)} (명령이 다 있는 것 {len(whole)}) · 못 옮김 {len(failed)}")
    for kind, name, error in failed:
        print(f"  못 옮김 {kind} {name}: {error}")
    print("아직 없는 명령(쓰는 블록 수): " + ", ".join(f"{k}({v})" for k, v in missing.most_common()))

    if not writing:
        print("\n--쓰기 를 붙이면 실제로 만듭니다.")
        return 0

    write_made(made, nova_skills, nova_spells, skills, spells, taught)
    write_aliased(aliased)
    attached, undefined = attach_monster_spells(made)
    print(f"\n스크립트·템플릿 {len(made)}쌍을 만들었습니다. 하데스 스크립트를 붙인 것 {len(aliased)}개.")
    print(f"괴물 템플릿 {attached}장에 괴물 마법을 붙였습니다.")
    if undefined:
        print("5.99 에 정의가 없는 괴물 마법(템플릿 수): " + ", ".join(f"{k}({v})" for k, v in undefined.most_common()))
    run_nova_effects()
    return 0


def run_nova_effects():
    """이펙트 번호는 노바 것이 원작이다(사용자 결정 2026-09-26) — 옮긴 뒤 `build-nova-effects.py` 로 바꾼다."""
    from lib._nova_effects import apply
    apply()


if __name__ == "__main__":
    raise SystemExit(main())
