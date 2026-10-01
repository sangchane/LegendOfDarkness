#!/usr/bin/env python3
"""Hades Drops에 이미 있는, 5.99 확률 근거까지 확인된 죽은 드랍만 살린다.

아이템 템플릿(1,364개)에는 어느 괴물이 떨어뜨리는지가 없다. 따라서 DropRate가 남아 있다는
이유로 새 괴물 연결을 만들지 않는다. 대상은 다음 조건을 모두 만족해야 한다.

1. Hades 괴물 템플릿의 Drops에 이름이 이미 있다.
2. 아이템 템플릿은 있지만 DropRate가 없거나 0이다.
3. 5.99 mobs.json에도 같은 괴물 이름 → 같은 아이템과 양수 확률이 있다.

현재 서버는 실제 확률이 ``DropRate × 1.5 ÷ 목록 칸수``다. 원본 확률을 그 식으로 환산하고,
아이템 하나의 전역 DropRate로 여러 원본 확률을 모두 표현할 수 없으면 가장 낮은 값을 쓴다.
단일 Table 드랍은 양수 가중치 하나가 100% 선택되므로 Random으로 바꾸되 Gold 등 다른 비트는
그대로 보존한다.

  python3 scripts/build-evidence-backed-drops.py
  python3 scripts/build-evidence-backed-drops.py --쓰기
"""

import argparse
import json
import sys
import math
from collections import defaultdict


from lib._paths import ROOT
from lib._drops import drops_of
from lib._io import read_lenient_json
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MONSTERS = SERVER / "templates/monsters"
PACK_MOBS = ROOT / "data/server-packs/extracted/5.99-server/mobs.json"

DROP_BOOST = 1.5
CAP = 0.80
LOOT_RANDOM = 2
LOOT_TABLE = 4
ROUNDING = 10_000

# 2026-09-27 감사에서 Hades Drops에는 이미 있지만 DropRate가 0이고, 같은 괴물-아이템 양수 확률이
# 5.99 원본에도 있음을 155개 연결 전부 확인한 목록. 적용 뒤에도 이 목록을 계속 계산해야 누군가
# 확률이나 LootType을 되돌렸을 때 생성기가 고친다.
SUPPORTED = frozenset({
    "2갱도열쇠", "3갱도열쇠", "가위", "거북이등껍질", "고사목뿌리", "그래브의집게",
    "바크의척추뼈", "엑스쿠라눔", "좀비의막대기", "좀비의살", "좀비지팡이",
    "크리스마스얼음", "킹아크퍼스의팬던트", "퐁퐁이의점액질",
})


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def evidence_pairs(value):
    """Extract ``(percent, item)`` from the pack extractor's flat first pair and nested later pairs."""
    if not isinstance(value, list):
        return
    if len(value) >= 2 and isinstance(value[0], str) and isinstance(value[1], str):
        try:
            yield float(value[0]), value[1]
        except ValueError:
            pass
    for nested in value:
        if isinstance(nested, list):
            yield from evidence_pairs(nested)


def pack_evidence():
    found = defaultdict(set)
    for monster in json.loads(PACK_MOBS.read_text(encoding="utf-8")):
        name = monster.get("이름")
        dropped = (monster.get("fields") or {}).get("드롭아이템")
        for percent, item in evidence_pairs(dropped):
            if name and item and percent > 0:
                found[(name, item)].add(percent / 100)
    return found


def load_templates():
    items = {}
    bad = False
    for path in sorted(ITEMS.rglob("*.json")):
        try:
            item = read_lenient_json(path, errors="ignore")
        except json.JSONDecodeError:
            print(f"깨진 JSON 건너뜀: {path}", file=sys.stderr)
            bad = True
            continue
        if isinstance(item, dict) and item.get("Name"):
            items[item["Name"]] = (path, item)
    if bad:
        sys.exit(1)

    monsters = []
    for path in sorted(MONSTERS.rglob("*.json")):
        try:
            monster = read_lenient_json(path, errors="ignore")
        except json.JSONDecodeError:
            continue
        listed = drops_of(monster, items_only=True) if isinstance(monster, dict) else []
        if listed:
            monsters.append((path, monster, listed))
    return items, monsters


def floor_four(value):
    return math.floor((value + 1e-12) * ROUNDING) / ROUNDING


