#!/usr/bin/env python3
"""마을마다 그 마을 던전 서클의 장비만 판다 — 무기상·방어구상 둘씩(설계 `autopilot/circle-shops/SPEC.md`).

  python3 scripts/gen/items/build-circle-gear-shops.py            # 상인마다 서클·가짓수, 옮기기 전후 합
  python3 scripts/gen/items/build-circle-gear-shops.py --쓰기      # 상인 정의에 적는다

**사용자 결정(2026-10-08)** 「물건 너무 한 상점에서 많이 다루기도 하고 … 던젼이 존재하는 서클에 맞게 아이템도 판매하는게 좋겠어
예를들면 노비스는 1-2서클 아이템 가죽류겠지, 3서클은 아벨, 4서클은 뤼케시온 5서클은 마인에서」. 고른 것: 4서클은
뤼케시온해안대기실(마을은 앱 지도가 없다) · 5서클은 구광산대기실 · 수오미는 2서클(포테의숲) · 우드랜드입구 장신구상은 없앤다 ·
밀레스는 두지 않는다. 상점 창은 갈래 탭으로 나눈다(앱 `TalkPanel`) — 그래서 방어구상 하나가 무기 밖 입는 것을 모두 판다.

**물목은 옮기기만 한다.** 지금 장비 상인(델란·드보이·가이·아돌, 옛 보석상여주인@우드랜드입구)과 이 생성기가 세운 상인이 파는 것을
모두 모아 서클(레벨 1~10 · 11~40 · 41~70 · 71~98 · 99, `docs/item-prices-by-circle.md`)과 갈래(무기 = 자리 1 · 그 밖 = 방어구상)로
나눈다 — 사라지는 물건 0, 값은 그대로. 그 물목은 `build-novice-gear-shops.py`(델란·드보이) · `build-town-gear-shops.py`(가이·아돌·
보석상여주인)가 만든다. **그 둘을 다시 돌리면 이것을 다시 돌려라** — 그 둘은 상인 물목을 통째로 다시 쓰고 보석상여주인을 되살린다.
이것을 다시 돌려도 같다(모으는 곳에 이것이 세운 상인도 든다).

**새 상인 여섯** — 혼든 팩 그 마을 대장간 사람(`data/server-packs/honden-community/db/npc/마이소시아/*_spawn.txt` 의 「…대장간,x,y,
방향,이름,상점」), 이름·그림(16384 + 이미지)·인사말은 `*_npc.txt`. 자리:
  - 아벨무기점·방어구점은 수오미무기점·방어구점과 .map 이 같다(cmp) → 가이(7,7)·아돌(4,5) 자리.
  - 뤼케시온해안대기실 도착 칸(28,25)·구광산대기실 도착 칸(18,47)(월드맵 `templates/worldmaps/temuair.json`) 둘레가 트인 바닥이라
    도착 칸에서 대각선 두 칸 위에 둘 — 2026-10-08 `build-town-gear-shops.py` 의 벽 규칙(`walls`·`walkable_from`)으로 벽이 아니고
    걸어 닿는 칸임을 쟀다.

시험: `TownGearShopTests`·`NoviceGearShopTests`(서클 물목·새 상인).
"""

import argparse
import json
import re
import sys

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT
from lib._io import read_lenient_json as read

SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
MUNDANES = SERVER / "templates/mundanes"
HONDEN = ROOT / "data/server-packs/honden-community/db/npc/마이소시아"

#: 서클 윗레벨 — 1~10 · 11~40 · 41~70 · 71~98 · 99.
CIRCLE_TOPS = [10, 40, 70, 98]

#: (이름, 맵 번호, x, y, 갈래, 서클들, 혼든 npc 파일 — 새로 세울 때만).
SHOPS = [
    ("델란", 20375, 3, 7, "무기", {1, 2}, None),
    ("드보이", 20375, 6, 2, "방어구", {1, 2}, None),
    ("가이", 20356, 7, 7, "무기", {2}, None),
    ("아돌", 20357, 4, 5, "방어구", {2}, None),
    ("피어스", 20031, 7, 7, "무기", {3}, "아벨마을"),
    ("해리슨", 20032, 4, 5, "방어구", {3}, "아벨마을"),
    ("제이", 20466, 26, 23, "무기", {4}, "루어스마을"),
    ("프리드", 20466, 30, 23, "방어구", {4}, "루어스마을"),
    ("마이어", 20832, 16, 45, "무기", {5}, "마인마을"),
    ("마시", 20832, 20, 45, "방어구", {5}, "마인마을"),
]

