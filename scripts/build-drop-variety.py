#!/usr/bin/env python3
"""99레벨 이전 사냥터의 드랍 종류를 늘린다 — 속성·접미사 장비와 포션만, 재료는 늘리지 않는다.

  python3 scripts/build-drop-variety.py            # 무엇이 바뀌는지만 본다 (사냥터별 전후 표)
  python3 scripts/build-drop-variety.py --쓰기      # 서버 정의에 적는다

**사용자 결정(2026-09-26)**
  1. 드랍 확률 전체 1.5배 — `Formulas/monsterexp.cs` `DropBoost` 가 한다(이 생성기는 건드리지 않는다).
  2. 99레벨 이전 사냥터의 드랍 **종류**를 늘린다. 재료(잡템·괴물 부산물)는 늘리지 않는다 — 나중에 사용자가
     필요한 것만 정한다. 늘리는 것은 속성·접미사 장비와 포션 같은 소모품.
  3. 기본 장비(속성·접미사 없는 것)는 드랍하지 않는다 — 상점에서 판다. (지금 드랍 목록에 기본 장비는 없다 —
     2026-09-25 `build-gear-drops.py` 가 뺐다. 남은 넷 — 실버·골드아쿠아링 · 세줄금반지 · 그림록퀸홀 — 은
     5.99 팩이 이름 있는 괴물에 손수 적은 전리품이라 그대로 둔다.)
  4. 한 마리가 여러 개를 떨구지는 않는다 — 서버 셈(DetermineRandomDrop)이 원래 하나만 고른다.

**셈** — 실제 확률 = `DropRate` × 1.5 ÷ 목록 칸수. 목록에 한 칸을 더하면 **기존 물건이 모두 옅어진다**
(칸수로 나누니까). 그래서 새 칸을 더한 괴물의 기존 물건마다 `DropRate` 를 (새 칸수 ÷ 옛 칸수) 만큼 올려
**옛 실제 확률을 지킨다**. `DropRate` 는 아이템 하나에 하나뿐이라(괴물마다가 아니다) 같은 물건을 여러
괴물이 떨구면 **가장 크게 옅어진 괴물의 배율**을 쓴다 — 다른 괴물에서는 조금 오른다(내려가지는 않는다).
오른 폭은 실행할 때 "영향" 줄로 모두 보인다.

**사냥터마다 무엇을 더하나** (레벨문은 `build-gear-drops.py` TIERS 와 같다. 장비는 **그 사냥터 입장
레벨에서 바로 입을 수 있는 것**, 이미 한글 이름이 있고 **아무도 안 떨구던 것**만 고른다):
  - 우드랜드2-1·3-1·4-1(입장 11·21) — 방어 접미사 **가죽장갑**(11레벨) 7종을 괴물마다 하나씩.
  - 포테의숲1~6존(21) — 공격 속성 **가죽벨트**(11레벨) 4종. 이미 나오는 4원소 룬스톤목걸이와 짝.
  - 우드랜드5-1·6-1(51) — 공격 속성 **크리스탈목걸이**(51레벨) 4종.
  - 우드랜드14-1(81) — 공격 속성 **흑요석목걸이**(81레벨) 4종.
  - 아벨해안(51, 이름 있는 크라켄·킹아크퍼스는 `build-gear-drops.py` FIELD_BOSSES 몫이라 빼고) —
    포션이 하나도 없던 곳이다. **상급체력·상급마력포션**을 모든 일반 괴물에, 잡템 칸이 있는(목록 2칸)
    괴물에는 방어 접미사 **동장갑**(41레벨) 둘을 더 얹는다. 목록이 1칸인 괴물은 +2(3칸), 2칸인 괴물은
    +4(6칸)라 **배율이 모두 3배로 같다** — 같은 은제방패를 1칸·2칸 괴물이 함께 떨궈도 어느 쪽도 오르지
    않는다. 상급 포션은 다른 사냥터가 쓰지 않아(중급은 포테와 같이 쓴다) 확률을 따로 정할 수 있다.
  - 노비스·우드랜드1 은 더하지 않는다 — "저레벨 괴물은 잡템만"(사용자 2026-09-23, `build-gear-drops.py`
    EARLY). 노비스에는 쿠룸·마라디움이 이미 있고, 우드랜드1 괴물은 5.99 에서도 아무것도 안 떨궜다.

**새 장비의 확률** — 한 종의 실제 확률이 그 사냥터 기존 장비보다 높지 않고 2%(1.5배 전, 1.5배 후 3%)도
넘지 않게 `DropRate` 를 고른다: (그 무리 기존 장비의 가장 낮은 실제 확률, 2% 중 작은 것) × 가장 짧은 목록 칸수.

**다시 돌려도 같다** — 기존 물건의 `DropRate` 는 아래 `BASE_RATE`(이 생성기가 처음 돌기 전, 1.5배 전
값)에서 늘 새로 계산한다. 목록에서도 이 생성기가 더하는 이름을 먼저 빼고 옛 목록을 되살려 센다.
**`build-gear-drops.py` 를 다시 돌리면 장비 칸이 그 생성기의 한 벌로 되돌아간다** — 그 뒤에 이것을 다시
돌려라. 다른 생성기가 기존 물건의 기준값을 바꿨으면(`BASE_RATE` 와도 목표값과도 다르면) 경고를 낸다.
"""

