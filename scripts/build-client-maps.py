#!/usr/bin/env python3
"""게임 클라이언트가 맵을 스스로 맞춰 그릴 재료 — 바닥 타일 판 · 세운 그림(건물·나무·가로등) 판 · 배치 글.

원작 클라이언트의 `maps/lod*.map` 은 그림이 아니라 칸마다 번호 셋(바닥 · 왼쪽 그림 · 오른쪽 그림)이다. 클라이언트가 그
번호로 `seo.dat` 의 바닥 타일과 `ia.dat` 의 `stc#####.hpf` 를 꺼내 그린다. 여기서는 서버가 읽는 바로 그 맵 파일과
`sotp.dat` 으로 같은 일을 미리 해 둔다 — 그래서 클라이언트가 막힌 칸이라 여기는 곳이 서버와 같다.
(`build-map-images.py` 는 문서용이다 — 바닥만 한 장으로 그리고 워프를 찍는다.)

  쓰는 법: python3 scripts/build-client-maps.py [맵번호 …]      (번호를 안 주면 아래 MAPS)
  산출물:  mobile/client/assets/world/map<번호>.txt · map<번호>-floor.png · map<번호>-objects.png

아카이브는 5.99 한국 클라이언트의 `seo.dat`·`ia.dat` 이다(맵이 5.99 팩 것이라 타일 번호도 그쪽). 새로 뽑은 그림은
Godot 가 한 번 가져와야(.import) 빌드에 실린다: `godot --headless --path mobile/client --import`.
"""
import json
import subprocess
import sys
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
CLIENT = Path.home() / "Downloads" / "5.99 클라이언트"
OUT = ROOT / "mobile" / "client" / "assets" / "world"
DOTNET = ROOT / ".tools" / "dotnet-9.0.317" / "dotnet"
TOOL = ROOT / "tools" / "dat-extract" / "bin" / "Release" / "net8.0" / "dat-extract.dll"

# 1단계(2026-09-17 사용자: 시작 맵을 노비스마을처럼 제대로): 노비스마을 · 건물 안 여섯 · 마을 밖 평원 둘 · 지금 사냥터.
MAPS = [
    20373,  # 노비스마을
    20374,  # 노비스마을식당
    20375,  # 노비스무기방어구상점
    20376,  # 노비스민가1
    20377,  # 노비스민가2
    20378,  # 노비스잡화상점
    20379,  # 노비스주점
    20393,  # 노비스평원A
    20394,  # 노비스평원B
    20015,  # 우드랜드1-1
]

configure_utf8_stdio(sys.stdout, sys.stderr)


def areas():
    """서버 맵 정의 — 번호 → (이름, 가로, 세로, 맵 파일)."""
    found = {}
    for path in (SERVER / "areas").glob("*.json"):
        area = json.loads(path.read_text(encoding="utf-8-sig"))
        found[area["Id"]] = (area["Name"], area["Cols"], area["Rows"], SERVER / "maps" / Path(area["FilePath"]).name)
    return found


def main():
    if not TOOL.exists():
        print(f"도구가 없습니다. 먼저: {DOTNET} build tools/dat-extract/DatExtract.csproj -c Release")
        return 1

    wanted = [int(word) for word in sys.argv[1:]] or MAPS
    known = areas()
    OUT.mkdir(parents=True, exist_ok=True)
    failed = []

    for number in wanted:
        if number not in known:
            failed.append(f"{number}(맵 정의 없음)")
            continue
        name, columns, rows, map_file = known[number]
        proc = subprocess.run(
            [str(DOTNET), str(TOOL), "layout", str(CLIENT / "seo.dat"), str(CLIENT / "ia.dat"),
             str(SERVER / "static" / "sotp.dat"), str(map_file), str(columns), str(rows), str(OUT / f"map{number}")],
            capture_output=True, text=True, encoding="utf-8", errors="replace")
        if proc.returncode != 0:
            failed.append(f"{number} {name}: {proc.stderr.strip() or proc.stdout.strip()}")
            continue
        summary = next((line for line in proc.stdout.splitlines() if "바닥 타일" in line), "")
        print(f"{number} {name} {columns}x{rows} — {summary.split('—')[-1].split('→')[0].strip()}")

    if failed:
        print(f"못 뽑음 {len(failed)}: " + " / ".join(failed))
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
