#!/usr/bin/env python3
"""기술·마법을 화면에서 볼 수 있게 한 덩어리로 뽑는다 — 직업별, 선행 사슬대로.

613개를 표로 보면 무엇을 배워야 무엇이 열리는지가 안 보인다. 직업으로 나누고 사슬로
들여 쓰면 보인다. 구조·정렬은 Hades가 기준이고, 한글 이름은 세 서버팩이 완전히 일치할 때
자동 채택한다. `data/기술마법-한글이름.tsv`의 사람이 고친 값은 그보다 우선한다.

  쓰는 법: python3 scripts/build-ability-page-data.py   → docs/abilities-data.js
"""
import json, re
from collections import defaultdict
from pathlib import Path

from ability_name_consensus import load_consensus

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "game-data" / "abilities.json"
NAMES = ROOT / "data" / "기술마법-한글이름.tsv"
SCRIPTS = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server/scripts"
OUT = ROOT / "docs" / "abilities-data.js"
#: 게임이 실제로 쓰는 연출 번호. 그림·소리를 자르는 생성기들이 이것을 읽는다.
USED = ROOT / "data" / "game-data" / "ability-presentation.json"
WORKBOOK_FACTS = ROOT / "data" / "skill-spell-2023" / "skills.json"

CLASS = {1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "수도사"}
def original_monk_table():
    """정규화된 2023 표의 도가 일반 행만 화면에 낸다.

    화면이 워크북을 다시 해석하면 추출기와 서로 다른 사실을 만들 수 있다. 이 표는
    `build-skill-spell-2023.py`가 보존한 사실만 읽고, 연출은 아래 서버 자료와 별도로 붙인다.
    """
    if not WORKBOOK_FACTS.exists():
        return []
    rows = json.loads(WORKBOOK_FACTS.read_text(encoding="utf-8"))["records"]
    out = []
    for row in rows:
        if row["class"] != "도가" or row["section"] != "ordinary":
            continue
        requirement = row["requirement"]
        if requirement["type"] == "level":
            section = f"Lv.{requirement['value']:02d}"
        elif requirement["type"] == "ascension":
            section = "승급"
        else:
            section = ""
        out.append({
            "행": row["source"]["row"], "이름": row["name"], "구간": section,
            "레벨": requirement["value"] if requirement["type"] == "level" else 0,
            "비용": row.get("cost_text", ""),
            "재료": [row["materials"]] if row["materials"] else [],
            "배우는곳": row["learning_location"].replace("\n", " "),
            "표ID": row["id"],
        })
    return out


def manual_names():
    if not NAMES.exists():
        return {}
    out = {}
    for line in NAMES.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith(("#", "갈래\t")):
            continue
        c = line.split("\t")
        if len(c) >= 6 and c[5].strip():
            out[c[2].lstrip("· ").strip()] = c[5].strip()
    return out


def scripted():
    out = {}
    for f in SCRIPTS.rglob("*.cs"):
        text = f.read_text(encoding="utf-8", errors="replace")
        for name in re.findall(r'\[Script\("([^"]+)"', text):
            out.setdefault(name, []).append(text)
    return out


# 서버가 클라이언트에 연출을 보내는 길. 이펙트는 0x29, 소리는 0x13/0x19, 몸동작은 0x1A 다
# (docs/martial-artist-skill-presentation.md 1절). 5.99 팩 스크립트는 `Pack599.Call` 을 거친다.
PACK_CALL = re.compile(r'Call\("(effect|game_sound|motion)"\s*,(.*?)\);', re.S)
SEND_ANIMATION = re.compile(r'SendAnimation\((\d+)')
FORMAT_19 = re.compile(r'ServerFormat19\s*\{\s*Number\s*=\s*\(?[a-z]*\)?\s*(\d+)')
FORMAT_1A = re.compile(r'ServerFormat1A\s*\{[^}]*?Number\s*=\s*(?:\(byte\)\s*)?(0x[0-9A-Fa-f]+|\d+)', re.S)
#: 무도가 기술은 도우미를 거쳐 나간다 — 16진수로 적힌 인자가 몸동작 번호다(`MonkStrike.Afflict(…, 0x84)`).
MONK_CALL = re.compile(r'MonkStrike\.\w+\((.*?)\);', re.S)
HEX_OR_INT = re.compile(r'^\s*(?:\(byte\)\s*)?(0x[0-9A-Fa-f]+|\d+)\s*$')


