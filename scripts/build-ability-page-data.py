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

CLASS = {1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "수도사"}


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
    out = set()
    for f in SCRIPTS.rglob("*.cs"):
        out |= set(re.findall(r'\[Script\("([^"]+)"', f.read_text(encoding="utf-8", errors="replace")))
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
    manual, has = manual_names(), scripted()
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
                shots, sounds = [], []
                if media:
                    for level in media["레벨"]:
                        for directive in level["이펙트"]:
                            found = EFFECT_TAIL.search(directive)
                            if found and int(found.group(1)) not in shots:
                                shots.append(int(found.group(1)))
                        for pair in level["모션"]:
                            if pair[0] not in shots:
                                shots.append(pair[0])
                        for number in level["사운드"]:
                            if number not in sounds:
                                sounds.append(number)
                listed.append({"연출": shots, "소리": sounds,
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

    automatic_names = set(consensus)
    corrected_names = {name for name, value in manual.items()
                       if value and value != consensus.get(name, {}).get("korean", "")}
    effective_names = automatic_names | set(manual)
    data = {"요약": {"전체": len(rows), "기술": sum(1 for r in rows if r["kind"] == "skill"),
                    "마법": sum(1 for r in rows if r["kind"] == "spell"),
                    "자동확정": len(automatic_names), "사용자수정": len(corrected_names),
                    "한글채움": sum(1 for r in rows if r["name"] in effective_names),
                    "스크립트있음": sum(1 for r in rows if r["name"] in has)},
            "묶음": groups}
    OUT.write_text("window.ABILITY_DATA = " + json.dumps(data, ensure_ascii=False) + ";\n", encoding="utf-8")
    s = data["요약"]
    print(f"기술 {s['기술']} · 마법 {s['마법']} · 세 팩 합의 {s['자동확정']} · "
          f"사용자 수정 {s['사용자수정']} · 한글 표시 {s['한글채움']}")
    print(f"→ {OUT.relative_to(ROOT)}  ({OUT.stat().st_size//1024} KB)")


if __name__ == "__main__":
    main()
