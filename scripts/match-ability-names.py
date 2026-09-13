#!/usr/bin/env python3
"""두 서버팩이 합의한 기술·마법 한글 이름을 대조하고 감사표를 만든다.

Hades의 갈래·아이콘별 영문 이름이 하나이고, 5.99와 혼든도 같은 갈래·아이콘에서
각각 하나의 동일한 한글 이름을 가질 때만 자동 확정한다. ``--write``는 이름표의 빈칸만
채우며 사용자가 고친 기존 값은 절대 덮지 않는다.
"""
import sys
from pathlib import Path

from ability_name_consensus import LABELS, load_consensus

ROOT = Path(__file__).resolve().parent.parent
TABLE = ROOT / "data/기술마법-한글이름.tsv"
OUT = ROOT / "plans/서버팩-기술마법-합의.tsv"


def joined(values):
    return " | ".join(values)


def main(write=False):
    accepted, audit = load_consensus()
    lines = [
        "# Hades 아이콘과 서버팩 2개의 한글 이름을 맞댄 감사표.",
        "# 5.99·혼든이 같은 이름이며 세 자료 모두 아이콘당 이름이 하나일 때만 `채택`한다.",
        "# 구조·정렬·영문 이름은 Hades가 기준이고 서버팩은 한글 표시 후보로만 사용한다.",
        "# 칸: 갈래 / 아이콘 / Hades 영문 / 5.99 한글 / 혼든 한글 / 판정",
        "",
    ]
    for row in audit:
        lines.append("\t".join([
            LABELS[row["kind"]], str(row["icon"]), joined(row["english"]),
            joined(row["5.99-server"]), joined(row["honden-community"]), row["reason"],
        ]))
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")

    skills = sum(1 for value in accepted.values() if value["kind"] == "skill")
    spells = sum(1 for value in accepted.values() if value["kind"] == "spell")
    print(f"자동 확정 {len(accepted)}개 · 기술 {skills} · 마법 {spells}")
    print(f"→ {OUT.relative_to(ROOT)}")

    if not write:
        return
    table_lines, filled = TABLE.read_text(encoding="utf-8").splitlines(), 0
    for index, line in enumerate(table_lines):
        if not line or line.startswith(("#", "갈래\t")):
            continue
        columns = line.split("\t")
        if len(columns) < 6 or columns[5].strip():
            continue
        name = columns[2].lstrip("· ").strip()
        if name in accepted:
            columns[5] = accepted[name]["korean"]
            table_lines[index] = "\t".join(columns)
            filled += 1
    TABLE.write_text("\n".join(table_lines) + "\n", encoding="utf-8")
    print(f"이름표 빈칸 {filled}개를 채웠다. 기존 수정값은 보존했다 → {TABLE.relative_to(ROOT)}")


if __name__ == "__main__":
    main("--write" in sys.argv)
