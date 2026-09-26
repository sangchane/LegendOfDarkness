#!/usr/bin/env python3
"""레벨이 되면 저절로 배우는 기술·마법 표를 만든다 (사용자 결정 2026-09-26: "사범에게서 배우지 않고 레벨이 되면 자동으로").

근거는 5.99 사범 스크립트다 — 밀레스마을 직업 사범 20명(가렌·이블린·럭스·소라카·리신 1~4). 이미 C# 으로 옮겨 둔
`database/server/scripts/Pack599/Npcs/<이름>.cs`(`build-pack-npcs.py`)를 읽는다. 사범마다

  if (get_class != 직업) … 거절          → 직업
  if (v_select == N) { … get_level < L … skill_add/spell_add "이름" }  → 레벨 L 에 "이름"

기준은 **레벨·직업만** — 골드·재료·앞 단계 기술 조건은 보지 않는다(사용자). 갈래에 레벨 검사가 없으면 기술 템플릿의
`Prerequisites.ExpLevel_Required` 를 쓴다.

넣지 않는 것:
  - 달인(생활 기술, EG 로 산다 — 레벨·직업 조건이 없다)
  - 1차로오·1차이아·1차메투스·승급이아·화론 사범 5·선진(승급·2차 직업 기술, `get_class_sub`)
  - 초보자도우미2(처음 받는 기술 — 사범이 아니다)
  - 정권(사용자: 운영자 명령으로만)
  - 서버에 템플릿이 없는 이름(배울 수 없다 — 출력에 적는다)

  쓰는 법: python3 scripts/build-auto-learn.py [--쓰기]
  산출물:  sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Types/AutoLearnTable.cs (서버)
           mobile/client/assets/world/auto-learn.txt (앱 — 기술 목록에 "N레벨에 배움" 을 적는다, 알맹이 `LearnLadder`)
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server"
NPCS = SERVER / "database" / "server" / "scripts" / "Pack599" / "Npcs"
TEMPLATES = SERVER / "database" / "server" / "templates"
OUT = SERVER / "src" / "Hades.Server.Base" / "Types" / "AutoLearnTable.cs"
APP_OUT = ROOT / "mobile" / "client" / "assets" / "world" / "auto-learn.txt"

#: 밀레스마을 직업 사범 — 이름 뒤 숫자 없는 것과 2·3·4.
TEACHERS = [f"{name}{n}" for name in ("가렌", "이블린", "럭스", "소라카", "리신") for n in ("", "2", "3", "4")]
#: 사용자 결정으로 빼는 것.
EXCLUDED = {"정권": "운영자 명령으로만 (사용자)"}
#: 팩 직업 번호 = 하데스 `Class` 값(1 전사 · 2 도적 · 3 마법사 · 4 성직자 · 5 무도가).
CLASS_NAMES = {1: "Warrior", 2: "Rogue", 3: "Wizard", 4: "Priest", 5: "Monk"}
CLASS_KO = {1: "전사", 2: "도적", 3: "마법사", 4: "성직자", 5: "무도가"}

CLASS_CHECK = re.compile(r'p\.Call\("get_class", v_myid\)\) != \(V\)\(\(V\)(\d+)L\)')
BRANCH = re.compile(r'if \(V\.T\(\(\(V\)\(v_select\) == \(V\)\(\(V\)(\d+)L\)\)\)\)')
LEVEL = re.compile(r'p\.Call\("get_level", v_myid\)\) < \(V\)\(\(V\)(\d+)L\)')
ADD = re.compile(r'p\.Call\("(skill|spell)_add2?", \(V\)"([^"]+)"\)')
#: 배우면서 지우는 것(`skill_add "연천단각"; skill_del "단각"`) — 지워진 것을 다시 주면 안 된다.
ADD_DEL = re.compile(r'p\.Call\("(?:skill|spell)_add2?", \(V\)"([^"]+)"\);\s*p\.Call\("(?:skill|spell)_del2?", \(V\)"([^"]+)"\)')
DEL = re.compile(r'p\.Call\("(?:skill|spell)_del2?", \(V\)"([^"]+)"\)')
#: 사범이 "이미 이 스킬의 상위스킬을 습득 하셧습니다." 로 거절하는 것 — 그 윗 기술이 있으면 주지 않는다.
HIGHER = re.compile(r'_exist", \(V\)"([^"]+)"\)\)\)\s*\{\s*yield return Mes\(\(V\)1L, \(V\)"이미 이 스킬의 상위스킬을')


def templates(kind):
    found = {}
    for path in (TEMPLATES / f"{kind}s").rglob("*.json"):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if isinstance(data, dict) and data.get("Name"):
            data["_path"] = path
            found[data["Name"]] = data
    return found


def mark_abilities(known, ordered):
    """저절로 주는 기술 템플릿에 `Type` 이 없으면 1(Ability)을 적는다.

    없으면 0(Assail)으로 읽혀, 기본공격(0x13)을 누를 때마다 그 기술 스크립트가 함께 돌고(`GameServerHandlers.Assail` →
    `GetAssails`) 다른 기술을 누를 때도 기다림이 걸린다. 사범으로 배워도 같던 것이지만, 저절로 주면 모두가 겪는다.
    이미 적힌 값(양의신권 0 …)은 건드리지 않는다. 다른 생성기(`build-pack-abilities.py` …)는 템플릿을 읽어 칸 몇 개만
    바꿔 쓰므로 이 칸은 남는다."""
    changed = []
    for (path, kind, name), _ in ordered:
        template = known["skill"].get(name) if kind == "skill" else None
        if template is None or "Type" in template:
            continue
        file = template.pop("_path")
        data = json.loads(file.read_text(encoding="utf-8-sig"))
        data["Type"] = 1
        file.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8-sig")
        template["Type"] = 1
        changed.append(name)
    return changed


def main():
    write = "--쓰기" in sys.argv
    known = {"skill": templates("skill"), "spell": templates("spell")}
    rows, skipped = {}, []
    instead, replaces, icons = {}, {}, {}

    # 어느 NPC 든 배우면서 지우는 것: 지워진 것은 새것이 있으면 주지 않는다.
    for path in NPCS.glob("*.cs"):
        for new, old in ADD_DEL.findall(path.read_text(encoding="utf-8-sig")):
            instead.setdefault(old, set()).add(new)

    for teacher in TEACHERS:
        text = (NPCS / f"{teacher}.cs").read_text(encoding="utf-8-sig")
        checks = CLASS_CHECK.findall(text)
        if len(checks) != 1:
            sys.exit(f"{teacher}: 직업 검사가 {len(checks)}개 — 모양이 바뀌었다")
        path = int(checks[0])
        cuts = [m.start() for m in BRANCH.finditer(text)] + [len(text)]
        for start, end in zip(cuts, cuts[1:]):
            part = text[start:end]
            adds = ADD.findall(part)
            if not adds:
                continue
            levels = LEVEL.findall(part)
            higher = HIGHER.findall(part)
            dels = DEL.findall(part)
            for kind, name in adds:
                if name in EXCLUDED:
                    skipped.append((teacher, name, EXCLUDED[name]))
                    continue
                template = known[kind].get(name)
                if template is None:
                    skipped.append((teacher, name, f"{kind} 템플릿 없음"))
                    continue
                if levels:
                    level, source = int(levels[0]), teacher
                else:
                    level = int((template.get("Prerequisites") or {}).get("ExpLevel_Required") or 1)
                    source = "템플릿"
                key = (path, kind, name)
                instead.setdefault(name, set()).update(higher)
                replaces.setdefault(name, set()).update(dels)
                if key not in rows or rows[key][0] > level:
                    rows[key] = (level, source)
                    icons[key] = int(template.get("Icon") or 0)

    ordered = sorted(rows.items(), key=lambda r: (r[0][0], r[1][0], r[0][1], r[0][2]))
    def listed(names):
        return "new string[] { " + ", ".join(f'"{n}"' for n in sorted(names)) + " }" if names else "System.Array.Empty<string>()"

    for (path, kind, name), (level, source) in ordered:
        extra = ""
        if instead.get(name):
            extra += f"\t있으면 안 줌: {', '.join(sorted(instead[name]))}"
        if replaces.get(name):
            extra += f"\t주면서 지움: {', '.join(sorted(replaces[name]))}"
        print(f"{CLASS_KO[path]}\t{level}\t{'기술' if kind == 'skill' else '마법'}\t{name}\t{source}{extra}")
    for teacher, name, why in skipped:
        print(f"뺌\t{teacher}\t{name}\t{why}")
    print(f"모두 {len(ordered)}개 (뺀 것 {len(skipped)})")

    lines = [
        "// 손으로 고치지 말 것. `python3 scripts/build-auto-learn.py --쓰기` 가 다시 만든다.",
        "// 근거: 5.99 밀레스마을 직업 사범 20명의 스크립트(`database/server/scripts/Pack599/Npcs`) — 직업 검사와 갈래마다의 레벨 검사.",
        "namespace Darkages.Types",
        "{",
        "    public static partial class AutoLearn",
        "    {",
        "        /// <summary>",
        "        /// 직업 · 배우는 레벨 · 기술이면 true(마법이면 false) · 템플릿 이름 · 이 중 하나라도 있으면 주지 않는다(윗 기술 — 사범의",
        "        /// \"상위스킬\" 거절, 승급하며 지운 것) · 주면서 지운다(사범의 `spell_del`).",
        "        /// </summary>",
        "        public static readonly (Class Path, int Level, bool Skill, string Name, string[] Instead, string[] Replaces)[] Table =",
        "        {",
    ]
    for (path, kind, name), (level, source) in ordered:
        lines.append(f'            (Class.{CLASS_NAMES[path]}, {level}, {"true" if kind == "skill" else "false"}, "{name}", '
                     f'{listed(instead.get(name))}, {listed(replaces.get(name))}), // {source}')
    lines += ["        };", "    }", "}", ""]

    # 앱: 직업 번호(서버 `Class`, 프로필 0x39 의 직업 바이트와 같다) · 레벨 · skill/spell · 이름 · 그림 번호(템플릿 `Icon`,
    # 서버가 0x2C/0x17 로 보내는 것과 같은 값 — 없으면 0).
    app = [
        "# tools: scripts/build-auto-learn.py 가 서버와 같은 표로 만든다. 손으로 고치지 말 것.",
        "# 직업(1 전사 · 2 도적 · 3 마법사 · 4 성직자 · 5 무도가)\t레벨\tskill|spell\t이름\t그림",
    ]
    for (path, kind, name), (level, source) in ordered:
        app.append(f"{path}\t{level}\t{kind}\t{name}\t{icons[(path, kind, name)]}")

    if write:
        OUT.write_text("\n".join(lines), encoding="utf-8")
        print(f"썼다: {OUT.relative_to(ROOT)}")
        APP_OUT.write_text("\n".join(app) + "\n", encoding="utf-8")
        print(f"썼다: {APP_OUT.relative_to(ROOT)}")
        changed = mark_abilities(known, ordered)
        print(f"기술 템플릿 Type 1 로: {len(changed)}개 {', '.join(changed)}")


if __name__ == "__main__":
    main()
