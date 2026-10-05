#!/usr/bin/env python3
"""사냥터 레벨이 높을수록 포션이 훨씬 많이, 더 높은 등급으로 나오게 한다 — 드랍 생성기들 사이, `build-drop-cap.py` 앞에 돌린다.

  python3 scripts/gen/items/build-potion-by-level.py            # 무엇이 바뀌는지만 본다 (사냥터별 전후 표)
  python3 scripts/gen/items/build-potion-by-level.py --쓰기      # 서버 정의에 적는다

**사용자 결정(2026-10-05)** 「아이템 드랍도 고렙 던젼일수록 포션이 훨씬 많이 나오도록 바꿔」.

**먼저 알아 둘 것(지금 자료)** — 사용자는 저레벨 5% 안팎이라 짐작했지만 실제는 거꾸로였다: 노비스·우드랜드2~6·포테의숲은
이미 한 마리당 50~70%(2026-09-24 「포션을 주 드랍으로」), 아벨해안은 8~19%, 서·북의우드랜드·구광산은 **0%** 였다.
그래서 저레벨 값을 깎지 않고(요청 없는 삭제 금지, 쿠룸·마라디움은 초반 생명줄) **레벨별 바닥선**을 세워 모자란 곳만 채운다.

**셈** — `Formulas/monsterexp.cs` DetermineRandomDrop: 한 마리가 목록에서 **하나**만 뽑고, 한 물건이 나올 확률 =
`DropRate × 1.5 ÷ 목록 칸수`(같은 이름을 두 번 적으면 두 칸 — 서버가 칸마다 굴린다). 그 괴물이 포션을 떨굴 확률 P = 포션 칸 무게의 합 ÷ 칸수.
`DropRate` 는 아이템 하나에 하나뿐이라 괴물마다 못 바꾸므로 **괴물마다 포션 칸 수(같은 포션 되풀이 포함)를 달리해** 목표에 맞춘다.

**바닥선(`target`)** — 1.5배 뒤 실제 확률, 사냥터 레벨 L: 41 까지 60%(지금 포테의숲·우드랜드가 이미 60% 안팎이라 거기서 거꾸로 가지
않게), 41 → 99 에서 78% 까지 직선(L51 63 · 71 69 · 81 72 · 99 78). 지금 그보다 높은 곳은 그대로. 한 괴물 합 80% 상한(`build-drop-cap.py`)을 못 넘게 포션 + 기존 물건의 합이 79.5% 안에서만 더한다.
**등급** — 레벨대마다 쓰는 포션 풀이 다르다(`POOLS`): 낮은 곳은 최하급·하급, 높은 곳은 상급체력·상급마력·파프리카·블루피치.
체력:마력 = 1:2 로 맞춘다(2026-09-26 마력 두 배). **엑스쿠라눔은 더하지 않는다** — 판매가 700,000 · DropRate 0.02 는 5.99 증거값으로 시험이 못박았다.

**사냥터 레벨** — 우드랜드2-1 11 · 3-1·4-1 21 · 5-1·6-1 51 · 14-1 81, 포테의숲 21, 아벨해안 1~4구역 51·59·67·75(입장 51~80 을 4등분),
서·북의우드랜드는 `lib/_drops.py` 층(11·26·41·56·71·86), 구광산 99. 노비스·수오미·죽음의마을·신죽·뤼케시온해안·호러캐슬·카스마늄·마운틴메리는
건드리지 않는다(자기 확률 `DropRate` 를 괴물에 적었거나 5.99 증거값 한 칸 목록이라 칸을 늘리면 확률이 바뀐다).
단 2026-10-05 부터 신죽·카스마늄·드라큐라백작의성·지하수로D 에서 `build-drop-variety.py` 가 99레벨 장비를 붙인(맨손이던) 괴물은 99 로 채운다(`FRESH_99`).
**구광산 일반 괴물**은 `LootType` 이 None(256) 이라 아무것도 안 떨궜다 — Random(2) 로 바꾸고 포션만 넣는다(금화는 늘 준다).

**기존 물건은 안 지운다** — 포션 칸을 더할 뿐이라 칸수가 늘어난 만큼 같은 목록의 장비·잡템 실제 확률이 (옛 칸수 ÷ 새 칸수)로 옅어진다
(한 마리가 하나만 떨구므로 포션이 늘면 다른 것이 준다). `DropRate` 를 올려 보정하지 않는다 — 그 값은 다른 사냥터와 같이 쓰는 하나라서.
실행하면 옛→새 실제 확률 표가 나온다.

**다시 돌려도 같다** — 이미 바닥선을 넘은 괴물은 건드리지 않는다. 다른 드랍 생성기(`build-drop-variety.py` 등)를 다시 돌리면
더한 칸이 사라지니 그 뒤에 이것을 돌리고, 마지막에 `build-drop-cap.py`.
시험: `ManaPotionDropTests`(합 80% 상한)·`DropVarietyTests`(아벨 상급 포션)·`EvidenceBackedDropDistributionTests`. 이 생성기 전용 시험은 아직 없다.
"""

