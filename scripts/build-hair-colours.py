#!/usr/bin/env python3
"""머리색 72가지의 실제 RGB 값 — 원작 `color0.tbl` 을 그대로 자료로 옮긴다.

`data/character-creation/hairstyles.json`(build-hairstyle-inventory.py) 은 색이 72가지(0~71)이고
`color0.tbl` 구조가 무엇인지는 적어 뒀지만, 색 값 자체는 담지 않았다. 만들기 화면이 실제로 색을 입히려면
번호마다 6색이 필요하다 — 원작 염색 방식이 팔레트의 98번부터 6칸을 이 6색으로 덮는 것이기 때문이다
(`sources/wren11/da-lib/DALib/Drawing/Palette.cs:65-73` · `Definitions/CONSTANTS.cs` 의
`PALETTE_DYE_INDEX_START = 98`). 클라이언트가 실행 중에 쓰는 같은 표는 이미
`mobile/client/assets/actor/parts/dye-colours.txt` 에 복사돼 있다(`scripts/build-client-assets.ps1:107`) —
이 스크립트는 같은 원본을 사람이 읽을 자료(JSON)로 한 번 더 옮길 뿐, 클라이언트 자산을 새로 만들지 않는다.

표 형식(둘 다 같다): 머리말 1줄("6" = 색조 수) + 72묶음 × (번호줄 + RGB 6줄).

  쓰는 법: python3 scripts/build-hair-colours.py   → data/character-creation/hair-colours.json
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "data" / "legend-tables" / "color0.tbl"
OUT = ROOT / "data" / "character-creation" / "hair-colours.json"


def parse(text):
    """번호줄이 열고 그 아래 RGB 줄들이 속한다 — 콤마 없는 줄은 새 번호, 있는 줄은 색(DALib DyeTable 과 같은 규칙)."""
    table, colours, entry = {}, [], None

    def close():
        if entry is not None and colours:
            table[entry] = list(colours)
        colours.clear()

    for raw in text.splitlines():
        line = raw.strip()
        if not line:
            continue
        parts = line.split(",")
        if len(parts) == 3 and all(p.strip().isdigit() for p in parts):
            colours.append([int(p) for p in parts])
            continue
        close()
        entry = int(line) if line.isdigit() else None

    close()
    return table


def main():
    if not SOURCE.exists():
        print(f"원본이 없습니다: {SOURCE}")
        return 1

    table = parse(SOURCE.read_text(encoding="utf-8"))
    wanted = {n: table[n] for n in range(72) if n in table}

    if len(wanted) != 72:
        print(f"72가지가 아니라 {len(wanted)}가지가 읽혔습니다 — {SOURCE} 를 확인하세요.")
        return 1

    result = {
        "표": str(SOURCE.relative_to(ROOT)),
        "가짓수": 72,
        "범위": "0~71",
        "염색 방식": ("팔레트의 98번부터 6칸을 그 번호의 6색으로 덮는다 "
                    "(sources/wren11/da-lib/DALib/Drawing/Palette.cs:65-73, dyeIndexStart 기본값 98)"),
        "색": {str(n): wanted[n] for n in sorted(wanted)},
    }

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"색 {len(wanted)}가지 → {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
