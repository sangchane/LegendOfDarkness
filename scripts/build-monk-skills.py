#!/usr/bin/env python3
"""무도가 기술을 5.99 서버팩 스크립트에서 읽어 하데스 스크립트·템플릿으로 찍어낸다.

하데스는 기술 258장 중 30장만 구현돼 있다. 나머지는 이름만 있고 눌러도 아무 일이 없다. 팩 쪽에는
기술마다 피해식·모션·이펙트·소리·딜레이가 한 블록에 다 있으므로, 손으로 옮기지 말고 읽어서 만든다.

**때리는 기술 대부분은 5.99 의 모양 그대로 옮긴다.**

    피해 = 공격력 × 공격력배율 + 지구력 × 지구력배율

5.99 는 기술마다 **두 계수만** 다르게 준다 — 단각 2.8배, 붕각 3.5배 + 지구력 59, 선풍각 3.5배 +
지구력 66. Novaonline 은 같은 것을 `힘 + 상수`(단각 75 · 붕각 114 · 선풍각 184)로 적었는데 **순서가
5.99 와 같다.** 상수는 레벨이 올라도 안 커지므로 배율 쪽을 쓴다. 혼든은 자릿수가 100배라 쓰지 않는다.

그 밖의 일곱 모양은 `shape` 에 하나씩 적었다 — 체력 비례(달마신공·구양신공·늑대의위상), 능력치 곱
(마구때리기), 상태 이상(일음지 실명·발경 빙결), 자기 강화(소수신공), 두 칸 건너뛰기(이형환위).
체력 명령의 뜻은 팩이 보여 준다: `get_vita` 는 **현재** 체력, `get_basevita` 는 **최대** 체력,
`set_vital X` 는 체력을 X 로 **맞춘다**(`MonkStrike.cs` 머리글에 근거).

  쓰는 법: python3 scripts/build-monk-skills.py [--쓰기]
  산출물:  sources/…/scripts/Skills/Monk/<이름>.cs · templates/skills/<이름>.json
"""
import json
import re
import sys
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
PACK = ROOT / "data" / "server-packs" / "5.99-server" / "db" / "script"
HADES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
SCRIPTS = HADES / "scripts" / "Skills" / "Monk"
TEMPLATES = HADES / "templates" / "skills"

configure_utf8_stdio(sys.stdout, sys.stderr)

BLOCK = re.compile(r"SKILL_([가-힣A-Za-z0-9_]+)\s*\{\n(.*?)\n\}", re.S)

#: 무도가 것만 고른다. 파일 이름이 갈래를 말한다.
MONK_FILES = re.compile(r"Monk|무도가")

#: `수도사`. `Class.Monk` 의 값이다.
MONK_CLASS = 5

#: 이 생성기가 만든 것임을 알아보는 표시. 손으로 쓴 것과 섞이지 않게 한다.
MARK = "5.99표/무도가"

#: 원작에 없는 팩 전용 기술. 사용자 확인(2026-09-16) — 지우지 않고 표시만 한다.
PACK_ONLY = {"붕신선각"}

#: 이 생성기가 만들지 않는 기술. 정권은 `주먹단련` 유무로 갈리는 두 갈래라 이 틀(한 모양)에 안 맞아
#: `build-pack-abilities.py` 가 문장 그대로 옮긴다(2026-09-25, `scripts/Pack599/Skills/정권.cs`).
EXCLUDED = {"정권"}

#: 팩은 마나를 쓰게 했지만 원작은 안 쓴다. 사용자 확인(2026-09-16).
NO_MANA = {"단각"}

#: 배우는 레벨은 5.99 NPC 스크립트에서 읽는다 — 사용자 지시(2026-09-16). 이 생성기가 만든 템플릿에만 넣는다.
NPC = PACK / "Npc" / "Npc_Skill.txt"

#: 원작 템플릿이 따로 있는 같은 기술. 원작의 배우는 레벨이 있으니 팩 값으로 덮지 않는다.
ORIGINAL_TWIN = {"단각": "Kick", "붕각": "Martial Awareness"}


def learn_levels():
    """`기술 → 레벨`. NPC 가 `skill_add "기술"` 하기 전에 `get_level(@myid) < N` 으로 막는 값.

    메뉴 글자(`연환포[75]`)가 아니라 실제 검사를 읽는다 — 연환포는 메뉴 75, 검사 74 로 다르다.
    승급·2차 NPC 로 배우는 기술은 레벨 검사가 없어 빠진다(2차 직업과 함께 나중에).
    """
    text = read(NPC)
    out = {}
    for found in re.finditer(r'skill_add2?\s+"([^"]+)"', text):
        before = text[max(0, found.start() - 900):found.start()]
        before = before[before.rfind("if(@select"):] if "if(@select" in before else before
        levels = re.findall(r"get_level\(@myid\)\s*<\s*(\d+)", before)
        if levels:
            out.setdefault(found.group(1), int(levels[-1]))
    return out