import argparse
import itertools
import json
import re
import sys
from collections import defaultdict

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT
from lib._drops import drops_of, woodland_west_north_layers
from lib._io import read_lenient_json as read
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MONSTERS = SERVER / "templates/monsters"

DROP_BOOST = 1.5            # `Formulas/monsterexp.cs` DropBoost
CAP = 0.795                 # `build-drop-cap.py` 80% 아래
LOOT_RANDOM, LOOT_NONE = 1 << 1, 256
DROPS_TYPE = "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib"
RESERVED = {"크라켄1", "크라켄2", "킹아크퍼스1", "킹아크퍼스2", "그림록퀸"}   # `build-gear-drops.py` FIELD_BOSSES

# 새로 정하는 DropRate(1.5배 전) — 지금 DropRate 가 없는(=안 떨구던) 포션만. 체력:마력 = 1:2. 하급·중급·상급 은 그대로 둔다.
NEW_RATES = {"최하급체력포션": 0.6, "최하급마력포션": 1.2, "파프리카": 1.4, "블루피치": 2.0}

HEALTH = {"쿠룸", "최하급체력포션", "하급체력포션", "중급체력포션", "상급체력포션", "엑스쿠라눔"}
MANA = {"마라디움", "최하급마력포션", "하급마력포션", "중급마력포션", "상급마력포션", "파프리카", "블루피치"}
POTIONS = HEALTH | MANA

# 레벨 L 이하 가장 가까운 칸의 풀(낮은 등급 → 높은 등급). 풀 안의 포션으로만 더한다.
# 하급·중급 은 DropRate 가 큰 물건(칸이 20개쯤인 우드랜드·포테용 값)이라 5칸짜리 괴물에서는 한 칸만 넣어도 넘친다 — 작은 물건과 함께 둬서 칸수에 맞는 쪽이 골라지게.
POOLS = [
    (0, ["최하급체력포션", "최하급마력포션", "하급체력포션", "하급마력포션"]),
    (26, ["최하급체력포션", "최하급마력포션", "파프리카", "하급체력포션", "하급마력포션"]),
    (41, ["파프리카", "상급체력포션", "상급마력포션", "중급체력포션", "중급마력포션"]),
    (61, ["상급체력포션", "상급마력포션", "파프리카", "블루피치", "중급마력포션"]),
    (81, ["상급체력포션", "상급마력포션", "블루피치", "파프리카"]),
    (99, ["상급체력포션", "상급마력포션", "블루피치"]),
]
MAX_EACH, MAX_SLOTS = 4, 12

# 2026-10-05 드랍 검수(`plans/drop-audit-2026-10-05.md`) — `build-drop-variety.py` FRESH_REGIONS 가 99레벨 장비를 붙인 맵(구광산 밖).
FRESH_99 = r"드라큐라백작의성.+|지하수로D-\d+|신죽(마집안|음의마을)[\d-]+|카스마늄제\d-\d갱도"


