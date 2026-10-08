#!/usr/bin/env python3
"""어둠템에 없는 센 팩 장비를 운영자만 사는 상인 한 명에게 모은다.

  python3 scripts/gen/items/build-operator-shop.py            # 무엇이 옮겨지는지만 본다
  python3 scripts/gen/items/build-operator-shop.py --쓰기      # 상인을 세우고 다른 상점·드랍에서 뺀다

**사용자 결정(2026-10-09)**: 장비 수치를 어둠템(`docs/items/어둠템#1~5.xlsx`)대로 되돌린 뒤에도(`build-gear-from-original.py`)
어둠템에 줄이 없어 되돌릴 근거가 없는 팩 장비 가운데 체력·마력을 1000 넘게 올리는 것이 남았다 — 전통한복(블랙팜상인, 체·마
15,000) · 2차 무기 Lev8·Lev18(노엠마을·아슬론, 체·마 1,000~3,000) · 적흑갑·월화이어·강화된세피라링(구할 곳 없음). 사용자:
「체력이 1000이상 오르는건 승급이후에나 나오고 잘 있지도 않아」 → 「5번장비만 취급하는 npc 따로 만들어둬 운영자 권한으로만 들어갈
수 있거나 구매할 수 있게해서」.

**고르는 것(「5번」)**: 입는 물건(`EquipmentSlot` > 0) · 이름이 어둠템에 없음 · 요구 레벨 99 밑 · 체력이나 마력 +1000 이상.
자료가 바뀌면(새 팩 장비 등) 다시 돌리면 따라간다.

**하는 일**
  1. `운영자상인@수오미방어구점#2,5` 를 세운다 — 스크립트 `operator_shop`(`scripts/Mundanes/OperatorShop.cs`): 운영자
     (`LoruleConfig.json` `GameMasters`)가 아니면 「운영자만 거래할 수 있습니다」. 앱의 일괄 사기·팔기도 같은 문으로 막는다
     (`GameServerHandlers` 일괄 거래). 수오미방어구점은 앱이 그리는 건물이라 운영자가 앱으로 들어가 살 수 있다 — 아돌(4,5) 옆 빈 칸.
  2. 다른 상점 물목과 괴물 드랍에서 그 이름을 뺀다(옮기기 — 사용자 지시).
  3. 길 안내(`guide.txt`)에는 싣지 않는다(`build-client-guide.py` 가 `operator_shop` 을 건너뛴다) — 손님 목록·미니맵·봇 동선에 안 나온다.
"""
import argparse
import json
import sys

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from graphify_runtime import configure_utf8_stdio

from lib._paths import ROOT
from lib._gear_original import modifier_of

configure_utf8_stdio(sys.stdout, sys.stderr)

SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MUNDANES = SERVER / "templates/mundanes"
MONSTERS = SERVER / "templates/monsters"
SHEET = ROOT / "data" / "game-data" / "items-original-sheets.json"

SCRIPT = "operator_shop"
NAME, AREA, AREA_NAME, X, Y = "운영자상인", 20357, "수오미방어구점", 2, 5
IMAGE = 16440  # 마시(구광산대기실)와 같은 그림 — 앱에 그림이 있고 옆의 아돌(16414)과 달라 헷갈리지 않는다
FLOOR = 1000   # 체력·마력 이만큼 넘게 올리면 「승급 이후에나 드물게」 수준


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, data, ending="\n"):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + ending, encoding="utf-8")


def ending_of(path):
    """원래 파일 끝 줄바꿈을 그대로 둔다 — 줄바꿈만 바뀐 diff 를 만들지 않는다."""
    return "\n" if path.read_bytes().endswith(b"\n") else ""


def chosen():
    rows = {row["이름"] for row in json.loads(SHEET.read_text(encoding="utf-8"))["수치표"]}
    picked = []
    for path in sorted(ITEMS.glob("*.json")):
        try:
            item = read(path)
        except json.JSONDecodeError:
            continue
        if not item.get("EquipmentSlot") or item["Name"] in rows or item.get("LevelRequired", 0) >= 99:
            continue
        if max(modifier_of(item, "HealthModifer"), modifier_of(item, "ManaModifer")) >= FLOOR:
            picked.append(item)
    return picked


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    picked = chosen()
    names = [item["Name"] for item in sorted(picked, key=lambda item: (item.get("EquipmentSlot"), item["Name"]))]
    wanted = set(names)
    print(f"운영자 상인 물목 {len(names)}종:")
    for item in sorted(picked, key=lambda item: item["Name"]):
        print(f"  {item['Name']} — 레벨 {item.get('LevelRequired')} · 체력 {modifier_of(item, 'HealthModifer'):+} · "
              f"마력 {modifier_of(item, 'ManaModifer'):+} · 방어 {modifier_of(item, 'AcModifer')}")

    mine = MUNDANES / f"{NAME}@{AREA_NAME}#{X},{Y}.json"
    for path in sorted(MUNDANES.glob("*.json")):
        if path == mine:
            continue
        npc = read(path)
        if npc.get("AreaID") == AREA and (npc.get("X"), npc.get("Y")) == (X, Y):
            raise SystemExit(f"{AREA_NAME} ({X},{Y}) 에 이미 {npc['Name']} 가 서 있다 — 자리를 고른다")
        stock = npc.get("DefaultMerchantStock") or []
        gone = [name for name in stock if name in wanted]
        if gone:
            print(f"  상점에서 뺀다 — {npc['Name']}: {', '.join(gone)}")
            npc["DefaultMerchantStock"] = [name for name in stock if name not in wanted]
            if writing:
                write(path, npc, ending_of(path))

    for path in sorted(MONSTERS.rglob("*.json")):
        try:
            monster = read(path)
        except json.JSONDecodeError:
            continue
        drops = monster.get("Drops") or []
        gone = [name for name in drops if name in wanted]
        if gone:
            print(f"  드랍에서 뺀다 — {monster.get('Name')} ({path.parent.name}): {', '.join(gone)}")
            monster["Drops"] = [name for name in drops if name not in wanted]
            if writing:
                write(path, monster, ending_of(path))

    body = {
        "Name": f"{NAME}@{AREA_NAME}#{X},{Y}", "AreaID": AREA, "X": X, "Y": Y, "Direction": 2, "Image": IMAGE,
        "Level": 1, "MaximumHp": 1000, "MaximumMp": 1000,
        "Speech": [f"{NAME}: 운영자 전용 물건입니다."],
        "ScriptKey": SCRIPT, "DefaultMerchantStock": names,
        "EnableWalking": False, "EnableTurning": False, "EnableAttacking": False, "EnableCasting": False,
        "WalkRate": 0, "TurnRate": 0, "CastRate": 0, "ChatRate": 0, "PathQualifer": 1, "ViewingQualifer": 1,
    }
    same = mine.exists() and read(mine) == body
    if writing and not same:
        write(mine, body)
    print(f"{mine.name}: {'그대로' if same else ('적었다' if writing else '미리보기 — --쓰기 로 적는다')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