import argparse
import json
import pathlib
import re
import sys
from collections import defaultdict

ROOT = pathlib.Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MONSTERS = SERVER / "templates/monsters"

# `Formulas/monsterexp.cs` DropBoost — 표를 읽을 때만 곱한다(자료에는 곱하지 않는다).
DROP_BOOST = 1.5

# 새 장비 한 종의 실제 확률 윗선(1.5배 전). 사용자: "지금 수준(1.2~2%)을 넘지 않게".
GEAR_CAP = 0.02

LOOT_RANDOM = 1 << 1

# `build-gear-drops.py` FIELD_BOSSES 가 한 칸짜리 목록으로 관리한다 — 건드리지 않는다.
RESERVED_NAMES = {"크라켄1", "크라켄2", "킹아크퍼스1", "킹아크퍼스2"}

DEFENSE = ["로오", "이아", "메투스", "세토아", "세오", "셔스", "칸"]
ELEMENT = ["화염", "바다", "바람", "대지"]

ABEL = [20584, 20585, 20586, 20587, 20588, 20589, 20590, 20591, 20592, 20593, 20594]

# 칸: 이름, 맵들, 입장 레벨, 괴물마다 더할 장비 수, 장비 한 벌, 모든 괴물에 더할 소모품 {이름: DropRate},
#     장비를 얹을 괴물의 옛 목록 최소 칸수.
GROUPS = [
    dict(name="우드랜드2-1·3-1·4-1", areas=[20022, 20023, 20024], entry=11, per=1,
         gear=[f"{p}의가죽장갑" for p in DEFENSE], potions={}, gear_min_slots=1),
    dict(name="포테의숲1~6존", areas=[20263, 20264, 20265, 20266, 20267, 20268], entry=21, per=1,
         gear=[f"{p}의가죽벨트" for p in ELEMENT], potions={}, gear_min_slots=1),
    dict(name="우드랜드5-1·6-1", areas=[20025, 20026], entry=51, per=1,
         gear=[f"{p}의크리스탈목걸이" for p in ELEMENT], potions={}, gear_min_slots=1),
    dict(name="우드랜드14-1", areas=[20020], entry=81, per=1,
         gear=[f"{p}의흑요석목걸이" for p in ELEMENT], potions={}, gear_min_slots=1),
    # 상급 포션 확률: 3칸 괴물에서 체력 10%·마력 20%, 6칸 괴물에서 5%·10% (1.5배 전). 다른 사냥터의
    # 체력:마력 = 1:2 (`build-hunting-ground-rules.py` 마력 두 배)를 따른다.
    dict(name="아벨해안(일반 괴물)", areas=ABEL, entry=51, per=2,
         gear=["로오의동장갑", "칸의동장갑"], potions={"상급체력포션": 0.3, "상급마력포션": 0.6},
         gear_min_slots=2),
]