SPELLS = HADES / "templates" / "spells"


def read(path):
    for encoding in ("utf-8", "cp949"):
        try:
            return path.read_text(encoding=encoding)
        except (UnicodeDecodeError, ValueError):
            continue
    return ""


def blocks():
    """`이름 → 스크립트 본문`."""
    out = {}
    for path in PACK.rglob("*.txt"):
        if not MONK_FILES.search(str(path)):
            continue
        for name, body in BLOCK.findall(read(path)):
            out.setdefault(name, body)
    return out


def attack_multiplier(body):
    """평타의 몇 배인가. 백분율로 돌려준다 — 2.8배는 280.

    5.99 가 쓰는 네 모양을 모두 읽는다. 곱셈과 나눗셈이 섞여 있어 정수 배수로는 못 담는다.
    """
    joined = re.sub(r"\s+", "", body)

    # (공격력 * 3) + (공격력 / 2)  →  3.5배
    found = re.search(r"\(get_att_damage\(@\w*\)\*(\d+)\)\+\(get_att_damage\(@\w*\)/2\)", joined)
    if found:
        return int(found.group(1)) * 100 + 50

    # 공격력 / 10 * 28  →  2.8배
    found = re.search(r"get_att_damage\(@\w*\)/10\*(\d+)", joined)
    if found:
        return int(found.group(1)) * 10

    # 공격력 / 2 * 7  →  3.5배
    found = re.search(r"get_att_damage\(@\w*\)/2\*(\d+)", joined)
    if found:
        return int(found.group(1)) * 50

    # 공격력 * 6  →  6배
    found = re.search(r"get_att_damage\(@\w*\)\*(\d+)", joined)
    if found:
        return int(found.group(1)) * 100

    return None


def one(text, pattern):
    found = re.search(pattern, text)
    return int(found.group(1)) if found else None


def without_missing_spell(body):
    """`if(spell_exist("X") == 1){ … }else{ … }` 에서 하데스에 X 가 없으면 else 쪽만 남긴다.

    정권이 그렇다 — `주먹단련` 이 있으면 ×2.5·마나 25, 없으면 ×2.0·마나 15.
    """
    found = re.search(r'if\s*\(\s*spell_exist\("([^"]+)"\)\s*==\s*1\s*\)\s*\{.*?\}\s*else\s*\{', body, re.S)
    if not found or (SPELLS / f"{found.group(1)}.json").exists():
        return body
    return body[:found.start()] + body[found.end():]


def gather(name, body):
    """한 기술에서 옮길 수 있는 것만 골라 낸다."""
    body = without_missing_spell(body)
    joined = re.sub(r"\s+", "", body)
    return {
        "마나": None if name in NO_MANA else one(joined, r"get_mana\(@\w*\)<(\d+)"),
        "이름": name,
        "공격력배수": attack_multiplier(body),
        "지구력배수": one(joined, r"get_con\(@\w*\)\*(\d+)"),
        "체력분율": one(joined, r"get_vita\(@\w*\)/100\*(\d+)"),
        "체력배수": one(joined, r"get_vita\(@\w*\)\*(\d+)"),
        "남길체력분율": one(joined, r"set_vitalget_basevita\(@\w*\)/100\*(\d+)"),
        "최대체력배수": [int(n) for n in re.findall(r"\(get_basevita\(@\w*\)\+1\)\)\*(\d+)", joined)],
        "힘지구력": re.search(r"\(\(get_str\(\)\+(\d+)\)\+\(get_con\(\)\+(\d+)\)\)\*(\d+)", joined),
        "실명초": one(joined, r"mob_strabismus\(@\w+,(\d+)\)"),
        "빙결초": one(joined, r"magic7,@\w+,\d+,(\d+)"),
        "강화초": one(joined, r"sosusin\(@\w+,(\d+)\)"),
        "외움말": (re.search(r'message\s+3\s*,\s*"([^"]*외웠습니다[^"]*)"', body) or [None, ""])[1],
        "건너뛸칸": one(joined, r"set_ysget_ys\(\)-(\d+)"),
        "최대체력더함": "@damage+get_basevita(@myid);" in joined,
        "사람도": "get_map_pk()" in joined,
        # 칸을 직접 고르는 기술. 보는 쪽으로 몇 칸까지(백보신권 3), 또는 방향 없이 둘레 네 칸(선풍각).
        "앞칸수": max([int(n) for n in re.findall(r"get_mobxy\(@x1,\(@y1\)-(\d+)\)", joined)] or [1])
                 if "get_side(@myid)" in joined else 1,
        "둘레": "get_side(@myid)" not in joined and "get_mobxy((@x1)+1,@y1)" in joined,
        "모션": one(body, r"\bmotion\s+(\d+)"),
        "이펙트": one(body, r"\beffect\s+@\w+\s*,\s*\d+\s*,\s*(\d+)"),
        "소리": one(body, r"\bgame_sound\s+(\d+)"),
        "딜레이": one(body, r"\bskill_delay\s+(\d+)"),
    }


