#!/usr/bin/env python3
"""수·토·풍·화 속성 무기·옷을 원작 표대로 만든다.

  python3 scripts/gen/items/build-elemental-gear.py          # 무엇이 생기는지만 본다
  python3 scripts/gen/items/build-elemental-gear.py --쓰기    # 아이템 템플릿에 적는다

**사용자 결정(2026-10-04)**: 무기·옷 모두, 원작 표 가격 그대로, 수오미에서 한꺼번에 판다(마을마다 찾아다니지 않게 —
원작·혼든은 마을마다 속성이 달랐다, `docs/elemental-shop-rotation.md`). 파는 쪽은 `build-town-gear-shops.py` 가 가이·아돌
물목에 이 변형을 더한다.

**무엇을 만드나** — 원작 표(`data/game-data/items-original-sheets.json` 수치표)에 기본형+수·토·풍·화가 다 있고, 기본형이
서버에 이미 있는 무기·갑옷. 기본형 템플릿을 그대로 베끼고(그림·자리·직업·레벨·내구·무게·동작) 표에서 셋만 바꾼다:
  - 판매가격 → Value (기본의 2·3·4·5배)
  - 무기: 공격력 `77m97` → DmgMin/DmgMax (+2씩)
  - 옷: 방어력(표는 음수) → AcModifer 절대값 (+1씩)
무기·옷의 수·토·풍·화는 **수치만** 바꾸고 속성을 주지 않는다 — 공격 속성은 목걸이, 방어 속성은 벨트(사용자 2026-10-07
「옷에 속성은 방어력 같은 수치에 영향을 주는거야 방어 속성이 아니라」). 10-04 에는 무기 OffenseElement·옷 DefenseElement 를 적었다.

**목걸이·벨트 속성도 적는다** — 이름 앞말(화염·바다·바람·대지·생명·암흑)로 목걸이 OffenseElement·벨트 DefenseElement.
그 밖의 칸은 건드리지 않는다.

기본형이 서버에 없는 표 줄(오렌○○·수오미○○ 같은 마을 접두 옷 등)은 만들지 않는다 — 그림 정본이 없다.
**지우지 않는다.** 이미 있는 변형 파일은 같은 규칙으로 다시 쓴다.
"""

import argparse
import copy
import json
import sys

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT
from lib._io import read_lenient_json as read

ITEMS = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server/templates/items"
SHEET = ROOT / "data/game-data/items-original-sheets.json"

#: ElementManager.Element
ELEMENT = {"수": 2, "토": 4, "풍": 3, "화": 1}
WEAPON, ARMOUR = 1, 2

#: 목걸이·벨트 이름 앞말 → 속성(사용자 2026-10-04: 암흑은 수토풍화 모두에 강하고 생명은 암흑에 강하다).
#: Dark 6 · Light 5 = 생명. 반지·씰 등 다른 자리는 원작처럼 속성을 갖지 않는다.
JEWEL_ELEMENT = {"화염의": 1, "바다의": 2, "바람의": 3, "대지의": 4, "생명의": 5, "암흑의": 6}
JEWEL_FIELD = {6: "OffenseElement", 11: "DefenseElement"}

MONSTERS = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server/templates/monsters"
#: 99레벨 사냥터 — 구광산(마인마을 워프 LevelRequired 99) · 카스마늄 갱도(드라코가 사는 5.99 갱도, 지금은 들어갈 길 없음).
DARK_GROUNDS = ("구광산", "카스마늄")
DARK = 6


def templates():
    out = {}
    for path in ITEMS.glob("*.json"):
        try:
            body = read(path)
        except ValueError:
            continue
        if isinstance(body, dict) and body.get("Name"):
            out[body["Name"]] = body
    return out


def variant(base, row, suffix):
    body = copy.deepcopy(base)
    body["Name"] = row["이름"]
    body["Value"] = int(row["판매가격"])
    if base["EquipmentSlot"] == WEAPON:
        lo, hi = row["공격력"].split("m")
        body["DmgMin"], body["DmgMax"] = int(lo), int(hi)
    else:
        body["AcModifer"] = {"$type": "Darkages.Types.StatusOperator, Darkages.Server",
                             "Option": 1, "Value": abs(int(row["방어력"]))}
    return body