# 이 생성기가 처음 돌기 전(2026-09-26, 1.5배 전)의 DropRate — 기존 물건은 늘 여기서 다시 계산한다.
# 장비 0.06 은 `build-gear-drops.py` GEAR_RATE, 포션은 `build-hunting-ground-rules.py`
# POTION_DROP_RATE·MANA_POTION_DROP_RATE, 잡템은 그 두 생성기가 적은 값이다.
BASE_RATE = {
    **{f"{p}의{kind}": 0.06 for p in DEFENSE for kind in ("동각반", "은각반", "은제방패")},
    **{f"{p}의호안석반지": 0.06 for p in ["이아", "메투스", "세토아", "세오", "셔스"]},
    "로오의반지": 0.06, "칸의목걸이": 0.06,
    **{f"{e}의룬스톤목걸이": 0.06 for e in ELEMENT},
    "하급체력포션": 0.6, "하급마력포션": 1.2, "중급체력포션": 0.6, "중급마력포션": 1.2,
    "엘란디스": 0.4, "이슬": 0.4, "아칸더스": 0.4,
    "그린팜팻의알": 0.2, "레드팜팻의알": 0.2, "옐로우팜팻의알": 0.2, "퍼플팜팻의알": 0.2,
    "놀의단검": 0.2, "엔트자이언트의날개": 0.2, "사슴의정수": 0.35, "실버팜팻의인장": 0.1,
    "엔트라이온의몸통": 0.15, "트랜트의뿌리": 0.15, "은빛늑대의갈기털": 0.15,
    "거북이등껍질": 0, "그래브의집게": 0, "바크의척추뼈": 0, "퐁퐁이의점액질": 0,
}

DROPS_TYPE = "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib"

LENIENT = re.compile(r",(\s*[\]}])")


def read(path):
    return json.loads(LENIENT.sub(r"\1", path.read_text(encoding="utf-8-sig")))


def write(path, data, writing, newline):
    if writing:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + newline, encoding="utf-8")


def drops_of(monster):
    listed = monster.get("Drops")
    values = listed.get("$values") if isinstance(listed, dict) else listed
    return [name for name in (values or []) if isinstance(name, str)]


def load_items():
    items = {}
    for path in ITEMS.rglob("*.json"):
        try:
            item = read(path)
        except json.JSONDecodeError:
            continue
        if item.get("Name"):
            items[item["Name"]] = (path, item)
    return items


def load_area_names():
    names = {}
    for path in (SERVER / "areas").glob("*.json"):
        try:
            area = read(path)
        except json.JSONDecodeError:
            continue
        names[area.get("ID") or area.get("Id")] = area.get("Name")
    return names


AREA_NAMES = load_area_names()


def load_monsters():
    monsters = []
    for path in sorted(MONSTERS.rglob("*.json")):
        try:
            monsters.append((path, read(path)))
        except json.JSONDecodeError:
            continue
    return monsters


ADDED = {name for g in GROUPS for name in g["gear"]} | {name for g in GROUPS for name in g["potions"]}


