#!/usr/bin/env python3
"""아이콘 번호로 한글 이름과 영문 이름을 맞대 본다.

팩(한글)에도 아이콘 번호가 있고(`이미지` 칸) 원작(영문)에도 있다(`raw[1]` 첫 값).
**같은 아이콘이면 같은 기술일 공산이 크다** — 실제로 맞춰 보면 뜻이 통한다:

    아이콘 9  센스 = Sense · 15 윈드블레이드 = Wind Blade · 17 투핸드어택 = Two-handed Attack
    아이콘 29~31  쿠라노/쿠라노소/수페라쿠라노 = ioc / mor ioc / ard ioc   (등급 순서까지 맞다)

다만 **짐작이다.** 한 아이콘을 여럿이 나눠 쓰므로, 양쪽 다 그 아이콘을 하나만 쓸 때만
곧바로 쌍이 된다. 나머지는 후보로만 적고 사람이 고른다. 직업이 같으면 더 좁혀진다.

  쓰는 법: python3 scripts/match-ability-names.py          # 보기만
           python3 scripts/match-ability-names.py --write  # 이름표의 빈칸을 채운다
"""
import json, sys, collections
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
E = ROOT / "data" / "server-packs" / "extracted" / "5.99-server"
ORIG = ROOT / "data" / "game-data" / "abilities.json"
TABLE = ROOT / "data" / "기술마법-한글이름.tsv"
OUT = ROOT / "plans" / "5.99-기술마법-아이콘대조.tsv"

CLASS = {1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "수도사"}


def icon_of(r):
    try:
        return int(r["raw"][1].split("/")[0])
    except (IndexError, ValueError):
        return None


def main(write=False):
    orig = json.loads(ORIG.read_text(encoding="utf-8-sig"))
    rows, pairs = [], {}

    for kind, packfile, label in (("skill", "skills", "기술"), ("spell", "spells", "마법")):
        pack = json.loads((E / f"{packfile}.json").read_text(encoding="utf-8"))
        by_icon_ko = collections.defaultdict(list)
        for p in pack:
            v = p["fields"].get("이미지")
            if v and str(v).isdigit():
                by_icon_ko[int(v)].append(p["이름"])

        by_icon_en = collections.defaultdict(list)
        for r in orig:
            if r["kind"] == kind and icon_of(r) is not None:
                by_icon_en[icon_of(r)].append(r)

        for ic in sorted(set(by_icon_ko) & set(by_icon_en)):
            ko, en = by_icon_ko[ic], by_icon_en[ic]
            sure = len(ko) == 1 and len(en) == 1
            for e in en:
                rows.append([label, str(ic), CLASS.get(e.get("class"), "?"), e["name"],
                             " | ".join(ko), "확실" if sure else f"후보 {len(ko)}×{len(en)}"])
            if sure:
                pairs[en[0]["name"]] = ko[0]

    OUT.write_text("\n".join([
        "# 아이콘 번호로 맞대 본 한글↔영문 이름. **짐작이다** — 사람이 확인해야 한다.",
        "# 한 아이콘을 여럿이 나눠 쓰므로, 양쪽 다 하나뿐일 때만 '확실' 로 적었다.",
        "# 팩 아이콘 = skills/spells.json 의 `이미지`, 원작 = abilities.json 의 raw[1] 첫 값.",
        "#",
        "# 칸: 갈래 / 아이콘 / 직업 / 영문이름 / 한글후보 / 판정",
        "",
    ] + ["\t".join(r) for r in rows]) + "\n", encoding="utf-8")

    sure = sum(1 for r in rows if r[5] == "확실")
    print(f"맞댄 줄 {len(rows)} · 곧바로 쌍이 되는 것 {len(pairs)}")
    print(f"→ {OUT.relative_to(ROOT)}")

    if not write:
        print("\n이름표에 넣으려면 --write")
        return

    lines, filled = TABLE.read_text(encoding="utf-8").splitlines(), 0
    for i, line in enumerate(lines):
        if not line or line.startswith(("#", "갈래\t")):
            continue
        c = line.split("\t")
        if len(c) < 6 or c[5].strip():          # 이미 사람이 채운 것은 건드리지 않는다
            continue
        name = c[2].lstrip("· ").strip()
        if name in pairs:
            c[5] = pairs[name]
            lines[i] = "\t".join(c)
            filled += 1
    TABLE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"이름표 빈칸 {filled}개를 채웠다 → {TABLE.relative_to(ROOT)}")


if __name__ == "__main__":
    main("--write" in sys.argv)
