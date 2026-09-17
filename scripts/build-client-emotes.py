#!/usr/bin/env python3
"""게임 클라이언트가 머리 위에 띄우는 감정표현 그림 — `Legend.dat` 의 `emot01.epf` 를 옷장과 같은 칸에 한 줄로 뽑는다.

서버는 감정표현을 몸 동작 번호로 보낸다(0x1A, 9~17 · 23~44). 어느 칸을 얼마나 띄우나는 알맹이
`Lod.Mobile.Core.Art.Emote` 가 원작 표(Legend.exe 2005 0x869880)대로 고른다. 칸 0~10 은 얼굴 표정, 11~41 은
말풍선과 표정, 42~49 는 비어 있어 뽑지 않는다.

**자리:** 칸마다 목차의 left·top 에 그린다. 원작은 몸 조각과 다른 기준(0x4e7be6 가로 +55−28 · 세로 +10)으로 그리는데,
우리 옷장 칸(120x96, `build-client-wardrobe.py`)으로 옮기면 (+2, +2) 다 — 표정 머리(칸 0, 11x15)의 윤곽이 남녀 몸
`mb001`·`wb001` 앞모습(칸 5)의 머리와 그 자리에서 한 점도 어긋나지 않고 겹친다. 그래서 몸 조각과 같은 자리에 두면 된다.

색표는 `legend.pal` 이다. 02~04 는 얼굴 장식(C 30·31·32 — 빨간 안경·파란 안경·물안경)을 쓴 사람의 것이라 뽑지 않는다.

  쓰는 법: python3 scripts/build-client-emotes.py
  산출물:  mobile/client/assets/actor/emote.png
"""
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
LEGEND = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives" / "legend" / "Legend.dat"
OUT = ROOT / "mobile" / "client" / "assets" / "actor" / "emote.png"
DOTNET = ROOT / ".tools" / "dotnet-9.0.317" / "dotnet"
TOOL = ROOT / "tools" / "dat-extract" / "bin" / "Release" / "net8.0" / "dat-extract.dll"

CELL_W, CELL_H = 120, 96
FRAMES = 42
SHIFT = (2, 2)

configure_utf8_stdio(sys.stdout, sys.stderr)


def dumped(scratch, name):
    subprocess.run([str(DOTNET), str(TOOL), "dump", str(LEGEND), scratch, name], check=True, capture_output=True)
    return next(Path(scratch).rglob(name)).read_bytes()


def main():
    if not TOOL.exists():
        print(f"도구가 없습니다. 먼저: {DOTNET} build tools/dat-extract/DatExtract.csproj -c Release")
        return 1

    with tempfile.TemporaryDirectory() as scratch:
        epf = dumped(scratch, "emot01.epf")
        pal = dumped(scratch, "legend.pal")

    count, _, _, _, toc = struct.unpack("<HHHHI", epf[:12])
    sheet = Image.new("RGBA", (CELL_W * FRAMES, CELL_H), (0, 0, 0, 0))
    for frame in range(min(count, FRAMES)):
        record = 12 + toc + frame * 16
        top, left, bottom, right, start, _ = struct.unpack("<HHHHII", epf[record:record + 16])
        width, height = right - left, bottom - top
        for y in range(height):
            for x in range(width):
                index = epf[12 + start + y * width + x]
                if index:
                    colour = tuple(pal[index * 3:index * 3 + 3]) + (255,)
                    sheet.putpixel((frame * CELL_W + left + x + SHIFT[0], top + y + SHIFT[1]), colour)

    sheet.save(OUT)
    print(f"감정표현 {min(count, FRAMES)}칸 → {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