def plan(monsters, items, said):
    """괴물 파일마다 (옛 목록, 새 목록) 과 아이템마다 새 DropRate 를 정한다."""
    lists = {}  # path -> (monster, old, new, group)
    new_rates = {}

    for group in GROUPS:
        for name in group["gear"] + list(group["potions"]):
            if name not in items:
                raise SystemExit(f"없는 아이템: {name}")
            item = items[name][1]
            if name in group["gear"]:
                level = item.get("LevelRequired") or 0
                if level > group["entry"] or not (item.get("EquipmentSlot") or 0):
                    raise SystemExit(f"{name}: 레벨 {level} — {group['name']} 입장 {group['entry']} 에 못 입는다")

        here = [(p, m) for p, m in monsters
                if m.get("AreaID") in group["areas"] and m.get("Name") not in RESERVED_NAMES]
        old_of = {p: [n for n in drops_of(m) if n not in ADDED] for p, m in here}

        # 장비를 얹을 괴물 이름 — 이름 차례대로 한 벌을 돌려 가며(`build-gear-drops.py` lay_gear 와 같은 꼴).
        wearers = sorted({m["Name"] for p, m in here if len(old_of[p]) >= group["gear_min_slots"]})
        carried = {}
        for index, name in enumerate(wearers):
            carried[name] = [group["gear"][(index * group["per"] + k) % len(group["gear"])]
                             for k in range(group["per"])]

        for p, m in here:
            if not (m.get("LootType") or 0) & LOOT_RANDOM:
                raise SystemExit(f"{m['Name']}({p.name}) 가 목록 갈래(Random)가 아니다 — LootType {m.get('LootType')}")
            old = old_of[p]
            new = old + list(group["potions"]) + carried.get(m["Name"], [])
            lists[p] = (m, old, new, group)

    # 기존 물건: 가장 크게 옅어진 괴물의 배율.
    factor = defaultdict(lambda: 1.0)
    for m, old, new, group in lists.values():
        for name in old:
            factor[name] = max(factor[name], len(new) / len(old))

    for name, f in factor.items():
        if name not in BASE_RATE:
            raise SystemExit(f"{name}: BASE_RATE 에 없다 — 기준값을 먼저 적어라")
        if BASE_RATE[name]:  # 0 인 잡템(아벨해안)은 몇 배를 해도 0 이다 — 적지 않는다.
            new_rates[name] = round(BASE_RATE[name] * f, 6)

    # 새 소모품: 정한 값 그대로.
    for group in GROUPS:
        new_rates.update(group["potions"])

    # 새 장비: (그 무리 기존 장비의 가장 낮은 실제 확률, GEAR_CAP) × 가장 짧은 새 목록.
    for group in GROUPS:
        rows = [(m, old, new) for m, old, new, g in lists.values() if g is group]
        existing = [new_rates[n] / len(new) for m, old, new in rows for n in old
                    if (items[n][1].get("EquipmentSlot") or 0) > 0]
        target = min([GEAR_CAP] + existing)
        shortest = min(len(new) for m, old, new in rows if set(new) & set(group["gear"]))
        for name in group["gear"]:
            new_rates[name] = round(target * shortest, 6)

    return lists, new_rates


def real(rate, slots):
    return rate * DROP_BOOST / slots if slots else 0.0