def plan(items, monsters, evidence):
    uses = defaultdict(list)
    for path, monster, listed in monsters:
        for name in listed:
            uses[name].append((path, monster, listed))

    missing_templates = sorted(name for name in uses if name not in items)
    if missing_templates:
        raise SystemExit("Drops가 정의 없는 아이템을 가리킨다: " + " · ".join(missing_templates))

    absent = sorted(SUPPORTED - set(uses))
    if absent:
        raise SystemExit("근거 기반 관리 대상이 Hades Drops에서 사라졌다: " + " · ".join(absent))

    rates = {}
    random_monsters = {}
    unsupported = []

    for item_name in sorted(SUPPORTED):
        constraints = []
        for path, monster, listed in uses[item_name]:
            monster_name = monster.get("Name")
            source_rates = evidence.get((monster_name, item_name), set())
            if not source_rates:
                unsupported.append(f"{monster_name}@{monster.get('AreaID')} → {item_name}: 5.99 근거 없음")
                continue
            if len(source_rates) != 1:
                unsupported.append(
                    f"{monster_name} → {item_name}: 5.99 확률 충돌 {sorted(source_rates)}")
                continue

            loot = int(monster.get("LootType") or 0)
            if loot & LOOT_TABLE:
                if len(listed) != 1:
                    unsupported.append(
                        f"{monster_name}@{monster.get('AreaID')} → {item_name}: 여러 칸 Table은 확률 환산 불가")
                    continue
            if len(listed) == 1:
                random_monsters[path] = monster
            elif not loot & LOOT_RANDOM:
                unsupported.append(
                    f"{monster_name}@{monster.get('AreaID')} → {item_name}: 알 수 없는 LootType {loot}")
                continue

            source_probability = next(iter(source_rates))
            constraints.append(source_probability * len(listed) / DROP_BOOST)

        if constraints and len(constraints) == len(uses[item_name]):
            rates[item_name] = floor_four(min(constraints))

    if unsupported:
        raise SystemExit("근거 없는 죽은 드랍은 반영하지 않는다:\n  " + "\n  ".join(unsupported))
    if SUPPORTED != set(rates):
        absent = sorted(SUPPORTED - set(rates))
        raise SystemExit("환산하지 못한 드랍: " + " · ".join(absent))

    return uses, rates, random_monsters


def total_probability(listed, rates):
    return DROP_BOOST * sum(rates.get(name, 0) for name in listed) / len(listed)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    args = parser.parse_args()

    items, monsters = load_templates()
    evidence = pack_evidence()
    uses, planned, random_monsters = plan(items, monsters, evidence)
    before = {name: float(item.get("DropRate") or 0) for name, (_, item) in items.items()}
    after = {**before, **planned}
    changed_rates = {name: rate for name, rate in planned.items() if abs(before[name] - rate) > 1e-12}
    changed_loot = {
        path: monster for path, monster in random_monsters.items()
        if int(monster.get("LootType") or 0) & LOOT_TABLE
        or not int(monster.get("LootType") or 0) & LOOT_RANDOM
    }

    print(f"아이템 템플릿 {len(items):,}종")
    print(f"괴물 Drops {len(uses):,}종 · {sum(len(rows) for rows in uses.values()):,}개 연결")
    print(f"근거 기반 관리 대상 {len(planned):,}종 · {sum(len(uses[n]) for n in planned):,}개 연결")
    for name in sorted(planned):
        source = sorted({
            rate
            for _, monster, _ in uses[name]
            for rate in evidence[(monster.get("Name"), name)]
        })
        print(f"  {name}: DropRate {planned[name]:g} · 원본 {', '.join(f'{rate:.0%}' for rate in source)} · "
              f"괴물 연결 {len(uses[name])}개")

    worst = (0.0, "없음")
    for _, monster, listed in monsters:
        total = total_probability(listed, after)
        worst = max(worst, (total, f"{monster.get('Name')}@{monster.get('AreaID')}"))
        if total > CAP + 1e-9:
            raise SystemExit(f"80% 상한 초과: {monster.get('Name')}@{monster.get('AreaID')} {total:.2%}")
    print(f"가장 높은 목록 드랍 합 {worst[0]:.2%} ({worst[1]})")

    if args.writing:
        for name, rate in changed_rates.items():
            path, item = items[name]
            item["DropRate"] = rate
            write(path, item)
        for path, monster in changed_loot.items():
            loot = int(monster.get("LootType") or 0)
            monster["LootType"] = (loot & ~LOOT_TABLE) | LOOT_RANDOM
            write(path, monster)

    print(f"바뀔 DropRate {len(changed_rates):,}종 · 단일 Table → Random {len(changed_loot):,}개")
    print("적었습니다." if args.writing else "미리 본 것입니다 — 적으려면 --쓰기")


if __name__ == "__main__":
    main()
