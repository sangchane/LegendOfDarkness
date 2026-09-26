#!/usr/bin/env python3
"""한 괴물이 무언가를 떨굴 확률(목록 드랍의 합)을 80% 로 누른다 — 드랍 생성기들 **맨 뒤에** 돌린다.

  python3 scripts/build-drop-cap.py            # 무엇이 바뀌는지만 본다 (넘는 괴물 · 바뀌는 DropRate · 영향)
  python3 scripts/build-drop-cap.py --쓰기      # 아이템 정의의 DropRate 를 적는다

**사용자 결정(2026-09-26)** — "100% 나오는 건 좀 그렇다, 적당히 낮춰".
목록 드랍은 `Formulas/monsterexp.cs` `DetermineRandomDrop` 이 한 마리에 **하나까지** 고른다. 한 물건의 실제 확률은
`DropRate × 1.5(DropBoost) ÷ 목록 칸수`, 그 괴물이 뭐라도 떨굴 확률은 그 합이다. 1.5배 뒤 합이 100% 를 넘는
괴물(노비스 쿠룸·마라디움 괴물 · 자이언트맨티스 세줄금반지 120%)과 85~95% 인 괴물(우드랜드·포테)이 있었다.

**상한 80%** — 근거:
  - 1.5배 전(2026-09-26 아침) 노비스 괴물의 합은 60~65%, 우드랜드 60% 안팎이었다. 1.5배를 그대로 곱하면 90~97%.
    80% 는 그 1.5배의 대부분(+15~20%p)을 남기면서 **다섯 마리에 한 마리는 빈손**이 되게 하는 선이다.
  - 자이언트맨티스 세줄금반지 — 5.99 팩이 `드롭아이템 80` 으로 적은 값(`build-gear-drops.py`)과 같아진다.

**어떻게 낮추나** — `DropRate` 는 아이템마다 하나(전역)라 괴물마다 못 바꾼다. 그래서 되풀이한다:
넘는 괴물마다 필요한 배율(80% ÷ 지금 합)을 구하고, 그 괴물의 물건마다 **가장 작은 배율**을 곱한다(같은 물건을
떨구는 다른 괴물에서는 내려가기만 한다). 단 **마력 포션은 바닥이 있다** — 사용자가 같은 날 "마력 포션 드랍률
두 배"를 정했으므로(`ManaPotionDropTests`) `DropRate × 1.5 ≥ 2 × 그 전 값` 아래로는 내리지 않고, 모자란 몫은
같은 목록의 다른 물건이 진다. 마지막에 넷째 자리에서 내림한다(올림하면 80% 를 살짝 넘을 수 있다).

**다른 드랍 생성기를 다시 돌리면**(`build-novice-drops.py` · `build-hunting-ground-rules.py` · `build-gear-drops.py`
· `build-drop-variety.py`) 그들이 적는 `DropRate` 가 돌아온다 — 그 뒤에 이것을 다시 돌려라. 이 생성기는 값을
**내리기만** 한다(목록·아이템을 더하거나 빼지 않는다). 시험: `ManaPotionDropTests`.
"""

import argparse
import json
import math
import pathlib
import re
import sys
from collections import defaultdict

ROOT = pathlib.Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MONSTERS = SERVER / "templates/monsters"

DROP_BOOST = 1.5  # `Formulas/monsterexp.cs` DropBoost
CAP = 0.80

# 마력 포션의 바닥: 2 × (2026-09-26 두 배 전 DropRate) ÷ DropBoost — `ManaPotionDropTests.ManaPotions` 와 같은 값.
MANA_BEFORE = {"마라디움": 0.5, "하급마력포션": 0.6, "중급마력포션": 0.6}
FLOOR = {name: 2 * before / DROP_BOOST for name, before in MANA_BEFORE.items()}

LENIENT = re.compile(r",(\s*[\]}])")
RATE = re.compile(r'("DropRate"\s*:\s*)(-?[0-9.eE+-]+)')


def read(path):
    return json.loads(LENIENT.sub(r"\1", path.read_text(encoding="utf-8-sig")))


def drops_of(monster):
    listed = monster.get("Drops")
    values = listed.get("$values") if isinstance(listed, dict) else listed
    return [n for n in (values or []) if isinstance(n, str) and n and n != "random"]