def klass(name):
    """C# 클래스 이름. 한글은 못 쓰므로 자리만 잡고 이름은 `[Script]` 가 들고 있다."""
    return "Monk" + "".join(f"{ord(letter):04X}" for letter in name)[:40]


#: 하데스 평타가 능력치를 세는 비율. `Assail.cs:55` — `힘 × 4 + 민첩성 × 2`.
BLOW_STRENGTH, BLOW_AGILITY = 4, 2


def stat_percents(skill):
    """팩이 준 두 계수를 백분율로 돌려준다 — 100 이 1배다.

    펴지 않는다. 팩의 `get_att_damage` 는 **무기까지 낀 평타 최종 공격력**이고, 하데스에서도
    `Sprite.ApplyWeaponBonuses` 가 그 값을 만들 수 있다. 능력치로 펴면 무기가 빠진다.
    """
    return (skill["공격력배수"] or 100, (skill["지구력배수"] or 0) * 100)


DEBUFFS = "using Darkages.Storage.locales.debuffs;\n"


def shape(skill):
    """`(설명, MonkStrike 부르는 줄, 더 쓸 using)` — 옮길 모양이 없으면 None."""
    motion = f"0x{skill['모션'] or 0x84:02X}"
    if skill["체력배수"] is not None and skill["남길체력분율"] is not None:
        return (f"현재 체력 ×{skill['체력배수']} 로 사방 네 칸, 내 체력은 최대의 {skill['남길체력분율']}% 로",
                f"MonkStrike.UseCross(sprite, Skill, {skill['체력배수']}, {skill['남길체력분율']}, {motion});", "")
    if skill["체력분율"] is not None:
        return (f"현재 체력의 {skill['체력분율']}%, 내 체력도 그 값으로",
                f"MonkStrike.UseVitality(sprite, Skill, {skill['체력분율']}, {motion});", "")
    if len(skill["최대체력배수"]) == 2:
        low, high = skill["최대체력배수"]
        return (f"(최대 체력 + 1) × {low} 또는 × {high} 반반",
                f"MonkStrike.UseWolf(sprite, Skill, {low}, {high}, {motion});", "")
    if skill["힘지구력"]:
        strength, endurance, multiplier = skill["힘지구력"].groups()
        return (f"((힘 + {strength}) + (지구력 + {endurance})) × {multiplier}",
                f"MonkStrike.UseStrengthAndEndurance(sprite, Skill, {strength}, {endurance}, {multiplier}, {motion});",
                "")
    players = "true" if skill["사람도"] else "false"
    if skill["실명초"] is not None:
        return (f"앞의 적을 {skill['실명초']}초 실명",
                f"MonkStrike.Afflict(sprite, Skill, new debuff_blind(), {skill['실명초']}, {players}, {motion});",
                DEBUFFS)
    if skill["빙결초"] is not None:
        return (f"앞의 적을 {skill['빙결초']}초 빙결",
                f"MonkStrike.Afflict(sprite, Skill, new debuff_frozen(), {skill['빙결초']}, {players}, {motion});",
                DEBUFFS)
    if skill["강화초"] is not None:
        return (f"{skill['강화초']}초 동안 공격력 +40%",
                f"MonkStrike.Empower(sprite, Skill, {skill['강화초']}, {motion}, \"{skill['외움말']}\");", "")
    if skill["건너뛸칸"] is not None and skill["공격력배수"] is not None:
        attack, endurance = stat_percents(skill)
        health = 100 if skill["최대체력더함"] else 0
        return (f"앞의 적을 넘어 {skill['건너뛸칸']}칸 건너뛰고 돌아서서 공격력 ×{attack / 100:g}"
                + (" + 최대 체력" if health else "") + f" + 지구력 ×{endurance / 100:g}",
                f"MonkStrike.Step(sprite, Skill, {skill['건너뛸칸']}, {attack}, {endurance}, {health});", "")
    if skill["건너뛸칸"] is not None:
        return (f"앞의 적을 넘어 {skill['건너뛸칸']}칸 건너뛰고 돌아선다",
                f"MonkStrike.Step(sprite, Skill, {skill['건너뛸칸']});", "")
    if skill["공격력배수"] is not None:
        attack, endurance = stat_percents(skill)
        where, extra = "", ""
        if skill["둘레"]:
            where, extra = "둘레 네 칸에 ", ", around: true"
        elif skill["앞칸수"] > 1:
            where, extra = f"앞 {skill['앞칸수']}칸에 ", f", reach: {skill['앞칸수']}"
        return (where + f"공격력 ×{attack / 100:g}" + (f" + 지구력 ×{endurance / 100:g}" if endurance else ""),
                f"MonkStrike.Use(sprite, Skill, {attack}, {endurance}, {motion}{extra});", "")
    return None


