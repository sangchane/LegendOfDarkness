#!/usr/bin/env python3
"""사람이 모아 둔 원작 아이템 표(`docs/items/*.xlsx`)를 읽을 수 있는 한 덩어리로 만든다.

엑셀 다섯 장이 서로 다른 것을 담고 있다. `#1` 과 `#2` 는 **같은 파일**이라(같은 md5) 하나만 읽는다.

  #1  5,722행 x 27열   수치표. 머리글이 없어 칸 이름을 자료로 맞췄다 (아래 COLUMNS).
  #3    686행 x  4열   이름 · 번호 · 설명
  #4  3,599행 x  5열   이름 · 분류 · 설명
  #5  2,197행 x  3열   이름 · 분류 · 설명

`#1` 의 칸 이름은 짐작이 아니다. 서버팩 셋과 양쪽에 다 있는 3,026개를 맞대어, 0 이 아닌 값이
같은 비율로 정했다 (0 은 어느 칸에나 맞아 가짜 신호가 된다). 일치율을 그대로 적어 둔다 —
낮은 것은 낮은 대로 믿으라는 뜻이다. 여섯 칸은 끝내 못 가려 번호로 남긴다.

  쓰는 법: python3 scripts/build-original-item-sheets.py
  산출물:  data/game-data/items-original-sheets.json
"""
import json
import sys
from pathlib import Path

import openpyxl

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
SHEETS = ROOT / "docs" / "items"
OUT = ROOT / "data" / "game-data" / "items-original-sheets.json"

configure_utf8_stdio(sys.stdout, sys.stderr)

# (열 번호 1부터, 칸 이름, 서버팩과의 일치율%). 못 가린 칸은 이름을 비워 둔다.
COLUMNS = [(1, "이름", None), (2, "이름2", None), (3, "무게", 81), (4, "", None), (5, "수리여부", 98),
           (6, "", None), (7, "", None), (8, "", None), (9, "내구력", 84), (10, "방어력", 84),
           (11, "명중수정", 78), (12, "공격수정", 77), (13, "체력변화", 73), (14, "마력변화", 69),
           (15, "힘변화", 70), (16, "덱스변화", 62), (17, "인트변화", 72), (18, "위즈변화", 72),
           (19, "콘변화", 72), (20, "직업제한", 99), (21, "수리가격", 99), (22, "레벨제한", 88),
           (23, "", None), (24, "", None), (25, "", None), (26, "", None), (27, "", None)]


def read_rows(path):
    book = openpyxl.load_workbook(path, read_only=True, data_only=True)
    rows = [r for r in book.worksheets[0].iter_rows(values_only=True) if r and r[0]]
    book.close()
    return rows


def cell(value):
    return str(value).strip() if value is not None else ""


def main():
    stats_file = SHEETS / "어둠템#1.xlsx"
    numbers = []
    if stats_file.exists():
        for row in read_rows(stats_file):
            entry = {}
            for index, name, _ in COLUMNS:
                value = cell(row[index - 1]) if index - 1 < len(row) else ""
                if value:
                    entry[name or f"열{index}"] = value
            if entry.get("이름"):
                numbers.append(entry)

    described = []
    for name, cols in (("어둠템#3.xlsx", ("이름", "번호", "설명")),
                       ("어둠템#4.xlsx", ("이름", "분류", "설명")),
                       ("어둠템#5.xlsx", ("이름", "분류", "설명"))):
        path = SHEETS / name
        if not path.exists():
            continue
        for row in read_rows(path):
            entry = {key: cell(row[i]) for i, key in enumerate(cols) if i < len(row) and cell(row[i])}
            if entry.get("이름"):
                entry["출처"] = name
                described.append(entry)

    payload = {
        "생성": "scripts/build-original-item-sheets.py",
        "출처": "docs/items/*.xlsx — 사람이 원작에서 모아 둔 표",
        "칸이름_근거": "서버팩 셋과 겹치는 3,026개를 맞대어 0 이 아닌 값의 일치율로 정했다",
        "칸": [{"열": i, "이름": n or None, "일치율": r} for i, n, r in COLUMNS],
        "수치표": numbers,
        "설명표": described,
    }
    OUT.write_text(json.dumps(payload, ensure_ascii=False, indent=1), encoding="utf-8")

    named = len({e["이름"] for e in described})
    print(f"수치표 {len(numbers)}행 · 설명표 {len(described)}행(이름 {named}종) → {OUT.relative_to(ROOT)}")
    with_desc = sum(1 for e in described if e.get("설명"))
    print(f"  설명이 있는 것 {with_desc}행 · 분류가 있는 것 {sum(1 for e in described if e.get('분류'))}행")


if __name__ == "__main__":
    main()
