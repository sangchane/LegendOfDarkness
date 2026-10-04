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
그리고 속성 — 무기는 OffenseElement, 옷은 DefenseElement (수=Water 2 · 토=Earth 4 · 풍=Wind 3 · 화=Fire 1).
지금 하데스 전투는 목걸이·벨트 속성만 읽는다. 무기·옷 속성을 피해에 쓰는 것은 전투 리뉴얼에서 한다.

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
        body["OffenseElement"] = ELEMENT[suffix]
    else:
        body["AcModifer"] = {"$type": "Darkages.Types.StatusOperator, Darkages.Server",
                             "Option": 1, "Value": abs(int(row["방어력"]))}
        body["DefenseElement"] = ELEMENT[suffix]
    return body


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

    print(f"기본형 무기 {kinds[WEAPON]} · 옷 {kinds[ARMOUR]} → 변형 {len(made)}종"
          f"{'' if args.writing else ' (아직 안 씀 — --쓰기)'}")
    for body in made[:8]:
        print(f"  {body['Name']}: {body['Value']}골드 · 공격 {body.get('DmgMin', 0)}~{body.get('DmgMax', 0)}"
              f" · 방어 {body.get('AcModifer', {}).get('Value', 0)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