def load():
    items = {}
    for path in ITEMS.rglob("*.json"):
        try:
            item = read(path)
        except json.JSONDecodeError:
            continue
        if item.get("Name"):
            items[item["Name"]] = (path, item)
    monsters = []
    for path in sorted(MONSTERS.rglob("*.json")):
        try:
            m = read(path)
        except json.JSONDecodeError:
            continue
        listed = drops_of(m)
        if listed:
            monsters.append((m, listed))
    return items, monsters


def total(listed, rates):
    return DROP_BOOST * sum(rates.get(n, 0) for n in listed) / len(listed)


def plan(items, monsters):
    rates = {name: float(item.get("DropRate") or 0) for name, (path, item) in items.items()}
    for _ in range(10000):
        # 가장 많이 넘는 괴물 하나씩 — 칸이 적어 제일 빡빡한 괴물이 먼저 풀리고, 그 뒤 괴물은 남은 만큼만 깎는다.
        t, m, listed = max(((total(l, rates), m, l) for m, l in monsters), key=lambda row: row[0])
        if t <= CAP + 1e-12:
            break
        floored = sum(rates.get(n, 0) for n in listed if n in FLOOR and rates.get(n, 0) <= FLOOR[n] + 1e-12)
        free = sum(rates.get(n, 0) for n in listed) - floored
        target = CAP * len(listed) / DROP_BOOST - floored
        if free <= 0 or target < 0:
            raise SystemExit(f"{m['Name']}@{m.get('AreaID')}: 마력 포션 바닥만으로 80% 를 넘는다 — 바닥을 다시 보라")
        f = target / free
        for n in set(listed):
            if n in rates and not (n in FLOOR and rates[n] <= FLOOR[n] + 1e-12):
                rates[n] = max(rates[n] * f, min(rates[n], FLOOR.get(n, 0)))
    else:
        raise SystemExit("되풀이가 끝나지 않는다")
    # 넷째 자리에서 내림(올림하면 80% 를 살짝 넘을 수 있다) — 바닥에 닿은 마력 포션만 올림(바닥 아래로 가지 않게).
    new = {}
    for name, (path, item) in items.items():
        was = float(item.get("DropRate") or 0)
        if rates[name] < was - 1e-9:
            if name in FLOOR and rates[name] <= FLOOR[name] + 1e-9:
                new[name] = math.ceil(FLOOR[name] * 10000) / 10000
            else:
                new[name] = math.floor(rates[name] * 10000) / 10000
    return new


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    items, monsters = load()
    before = {name: float(item.get("DropRate") or 0) for name, (path, item) in items.items()}
    new = plan(items, monsters)
    after = {**before, **new}

    print(f"상한 {CAP:.0%} (1.5배 뒤 실제 확률의 합) — 넘는 괴물")
    seen = set()
    for m, listed in monsters:
        t0 = total(listed, before)
        key = (m["Name"], tuple(listed))
        if t0 > CAP + 1e-9 and key not in seen:
            seen.add(key)
            print(f"  {m['Name']} ({len(listed)}칸): {t0:.1%} → {total(listed, after):.1%}")

    print("\n바뀌는 DropRate (전 → 후) · 그 물건을 떨구는 괴물 합의 전→후 범위")
    for name in sorted(new):
        users = [(total(l, before), total(l, after)) for m, l in monsters if name in l]
        lo = min(u[1] for u in users)
        hi = max(u[1] for u in users)
        print(f"  {name}: {before[name]} → {new[name]}  (괴물 {len(users)}마리 · 후 합 {lo:.1%}~{hi:.1%})")

    worst = max((total(l, after), m["Name"]) for m, l in monsters)
    if worst[0] > CAP + 1e-9:
        raise SystemExit(f"아직 넘는다: {worst[1]} {worst[0]:.1%}")
    print(f"\n가장 높은 괴물 합: {worst[1]} {worst[0]:.1%}")

    if writing:
        for name, rate in new.items():
            path = items[name][0]
            text = path.read_text(encoding="utf-8-sig")
            text, count = RATE.subn(lambda mm: f"{mm.group(1)}{rate}", text, count=1)
            if count != 1:
                raise SystemExit(f"{path}: DropRate 줄을 못 찾았다")
            path.write_text(text, encoding="utf-8")
    print(f"\n아이템 {len(new)}장이 바뀐다.")
    print("적었습니다." if writing else "미리 본 것입니다 — 적으려면 --쓰기")


if __name__ == "__main__":
    sys.exit(main())