#: 없애는 상인 — 물목은 모으는 곳에 넣고 파일은 지운다(사용자 「나머지 비움」).
GONE = ["보석상여주인@우드랜드입구#10,15"]

#: 새 상인의 칸 꼴은 가이를 본뜬다(움직이지 않는 shop1).
LIKE = "가이@수오미무기점#7,7"


def circle(level):
    return next((at + 1 for at, top in enumerate(CIRCLE_TOPS) if level <= top), len(CIRCLE_TOPS) + 1)


def honden(file, name):
    """혼든 `<file>_npc.txt` 의 { 이름 · 이미지 · 말하기 } 묶음 하나."""
    text = (HONDEN / f"{file}_npc.txt").read_text(encoding="utf-8")
    for block in text.split("}"):
        if re.search(rf"^이름\t{re.escape(name)}$", block, re.M):
            image = int(re.search(r"^이미지\t(\d+)", block, re.M).group(1))
            speech = re.findall(r"^말하기\t\d+\t(.+)$", block, re.M)
            return image, speech
    raise SystemExit(f"혼든 {file}_npc.txt 에 {name} 이 없다")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    areas = {}
    for path in (SERVER / "areas").glob("*.json"):
        area = read(path)
        areas[area["ID"]] = area["Name"]
    items = {}
    for path in (SERVER / "templates/items").rglob("*.json"):
        item = read(path)
        if item.get("Name"):
            items[item["Name"]] = item

    def file_of(name, area, x, y):
        return MUNDANES / f"{name}@{areas[area]}#{x},{y}.json"

    # 모으기 — 상인 자리마다 지금 파는 것.
    catalog = []
    for path in [file_of(n, a, x, y) for n, a, x, y, *_ in SHOPS] + [MUNDANES / f"{gone}.json" for gone in GONE]:
        if path.exists():
            catalog += read(path).get("DefaultMerchantStock") or []
    catalog = list(dict.fromkeys(name for name in catalog if name in items))

    stock = {name: [] for name, *_ in SHOPS}
    for item_name in catalog:
        item = items[item_name]
        kind = "무기" if (item.get("EquipmentSlot") or 0) == 1 else "방어구"
        ring = circle(int(item.get("LevelRequired") or 1))
        for name, _, _, _, shop_kind, circles, _ in SHOPS:
            if shop_kind == kind and ring in circles:
                stock[name].append(item_name)

    homeless = [n for n in catalog if not any(n in s for s in stock.values())]
    if homeless:
        raise SystemExit(f"둘 곳이 없는 물건 {homeless}")

    template = read(MUNDANES / f"{LIKE}.json")
    for name, area, x, y, kind, circles, source in SHOPS:
        goods = sorted(stock[name], key=lambda n: (int(items[n].get("LevelRequired") or 1), int(items[n].get("Value") or 0), n))
        path = file_of(name, area, x, y)
        if path.exists():
            body = read(path)
        elif source:
            image, speech = honden(source, name)
            body = {**template, "Name": f"{name}@{areas[area]}#{x},{y}", "AreaID": area, "X": x, "Y": y, "Direction": 2,
                    "Image": 16384 + image, "Speech": speech}
        else:
            raise SystemExit(f"{path.name} 가 없다 — build-novice-gear-shops.py · build-town-gear-shops.py --쓰기 먼저")
        before = len(body.get("DefaultMerchantStock") or []) if path.exists() else 0
        body["DefaultMerchantStock"] = goods
        levels = [int(items[n].get("LevelRequired") or 1) for n in goods]
        span = f"레벨 {min(levels)}~{max(levels)}" if levels else "없음"
        print(f"{name}@{areas[area]}({x},{y}) {kind} {sorted(circles)}서클: {before} → {len(goods)}종 · {span}"
              + ("" if path.exists() else " · 새로"))
        if writing:
            path.write_text(json.dumps(body, ensure_ascii=False, indent=2), encoding="utf-8")

    for gone in GONE:
        path = MUNDANES / f"{gone}.json"
        if path.exists():
            print(f"{gone} 지움")
            if writing:
                path.unlink()

    print(f"모은 물건 {len(catalog)}종 — 모두 어딘가에서 판다.")
    if not writing:
        print("미리 본 것입니다 — 적으려면 --쓰기")
    return 0


if __name__ == "__main__":
    sys.exit(main())
