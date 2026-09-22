#!/usr/bin/env python3
"""`docs/feature-map.md` 의 기능 표를 화면에서 거를 수 있는 모양으로 옮긴다.

산문 표는 읽기에는 좋지만 "지금 되는 것만 보여 줘"를 못 한다. 표는 그대로 두고(그쪽이 원본이다)
판정 칸만 뽑아 온다. 판정은 표의 「읽는 법」 절에 적힌 말 그대로다 —
서버는 돌아감·부분·틀만·없음, 모바일은 됨·일부·없음.

  쓰는 법: python3 scripts/build-feature-map-data.py   → docs/feature-map-data.js
"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "docs" / "feature-map.md"
OUT = ROOT / "docs" / "feature-map-data.js"

# 판정 말. 칸의 맨 앞에서 찾는다 — 뒤에 근거가 붙어 있어서 앞머리만 본다.
SERVER_MARKS = ["돌아감", "부분", "틀만", "없음"]
MOBILE_MARKS = ["됨", "일부", "없음"]


def verdict(cell, marks):
    """칸의 앞머리에서 판정을 읽는다. 굵게(**없음**) 쓴 것도 같이 본다."""
    plain = cell.replace("*", "").strip()
    for mark in marks:
        if plain.startswith(mark):
            return mark, plain[len(mark):].lstrip(" —-·").strip()
    if plain in ("", "—", "-"):
        return "해당없음", ""
    return "모름", plain


def main():
    groups = []
    current = None

    for line in SRC.read_text(encoding="utf-8").splitlines():
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]

        # 묶음 머리줄: | **접속** ||||| — 첫 칸만 차 있다. 뒤의 빈 칸은 strip 에 함께 깎여
        # 칸이 하나만 남을 수도 있으므로 개수로 거르지 않는다.
        if cells and cells[0].startswith("**") and not any(cells[1:]):
            current = {"이름": cells[0].strip("*"), "기능": []}
            groups.append(current)
            continue

        if len(cells) < 6 or not cells[0].isdigit() or current is None:
            continue

        number, name, server, mobile, source, note = cells[:6]
        server_mark, server_text = verdict(server, SERVER_MARKS)
        mobile_mark, mobile_text = verdict(mobile, MOBILE_MARKS)
        current["기능"].append({
            "번호": int(number),
            "이름": name,
            "서버": server_mark,
            "서버설명": server_text,
            "모바일": mobile_mark,
            "모바일설명": mobile_text,
            "근거": source,
            "메모": note,
        })

    groups = [g for g in groups if g["기능"]]
    rows = [f for g in groups for f in g["기능"]]

    def tally(key, marks):
        counted = {m: sum(1 for r in rows if r[key] == m) for m in marks}
        counted["그밖"] = len(rows) - sum(counted.values())
        return counted

    # 조사일은 표 머리글에 적혀 있다 — 손으로 옮기지 않고 읽어 온다.
    head = SRC.read_text(encoding="utf-8")[:2000]
    surveyed = re.search(r"조사:\s*(\d{4}-\d{2}-\d{2})", head)

    payload = {
        "생성": "scripts/build-feature-map-data.py",
        "원본": "docs/feature-map.md",
        "조사일": surveyed.group(1) if surveyed else "",
        "묶음": groups,
        "셈": {
            "전체": len(rows),
            "서버": tally("서버", SERVER_MARKS),
            "모바일": tally("모바일", MOBILE_MARKS),
            # 사람이 지금 만져 볼 수 있는 것 = 양쪽 다 살아 있는 것.
            "양쪽됨": sum(1 for r in rows if r["서버"] == "돌아감" and r["모바일"] == "됨"),
            "서버만": sum(1 for r in rows if r["서버"] in ("돌아감", "부분") and r["모바일"] == "없음"),
            "양쪽없음": sum(1 for r in rows if r["서버"] in ("틀만", "없음") and r["모바일"] == "없음"),
        },
    }

    OUT.write_text(
        "window.LOD_FEATURES = " + json.dumps(payload, ensure_ascii=False) + ";\n",
        encoding="utf-8")
    c = payload["셈"]
    print(f"기능 {c['전체']} · 양쪽 됨 {c['양쪽됨']} · 서버만 {c['서버만']} · 양쪽 없음 {c['양쪽없음']}")
    print(f"서버 {c['서버']}")
    print(f"모바일 {c['모바일']}")
    print(f"→ {OUT.relative_to(ROOT)}  ({OUT.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
