#!/usr/bin/env python3
"""무도가 기술을 5.99 서버팩 스크립트에서 읽어 하데스 스크립트·템플릿으로 찍어낸다.

하데스는 기술 258장 중 30장만 구현돼 있다. 나머지는 이름만 있고 눌러도 아무 일이 없다. 팩 쪽에는
기술마다 피해식·모션·이펙트·소리·딜레이가 한 블록에 다 있으므로, 손으로 옮기지 말고 읽어서 만든다.

**피해식은 능력치 배율 하나로 통일한다.**

    피해 = (힘 × 힘배율 + 지구력 × 지구력배율 + 민첩성 × 민첩성배율) ÷ 100

팩은 기술을 「공격력의 몇 배」로 적는다. 그 `get_att_damage` 자리에 하데스의 평타
(`Assail.cs:55` 의 `힘×4 + 민첩성×2`)를 놓고 펴면 능력치 배율이 된다 — 단각의 2.8배는
`힘 ×11.2 + 민첩성 ×5.6` 이다. **그 자리 맞춤은 추측이다**: 팩 엔진의 `공격력` 이 무엇을 세는지
우리 자료에는 없고(명령 문서에 옵코드 `0x8C` 뿐) 하데스 평타와 같다고 본 것이다. 5.99 는 기술마다 **두 계수만** 다르게 준다 — 단각 2.8배,
붕각 3.5배 + 지구력 59, 선풍각 3.5배 + 지구력 66. Novaonline 은 같은 것을 `힘 + 상수`(단각 75 ·
붕각 114 · 선풍각 184)로 적었는데 **순서가 5.99 와 같다.** 상수는 레벨이 올라도 안 커지므로 배율
쪽을 쓴다. 혼든은 자릿수가 100배라(공격력×100 + 지구력×2500) 쓰지 않는다.

체력 비례 기술(달마신공·구양신공)은 배수로 옮길 수 없어 따로 적는다 — 5.99 와 Novaonline 이
글자까지 같은 `최대체력 ÷ 100 × 30` 이다.

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


def gather(name, body):
    """한 기술에서 옮길 수 있는 것만 골라 낸다."""
    joined = re.sub(r"\s+", "", body)
    return {
        "이름": name,
        "공격력배수": attack_multiplier(body),
        "지구력배수": one(joined, r"get_con\(@\w*\)\*(\d+)"),
        "체력분율": one(joined, r"get_vita\(@\w*\)/100\*(\d+)"),
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
    """팩의 「공격력 × n」을 능력치 배율로 편다. 백분율이라 100 이 1배다."""
    attack = skill["공격력배수"] or 100
    return (attack * BLOW_STRENGTH,
            (skill["지구력배수"] or 0) * 100,
            attack * BLOW_AGILITY)


def csharp(skill):
    """하데스 스크립트 한 장. 이름은 팩의 한글 그대로 쓴다 — 영문 짝이 아직 없다."""
    name, motion = skill["이름"], skill["모션"] or 0x84
    note = []
    if skill["체력분율"] is not None:
        note.append(f"최대 체력의 {skill['체력분율']}%. 5.99 와 Novaonline 이 글자까지 같다.")
        call = f"MonkStrike.UseVitality(sprite, Skill, {skill['체력분율']}, 0x{motion:02X});"
    else:
        strength, endurance, agility = stat_percents(skill)
        note.append(f"힘 ×{strength / 100:g}"
                    + (f" + 지구력 ×{endurance / 100:g}" if endurance else "")
                    + f" + 민첩성 ×{agility / 100:g}"
                    + f"  (팩의 공격력 {(skill['공격력배수'] or 100) / 100:g}배를 편 것)")
        call = (f"MonkStrike.Use(sprite, Skill, {strength}, {endurance}, {agility}, "
                f"0x{motion:02X});")

    where = klass(name)
    return f"""using Darkages.Scripting;
using Darkages.Types;

namespace Darkages.Storage.locales.Scripts.Skills
{{
    /// <summary>
    /// {name} — {note[0]}
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
        skill = gather(name, body)
        if skill["공격력배수"] is None and skill["체력분율"] is None:
            skipped.append(name)
            continue
        made.append(skill)

    print(f"무도가 기술 {len(found)}개 중 옮길 수 있는 것 {len(made)}개")
    for skill in made:
        if skill["체력분율"] is not None:
            shape = f"최대체력 {skill['체력분율']}%"
        else:
            strength, endurance, agility = stat_percents(skill)
            shape = (f"힘 ×{strength / 100:g}"
                     + (f" 지구력 ×{endurance / 100:g}" if endurance else "")
                     + f" 민첩성 ×{agility / 100:g}")
        print(f"  {skill['이름']:10} {shape:34} 모션 {skill['모션']} · 이펙트 {skill['이펙트']}"
              f" · 소리 {skill['소리']} · 딜레이 {skill['딜레이']}")
    if skipped:
        print(f"  피해식이 없어 건너뜀 {len(skipped)}개: {', '.join(skipped)}")

    if not writing:
        print("\n--쓰기 를 붙이면 실제로 만듭니다.")
        return 0

    SCRIPTS.mkdir(parents=True, exist_ok=True)
    TEMPLATES.mkdir(parents=True, exist_ok=True)
    for skill in made:
        (SCRIPTS / f"{skill['이름']}.cs").write_text(csharp(skill), encoding="utf-8-sig")
        (TEMPLATES / f"{skill['이름']}.json").write_text(json.dumps({
            "Name": skill["이름"],
            "ScriptName": skill["이름"],
            "Prerequisites": {"Class_Required": MONK_CLASS},
            "MaxLevel": 100,
            "ID": 0,
            "Description": None,
            "Group": MARK,
            "Sound": skill["소리"] or 0,
            "TargetAnimation": skill["이펙트"] or 0,
            "Cooldown": skill["딜레이"] or 0,
        }, ensure_ascii=False, indent=2), encoding="utf-8-sig")
    print(f"\n스크립트·템플릿 {len(made)}쌍을 만들었습니다.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
