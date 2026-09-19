#!/usr/bin/env python3
"""캐릭터 만들기 미리보기가 얹을 머리 그림 — 맨몸 위에 놓을 머리 모양 번호마다 한 장씩 뽑는다.

`build-client-wardrobe.py` 는 아이템(갑옷·투구 등)이 입히는 것만 뽑고 맨머리(1~100, 아이템이 아니라
캐릭터 만들기 자체의 값)는 일부러 건너뛴다(`worn_by_items()`: 투구는 100 초과만). 그 8개
(`mh001~mh008`·`wh001~wh008`)는 `build-client-assets.ps1` 이 자리만 잡아 둔 것이라, 진짜 목록
(남 59가지·여 56가지, `data/character-creation/hairstyles.json` — build-hairstyle-inventory.py 로 아카이브를
전수로 세어 확정)에는 한참 모자란다. 이 스크립트가 나머지를 채운다.

**칸은 10칸 그대로 뽑는다(한 방향만 쓰지만 1칸으로는 안 된다):** 원작 파일 하나(`…h###01.epf`)는 서기·걷기
프레임 10개를 한 장에 담고, 정면 서기는 그중 5번 칸이다(`Lod.Mobile.Core.Art.WalkMotion.Stand(Side.Front) == 5`).
1칸만 뽑으면 그 자리를 읽으려 할 때 칸 밖을 읽어 미리보기가 비어 보인다 — 그래서 10칸을 그대로 두고
클라이언트가 5번 칸만 보여주게 한다(정지 미리보기라 나머지 칸은 그려지지 않는다). "미리보기에 한 프레임이면
된다"는 것은 02(평타)·03(손짓)·b~f(직업 동작) 같은 다른 파일들을 뽑지 않는다는 뜻이다 — 그건 안 뽑는다.

**이미 있는 것은 다시 뽑지 않는다** — `mh001~008`·`wh001~008` 은 그대로 둔다.

  쓰는 법: python3 scripts/build-client-hair.py
  산출물: mobile/client/assets/actor/parts/<성별>h<번호(3자리)>.png
"""
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PARTS = ROOT / "mobile" / "client" / "assets" / "actor" / "parts"
HADES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives"
HAIRSTYLES = ROOT / "data" / "character-creation" / "hairstyles.json"
DOTNET = ROOT / ".tools" / "dotnet-9.0.317" / "dotnet"
TOOL = ROOT / "tools" / "dat-extract" / "bin" / "Release" / "net8.0" / "dat-extract.dll"

#: 몸·바지와 같은 칸 — build-client-assets.ps1 의 $cell, build-client-wardrobe.py 의 CELL.
CELL = "120x96"
#: 서기·걷기 10칸 전부(WalkMotion 이 그중 하나를 골라 보여준다). zoom 은 1(원본 크기).
FRAMES = ",".join(str(n) for n in range(10))
ZOOM = "1"

ARCHIVES = {"m": HADES / "khan" / "khan.dat", "w": HADES / "khan2" / "khan2.dat"}


def cut(archive, entry, out):
    """실패하면 마지막 줄(이유)을 돌려주고, 성공하면 None."""
    proc = subprocess.run(
        [str(DOTNET), str(TOOL), "pose", str(archive), entry, str(out), FRAMES, ZOOM, CELL, "marker"],
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    if proc.returncode == 0:
        return None
    said = (proc.stderr.strip() or proc.stdout.strip()).splitlines()
    return said[-1] if said else "알 수 없는 실패"


def main():
    if not TOOL.exists():
        print(f"도구가 없습니다. 먼저: {DOTNET} build tools/dat-extract/DatExtract.csproj -c Release")
        return 1

    if not HAIRSTYLES.exists():
        print(f"머리 번호 목록이 없습니다: {HAIRSTYLES} — 먼저 scripts/build-hairstyle-inventory.py")
        return 1

    counted = json.loads(HAIRSTYLES.read_text(encoding="utf-8"))
    by_gender = {"m": counted["male_head_numbers"], "w": counted["female_head_numbers"]}

    made, skipped, already = 0, [], 0
    for gender, numbers in by_gender.items():
        archive = ARCHIVES[gender]
        for number in numbers:
            name = f"{gender}h{number:03d}"
            out = PARTS / f"{name}.png"

            if out.exists():
                already += 1
                continue

            if (why := cut(archive, f"{name}01", out)) is not None:
                skipped.append(f"{name}({why})")
                continue

            made += 1
            print(f"  {name}")

    print(f"새로 뽑은 그림 {made}장 (이미 있던 것 {already}장) → {PARTS.relative_to(ROOT)}")
    if skipped:
        print(f"  못 뽑은 것 {len(skipped)}개: {', '.join(skipped)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
