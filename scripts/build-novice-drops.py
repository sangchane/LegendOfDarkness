#!/usr/bin/env python3
"""노비스 지역 괴물이 시약과 저레벨 직업 장비를 떨구게 한다.

  python3 scripts/build-novice-drops.py            # 무엇이 바뀌는지만 본다
  python3 scripts/build-novice-drops.py --쓰기      # 서버 정의에 적는다

**쿠룸과 마라디움은 원작 시약이고 5.99 팩에는 없다.** 값은 혼든 팩의 정의를 그대로 쓴다
(`data/server-packs/honden-community/db/item/아이템/포션.txt` — 쿠룸 체력 +250·300전, 마라디움 마력 +100·1000전).
그림 번호는 원작 번호 + 32768 이다(코마디움 46 → 32814 로 확인).

**떨어질 확률은 두 값의 곱이다.** 하데스는 괴물의 `Drops` 에서 **하나를 고르고**(같은 확률) 그 물건의
`DropRate` 를 굴린다(`database/server/scripts/Formulas/monsterexp.cs` DetermineRandomDrop). 그래서 목록이
넷이면 쿠룸이 나올 확률은 1/4 × DropRate 다. 아래 출력은 그 곱을 적어 준다.
"""

import argparse
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MONSTERS = SERVER / "templates/monsters/5.99"

# 노비스 지역 맵. 마을(20373)에는 주민만 있어 빼고, 사냥하는 곳만 넣는다.
NOVICE_MAPS = {20393: "평원A", 20394: "평원B"} | {20380 + n: f"지하던전{'ABC'[n // 3]}{n % 3 + 1}" for n in range(9)}

# 시약. 값은 혼든 팩(원작 이름을 쓴 유일한 팩)에서 왔다.
POTIONS = {
    "쿠룸": {"DisplayImage": 32813, "HealthRestore": 250, "Value": 300, "DropRate": 0.80},
    "마라디움": {"DisplayImage": 32815, "ManaRestore": 100, "Value": 1000, "DropRate": 0.50},
}

# 한 칸에 쌓을 수 있는 양. 요즘 게임처럼 넉넉히 든다(사용자, 2026-09-18).
BUNDLE = 1000

# 저레벨 직업 장비. 직업마다 무기 하나와 옷 하나 — 괴물마다 돌려 가며 붙인다.
# (직업 번호는 서버의 ClassType: 1 전사 · 2 도적 · 3 마법사 · 4 성직자 · 5 무도가)
CLASS_GEAR = {
    1: ["커틀라스", "레더튜닉"],
    2: ["설단검", "스카웃튜닉"],
    3: ["매직마르시아", "매직스커트"],
    4: ["홀리마르시아", "로브"],
    5: ["용의발톱", "도복"],
}

# 장비가 나올 확률(목록에서 뽑힐 확률과 곱해진다).
GEAR_RATE = 0.10

DROPS_TYPE = "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, data, writing):
    if writing:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def make_potions(writing, said):
    for name, spec in POTIONS.items():
        path = ITEMS / f"{name}.json"
        item = read(path) if path.exists() else {"$type": "Darkages.Types.ItemTemplate, Darkages.Server"}

        item.update(
            {
                "Name": name,
                "DisplayImage": spec["DisplayImage"],
                "Flags": 444,
                "CanStack": True,
                "MaxStack": BUNDLE,
                "Value": spec["Value"],
                "ScriptName": "Consumable",
                "DropRate": spec["DropRate"],
                "Group": "원작표/시약",
            }
        )

        for restore in ("HealthRestore", "ManaRestore"):
            if restore in spec:
                item[restore] = spec[restore]

        write(path, item, writing)
        said.append(
            f"시약 {name}: 그림 {spec['DisplayImage']} · "
            f"{'체력' if 'HealthRestore' in spec else '마력'} "
            f"+{spec.get('HealthRestore') or spec.get('ManaRestore')} · 한 칸 {BUNDLE} 개"
        )


def bundle_potions(writing, said):
    changed = 0

    for path in ITEMS.glob("*.json"):
        item = read(path)
        group = str(item.get("Group") or "")

        if not (group.endswith("/물약") or group.endswith("/시약")) or item.get("MaxStack") == BUNDLE:
            continue

        item["CanStack"] = True
        item["MaxStack"] = BUNDLE
        write(path, item, writing)
        changed += 1

    said.append(f"시약·물약 {changed} 가지를 한 칸에 {BUNDLE} 개까지 쌓게 했다")


def rate_gear(writing, said):
    for pieces in CLASS_GEAR.values():
        for piece in pieces:
            path = ITEMS / f"{piece}.json"

            if not path.exists():
                said.append(f"  없는 장비: {piece}")
                continue

            item = read(path)
            item["DropRate"] = GEAR_RATE
            write(path, item, writing)


def spread_drops(writing, said):
    monsters = []

    for path in sorted(MONSTERS.glob("*.json")):
        monster = read(path)
        if monster.get("AreaID") in NOVICE_MAPS:
            monsters.append((path, monster))

    gear = [piece for pieces in CLASS_GEAR.values() for piece in pieces]
    lines = []

    for index, (path, monster) in enumerate(monsters):
        drops = list((monster.get("Drops") or {}).get("$values") or [])
        piece = gear[index % len(gear)]

        drops += [name for name in POTIONS if name not in drops]

        if piece not in drops:
            drops.append(piece)

        monster["Drops"] = {"$type": DROPS_TYPE, "$values": drops}

        # 돈과 함께 목록에서 하나를 굴린다.
        monster["LootType"] = 34
        write(path, monster, writing)

        lines.append(
            f"  {monster['Name']}@{NOVICE_MAPS[monster['AreaID']]}: {' · '.join(drops)}"
            f"  (쿠룸 {100 * POTIONS['쿠룸']['DropRate'] / len(drops):.0f}%"
            f" · 마라디움 {100 * POTIONS['마라디움']['DropRate'] / len(drops):.0f}%"
            f" · {piece} {100 * GEAR_RATE / len(drops):.0f}%)"
        )

    said.append(f"노비스 괴물 {len(monsters)} 마리에 시약과 직업 장비를 붙였다")
    said.extend(lines)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    said = []
    make_potions(writing, said)
    bundle_potions(writing, said)
    rate_gear(writing, said)
    spread_drops(writing, said)

    print("\n".join(said))
    print("\n" + ("적었습니다." if writing else "미리 본 것입니다 — 적으려면 --쓰기"))


if __name__ == "__main__":
    sys.exit(main())
