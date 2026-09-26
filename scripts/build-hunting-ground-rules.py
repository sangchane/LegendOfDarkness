#!/usr/bin/env python3
"""사용자 결정(2026-09-24)을 사냥터 드랍·상점 자료에 적는다.

  python3 scripts/build-hunting-ground-rules.py            # 무엇이 바뀌는지만 본다
  python3 scripts/build-hunting-ground-rules.py --쓰기      # 서버 정의에 적는다

## 무엇을 하나

1. **금화가 아예 없던 괴물**(사슴·홉고블린류·바크·킹아크퍼스 — 5.99 팩이 `골드` 줄을 안 적어
   원래 0이던 자리, `scripts/build-pack-gold.py` 참고)에 **같은 사냥터 동료와 맞춘 금액**을 적는다.
   사용자가 "골드 플래그가 없어 안 주던 괴물도 항상 주게" 라고 정했고(2026-09-24), 무조건 지급은
   `Formulas/monsterexp.cs` 의 `GenerateGold()` 가 코드로 하므로 여기서는 **액수만** 채운다.
   근거가 없는 값이라 사냥터 동료의 금액을 그대로 썼다 — 사슴(포테의숲)=60, 우드랜드14-1
   고블린류=170(우드랜드3-6 상단과 맞춤), 바크(아벨해안)=250, 킹아크퍼스(아벨해안)=300.

2. **방어 접미사(로오의반지·칸의목걸이) + 공격 속성(화염·바다·바람·대지 목걸이·벨트)** 을
   우드랜드3-6·14 존과 포테의숲1~6존(장비가 이미 나오는 사냥터, `GearDropTests.Later`)의
   괴물 47마리에 하나씩 얹는다. 확률은 **일반 장비(1~5%)보다 드물게 0.5~1%** — 목록이 6칸으로
   늘어나므로 DropRate 0.045(÷6=0.75%)로 맞춘다.

3. **기존 장비 칸의 실제 확률을 그대로 지킨다** — 목록이 2칸(장비+잡템)에서 6칸으로 늘면 장비의
   `DropRate` 를 3배로 올려야 같은 1~5% 안에 남는다(0.06→0.18, 실제 3%로 그대로).

4. **잡템은 남기되 덜 나오게, 포션을 주 드랍으로** — 우드랜드2-1 이상·우드랜드3-6·14·포테의숲1~6에
   사냥터 수준에 맞는 포션(이미 수입된 소모품 중 하급/중급 체력·마력포션)을 얹고, 기존 잡템의
   `DropRate` 는 절반으로 낮춘다. 우드랜드2-1 은 지금 아이템 드랍이 아예 없어(금화만) `LootType`
   에 Random 갈래(2)를 더한다.

아벨해안은 이번에 손대지 않는다 — 아직 워프로 못 가고(`NEXT.md`), 괴물마다 `LootType` 이
금화만/표(Table)만 섞여 있어 이 스크립트가 쓰는 "목록에서 하나를 뽑는" 계산이 그대로 안 맞는다.
"""

import argparse
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MONSTERS = SERVER / "templates/monsters"

LOOT_RANDOM = 2

# 금화가 없던 괴물 — (이름, AreaID) -> 채울 금액.
MISSING_GOLD = {
    ("사슴", 20263): 60, ("사슴", 20264): 60, ("사슴", 20265): 60,
    ("사슴", 20266): 60, ("사슴", 20267): 60, ("사슴", 20268): 60,
    ("홉고블린1", 20020): 170, ("고블린전사1", 20020): 170,
    ("고블린병사1", 20020): 170, ("고블린가드1", 20020): 170,
    ("바크", 20586): 250, ("바크", 20587): 250, ("바크", 20588): 250,
    ("킹아크퍼스1", 20592): 300, ("킹아크퍼스2", 20592): 300,
    ("킹아크퍼스1", 20594): 300, ("킹아크퍼스2", 20594): 300,
}

# 장비가 이미 나오는 사냥터 (`GearDropTests.Later`) — 접미사·속성·포션을 얹는 곳.
LATER_ZONES = {
    20023: "하급", 20024: "하급", 20025: "하급", 20026: "하급", 20020: "하급",
    20263: "중급", 20264: "중급", 20265: "중급", 20266: "중급",
    20267: "중급", 20268: "중급",
}

# 아직 아이템이 하나도 안 나오는 성장 구간 — 포션만 얹는다.
GROWTH_ZONES = {20022: "하급"}

DEFENSE_SUFFIX_ITEMS = ["로오의반지", "칸의목걸이"]
ATTACK_ELEMENT_ITEMS = [
    "대지의목걸이", "대지의벨트", "바다의목걸이", "바다의벨트",
    "바람의목걸이", "바람의벨트", "화염의목걸이", "화염의벨트",
]

GEAR_DROP_RATE = 0.18  # 기존 0.06 의 3배 — 목록 2칸→6칸이라도 실제 3%를 지킨다.
SUFFIX_ELEMENT_DROP_RATE = 0.045  # 6칸 목록에서 0.75% (0.5~1% 안)
POTION_DROP_RATE = 0.6
# 2026-09-26 사용자 "맵 전체에 마력 포션 드랍률 좀 높이고" — 마력포션만 두 배(0.6 → 1.2). 1 을 넘어도
# 서버 셈(DetermineRandomDrop)이 DropRate ÷ 칸수 를 그대로 지킨다.
MANA_POTION_DROP_RATE = 1.2


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, data, writing):
    if writing:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def monster_files():
    for path in sorted(MONSTERS.rglob("*.json")):
        try:
            yield path, read(path)
        except json.JSONDecodeError:
            continue


