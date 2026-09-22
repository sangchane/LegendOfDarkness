#!/usr/bin/env python3
"""노바온라인 팩의 무도가 너클 사다리에서 1~25레벨 두 칸을 하데스 아이템 템플릿으로 들인다.

  python3 scripts/build-nova-knuckles.py          # 무엇이 바뀌는지만 본다
  python3 scripts/build-nova-knuckles.py --쓰기    # 아이템 템플릿에 적는다

**왜 필요한가.** 무도가가 1~20레벨에 들 수 있는 무기가 공용 에페(15~20) 하나뿐이었다. 5.99 상점 47개에
`무도가무기` 목록 자체가 없고, 서버의 무도가 전용 무기 6개 중 25레벨 이하는 `용의발톱` 하나인데 그것은
`Value 0`·피해 180~200 이라 쓸 수 없다(5.99 의 오기로 보인다 — 노바는 같은 이름을 99레벨로 적었다).

**제대로 된 너클 사다리는 노바 팩에만 있다** — `item/armor/LCT.txt` 에 글러브1(1레벨) · 견습자의글러브(11) ·
숙련자의글러브(41) · 지존의글러브(71). 이번에 들이는 것은 **1~25레벨에 해당하는 아래 두 칸뿐**이다
(사용자 결정 2026-09-23).

**값은 어디서 왔나** (사용자 결정: 팩 값을 쓰되 원작 도감에 같은 이름이 있으면 도감이 정본):
  - **원작 도감에 둘 다 없다** — `data/game-data/items-original-sheets.json`(어둠템#1~5)에도
    원작 아카이브 `data/game-data/items.json`(ItemInfo0~11)에도 이 이름이 없다. 그래서 **팩 값**을 쓴다.
  - 피해·내구력·체력마력변화·그림·공격모션(132, 도복과 같은 맨주먹 동작)은 **노바 팩 값 그대로**.
  - **가격만 팩 값을 못 쓴다.** 노바는 넷 다 `판매가격 0` 이고, 0 이면 shop1 이 공짜로 내준다
    (`shop1.cs:149` — `GoldPoints >= Value`). 그래서 값은 **5.99 표의 같은 레벨 무기 값**을 쓴다:
    1레벨 500(에페·설단검·매직마르시아·홀리마르시아 넷이 모두 500) · 11레벨 1500(커틀라스. 5.99 의
    11레벨 무기는 커틀라스 1500 과 중단검 4000 둘뿐이라 낮은 쪽). **이 두 숫자만 우리가 고른 것이다.**

**칸을 잇는 법은 `scripts/build-pack-equipment.py` 그대로다** — 같은 규칙을 두 번 적으면 어긋나므로
그 생성기의 `template()` 을 그대로 불러 쓰고, `Group` 과 `Value` 만 여기서 덮는다.

**덮어쓰지 않는다.** 같은 이름의 템플릿이 이미 있고 그것이 이 생성기가 쓴 것이 아니면 멈춘다.
"""

import argparse
import importlib.util
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))

PACK = ROOT / "data/server-packs/extracted/novaonline/items.json"
OUT = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server/templates/items"
SHEETS = ROOT / "data/game-data/items-original-sheets.json"
ARCHIVE = ROOT / "data/game-data/items.json"

#: 이 생성기가 쓴 템플릿이라는 표시. 5.99 팩에서 온 것(`5.99표/…`)과 섞이지 않는다.
GROUP = "노바표/무기/LCT"

#: 들일 것과 그 값. 값만 5.99 표에서 왔다(위 docstring).
WANTED = {"글러브1": 500, "견습자의글러브": 1500}


def pack_equipment():
    """`build-pack-equipment.py` 를 모듈로 읽는다 — 파일 이름에 점·빼기가 있어 그냥은 import 가 안 된다."""
    path = ROOT / "scripts" / "build-pack-equipment.py"
    spec = importlib.util.spec_from_file_location("build_pack_equipment", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def named(path, name):
    """원작 자료에 그 이름이 있나. 없으면 팩 값이 정본이라는 근거가 된다."""
    if not path.exists():
        return False
    return f'"{name}"' in path.read_text(encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description="노바 팩의 무도가 너클 두 종을 들인다")
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    args = parser.parse_args()

    equipment = pack_equipment()
    items = json.loads(PACK.read_text(encoding="utf-8"))

    trouble = []
    for name, value in WANTED.items():
        found = [one for one in items if one.get("이름") == name]
        if len(found) != 1:
            trouble.append(f"노바 팩에 {name} 가 {len(found)}개다")
            continue

        kind, why = equipment.classify(found[0]["fields"])
        if kind != "무기":
            trouble.append(f"{name} 가 무기로 읽히지 않는다({why or kind})")
            continue

        body = equipment.template(found[0])
        body["Group"] = GROUP
        packed = body["Value"]
        body["Value"] = value

        out = OUT / f"{name}.json"
        before = json.loads(out.read_text(encoding="utf-8-sig")) if out.exists() else None
        if before is not None and (before.get("Group") or "") != GROUP:
            trouble.append(f"{name} 템플릿이 이미 있고 이 생성기가 쓴 것이 아니다({before.get('Group')})")
            continue

        if args.writing:
            out.write_text(json.dumps(body, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

        print(f"\n■ {name}  {'고침' if before else '새로 씀'}{'' if args.writing else ' (아직 안 씀 — --쓰기)'}")
        print(f"   {body['LevelRequired']}레벨 무도가(Class {body['Class']}) · 피해 {body['DmgMin']}~{body['DmgMax']}"
              f" · 내구력 {body['MaxDurability']} · 착용그림 {body['Image']} · 평타동작 {body.get('AttackMotion')}")
        print(f"   값 {body['Value']}골드 — 노바 팩은 {packed} 이라 못 쓴다(공짜로 나간다). 5.99 표의 "
              f"{body['LevelRequired']}레벨 무기 값을 쓴다")
        print(f"   원작 도감에 있나 {named(SHEETS, name)} · 원작 아카이브에 있나 {named(ARCHIVE, name)}"
              f" → 없으니 나머지 칸은 노바 팩 값 그대로")

    if trouble:
        print("\n막는 것:")
        for line in trouble:
            print("  -", line)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
