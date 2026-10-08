#!/usr/bin/env python3
"""중급 보석(루비·사파이어·에메랄드·진주)이 떨어지는 99레벨 사냥터 목록 — 서버 `Gems` 가 읽는다.

  python3 scripts/gen/items/build-gem-grounds.py            # 무엇이 바뀌는지만 본다
  python3 scripts/gen/items/build-gem-grounds.py --쓰기      # static/gem-grounds.tsv 에 적는다

**사용자 결정(2026-10-08)** 「사냥터는 중급보석은 현재 99레벨 던전에서만 나오도록 하고 고가는 추후에 승급던전에서」.
설계 `autopilot/gems/SPEC.md`.

**99레벨 사냥터** = 둘을 합친 것:
  1. 생태계 봇 사냥터 표(`mobile/client/assets/world/eco-grounds.txt`, `scripts/gen/eco/build-eco-grounds.py`)가 적정 레벨 99 로
     적은 맵 — 구광산·뤼케시온해안·죽음의마을·아벨해안4·서·북의우드랜드 깊은 구역.
  2. 99레벨 장비를 붙인 지역(`build-drop-variety.py` FRESH_REGIONS 와 같은 이름 꼴) — 봇 표에 없는 드라큐라백작의성·지하수로D·신죽·카스마늄.

**왜 괴물 드랍 목록에 넣지 않나.** 목록에 한 칸을 더하면 그 괴물의 다른 물건이 모두 옅어지고(확률 = DropRate ÷ 칸수),
뤼케시온해안·죽음의마을은 괴물에 자기 확률(`DropRate`)을 적은 5.99 증거 목록이라 칸을 늘리면 그 확률이 반으로 준다
(`build-potion-by-level.py` 머리 주석). 그래서 서버가 목록과 따로 굴린다(`Gems.FromGround`).

산출물: `database/server/static/gem-grounds.tsv` — 줄마다 「맵번호<탭>이름」(이름은 사람이 읽으려고).
시험: `GemTests`(목록에 구광산·뤼케시온해안이 있고 우드랜드14-1 은 없다).
"""

import argparse
import re
import sys

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT
from lib._io import read_lenient_json as read

SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ECO_GROUNDS = ROOT / "mobile/client/assets/world/eco-grounds.txt"
OUT = SERVER / "static/gem-grounds.tsv"

# `build-drop-variety.py` FRESH_REGIONS 의 이름 꼴(99레벨 장비를 붙인 지역).
FRESH_99 = r"구광산\d+-\d+|드라큐라백작의성.+|지하수로D-\d+|신죽(마집안|음의마을)[\d-]+|카스마늄제\d-\d갱도"


def grounds():
    names = {}
    for path in (SERVER / "areas").glob("*.json"):
        area = read(path)
        names[area.get("ID") or area.get("Id")] = area.get("Name") or ""

    picked = {}
    for line in ECO_GROUNDS.read_text(encoding="utf-8").splitlines():
        if line.startswith("#") or not line.strip():
            continue
        map_id, level, name = line.split(maxsplit=2)
        if int(level) == 99:
            picked[int(map_id)] = name
    for map_id, name in names.items():
        if re.fullmatch(FRESH_99, name):
            picked[map_id] = name
    return dict(sorted(picked.items()))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    picked = grounds()
    text = "# scripts/gen/items/build-gem-grounds.py 가 만든다. 손으로 고치지 말 것.\n" + "".join(
        f"{map_id}\t{name}\n" for map_id, name in picked.items())
    old = OUT.read_text(encoding="utf-8") if OUT.exists() else ""

    regions = {}
    for name in picked.values():
        region = re.sub(r"[\d\-A-Z]+$", "", name)
        regions[region] = regions.get(region, 0) + 1
    print(f"99레벨 사냥터 {len(picked)}곳: " + " · ".join(f"{region} {count}" for region, count in regions.items()))
    print("바뀌지 않음" if old == text else ("적었습니다" if writing else "바뀝니다 — 적으려면 --쓰기"))
    if writing and old != text:
        OUT.write_text(text, encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