def split_args(text):
    """맨 바깥 쉼표로만 가른다. 인자가 또 괄호를 품고 있어 `split(",")` 로는 안 된다."""
    out, depth, current = [], 0, ""
    for ch in text:
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        if ch == "," and depth == 0:
            out.append(current.strip())
            current = ""
        else:
            current += ch
    if current.strip():
        out.append(current.strip())
    return out


def literal(arg):
    """`(V)257L` · `0x84` · `35` 처럼 **그 자리에 박힌 수**. 변수면 None."""
    inner = re.fullmatch(r'\(V\)(\d+)L', arg.strip())
    if inner:
        return int(inner.group(1))
    found = HEX_OR_INT.match(arg)
    return int(found.group(1), 0) if found else None


def sent_by(bodies, template):
    """이 기술을 쓰면 **게임이 실제로 무엇을 보내나** — 채널마다 번호 목록으로.

    화면이 세던 「연출」은 노바온라인 팩의 표를 한글 이름으로 찾은 것이라 우리 서버가 보내는 것과
    다르다. 사제 마법이 특히 딴판이다 — 프라보는 팩 표가 43·33 인데 서버는 **257** 을 쏘고,
    쿠로는 21 이 아니라 **267**, 데프레코는 18·33 이 아니라 **243** 이다(사용자, 2026-09-19).
    일음지는 팩 표 42 · 서버 276 이고, **발경은 `TargetAnimation` 이 0 이라 이펙트가 안 나간다.**

    보내는 길은 둘이다: 템플릿의 `TargetAnimation`·`Sound` 를 하데스가 쏘거나, 5.99 팩 스크립트가
    `effect`·`game_sound`·`motion` 으로 직접 쏘거나. **팩의 `effect` 는 `@대상, 쓴쪽그림, 대상그림,
    속도`** 라 둘째·셋째만 그림이다 — 넷째까지 그림으로 세면 프라보의 속도 140 이 이펙트로 둔갑한다.
    """
    out = {"이펙트": [], "소리": [], "몸동작": []}

    def add(channel, number):
        if number and number > 0 and number not in out[channel]:
            out[channel].append(number)

    for body in bodies:
        for command, raw in PACK_CALL.findall(body):
            args = split_args(raw)
            if command == "effect":
                for at in (1, 2):
                    if at < len(args):
                        add("이펙트", literal(args[at]))
            elif command == "game_sound" and args:
                add("소리", literal(args[0]))
            elif command == "motion" and args:
                add("몸동작", literal(args[0]))
        for number in SEND_ANIMATION.findall(body):
            add("이펙트", int(number))
        for number in FORMAT_19.findall(body):
            add("소리", int(number))
        for number in FORMAT_1A.findall(body):
            add("몸동작", int(number, 0))
        for raw in MONK_CALL.findall(body):
            for arg in split_args(raw):
                # 몸동작만 16진수로 적혀 있다(`0x84`). 나머지 인자는 배율·초 같은 10진수다.
                if arg.strip().lower().startswith("0x"):
                    add("몸동작", literal(arg))

    add("이펙트", template.get("TargetAnimation") or 0)
    add("소리", template.get("Sound") or 0)
    return out


TEMPLATES = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server/templates"


def template_scripts():
    """하데스 템플릿 이름 → 그 템플릿. 스크립트는 기술이 `ScriptName`(`Skill.cs:70`), 마법이 `ScriptKey`(`Spell.cs:37`)."""
    out = {}
    for folder, field in (("skills", "ScriptName"), ("spells", "ScriptKey")):
        for f in (TEMPLATES / folder).glob("*.json"):
            try:
                t = json.loads(f.read_text(encoding="utf-8-sig"))
            except ValueError:
                continue
            if t.get("Name"):
                out[t["Name"]] = dict(t, 스크립트=t.get(field) or "")
    return out


EFFECTS = ROOT / "data/game-data/ability-effects.json"
EFFECT_TAIL = re.compile(r",\s*(\d+)\s*,\s*\d+\s*$")


