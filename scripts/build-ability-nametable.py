#!/usr/bin/env python3
"""기술·마법에 한글 이름을 채워 넣을 표를 만든다.

구조와 정렬은 Hades(영문)가 정답지다 — `data/game-data/abilities.json` 613개에는 무엇을
배워야 무엇이 열리는지가 들어 있다. 한글 이름은 세 서버팩이 같은 경우만 자동 후보가 되고,
이 표의 마지막 칸은 사람이 틀린 번역을 고치는 프로젝트 수준의 수정값으로도 쓰인다.

**이미 채운 칸은 건드리지 않는다.** 다시 돌려도 새로 생긴 것만 빈칸으로 붙는다.

읽기 쉽게 배치한다: 직업 → 갈래 → **선행 사슬 순서**. 맨 처음 배우는 것 바로 아래에
그것으로 열리는 것이 오므로, 위에서 아래로 읽으면 기술 나무가 된다.

  쓰는 법: python3 scripts/build-ability-nametable.py
"""
import json
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "game-data" / "abilities.json"
OUT = ROOT / "data" / "기술마법-한글이름.tsv"

CLASS = {1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "수도사"}
HEAD = ["갈래", "직업", "영문이름", "선행", "레벨", "한글이름"]


def main():
    rows = json.loads(SRC.read_text(encoding="utf-8-sig"))

    # 이미 채워 둔 것은 지키고, 새로 생긴 것만 빈칸으로 붙인다.
    filled = {}
    if OUT.exists():
        for line in OUT.read_text(encoding="utf-8").splitlines():
            if not line or line.startswith("#"):
                continue
            c = line.split("\t")
            if len(c) >= 6 and c[5].strip():
                filled[c[2]] = c[5].strip()

    unlocks = defaultdict(list)
    for r in rows:
        if r.get("requires"):
            unlocks[r["requires"]].append(r["name"])

    seen, out = set(), []

    def walk(name, depth, group):
        """선행 사슬을 따라 내려간다 — 배우는 차례대로 읽히도록."""
        if name in seen:
            return
        seen.add(name)
        r = group.get(name)
        if r:
            out.append((r, depth))
        for nxt in sorted(unlocks.get(name, [])):
            if nxt in group:
                walk(nxt, depth + 1, group)

    for cls in sorted({r.get("class") for r in rows} - {None}):
        for kind in ("skill", "spell"):
            group = {r["name"]: r for r in rows
                     if r.get("class") == cls and r["kind"] == kind}
            for name in sorted(group):
                if not group[name].get("requires"):
                    walk(name, 0, group)
            for name in sorted(group):        # 선행이 이 직업 안에 없는 것
                walk(name, 0, group)

    lines = [
        "# 기술·마법 한글 이름 — 자동 확정값을 수정할 수 있는 프로젝트 이름표.",
        "# 구조·정렬(선행·직업·갈래)은 Hades 표가 정답지다: data/game-data/abilities.json",
        "# 자동 이름은 5.99·혼든·Novaonline 세 서버팩이 같은 갈래·아이콘에서 완전히 일치할 때만 쓴다.",
        "#",
        "# 마지막 칸 `한글이름` 만 채우면 된다. 비워 두면 영문 이름을 그대로 쓴다.",
        "# 배치: 직업 → 갈래 → 선행 사슬. 들여쓰기(`·`)가 깊을수록 나중에 배우는 것이다.",
        "# 다시 만들어도 채운 칸은 지켜진다: python3 scripts/build-ability-nametable.py",
        "#",
        "\t".join(HEAD),
        "",
    ]
    for r, depth in out:
        lines.append("\t".join([
            "기술" if r["kind"] == "skill" else "마법",
            CLASS.get(r.get("class"), str(r.get("class"))),
            ("·" * depth + " " if depth else "") + r["name"],
            r.get("requires") or "",
            str(r.get("atLevel") or 0),
            filled.get(r["name"], ""),
        ]))

    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    done = sum(1 for r, _ in out if filled.get(r["name"]))
    print(f"기술·마법 {len(out)}줄 · 이미 채운 것 {done} · 빈칸 {len(out) - done}")
    print(f"→ {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
