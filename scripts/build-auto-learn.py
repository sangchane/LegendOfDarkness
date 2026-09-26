#!/usr/bin/env python3
"""레벨이 되면 저절로 배우는 기술·마법 표를 만든다 (사용자 결정 2026-09-26: "사범에게서 배우지 않고 레벨이 되면 자동으로").

**표의 근거는 노바 팩이다**(사용자 결정 2026-09-27: 「기술·마법 목록과 배우는 레벨을 노바처럼, 5.99 에만 있는 것은 뺀다」).

  - 노바 `db/script/스킬배우기.txt` 의 직업별 **1차 스킬상인**(`전사스킬상인` …) — 메뉴 글 끝 숫자가 레벨("단각11"),
    갈래의 `skill_add`/`spell_add` 가 이름, `…_exist("윗기술") … 상위단계` 거절이 「있으면 안 줌」.
  - 노바 `db/script/npc_script.txt` 의 전직(`set_class N; … "리치 1년, 봄 …의 길로"; skill_add …`) — 전직 때 주는 첫 기술, 1레벨.
  - 넣지 않는 것: 2·3 스킬상인(승급·2차 승급 — 우리에게 승급 제도가 없다) · 모든 직업 기본(`script.txt` 로그인의
    기본공격·탐색: 기본공격은 하데스 `Assail` 이 바로 그것이라 새 캐릭터가 이미 가진다, 탐색은 명령 `discovery_skill` 이
    `Pack599.cs` 에 없어 눌러도 아무 일이 없다) · 정권(사용자 2026-09-25: 운영자 명령으로만) · 서버에 템플릿이 없는 이름.

**5.99 에만 있던 것은 치운다**: 예전 표(5.99 사범, 아래)에는 있는데 노바 1차 목록(같은 직업)에 없는 것을 `Withdrawn` 으로
적는다. 서버가 로그인·레벨업 때(`AutoLearn.Catchup`) 그 직업 캐릭터의 기술·마법창에서 **이 목록에 있는 것만** 지운다 —
퀘스트 보상·운영자 명령으로 받은 다른 기술은 건드리지 않는다.

예전 표(5.99) — 밀레스마을 직업 사범 20명(가렌·이블린·럭스·소라카·리신 1~4). 이미 C# 으로 옮겨 둔
`database/server/scripts/Pack599/Npcs/<이름>.cs`(`build-pack-npcs.py`)를 읽는다. 사범마다

  if (get_class != 직업) … 거절          → 직업
  if (v_select == N) { … get_level < L … skill_add/spell_add "이름" }  → 레벨 L 에 "이름"

기준은 **레벨·직업만** — 골드·재료·앞 단계 기술 조건은 보지 않는다(사용자). 갈래에 레벨 검사가 없으면 기술 템플릿의
`Prerequisites.ExpLevel_Required` 를 쓴다.

예전 표에서 넣지 않던 것:
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
NOVA = ROOT / "data" / "server-packs" / "novaonline" / "db" / "script"

#: 밀레스마을 직업 사범 — 이름 뒤 숫자 없는 것과 2·3·4.
TEACHERS = [f"{name}{n}" for name in ("가렌", "이블린", "럭스", "소라카", "리신") for n in ("", "2", "3", "4")]
#: 사용자 결정으로 빼는 것.
EXCLUDED = {"정권": "운영자 명령으로만 (사용자)"}
#: 팩 직업 번호 = 하데스 `Class` 값(1 전사 · 2 도적 · 3 마법사 · 4 성직자 · 5 무도가).
CLASS_NAMES = {1: "Warrior", 2: "Rogue", 3: "Wizard", 4: "Priest", 5: "Monk"}
CLASS_KO = {1: "전사", 2: "도적", 3: "마법사", 4: "성직자", 5: "무도가"}
CLASS_BY_KO = {v: k for k, v in CLASS_KO.items()}

NOVA_MERCHANT = re.compile(r"^0,0,0,0,0,0,0\t([23]?)(전사|도적|마법사|성직자|무도가)스킬상인\t\{", re.M)
NOVA_HEAD = re.compile(r"^0,0,0,0,0,0,0\t", re.M)
NOVA_ADD = re.compile(r'(skill|spell)_add\s+"([^"]+)"')
NOVA_HIGHER = re.compile(r'(?:skill|spell)_exist\("([^"]+)"\) == 1\)\{mes 1, "[^"]*상위단계')
NOVA_CLASS_CHANGE = re.compile(r'set_class (\d);.*?"리치 1년, 봄 [^"]*의 길로"; (skill|spell)_add "([^"]+)"')

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


def nova():
    """노바 1차 스킬상인 + 전직 첫 기술 → `{(직업, 종류, 이름): (레벨, 출처)}`, 윗 기술, 승급 목록(보고용)."""
    text = (NOVA / "스킬배우기.txt").read_text(encoding="utf-8-sig")
    heads = list(NOVA_HEAD.finditer(text))
    rows, instead, promoted = {}, {}, []
    for m in NOVA_MERCHANT.finditer(text):
        tier, path = m.group(1), CLASS_BY_KO[m.group(2)]
        end = next((h.start() for h in heads if h.start() > m.start()), len(text))
        body = text[m.end():end]
        labels = re.findall(r'"([^"]*)"', re.search(r'menu\("[^"]*",(.*?)\);', body).group(1))
        parts = re.split(r"@select == (\d+)\)", body)
        for select, part in zip(parts[1::2], parts[2::2]):
            label = labels[int(select) - 1]
            for kind, name in NOVA_ADD.findall(part):
                if tier:
                    promoted.append((path, "승급" if tier == "2" else "2차승급", kind, name))
                    continue
                level = int(re.search(r"(\d+)$", label).group(1))
                key = (path, kind, name)
                if key not in rows or rows[key][0] > level:
                    rows[key] = (level, f"노바 {m.group(2)}스킬상인")
                instead.setdefault(name, set()).update(NOVA_HIGHER.findall(part))
    for line in (NOVA / "npc_script.txt").read_text(encoding="utf-8-sig").splitlines():
        found = NOVA_CLASS_CHANGE.search(line)
        if found:
            rows.setdefault((int(found.group(1)), found.group(2), found.group(3)), (1, "노바 전직"))
    return rows, instead, promoted


def pack599(known):
    """예전 표 — 5.99 사범 20명. `(rows, skipped, instead, replaces)`."""
    rows, skipped = {}, []
    instead, replaces = {}, {}

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
    return rows, skipped, instead, replaces


def main():
    write = "--쓰기" in sys.argv
    known = {"skill": templates("skill"), "spell": templates("spell")}
    old, _, _, _ = pack599(known)
    found, instead, promoted = nova()

    rows, skipped, icons = {}, [], {}
    for (path, kind, name), (level, source) in found.items():
        if name in EXCLUDED:
            skipped.append((source, name, EXCLUDED[name]))
            continue
        template = known[kind].get(name)
        if template is None:
            skipped.append((source, name, f"{kind} 템플릿 없음"))
            continue
        rows[(path, kind, name)] = (level, source)
        icons[(path, kind, name)] = int(template.get("Icon") or 0)
    replaces = {}

    # 5.99 에만 있던 것 — 같은 직업의 노바 1차 목록(템플릿이 없어 못 넣은 것·정권까지)에 이름이 없는 것.
    taught = {(path, name) for (path, _, name) in found}
    withdrawn = sorted({(path, kind, name) for (path, kind, name) in old if (path, name) not in taught})

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
        print(f"못 넣음\t{teacher}\t{name}\t{why}")
    for path, kind, name in withdrawn:
        print(f"치움(5.99 전용)\t{CLASS_KO[path]}\t{'기술' if kind == 'skill' else '마법'}\t{name}\t5.99 {old[(path, kind, name)][0]}레벨")
    for path, tier, kind, name in promoted:
        print(f"안 넣음({tier})\t{CLASS_KO[path]}\t{'기술' if kind == 'skill' else '마법'}\t{name}")
    print(f"모두 {len(ordered)}개 (못 넣은 것 {len(skipped)} · 치울 5.99 전용 {len(withdrawn)} · 승급 {len(promoted)})")

    lines = [
        "// 손으로 고치지 말 것. `python3 scripts/build-auto-learn.py --쓰기` 가 다시 만든다.",
        "// 근거: 노바 팩 1차 스킬상인(`db/script/스킬배우기.txt` 메뉴 글의 레벨)과 전직 첫 기술(`npc_script.txt`).",
        "// 치울 것(Withdrawn): 5.99 밀레스마을 직업 사범 20명(`database/server/scripts/Pack599/Npcs`)이 가르치는데 노바 1차 목록에 없는 것.",
        "namespace Darkages.Types",
        "{",
        "    public static partial class AutoLearn",
        "    {",
        "        /// <summary>",
        "        /// 직업 · 배우는 레벨 · 기술이면 true(마법이면 false) · 템플릿 이름 · 이 중 하나라도 있으면 주지 않는다(윗 기술 — 사범의",
        "        /// \"상위스킬\" 거절, 승급하며 지운 것) · 주면서 지운다(사범의 `spell_del`). 노바 1차 스킬상인 기준(사용자 2026-09-27).",
        "        /// </summary>",
        "        public static readonly (Class Path, int Level, bool Skill, string Name, string[] Instead, string[] Replaces)[] Table =",
        "        {",
    ]
    for (path, kind, name), (level, source) in ordered:
        lines.append(f'            (Class.{CLASS_NAMES[path]}, {level}, {"true" if kind == "skill" else "false"}, "{name}", '
                     f'{listed(instead.get(name))}, {listed(replaces.get(name))}), // {source}')
    lines += [
        "        };",
        "",
        "        /// <summary>",
        "        /// 5.99 사범만 가르치던 것 — 이 직업 캐릭터의 창에서 치운다(사용자 2026-09-27 「5.99 에만 있는 것은 뺀다」).",
        "        /// 이 목록에 있는 이름만 지운다. 직업 · 기술이면 true(마법이면 false) · 템플릿 이름.",
        "        /// </summary>",
        "        public static readonly (Class Path, bool Skill, string Name)[] Withdrawn =",
        "        {",
    ]
    for path, kind, name in withdrawn:
        lines.append(f'            (Class.{CLASS_NAMES[path]}, {"true" if kind == "skill" else "false"}, "{name}"), '
                     f'// 5.99 {old[(path, kind, name)][1]} {old[(path, kind, name)][0]}레벨')
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
