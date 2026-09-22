#!/usr/bin/env python3
"""괴물이 떨구는 금화를 5.99 팩 값으로 맞추고, 주워서 팔 잡템에 값을 붙인다.

  python3 scripts/build-pack-gold.py            # 무엇이 바뀌는지만 본다
  python3 scripts/build-pack-gold.py --쓰기      # 서버 정의에 적는다

## 금화 — 팩에 그대로 적혀 있다

5.99 팩의 괴물 정의는 `골드 <액수> <확률%>` 한 줄을 갖는다
(`data/server-packs/5.99-server/db/mob/Novice/Novice_Monster.txt` — 팜팻1 은 `골드 20 30`).
둘째 칸이 확률인 것은 243마리에서 그 값이 20·30·40·50·60·100 여섯 가지뿐이고
첫째 칸은 20 에서 500,000 까지 흩어지는 것으로 안다.

하데스는 이 칸이 없어 `Random(Level*500, Level*1000)` 으로 만들고 있었다
(`database/server/scripts/Formulas/monsterexp.cs`). **하데스의 괴물 정의 568개는 모두 `Level 1`** 이라
세상의 모든 괴물이 한 마리에 500~999 전을 냈다 — 레더튜닉이 300전이다.

팩에 이름은 있는데 `골드` 줄이 없는 정의(사슴·바크·킹아크퍼스 …)는 **0 을 적는다.** 팩의 침묵이
그 뜻이다 — 5.99 파서에는 안 적힌 골드를 만들어 주는 자리가 없다. 팩에 이름조차 없는 정의는
건드리지 않고 아래에 이름을 적어 준다(그 길로 가면 레벨 식이 그대로 쓰인다).

## 잡템 값 — 팩에 없어 팩의 버릇을 따랐다

**세 팩 어디에도 이 잡템들의 판매가격이 없다.** 5.99 는 전부 `판매가격 0`, 혼든은 칸 자체가 없고,
Novaonline 은 몇 가지만 적어 두었다. 원작 도감(어둠템#1~5)과 원작 아카이브(`ItemInfo0~11`)에는
가격 칸 자체가 없다(`docs/where-the-answers-are.md`).

그래서 **팩이 값을 적은 곳의 버릇**을 따른다. 5.99 가 재료에 값을 적을 때는 거의 언제나 100전이다:

- `item/Armor/재료.txt` — 41종 중 **38종이 100**
- `item/E.T.C.txt` — 값을 적은 재료 전부가 100 (엔트라이온의몸통·트랜트의뿌리·엔트자이언트의날개·
  은빛늑대의갈기털·킹아크퍼스의팬던트)
- Novaonline 도 같은 100 을 쓴다 (그린/레드/옐로우/퍼플팜팻의알·엔트자이언트의몸통)

팩 둘이 같은 값을 쓰므로 100 을 쓴다. **정확한 근거가 아니라 팩의 버릇**이고, 값이 나오면 그 값으로 바꾼다.

이미 값이 붙어 있는 것은 건드리지 않는다. 장비(`EquipmentSlot`)도 건드리지 않는다 — 장비 값은
팩 셋이 열 배씩 어긋나 있어(레더튜닉 300 / 950 / 5,000) 따로 정해야 한다.
"""

import argparse
import collections
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MONSTERS = SERVER / "templates/monsters"
PACK = ROOT / "data/server-packs/extracted/5.99-server"

# 팩이 재료에 값을 적을 때 쓰는 값. 위 설명 참고.
MATERIAL_VALUE = 100

# 상점이 물건을 사 주는 값 (`scripts/Mundanes/shop1.cs` 의 `Value / 1.6`).
SHOP_OFFER = 1.6

# `LootQualifer`. 34 = Gold|Random · 36 = Gold|Table · 2 = Random · 4 = Table.
LOOT_RANDOM = 2
LOOT_TABLE = 4

# 한 번에 표에서 뽑는 횟수의 상한 (`Lorule.Config/LoruleConfig.json` 의 LootTableStackSize).
LOOT_STACK = 3

# 옷 한 벌 값. 5.99 팩의 레더튜닉이다 — 몇 마리를 잡아야 하는지 세는 잣대로만 쓴다.
TUNIC = 300

# 셈을 보여 줄 사냥터.
GROUNDS = {
    "노비스평원A": 20393,
    "노비스평원B": 20394,
    "노비스지하던전A1": 20380,
    "우드랜드1-1": 20015,
    "포테의숲1존": 20263,
}


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, data, writing):
    if writing:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def monsters():
    """읽을 수 있는 괴물 정의 전부. minions/minion.json 은 JSON 이 아니라 건너뛴다."""
    for path in sorted(MONSTERS.rglob("*.json")):
        try:
            yield path, read(path)
        except json.JSONDecodeError:
            continue


def items():
    found = {}
    for path in sorted(ITEMS.rglob("*.json")):
        try:
            item = read(path)
        except json.JSONDecodeError:
            continue
        name = item.get("Name") or path.stem
        found[name] = (path, item)
    return found


def dropped(monster):
    drops = monster.get("Drops")
    values = drops.get("$values") if isinstance(drops, dict) else drops
    return [name for name in (values or []) if name and name != "random"]


