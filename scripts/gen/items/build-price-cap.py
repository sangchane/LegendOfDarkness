#!/usr/bin/env python3
"""5.99 팩에서 온 입는 물건의 값(`Value`)을 그 서클 사냥 10시간 금화로 누른다 — 아이템 생성기들 **맨 뒤에** 돌린다.

  python3 scripts/gen/items/build-price-cap.py            # 무엇이 바뀌는지만 본다
  python3 scripts/gen/items/build-price-cap.py --쓰기      # 아이템 정의의 Value 를 적는다

**사용자 2026-10-08** — 「아이템 가격이 대체로 너무 높은거 같거든? 서클별로 정렬해서 … 밸런스를 좀 맞춰야」,
「1레벨 아이템에는 이벤트 아이템 같은게 많아서 … 착용 장비류부터 따로 계산하고 직업코드 있는 갑옷도 따로」.

서클 = 레벨 구간(1·11·41·71·99). `Value` 는 상점이 파는 값이고, 되사는 값은 `Value ÷ 1.6`(`ShopPricing.Offer`) 이다.
금화는 경험치 × 0.1 로만 생긴다(`Formulas/monsterexp.cs`, 노비스 × 0.02). 클라우드 봇 실측(10-07~08, 사냥 중 주운
금화)은 서클 2 시간당 4.7만 · 3 49만 · 4 67만 · 5 45만 — 원작·하데스 장비(직업 방어구·원작표)의 가운데 값은 이것의
0.1~0.7시간이다. 5.99 팩 장비는 사설 서버 경제의 값(목걸이 2,500만 · 세일라링 7,000만 · 세트 갑옷 8억)이라 수백~
수천 시간이 되고, 봇은 떨군 산타모자(5억)를 되팔아 서클 3에서 시간당 3,900만을 번다(줍는 금화의 80배).

**상한 = 그 서클 사냥 10시간** — 서클 1은 봇이 노비스에서 거의 안 잡아(시간당 42마리) 실측 대신 시간당 약 1만
(322마리 × 약 30전)으로 잡았다. 99 서클은 71~98 보다 낮게 쟀지만 상한이 내려가지 않게 같은 값을 둔다.

누르는 것: 이름표가 `5.99표` 인 입는 물건(`EquipmentSlot` > 0) 중 상한을 넘는 것. 단 치장·이벤트(치장아이템·Dress·
공통장식·길드용품 묶음, 장식 칸 공용, 능력치 없는 것)는 **괴물이 떨구는 것만** 누른다 — 블랙팜상인 등 상점에서만
파는 치장은 금화를 빨아들이는 곳이라 그대로 둔다. 원작·하데스 값(`하데스표`·`원작표`)은 손대지 않는다(자료 출처 우선순위).
이 생성기는 값을 **내리기만** 한다. 5.99 장비 생성기(`build-pack-equipment.py`)를 다시 돌리면 그 뒤에 이것을 다시 돌려라.
"""

import argparse
import json
import re
import sys

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT
from lib._drops import drops_of
from lib._io import read_lenient_json as read
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MONSTERS = SERVER / "templates/monsters"

CAP = {1: 100_000, 2: 500_000, 3: 5_000_000, 4: 7_000_000, 5: 7_000_000}
COSMETIC = {"치장아이템", "Dress", "공통장식", "길드용품"}
STATS = ["AcModifer", "HealthModifer", "ManaModifer", "DexModifer", "StrModifer", "ConModifer", "IntModifer",
         "WisModifer", "MrModifer", "HitModifer", "DmgModifer"]
VALUE = re.compile(r'("Value"\s*:\s*)(\d+)')


def circle(level):
    return 1 if level < 11 else 2 if level < 41 else 3 if level < 71 else 4 if level < 99 else 5


def cosmetic(item):
    part = item["Group"].split("/")
    kind = part[2] if len(part) > 2 else part[1] if len(part) > 1 else ""
    stat = any(item.get(k) for k in STATS) or item.get("DmgMax", 0) > 0
    return kind in COSMETIC or item["EquipmentSlot"] == 14 and not item.get("Class") or not stat


def plan(items, dropped):
    """{이름: (지금 값, 새 값, 서클, 치장인가)}"""
    out = {}
    for name, (path, item) in items.items():
        if not item.get("EquipmentSlot") or not item.get("Group", "").startswith("5.99표"):
            continue
        c = circle(item.get("LevelRequired", 1))
        dress = cosmetic(item)
        if item["Value"] > CAP[c] and (not dress or name in dropped):
            out[name] = (item["Value"], CAP[c], c, dress)
    return out


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    items = {}
    for path in ITEMS.rglob("*.json"):
        item = read(path)
        if item.get("Name"):
            items[item["Name"]] = (path, item)
    dropped = set()
    for path in MONSTERS.rglob("*.json"):
        try:
            dropped.update(drops_of(read(path), items_only=True))
        except json.JSONDecodeError:
            continue

    new = plan(items, dropped)
    for c in sorted(CAP):
        rows = sorted(((v, name, d) for name, (v, _, cc, d) in new.items() if cc == c), reverse=True)
        print(f"서클 {c} — 상한 {CAP[c]:,} · {len(rows)}개")
        for v, name, d in rows:
            print(f"  {name}{' (치장·떨굼)' if d else ''}: {v:,} → {CAP[c]:,}")

    if writing:
        for name, (_, value, _, _) in new.items():
            path = items[name][0]
            text = path.read_text(encoding="utf-8-sig")
            text, count = VALUE.subn(lambda m: f"{m.group(1)}{value}", text, count=1)
            if count != 1:
                raise SystemExit(f"{path}: Value 줄을 못 찾았다")
            path.write_text(text, encoding="utf-8")
    print(f"\n아이템 {len(new)}장이 바뀐다.")
    print("적었습니다." if writing else "미리 본 것입니다 — 적으려면 --쓰기")


if __name__ == "__main__":
    sys.exit(main())
