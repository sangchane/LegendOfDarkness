#!/usr/bin/env python3
"""아이템 아이콘을 한 장짜리 띠로 뽑는다 — 도감 카드에 붙일 것.

원작은 아이콘을 `item###.epf` 에 266칸씩 나눠 싣고, 서버가 보내는 `DisplayImage` 가 그 칸을
가리킨다(0x8000 이 얹혀 있다). `tools/dat-extract` 의 `icon` 이 그 계산과 색표(`itempal.tbl`)를
이미 안다 — 여기서는 번호를 모아 한 번 부르고, 몇 번째 칸에 무엇이 들어갔는지만 적어 둔다.

한 줄짜리 띠로 만든다. 칸이 균일해서 `background-position` 한 칸씩 밀면 되고, 파일이 하나라
`file://` 로 열어도 요청이 한 번이다.

  쓰는 법: python3 scripts/build-item-icons.py
  산출물:  docs/ui/assets/item-icons.png · docs/ui/assets/item-icons.json
"""
import json
import re
import subprocess
import struct
import sys
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
HADES = ROOT / "data" / "game-data" / "items-hades.json"
ARCHIVE = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives" / "legend" / "Legend.dat"
PNG = ROOT / "docs" / "ui" / "assets" / "item-icons.png"
INDEX = ROOT / "docs" / "ui" / "assets" / "item-icons.json"

configure_utf8_stdio(sys.stdout, sys.stderr)


def png_size(path):
    head = path.read_bytes()[:24]
    return struct.unpack(">II", head[16:24])


def main():
    if not ARCHIVE.exists():
        print(f"원작 아카이브가 없습니다: {ARCHIVE.relative_to(ROOT)}")
        print("포크 submodule 을 받아야 합니다 — 아이콘 없이도 도감은 뜹니다.")
        return 1

    items = json.loads(HADES.read_text(encoding="utf-8-sig"))
    wanted = sorted({it["DisplayImage"] for it in items if it.get("DisplayImage")})

    PNG.parent.mkdir(parents=True, exist_ok=True)
    proc = subprocess.run(
        ["dotnet", "run", "--project", str(ROOT / "tools" / "dat-extract"), "-c", "Release", "--",
         "icon", str(ARCHIVE), ",".join(str(n) for n in wanted), str(PNG), "1"],
        capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)
    if proc.returncode != 0:
        print(proc.stderr[-800:])
        return proc.returncode

    # 그려진 것만, 그려진 차례대로. 못 찾은 번호는 건너뛰므로 넣은 차례와 다르다.
    # 아카이브에 파일 이름이 `item006.epf` 와 `Item006.epf` 로 섞여 있다. 대소문자를 가리면
    # 다섯 줄을 놓쳐 칸 너비 계산이 어긋난다 (띠는 350칸인데 345로 나눈다).
    drawn = [int(m) for m in re.findall(r"^\s*(\d+) -> item\d+\.epf", proc.stdout, re.M | re.I)]
    missing = sorted(set(wanted) - set(drawn))

    width, height = png_size(PNG)
    if width % len(drawn):
        print(f"띠 너비 {width} 가 칸 수 {len(drawn)} 로 나뉘지 않습니다 — 로그를 놓친 것입니다.")
        return 1
    cell = width // len(drawn)
    INDEX.write_text(json.dumps({
        "생성": "scripts/build-item-icons.py",
        "칸너비": cell,
        "칸높이": height,
        "자리": {str(n): i for i, n in enumerate(drawn)},
    }, ensure_ascii=False), encoding="utf-8")

    print(f"아이콘 {len(drawn)}개 · 칸 {cell}x{height} · 띠 {width}x{height} → {PNG.relative_to(ROOT)}")
    if missing:
        print(f"  아카이브에 없는 번호 {len(missing)}개: {', '.join(str(n) for n in missing)}")
        print("  그 아이템은 카드에 아이콘 없이 나옵니다.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