def report(monsters, items, lists, new_rates, said):
    # 괴물마다 확률 확인: 옛 물건이 떨어지지 않았나, 합이 100% 를 넘지 않나.
    for p, (m, old, new, group) in lists.items():
        for name in old:
            before = real(BASE_RATE[name], len(old))
            after = real(new_rates.get(name, 0), len(new))
            if after + 1e-9 < before:
                raise SystemExit(f"{m['Name']} {name}: {before:.2%} → {after:.2%} 로 떨어진다")
        total = sum(real(new_rates.get(n, items[n][1].get("DropRate") or 0), len(new)) for n in new)
        if total > 1 + 1e-9:
            raise SystemExit(f"{m['Name']}({p.name}): 합 {total:.0%} — 100% 를 넘으면 뒤 칸이 잘린다")

    # 사냥터별 전후 표 (1.5배 뒤 실제 확률).
    said.append("사냥터별 전후 (1.5배 뒤 실제 확률, 종류 = 그 맵 괴물들이 떨구는 서로 다른 물건 수)")
    said.append(f"{'맵':<14}{'종류 전':>6}{'종류 후':>6}   {'뭐라도(평균) 전':>14} → 후     더한 것")
    by_area = defaultdict(list)
    for p, (m, old, new, group) in lists.items():
        by_area[m["AreaID"]].append((m, old, new))
    for group in GROUPS:
        for area in group["areas"]:
            rows = by_area.get(area)
            if not rows:
                continue
            kinds_before = {n for m, old, new in rows for n in old}
            kinds_after = {n for m, old, new in rows for n in new}
            any_before = sum(sum(real(BASE_RATE[n], len(old)) for n in old) for m, old, new in rows) / len(rows)
            any_after = sum(sum(real(new_rates.get(n, 0), len(new)) for n in new) for m, old, new in rows) / len(rows)
            area_name = AREA_NAMES.get(area, str(area))
            said.append(f"{area_name:<14}{len(kinds_before):>6}{len(kinds_after):>6}   {any_before:>13.1%} → {any_after:.1%}"
                        f"   {' · '.join(sorted(kinds_after - kinds_before))}")

    said.append("\n괴물마다 새 목록 (1.5배 뒤 실제 확률)")
    seen = set()
    for p, (m, old, new, group) in sorted(lists.items(), key=lambda kv: (kv[1][0]["AreaID"], kv[1][0]["Name"])):
        key = (group["name"], m["Name"], tuple(new))
        if key in seen:
            continue
        seen.add(key)
        parts = [f"{n} {real(new_rates.get(n, 0), len(new)):.1%}" for n in new]
        said.append(f"  [{group['name']}] {m['Name']} ({len(old)}→{len(new)}칸): {' · '.join(parts)}")

    # 영향: 바꾼 DropRate 가 이 생성기 밖의 괴물(또는 배율이 더 작은 괴물)에서 오른 폭.
    said.append("\nDropRate 를 바꾼 물건과 영향 (다른 괴물에서 오른 것)")
    touched = {p for p in lists}
    for name in sorted(new_rates):
        was = items[name][1].get("DropRate") or 0
        base = BASE_RATE.get(name)
        if base is not None and abs(was - base) > 1e-9 and abs(was - new_rates[name]) > 1e-9:
            said.append(f"  경고: {name} 의 지금 DropRate {was} 가 기준 {base} 와도 목표 {new_rates[name]} 와도 다르다 —"
                        " 다른 생성기가 바꿨나? BASE_RATE 를 확인하라")
        rises = []
        for p, m in monsters:
            listed = drops_of(m)
            if name not in listed:
                continue
            if p in touched:
                mm, old, new, g = lists[p]
                if name not in old:
                    continue
                before, after = real(BASE_RATE[name], len(old)), real(new_rates[name], len(new))
            else:
                before, after = real(base if base is not None else was, len(listed)), real(new_rates[name], len(listed))
            if after > before + 1e-9:
                rises.append(f"{m['Name']}({m.get('AreaID')}) {before:.2%}→{after:.2%}")
        line = f"  {name}: {was} → {new_rates[name]}"
        if rises:
            uniq = sorted(set(rises))
            line += f"  · 오름 {len(uniq)}: {', '.join(uniq[:6])}{' …' if len(uniq) > 6 else ''}"
        said.append(line)


def apply(items, lists, new_rates, writing):
    changed_monsters = changed_items = 0
    for p, (m, old, new, group) in lists.items():
        if drops_of(m) != new:
            m["Drops"] = {"$type": DROPS_TYPE, "$values": new}
            write(p, m, writing, "")
            changed_monsters += 1
    for name, rate in new_rates.items():
        path, item = items[name]
        if item.get("DropRate") != rate:
            item["DropRate"] = rate
            write(path, item, writing, "\n")
            changed_items += 1
    return changed_monsters, changed_items


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    said = []
    items = load_items()
    monsters = load_monsters()
    lists, new_rates = plan(monsters, items, said)
    report(monsters, items, lists, new_rates, said)
    changed_monsters, changed_items = apply(items, lists, new_rates, writing)

    print("\n".join(said))
    print(f"\n괴물 정의 {changed_monsters}장 · 아이템 {changed_items}장이 바뀐다.")
    print("적었습니다." if writing else "미리 본 것입니다 — 적으려면 --쓰기")


if __name__ == "__main__":
    sys.exit(main())