def pack_gold():
    """5.99 괴물 이름 → (액수, 확률). 골드 줄이 없으면 (0, 100)."""
    table = {}
    for mob in json.loads((PACK / "mobs.json").read_text(encoding="utf-8")):
        gold = mob["fields"].get("골드")
        table[mob["이름"]] = (int(gold[0]), int(gold[1])) if gold else (0, 100)
    return table


def set_gold(writing, said):
    table = pack_gold()
    written = 0
    silent = 0
    unknown = collections.Counter()

    for path, monster in monsters():
        name = monster.get("Name")

        if name not in table:
            unknown[name] += 1
            continue

        amount, chance = table[name]

        if monster.get("Gold") == amount and monster.get("GoldChance") == chance:
            continue

        monster["Gold"] = amount
        monster["GoldChance"] = chance
        write(path, monster, writing)
        written += 1
        silent += 1 if amount == 0 else 0

    said.append(
        f"괴물 정의 {written} 개에 5.99 의 금화를 적었다 "
        f"(그중 {silent} 개는 팩에 `골드` 줄이 없어 0)."
    )

    if unknown:
        said.append(
            "팩에 이름이 없어 그냥 둔 것 "
            f"{sum(unknown.values())}개: {', '.join(sorted(unknown))} — 레벨 식이 그대로 쓰인다."
        )


def price_junk(writing, said):
    found = items()
    names = sorted({name for _, monster in monsters() for name in dropped(monster)})
    priced, gear, kept, missing = [], [], 0, []

    for name in names:
        if name not in found:
            missing.append(name)
            continue

        path, item = found[name]

        if (item.get("EquipmentSlot") or 0) > 0:
            gear.append(name)
            continue

        if (item.get("Value") or 0) > 0:
            kept += 1
            continue

        item["Value"] = MATERIAL_VALUE
        write(path, item, writing)
        priced.append(name)

    said.append(
        f"괴물이 떨구는 이름 {len(names)} 가지 가운데 값이 없던 잡템 {len(priced)} 가지에 "
        f"{MATERIAL_VALUE} 전을 붙였다 (상점이 {int(MATERIAL_VALUE / SHOP_OFFER)} 전에 사 준다). "
        f"이미 값이 있던 것 {kept} 가지·장비 {len(gear)} 가지는 그대로 둔다."
    )
    said.append("  " + " · ".join(priced))

    if gear:
        said.append("  장비라 그냥 둔 것: " + " · ".join(gear))

    if missing:
        said.append("  템플릿이 없는 이름(떨어지지 않는다): " + " · ".join(missing))


def earnings(said):
    """한 마리에 얼마를 벌고, 옷 한 벌에 몇 마리인가.

    정의에 적힌 것을 읽는다 — 그래서 `--쓰기` 전에는 고치기 전 값이, 뒤에는 고친 값이 나온다.
    """
    found = items()

    said.append("")
    said.append(f"한 마리에 얼마인가 (레더튜닉 {TUNIC} 전 기준)")
    said.append(
        f"{'사냥터':<16}{'괴물':<12}{'금화':>10}{'잡템':>8}{'장비':>8}{'시약':>8}{'합':>8}"
        f"{'옷 한 벌':>10}"
    )

    for ground, area in GROUNDS.items():
        rows = [m for _, m in monsters() if m.get("AreaID") == area]

        for monster in sorted(rows, key=lambda m: m.get("Name") or ""):
            if monster.get("Gold") is None:
                # 정의가 안 적으면 서버는 Random(Level*500, Level*1000) 으로 만든다 — 그 가운데값.
                coins = monster.get("Level", 1) * 750
            else:
                coins = monster["Gold"] * (monster.get("GoldChance") or 100) / 100

            loot = monster.get("LootType") or 0
            drops = dropped(monster)
            junk = potion = gear = 0.0

            for name in drops:
                if name not in found:
                    continue

                _, item = found[name]
                rate = item.get("DropRate") or 0

                if loot & LOOT_RANDOM:
                    # 목록에서 하나를 같은 확률로 고르고 그 물건의 DropRate 를 한 번 굴린다.
                    odds = rate / len(drops)
                elif loot & LOOT_TABLE:
                    # 표를 0~2번 굴린다 — 평균 한 번.
                    odds = rate * (LOOT_STACK - 1) / 2 / max(len(drops), 1)
                else:
                    odds = 0

                worth = odds * int((item.get("Value") or 0) / SHOP_OFFER)

                if (item.get("EquipmentSlot") or 0) > 0:
                    gear += worth
                elif str(item.get("Group") or "").endswith(("/시약", "/물약")):
                    potion += worth
                else:
                    junk += worth

            total = coins + junk
            many = TUNIC / total if total > 0 else 0
            kills = "못 산다" if total <= 0 else f"{many:.1f}마리" if many < 10 else f"{many:.0f}마리"

            said.append(
                f"{ground:<16}{monster.get('Name'):<12}{coins:>10.1f}{junk:>8.1f}"
                f"{gear:>8.1f}{potion:>8.1f}{total:>8.1f}{kills:>10}"
            )

    said.append(
        "  「합」은 금화 + 잡템 판 값이다. 시약은 쓰는 물건이고 장비는 입는 물건이라 따로 세고 합에 넣지 않았다."
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    said = []
    set_gold(writing, said)
    price_junk(writing, said)
    earnings(said)

    print("\n".join(said))
    print("\n" + ("적었습니다." if writing else "미리 본 것입니다 — 적으려면 --쓰기"))


if __name__ == "__main__":
    sys.exit(main())