def load_effects():
    """한글 밑말 → 연출·소리. 이펙트는 **한글 팩에만** 있어서 이 길밖에 없다.

    하데스 기술에는 이펙트도 소리도 없다. 그래서 한글 이름이 정해진 것만 이어진다 —
    안 정해진 것은 카드에 「연출 없음」으로 정직하게 나온다.
    """
    if not EFFECTS.exists():
        return {}
    return json.loads(EFFECTS.read_text(encoding="utf-8"))["밑말"]


def main():
    rows = json.loads(SRC.read_text(encoding="utf-8-sig"))
    manual, has, script_of = manual_names(), scripted(), template_scripts()
    consensus, _ = load_consensus()
    effects = load_effects()

    unlocks = defaultdict(list)
    for r in rows:
        if r.get("requires"):
            unlocks[r["requires"]].append(r["name"])

    groups = []
    for cls in sorted({r.get("class") for r in rows} - {None}):
        for kind, label in (("skill", "기술"), ("spell", "마법")):
            here = {r["name"]: r for r in rows if r.get("class") == cls and r["kind"] == kind}
            if not here:
                continue
            seen, listed = set(), []

            def walk(name, depth):
                if name in seen or name not in here:
                    return
                seen.add(name)
                r = here[name]
                # raw[1] 의 첫 값. Assail 이 1 이고 Hades 의 assail.json 도 Icon 1 이라 아이콘인가 했지만
                # Assault 도 1 이다 — **아이콘 번호가 아니다.** 뜻을 모르므로 원문 그대로만 보여 준다.
                icon = 0
                try:
                    icon = int(r["raw"][1].split("/")[0])
                except (IndexError, ValueError):
                    pass
                automatic = consensus.get(name, {}).get("korean", "")
                corrected = manual.get(name, "")
                if corrected and corrected != automatic:
                    korean, source = corrected, "사용자 수정"
                elif automatic:
                    korean, source = automatic, "서버팩 3개 일치"
                elif corrected:
                    korean, source = corrected, "사용자 수정"
                else:
                    korean, source = "", "미확정"
                media = effects.get(korean) if korean else None
                # 모션과 이펙트를 갈라 둔다. 둘은 같은 번호 공간을 쓰지만 **얹히는 데가 다르다** —
                # 모션은 시전자가 하는 것이고 이펙트는 맞는 쪽에 걸리는 것이다. 한 목록으로 합쳐
                # 두었더니 화면이 어느 것을 캐릭터에 두고 어느 것을 샌드백에 둘지 알 수가 없어
                # 둘 다 샌드백 위에 겹쳐 터졌다.
                motions, shots, sounds = [], [], []
                if media:
                    for level in media["레벨"]:
                        for directive in level["이펙트"]:
                            found = EFFECT_TAIL.search(directive)
                            if found and int(found.group(1)) not in shots:
                                shots.append(int(found.group(1)))
                        for pair in level["모션"]:
                            if pair[0] not in motions:
                                motions.append(pair[0])
                        for number in level["사운드"]:
                            if number not in sounds:
                                sounds.append(number)
                # `raw[0]` 은 `요구레벨/2차여부/요구어빌리티레벨` 이다. 613개가 1차 257 · 2차 356 으로
                # 갈린다 — 한 화면에 다 놓으면 무엇이 무엇인지 안 보여서 갈래를 하나 더 둔다.
                stage, ability = 1, 0
                first = (r.get("raw") or [""])[0].split("/")
                if len(first) >= 3:
                    stage = 2 if first[1] != "0" else 1
                    ability = int(first[2]) if first[2].isdigit() else 0

                # 구현 = 게임이 쓰는 템플릿이 가리키는 스크립트가 실제로 있다. 영문 이름에 스크립트가
                # 있어도 템플릿이 다른 것을 가리키면(달마신공 → `달마신공`) 그쪽을 본다.
                #
                # **이것은 「눌러서 뭔가 나온다」가 아니다.** 연출(모션·이펙트·소리)은 위에서 보듯
                # **한글 이름으로만** 찾는다. 그래서 연출이 비었다는 말은 원작에 연출이 없다는 뜻이
                # 아니라 **우리가 못 이었다**는 뜻이다 — 원작 기술에 연출 없는 것은 없다(사용자,
                # 2026-09-19). 왜 못 이었는지를 함께 적지 않으면 화면이 거짓말을 한다.
                template = script_of.get(name) or script_of.get(korean) or {}
                script = template.get("스크립트") or ""
                sends = sent_by(has.get(script, []), template) if script in has else {
                    "이펙트": [], "소리": [], "몸동작": []}
                if motions or shots or sounds:
                    blocked = ""
                elif not korean:
                    blocked = "한글이름없음"
                else:
                    blocked = "표에없음"
                listed.append({"구현": script in has, "연출막힘": blocked, "게임": sends,
                               "모션": motions, "이펙트": shots, "소리": sounds,
                               "차수": stage, "어빌리티": ability,
                               "이름": name, "한글": korean, "한글자동": automatic,
                               "한글수정": corrected if corrected != automatic else "",
                               "이름출처": source, "선행": r.get("requires") or "",
                               "레벨": r.get("atLevel") or 0, "깊이": depth, "아이콘": icon,
                               "스크립트": name in has, "요구": r.get("statCosts") or []})
                for nxt in sorted(unlocks.get(name, [])):
                    walk(nxt, depth + 1)

            for name in sorted(here):
                if not here[name].get("requires"):
                    walk(name, 0)
            for name in sorted(here):
                walk(name, 0)

            groups.append({"직업": CLASS.get(cls, str(cls)), "갈래": label, "목록": listed})

    # 같은 기술이 여러 직업에 걸려 있다 — `Assail`(평타)은 다섯 직업 전부에 나온다. 직업별로
    # 늘어놓으면 613 칸이지만 서로 다른 것은 587 개뿐이라, 같은 카드를 다섯 번 보게 된다
    # (사용자, 2026-09-19). 줄은 그대로 두고 **어느 직업들이 쓰는지**와 **어느 자리가 대표인지**만
    # 적어 둔다 — 직업으로 거를 때는 그 직업 자리를 보여 줘야 하므로 자리를 지우지는 않는다.
    seen_at = defaultdict(list)
    for group in groups:
        for row in group["목록"]:
            seen_at[(row["이름"], group["갈래"])].append(group["직업"])
    for group in groups:
        for row in group["목록"]:
            classes = seen_at[(row["이름"], group["갈래"])]
            row["직업들"] = classes
            row["공통"] = len(classes) > 1
            row["대표"] = classes[0] == group["직업"]

    # 원작표의 한글 이름으로 현재 도가 행을 이어 붙인다. 일치하지 않는 이름도 버리지 않고
    # 원작표에 남겨 화면에서 "매칭 없음"으로 검토할 수 있게 한다.
    monk_rows = [(group, row) for group in groups if group["직업"] == "수도사"
                 for row in group["목록"]]
    original_rows = original_monk_table()
    for source in original_rows:
        matches = [(group, row) for group, row in monk_rows if row.get("한글") == source["이름"]]
        source["매칭"] = [{"영문": row["이름"], "갈래": group["갈래"],
                           "출처": "페이지 행", "게임": row["게임"], "구현": row["구현"]}
                          for group, row in matches]
        for _, row in matches:
            row["원작표"] = source
        # 팩 이식 기술은 abilities.json의 영문 원작 목록에 없을 수 있다. 그 경우에도
        # 같은 이름의 Hades 템플릿/스크립트가 보내는 채널을 별도 provenance로 보여 준다.
        template = script_of.get(source["이름"])
        if template:
            script = template.get("스크립트") or ""
            source["매칭"].append({
                "영문": source["이름"], "갈래": "기술", "출처": "Hades 템플릿/스크립트",
                "게임": sent_by(has.get(script, []), template) if script in has else {
                    "이펙트": [], "소리": [], "몸동작": []},
                "구현": script in has,
            })

    automatic_names = set(consensus)
    corrected_names = {name for name, value in manual.items()
                       if value and value != consensus.get(name, {}).get("korean", "")}
    effective_names = automatic_names | set(manual)
    data = {"요약": {"전체": len(rows), "기술": sum(1 for r in rows if r["kind"] == "skill"),
                    "마법": sum(1 for r in rows if r["kind"] == "spell"),
                    "자동확정": len(automatic_names), "사용자수정": len(corrected_names),
                    "한글채움": sum(1 for r in rows if r["name"] in effective_names),
                    "스크립트있음": sum(1 for r in rows if r["name"] in has),
                    "구현": sum(1 for g in groups for r in g["목록"] if r["구현"]),
                    # 「구현」과 「눌러서 보인다」는 다른 말이다. 못 보이는 것은 왜 못 보이는지까지
                    # 세어 둔다 — 그러지 않으면 「연출 없음」이 원작의 사실처럼 읽힌다.
                    "서로다름": len({(r["이름"], g["갈래"]) for g in groups for r in g["목록"]}),
                    "연출셋다": sum(1 for g in groups for r in g["목록"]
                                 if r["대표"] and r["모션"] and r["이펙트"] and r["소리"]),
                    "연출일부": sum(1 for g in groups for r in g["목록"]
                                 if r["대표"] and not r["연출막힘"]
                                 and not (r["모션"] and r["이펙트"] and r["소리"])),
                    "이름없어못찾음": sum(1 for g in groups for r in g["목록"]
                                    if r["대표"] and r["연출막힘"] == "한글이름없음"),
                    "표에없음": sum(1 for g in groups for r in g["목록"]
                                 if r["대표"] and r["연출막힘"] == "표에없음"),
                    # 스크립트는 있는데 게임이 이펙트도 소리도 몸동작도 안 보내는 것. 「구현」이라고
                    # 적어 두면 화면이 거짓말을 한다 — 눌러도 아무 일이 안 일어난다.
                    "게임연출없음": sum(1 for g in groups for r in g["목록"]
                                  if r["대표"] and r["구현"] and not any(r["게임"].values())),
                    "게임이펙트": sum(1 for g in groups for r in g["목록"]
                                 if r["대표"] and r["구현"] and r["게임"]["이펙트"]),
                    "게임소리": sum(1 for g in groups for r in g["목록"]
                                if r["대표"] and r["구현"] and r["게임"]["소리"]),
                    "게임몸동작": sum(1 for g in groups for r in g["목록"]
                                 if r["대표"] and r["구현"] and r["게임"]["몸동작"]),
                    # 모션·이펙트·소리 셋이 다 나가는 것. 화면의 기본이 이것이다(사용자, 2026-09-19).
                    "게임연출셋다": sum(1 for g in groups for r in g["목록"]
                                  if r["대표"] and r["구현"] and all(r["게임"].values()))},
            "묶음": groups,
            "원작무도가": {
                "출처": "data/skill-spell-2023/skills.json · 도가/ordinary (원본: 어둠기술표(2023.01.03).xlsx)",
                "주의": "엑셀에는 모션·이펙트·소리 열이 없어, 매칭된 게임 값은 Hades 템플릿·스크립트에서 온다.",
                "목록": original_rows,
            }}
    OUT.write_text("window.ABILITY_DATA = " + json.dumps(data, ensure_ascii=False) + ";\n", encoding="utf-8")
    s = data["요약"]
    print(f"기술 {s['기술']} · 마법 {s['마법']} · 세 팩 합의 {s['자동확정']} · "
          f"사용자 수정 {s['사용자수정']} · 한글 표시 {s['한글채움']} · 구현 {s['구현']}")
    print(f"게임이 실제로 보내는 것 — 이펙트 {s['게임이펙트']} · 소리 {s['게임소리']} · "
          f"몸동작 {s['게임몸동작']} · 셋 다 {s['게임연출셋다']} · **아무것도 안 보냄 {s['게임연출없음']}**")

    # 그림·소리를 자르는 쪽이 읽는다. 화면이 게임과 같은 번호를 쓰게 하려면 출처가 하나여야 한다.
    used = {"생성": "scripts/build-ability-page-data.py", "채널": {}}
    for channel in ("이펙트", "소리", "몸동작"):
        used["채널"][channel] = sorted({n for g in groups for r in g["목록"] for n in r["게임"][channel]})
    USED.write_text(json.dumps(used, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"게임이 쓰는 번호 → {USED.relative_to(ROOT)} "
          f"(이펙트 {len(used['채널']['이펙트'])} · 소리 {len(used['채널']['소리'])} · 몸동작 {len(used['채널']['몸동작'])})")
    print(f"→ {OUT.relative_to(ROOT)}  ({OUT.stat().st_size//1024} KB)")


if __name__ == "__main__":
    main()