def csharp(skill):
    """하데스 스크립트 한 장. 이름은 팩의 한글 그대로 쓴다 — 영문 짝이 아직 없다."""
    name = skill["이름"]
    note, call, using = shape(skill)
    if skill["마나"]:
        note += f" · 마나 {skill['마나']}"
        call = (f"if (!MonkStrike.Spend(sprite, Skill, {skill['마나']}))\n"
                f"                return;\n\n            {call}")
    if name in PACK_ONLY:
        note += " · 원작에 없는 팩 전용 기술"

    where = klass(name)
    return f"""using Darkages.Scripting;
{using}using Darkages.Types;

namespace Darkages.Storage.locales.Scripts.Skills
{{
    /// <summary>
    /// {name} — {note}
    /// </summary>
    /// <remarks>
    /// 손으로 고치지 말 것. `scripts/build-monk-skills.py` 가 5.99 서버팩 스크립트에서 다시 만든다.
    /// </remarks>
    [Script("{name}", "{MARK}")]
    public class {where} : SkillScript
    {{
        public {where}(Skill skill) : base(skill)
        {{
        }}

        public override void OnFailed(Sprite sprite)
        {{
        }}

        public override void OnSuccess(Sprite sprite)
        {{
            {call}
        }}

        public override void OnUse(Sprite sprite)
        {{
            OnSuccess(sprite);
        }}
    }}
}}
"""


def main():
    writing = "--쓰기" in sys.argv or "--write" in sys.argv
    found = blocks()
    if not found:
        print(f"팩 스크립트를 찾지 못했습니다: {PACK.relative_to(ROOT)}")
        return 1

    made, skipped = [], []
    for name, body in sorted(found.items()):
        if name in EXCLUDED:
            skipped.append(name)
            continue
        skill = gather(name, body)
        if shape(skill) is None:
            skipped.append(name)
            continue
        made.append(skill)

    print(f"무도가 기술 {len(found)}개 중 옮길 수 있는 것 {len(made)}개")
    for skill in made:
        print(f"  {skill['이름']:10} {shape(skill)[0]:34} 마나 {skill['마나']} · 모션 {skill['모션']} · 이펙트 {skill['이펙트']}"
              f" · 소리 {skill['소리']} · 딜레이 {skill['딜레이']}")
    if skipped:
        print(f"  건너뜀 {len(skipped)}개(옮길 모양 없음·제외): {', '.join(skipped)}")

    if not writing:
        print("\n--쓰기 를 붙이면 실제로 만듭니다.")
        return 0

    levels = learn_levels()
    SCRIPTS.mkdir(parents=True, exist_ok=True)
    TEMPLATES.mkdir(parents=True, exist_ok=True)
    for skill in made:
        (SCRIPTS / f"{skill['이름']}.cs").write_text(csharp(skill), encoding="utf-8-sig")
        # 이미 있는 템플릿은 원작 자료다(요구 레벨·능력치·출처). 팩으로 덮지 않고 팩이 아는 네 칸만 바꾼다.
        path = TEMPLATES / f"{skill['이름']}.json"
        template = json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else {
            "Name": skill["이름"],
            "Prerequisites": {"Class_Required": MONK_CLASS},
            "MaxLevel": 100,
            "ID": 0,
            "Description": None,
            "Group": MARK,
        }
        template.update({
            "ScriptName": skill["이름"],
            "Sound": skill["소리"] or 0,
            "TargetAnimation": skill["이펙트"] or 0,
            "Cooldown": skill["딜레이"] or 0,
        })
        if skill["이름"] in PACK_ONLY:
            template["Group"] = f"{MARK}/원작없음"
        level = levels.get(skill["이름"])
        if level and str(template.get("Group", "")).startswith(MARK) and skill["이름"] not in ORIGINAL_TWIN:
            template.setdefault("Prerequisites", {})["ExpLevel_Required"] = level
        path.write_text(json.dumps(template, ensure_ascii=False, indent=2), encoding="utf-8-sig")
    print(f"\n스크립트·템플릿 {len(made)}쌍을 만들었습니다.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
