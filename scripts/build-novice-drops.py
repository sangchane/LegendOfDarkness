#!/usr/bin/env python3
"""노비스 지역 괴물이 시약을 떨구게 한다.

  python3 scripts/build-novice-drops.py            # 무엇이 바뀌는지만 본다
  python3 scripts/build-novice-drops.py --쓰기      # 서버 정의에 적는다

**쿠룸과 마라디움은 원작 시약이다.** 회복량과 그림 번호는 혼든 팩의 정의를 그대로 쓴다
(`data/server-packs/honden-community/db/item/아이템/포션.txt` — 쿠룸 체력 +250, 마라디움 마력 +100).
그림 번호는 원작 번호 + 32768 이다(원작 도감 설명표의 쿠룸 45 → 32813 · 마라디움 47 → 32815).

**값은 우리가 정했다 — 근거가 없어서다**(2026-09-23). 자료 출처를 위에서부터 훑었다:
  1. 원작 도감(`data/game-data/items-original-sheets.json`) — **수치표에 없다.** 수치표 5,722줄은 장비만
     담는다(사과·뱀고기·포션도 한 줄이 없다). 쿠룸·마라디움은 **설명표**에만 있고 거기에는 값 칸이 없다.
  2. 원작 아카이브 `ItemInfo0~11` — 영문 이름표라 이 둘이 없다.
  3. 서버팩 셋 — **혼든만** 적었다(쿠룸 300 · 마라디움 1,000). 5.99 와 Novaonline 은 이 시약을 아예
     안 싣는다. **셋이 일치하지 않으니 팩 값을 쓰지 않는다**(`AGENTS.md` 자료 출처 우선순위 4).
그래서 **혼든의 비(쿠룸 : 마라디움 = 3 : 10)는 그대로 두고 값만 절반으로 내렸다** — 쿠룸 150 · 마라디움 500.
2026-09-24 사용자: 마라디움이 초반 벌이의 절반을 넘으니 더 낮춘다 — 마라디움만 250(쿠룸은 150 그대로).
잣대는 사용자가 준 「**첫 옷까지 열 마리 안팎**」이다. 혼든 값 그대로면 노비스 한 마리 벌이가 170전이라
레더튜닉(950전)까지 **5.6마리**였고, 마라디움 한 가지가 벌이의 61% 를 냈다. 절반으로 내리면 **10.2마리**다
(표는 `python3 scripts/build-pack-gold.py`).

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

# 시약. 회복량·그림은 혼든 팩(원작 이름을 쓴 유일한 팩)에서 왔고, **값은 혼든의 절반**이다 — 위 머리글 참고.
POTIONS = {
    "쿠룸": {"DisplayImage": 32813, "HealthRestore": 250, "Value": 150, "DropRate": 0.80},
    "마라디움": {"DisplayImage": 32815, "ManaRestore": 100, "Value": 250, "DropRate": 1.00},  # 2026-09-26 두 배(0.50 → 1.00, 사용자 "마력 포션 드랍률 좀 높이고")
}

# 한 칸에 쌓을 수 있는 양. 요즘 게임처럼 넉넉히 든다(사용자, 2026-09-18).
BUNDLE = 1000

# **장비는 여기서 붙이지 않는다.** 저레벨 괴물은 잡템만 떨구고, 장비는 그 위 사냥터에서 낮은 확률로
# 나온다(사용자, 2026-09-23). 어느 사냥터에 어느 장비가 걸리는지는 `scripts/build-gear-drops.py` 가 정한다.

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


def spread_drops(writing, said):
    monsters = []

    for path in sorted(MONSTERS.glob("*.json")):
        monster = read(path)
        if monster.get("AreaID") in NOVICE_MAPS:
            monsters.append((path, monster))

    lines = []

    for path, monster in monsters:
        drops = list((monster.get("Drops") or {}).get("$values") or [])
        drops += [name for name in POTIONS if name not in drops]

        monster["Drops"] = {"$type": DROPS_TYPE, "$values": drops}

        # 돈과 함께 목록에서 하나를 굴린다.
        monster["LootType"] = 34
        write(path, monster, writing)

        lines.append(
            f"  {monster['Name']}@{NOVICE_MAPS[monster['AreaID']]}: {' · '.join(drops)}"
            f"  (쿠룸 {100 * POTIONS['쿠룸']['DropRate'] / len(drops):.0f}%"
            f" · 마라디움 {100 * POTIONS['마라디움']['DropRate'] / len(drops):.0f}%)"
        )

    said.append(f"노비스 괴물 {len(monsters)} 마리에 시약을 붙였다")
    said.extend(lines)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    said = []
    make_potions(writing, said)
    bundle_potions(writing, said)
    spread_drops(writing, said)

    print("\n".join(said))
    print("\n" + ("적었습니다." if writing else "미리 본 것입니다 — 적으려면 --쓰기"))


if __name__ == "__main__":
    sys.exit(main())
