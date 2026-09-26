#!/usr/bin/env python3
"""대시보드 「기술·마법」 의 무도가 아이콘 고르기 칸 자료.

사용자(2026-09-26): "그림은 아는데 골라줄 방법이 없잖아 — 무도가 기술·마법을 올려두고 아이콘 중에 골라 수정할 수 있게".
원작 자료에 번호가 없는 것(금강불괴·주먹단련·쿠라노토 …)은 사람이 그림을 보고 고른다. 고른 결과는 화면이 글로 내주고,
그 글을 받아 템플릿 `Icon` 을 고친 뒤 `scripts/build-auto-learn.py --쓰기` 로 앱 표를 다시 만든다.

목록은 앱과 같은 자동 습득 표(`mobile/client/assets/world/auto-learn.txt`)의 무도가 줄, 그림 시트는 앱의 것
(`mobile/client/assets/ability/skill.png`·`spell.png`, 한 줄 16칸 · 35×35, 번호 n = n%16 칸 · n//16 줄)을 복사한다.

    python3 scripts/build-ability-icon-picker.py
"""

import json
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
LADDER = ROOT / "mobile" / "client" / "assets" / "world" / "auto-learn.txt"
SHEETS = ROOT / "mobile" / "client" / "assets" / "ability"
OUT_SHEETS = ROOT / "docs" / "ui" / "assets" / "ability-icons"
OUT = ROOT / "docs" / "ability-icon-picker-data.js"
MONK = "5"
SIDE, COLUMNS = 35, 16


def main():
    rows = []
    for line in LADDER.read_text(encoding="utf-8").splitlines():
        if line.startswith("#") or not line.strip():
            continue
        path, level, kind, name, icon = line.split("\t")
        if path == MONK:
            rows.append({"이름": name, "종류": kind, "레벨": int(level), "번호": int(icon)})

    OUT_SHEETS.mkdir(parents=True, exist_ok=True)
    sheets = {}
    for kind in ("skill", "spell"):
        shutil.copyfile(SHEETS / f"{kind}.png", OUT_SHEETS / f"{kind}.png")
        width, height = Image.open(SHEETS / f"{kind}.png").size
        sheets[kind] = {"그림": f"ui/assets/ability-icons/{kind}.png", "폭": width, "높이": height,
                        "칸": (width // SIDE) * (height // SIDE)}

    data = {"직업": "무도가", "한변": SIDE, "한줄": COLUMNS, "시트": sheets, "목록": rows}
    OUT.write_text("window.ABILITY_ICON_PICKER = " + json.dumps(data, ensure_ascii=False) + ";\n", encoding="utf-8")
    print(f"→ {OUT.relative_to(ROOT)}  무도가 {len(rows)}줄 · 기술 {sheets['skill']['칸']}칸 · 마법 {sheets['spell']['칸']}칸")


if __name__ == "__main__":
    main()