def item_files():
    found = {}
    for path in sorted(ITEMS.rglob("*.json")):
        try:
            item = read(path)
        except json.JSONDecodeError:
            continue
        name = item.get("Name") or path.stem
        found[name] = (path, item)
    return found


def dropped_names(monster):
    drops = monster.get("Drops")
    values = drops.get("$values") if isinstance(drops, dict) else drops
    return [name for name in (values or []) if name and name != "random"]


def set_drops(monster, names):
    monster["Drops"] = {"$type": monster["Drops"]["$type"], "$values": names} \
        if isinstance(monster.get("Drops"), dict) and "$type" in monster["Drops"] \
        else {"$values": names}


def fix_missing_gold(writing, said):
    changed = 0
    for path, monster in monster_files():
        key = (monster.get("Name"), monster.get("AreaID"))
        if key in MISSING_GOLD and not monster.get("Gold"):
            monster["Gold"] = MISSING_GOLD[key]
            write(path, monster, writing)
            changed += 1
    said.append(f"금화가 없던 괴물 {changed}개에 사냥터 동료 금액을 채웠다.")


def retune_item_drop_rates(writing, said):
    found = item_files()
    later_monsters = [m for _, m in monster_files() if m.get("AreaID") in LATER_ZONES]
    gear_names = set()
    junk_names = set()
    for m in later_monsters:
        for n in dropped_names(m):
            item = found.get(n)
            if item is None:
                continue
            if (item[1].get("EquipmentSlot") or 0) > 0:
                gear_names.add(n)
            else:
                junk_names.add(n)

    gear_changed = junk_changed = 0
    for name in gear_names:
        path, item = found[name]
        item["DropRate"] = GEAR_DROP_RATE
        write(path, item, writing)
        gear_changed += 1

    for name in junk_names:
        path, item = found[name]
        item["DropRate"] = round((item.get("DropRate") or 0) / 2, 4)
        write(path, item, writing)
        junk_changed += 1

    for name in DEFENSE_SUFFIX_ITEMS + ATTACK_ELEMENT_ITEMS:
        path, item = found[name]
        item["DropRate"] = SUFFIX_ELEMENT_DROP_RATE
        write(path, item, writing)

    for name in ["하급체력포션", "하급마력포션", "중급체력포션", "중급마력포션"]:
        path, item = found[name]
        item["DropRate"] = MANA_POTION_DROP_RATE if "마력" in name else POTION_DROP_RATE
        write(path, item, writing)

    said.append(
        f"기존 장비 {gear_changed}종 DropRate 를 {GEAR_DROP_RATE}(실제 3%대) 로, "
        f"잡템 {junk_changed}종은 절반으로 낮췄다. 접미사·속성 10종과 포션 4종의 DropRate 를 정했다."
    )


def add_suffix_and_elements(writing, said):
    changed = 0
    idx = 0
    for path, monster in sorted(monster_files(), key=lambda pm: str(pm[0])):
        tier = LATER_ZONES.get(monster.get("AreaID"))
        if tier is None:
            continue

        names = dropped_names(monster)
        atk = ATTACK_ELEMENT_ITEMS[idx % len(ATTACK_ELEMENT_ITEMS)]
        defn = DEFENSE_SUFFIX_ITEMS[idx % len(DEFENSE_SUFFIX_ITEMS)]
        idx += 1

        if atk in names and defn in names:
            continue

        for extra in (atk, defn):
            if extra not in names:
                names.append(extra)

        set_drops(monster, names)
        write(path, monster, writing)
        changed += 1

    said.append(f"우드랜드3-6·14·포테의숲1~6 괴물 {changed}마리에 접미사·속성 장비를 하나씩 얹었다.")


def add_potions(writing, said):
    changed = 0
    for path, monster in monster_files():
        area = monster.get("AreaID")
        tier = LATER_ZONES.get(area) or GROWTH_ZONES.get(area)
        if tier is None:
            continue

        names = dropped_names(monster)
        hp, mp = f"{tier}체력포션", f"{tier}마력포션"

        if hp in names and mp in names:
            continue

        for extra in (hp, mp):
            if extra not in names:
                names.append(extra)

        set_drops(monster, names)

        if area in GROWTH_ZONES and not (monster.get("LootType", 0) & LOOT_RANDOM):
            monster["LootType"] = monster.get("LootType", 0) | LOOT_RANDOM

        write(path, monster, writing)
        changed += 1

    said.append(f"포션(하급/중급 체력·마력)을 사냥터 {len(LATER_ZONES) + len(GROWTH_ZONES)}곳 괴물 {changed}마리에 얹었다.")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    args = parser.parse_args()

    said = []
    fix_missing_gold(args.writing, said)
    retune_item_drop_rates(args.writing, said)
    add_suffix_and_elements(args.writing, said)
    add_potions(args.writing, said)

    print(("[쓰기]" if args.writing else "[미리보기]"))
    for line in said:
        print("-", line)


if __name__ == "__main__":
    main()