def dark_monsters(writing):
    """99레벨 사냥터에서 드라코만큼 세거나 마법을 쓰는 괴물은 공격·방어 모두 암흑으로 박는다(사용자 2026-10-04).

    맵마다 드라코의 체력을 잣대로 삼는다 — 정의의 Level 칸은 모두 1이라 쓸 수 없다. 나머지 괴물은 생길 때 무작위
    (`Creations/monsters.cs`)."""
    by_map = {}
    for path in sorted(MONSTERS.rglob("*.json")):
        try:
            body = read(path)
        except ValueError:
            continue
        if isinstance(body, dict) and body.get("Name") and "@" in path.stem:
            by_map.setdefault(path.stem.split("@", 1)[1], []).append((path, body))
    out = []
    for area, mobs in sorted(by_map.items()):
        if not area.startswith(DARK_GROUNDS):
            continue
        draco = [b["MaximumHP"] for _, b in mobs if b["Name"].startswith("드라코")]
        for path, body in mobs:
            spells = body.get("SpellScripts") or {}
            spells = spells.get("$values", []) if isinstance(spells, dict) else spells
            if not (spells or (draco and body.get("MaximumHP", 0) >= min(draco))):
                continue
            out.append(f"{area} {body['Name']} 체력 {body.get('MaximumHP')}{' · 마법 ' + ','.join(spells) if spells else ''}")
            if body.get("ElementType") == 2 and body.get("OffenseElement") == DARK == body.get("DefenseElement"):
                continue
            body["ElementType"], body["OffenseElement"], body["DefenseElement"] = 2, DARK, DARK
            if writing:
                path.write_text(json.dumps(body, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return out


def main():
    parser = argparse.ArgumentParser(description="수·토·풍·화 속성 무기·옷")
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    args = parser.parse_args()

    rows = {r["이름"]: r for r in read(SHEET)["수치표"]}
    made, kinds = [], {WEAPON: 0, ARMOUR: 0}
    for name, base in sorted(templates().items()):
        if base.get("EquipmentSlot") not in kinds or not all(name + s in rows for s in ELEMENT):
            continue
        kinds[base["EquipmentSlot"]] += 1
        for suffix in ELEMENT:
            body = variant(base, rows[name + suffix], suffix)
            made.append(body)
            if args.writing:
                (ITEMS / f"{body['Name']}.json").write_text(
                    json.dumps(body, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    # 목걸이(공격)·벨트(방어)는 이름 앞말이 속성이다 — 템플릿에 속성 칸이 비어 있어 전투가 못 읽었다.
    jewels = {}
    for name, body in sorted(templates().items()):
        prefix = next((p for p in JEWEL_ELEMENT if name.startswith(p)), None)
        field = JEWEL_FIELD.get(body.get("EquipmentSlot"))
        if not prefix or not field or body.get(field) == JEWEL_ELEMENT[prefix]:
            continue
        body[field] = JEWEL_ELEMENT[prefix]
        jewels[name] = f"{field} {JEWEL_ELEMENT[prefix]}"
        if args.writing:
            (ITEMS / f"{name}.json").write_text(
                json.dumps(body, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    dark = dark_monsters(args.writing)

    print(f"기본형 무기 {kinds[WEAPON]} · 옷 {kinds[ARMOUR]} → 변형 {len(made)}종 · 목걸이·벨트 속성 {len(jewels)}종"
          f" · 암흑 괴물 {len(dark)}자리{'' if args.writing else ' (아직 안 씀 — --쓰기)'}")
    for line in dark:
        print(f"  암흑 {line}")
    for name, what in list(jewels.items())[:6]:
        print(f"  {name}: {what}")
    for body in made[:8]:
        print(f"  {body['Name']}: {body['Value']}골드 · 공격 {body.get('DmgMin', 0)}~{body.get('DmgMax', 0)}"
              f" · 방어 {body.get('AcModifer', {}).get('Value', 0)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