def target(level):
    return 0.60 + 0.18 * max(0, level - 41) / 58


def pool_of(level):
    return [names for low, names in POOLS if level >= low][-1]


def write(path, data, writing, newline):
    if writing:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + newline, encoding="utf-8")


def load_items():
    items = {}
    for path in ITEMS.rglob("*.json"):
        item = read(path)
        if item.get("Name"):
            items[item["Name"]] = (path, item)
    return items


def load_levels():
    """맵 번호 → (사냥터 이름, 레벨). 이 생성기가 다루는 사냥터만."""
    layers = {a: layer for layer, areas in woodland_west_north_layers(SERVER / "areas").items() for a in areas}
    levels = {}
    for path in (SERVER / "areas").glob("*.json"):
        area = read(path)
        aid, name = area.get("ID") or area.get("Id"), area.get("Name", "")
        if aid in layers:
            levels[aid] = (name, layers[aid])
        elif name == "우드랜드2-1":
            levels[aid] = (name, 11)
        elif name in ("우드랜드3-1", "우드랜드4-1"):
            levels[aid] = (name, 21)
        elif name in ("우드랜드5-1", "우드랜드6-1"):
            levels[aid] = (name, 51)
        elif name == "우드랜드14-1":
            levels[aid] = (name, 81)
        elif re.fullmatch(r"포테의숲\d존", name):
            levels[aid] = (name, 21)
        elif m := re.match(r"아벨해안(\d)-", name):
            levels[aid] = (name, (51, 59, 67, 75)[int(m.group(1)) - 1])
        elif re.fullmatch(r"구광산\d+-\d+", name):
            levels[aid] = (name, 99)
        elif re.fullmatch(FRESH_99, name):
            levels[aid] = (name, 99)
    return levels


def load_monsters(levels, items):
    out = []
    for path in sorted(MONSTERS.rglob("*.json")):
        try:
            m = read(path)
        except json.JSONDecodeError:
            continue
        loot = m.get("LootType") or 0
        mine = str(levels.get(m.get("AreaID"), ("",))[0]).startswith("구광산")
        if m.get("AreaID") not in levels or m["Name"] in RESERVED or m.get("DropRate") is not None:
            continue
        if not (loot & LOOT_RANDOM or (mine and loot == LOOT_NONE)):
            continue
        # 99레벨 사냥터(`FRESH_99`) — `build-drop-variety.py` 가 장비를 붙인 괴물만. 5.99 증거 물건 한 칸 목록(열쇠·가위·엑스쿠라눔)은 그대로.
        if re.fullmatch(FRESH_99, levels[m["AreaID"]][0]) and not any(
                (items.get(n, (None, {}))[1].get("EquipmentSlot") or 0) > 0 for n in drops_of(m)):
            continue
        out.append((path, m))
    return out


def weight(name, rates):
    return DROP_BOOST * rates.get(name, 0)


def plan_one(listed, level, rates):
    key = (tuple(sorted(listed)), level)
    if key not in _CACHE:
        _CACHE[key] = _plan_one(listed, level, rates)
    return _CACHE[key]


_CACHE = {}


def _plan_one(listed, level, rates):
    """(더할 포션 이름들) — 이미 바닥선 이상이면 빈 목록."""
    n_old = len(listed)
    base = sum(weight(n, rates) for n in listed)
    pot = sum(weight(n, rates) for n in listed if n in POTIONS)
    goal = target(level)
    if n_old and pot / n_old >= goal - 0.005 and any(n in HEALTH for n in listed) and any(n in MANA for n in listed):
        return []
    pool = pool_of(level)
    have = {n: listed.count(n) for n in pool}
    best = None
    for counts in itertools.product(*(range(MAX_EACH - have[n] + 1) for n in pool)):
        k = sum(counts)
        if k > MAX_SLOTS - sum(1 for n in listed if n in POTIONS):
            continue
        added = [n for n, c in zip(pool, counts) for _ in range(c)]
        names = listed + added
        if not (any(n in HEALTH for n in names) and any(n in MANA for n in names)):
            continue
        add_w = sum(weight(n, rates) for n in added)
        total = (base + add_w) / (n_old + k)
        if total > CAP and k:
            continue
        p = (pot + add_w) / (n_old + k)
        ph = sum(weight(n, rates) for n in names if n in HEALTH) / (n_old + k)
        # 목표에 가깝게 · 체력:마력 = 1:2 · 칸은 적게(장비·잡템이 덜 옅어지게). k=0 도 후보라 이미 충분하면(다시 돌려도) 그대로.
        score = abs(p - goal) + 0.15 * abs(ph - p / 3) + 0.01 * k
        if best is None or score < best[0]:
            best = (score, added)
    return best[1] if best else []


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    items = load_items()
    rates = {name: float(item.get("DropRate") or 0) for name, (path, item) in items.items()}
    for name, rate in NEW_RATES.items():
        if not rates.get(name):
            rates[name] = rate
    levels = load_levels()
    monsters = load_monsters(levels, items)

    rows = defaultdict(list)    # 맵 이름 → [(전 포션, 후 포션, 전 합, 후 합, 칸 전, 칸 후)]
    changed = 0
    used = set()
    for path, m in monsters:
        listed = drops_of(m)
        name, level = levels[m["AreaID"]]
        added = plan_one(listed, level, rates)
        new = listed + added
        n0, n1 = len(listed), len(new)
        row = lambda names, n: (sum(weight(x, rates) for x in names if x in POTIONS) / n if n else 0,
                                sum(weight(x, rates) for x in names) / n if n else 0)
        (p0, t0), (p1, t1) = row(listed, n0), row(new, n1)
        rows[(level, name)].append((p0, p1, t0, t1, n0, n1))
        if added:
            used.update(added)
            m["Drops"] = {"$type": DROPS_TYPE, "$values": new}
            if (m.get("LootType") or 0) == LOOT_NONE:
                m["LootType"] = LOOT_RANDOM
            write(path, m, writing, "\n" if path.read_text(encoding="utf-8-sig").endswith("\n") else "")  # 옛 끝 줄바꿈을 지킨다
            changed += 1

    print("사냥터 레벨 · 바닥선 (1.5배 뒤 실제 확률) · 포션을 떨굴 확률 평균 전 → 후 · 뭐라도 떨굴 확률 전 → 후 · 칸수")
    by_level = defaultdict(list)
    for (level, name), rs in sorted(rows.items()):
        avg = lambda i: sum(r[i] for r in rs) / len(rs)
        by_level[level].extend(rs)
        print(f"  L{level:<3}{name:<16}바닥 {target(level):.0%}  포션 {avg(0):>5.1%} → {avg(1):>5.1%}   합 {avg(2):>5.1%} → {avg(3):>5.1%}   칸 {avg(4):.0f} → {avg(5):.0f}")
    print("\n레벨별 요약")
    for level, rs in sorted(by_level.items()):
        avg = lambda i: sum(r[i] for r in rs) / len(rs)
        print(f"  L{level:<3} 괴물 {len(rs):>3}마리  포션 {avg(0):.1%} → {avg(1):.1%}   바닥 {target(level):.0%}")

    for name, rate in NEW_RATES.items():
        if name in used and not items[name][1].get("DropRate"):
            items[name][1]["DropRate"] = rate
            write(items[name][0], items[name][1], writing, "\n")
            print(f"{name} DropRate {rate}")
    print(f"\n괴물 정의 {changed}장이 바뀐다.")
    print("적었습니다." if writing else "미리 본 것입니다 — 적으려면 --쓰기")


if __name__ == "__main__":
    sys.exit(main())
