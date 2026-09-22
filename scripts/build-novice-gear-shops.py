#!/usr/bin/env python3
"""노비스마을 무기방어구상점에 장비를 파는 NPC 둘을 세운다.

  python3 scripts/build-novice-gear-shops.py          # 무엇이 바뀌는지만 본다
  python3 scripts/build-novice-gear-shops.py --쓰기    # 서버 정의에 적는다

**왜 필요한가.** 세상에 1차 직업 무기·갑옷을 파는 NPC 가 한 명도 없다. 상점 틀은 이미 다 돈다
(`database/server/scripts/Mundanes/shop1.cs` 가 사기·팔기·수리를 하고 `DefaultMerchantStock` 만 읽는다).
5.99 팩에 팔 목록 9개(`전사무기`·`도적갑옷사기` …)가 그대로 있는데 **그 목록을 여는 NPC 가 팩 안에 없어서**
들여올 때 통째로 빠졌다(`plans/5.99-상점결합.tsv` 의 미결합 25개).

**어느 팩에서 무엇을 가져오나** (사용자 결정 2026-09-23):
  - **무엇을 파나 → 5.99 팩.** `data/server-packs/extracted/5.99-server/shops.json` 의 9개 목록 그대로.
  - **1~20레벨 옷은 → 혼든 팩.** 5.99 갑옷 목록은 **전부 21레벨부터**라 1~20레벨이 벗고 다녀야 했다.
    혼든 `노비스마을_shop.txt` 의 드보이 목록(레더튜닉·도복·스카웃튜닉 같은 원작 옷)을 더한다. 델란 목록도
    같은 자리에서 더한다 — 서버 아이템 템플릿에 있는 것만 넣으므로 끊긴 참조는 늘 0이다.
  - **어디서 누가 파나 → 혼든 팩.** `data/server-packs/honden-community/db/npc/마이소시아/노비스마을_{npc,spawn}.txt`
    의 `델란`(무기)·`드보이`(갑옷) — 이름·그림 번호·인사말·맵·좌표.
  - **무도가 너클은 → 노바 팩.** `글러브1`(1레벨)·`견습자의글러브`(11레벨)를
    `scripts/build-nova-knuckles.py` 가 아이템 템플릿으로 들이고, 여기서 델란 목록에 넣는다.
  - 값은 아이템 템플릿의 `Value` 를 그대로 쓴다(5.99 `판매가격`). 이 생성기는 아이템을 고치지 않는다.

**상인은 계산대 뒤에 선다.** 델란(3,7)·드보이(6,2) 가 선 칸은 문(9,18)에서 걸어서 닿지 않는다 — 벽 자료가
틀린 것이 아니라 원작이 그런 꼴이다. 서버의 상점 12곳이 하나같이 그렇고(상인과 손님이 설 수 있는 가장
가까운 칸 사이가 전부 3칸), 5.99·혼든·노바 세 팩이 모두 상인을 그 안쪽에 세웠다. 서버는 NPC 를 누를 때
거리를 재지 않고(`GameServerHandlers.Format43Handler`) 12칸까지 보이므로(`WithinRangeProximity`)
계산대 앞에서 말이 걸린다. 걸어 들어가 사는 것은 `NoviceGearShopTests` 가 실제로 확인한다.

**어긋난 곳은 어긋난 대로 적는다.** 5.99 는 같은 상점방(노비스무기방어구상점 20375)에 `스웨인` 쌍둥이를
(7,2)·(2,5) 에 세워 두었지만 파는 것이 없다(`pack_speaker`). 혼든의 드보이 자리가 하필 (7,2) 라 겹친다 —
스웨인을 지우지 않고 한 칸 옆(6,2)에 세운다. 자세한 것은 `SHOPS` 의 주석.

**지우지 않는다.** 이 생성기는 자기가 쓰는 두 파일만 만들고 고친다. 다른 NPC·아이템은 읽기만 한다.
"""

import argparse
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
AREAS = SERVER / "areas"
ITEMS = SERVER / "templates/items"
MUNDANES = SERVER / "templates/mundanes"

PACK599_SHOPS = ROOT / "data/server-packs/extracted/5.99-server/shops.json"
HONDEN = ROOT / "data/server-packs/honden-community/db/npc/마이소시아"
HONDEN_NPC = HONDEN / "노비스마을_npc.txt"
HONDEN_SPAWN = HONDEN / "노비스마을_spawn.txt"
HONDEN_SHOP = HONDEN / "노비스마을_shop.txt"

#: NPC 그림은 괴물과 같은 번호 체계로 간다(ServerFormat07). `tools/pack-import/import.py` 의 MONSTER_IMAGE_BASE 와 같은 값이다.
SPRITE_BASE = 0x4000

#: shop1.cs 가 사기·팔기·수리를 다 한다. 붙이는 것은 이 이름 하나뿐 — 코드는 짜지 않는다.
SHOP_SCRIPT = "shop1"

#: 1차 직업 동선. 다섯 직업이 이 구간에 살 것이 있어야 한다.
EARLY_LEVEL = 25

#: **여기가 실제로 막히던 자리다.** 5.99 갑옷은 전부 21레벨부터라 1~20레벨이 입을 것이 없었다.
#: 다섯 직업 모두 이 레벨 전에 입고 들 것이 있어야 한다.
STARTER_LEVEL = 20

CLASS_NAME = {0: "공용", 1: "전사", 2: "도적", 3: "마법사", 4: "성직자", 5: "무도가"}

# ── 무도가 무기 ─────────────────────────────────────────────────────────
# 5.99 상점 47개에 **무도가 무기 목록이 없다.** 네 직업은 `전사무기`·`도적무기`·`마법사무기`·`성직자무기`
# 가 있는데 무도가만 없다. 그래서 여기서 규칙으로 고른다 — **서버에 이미 있는 아이템 템플릿에서만** 고르고,
# 아이템의 능력치·값은 건드리지 않는다.
#
#   ① 무기 칸(EquipmentSlot 1)이고
#   ② 1~25레벨이 낄 수 있고(LevelRequired ≤ 25)
#   ③ 무도가가 낄 수 있고 — 서버는 `Class` 가 자기 직업이거나 **공용(0)** 일 때만 끼워 준다
#      (`src/Hades.Server.Base/Network/Game/GameClient.cs:171`)
#   ④ 값이 0 이 아니고 — shop1 은 `GoldPoints >= Value` 로 판다(`shop1.cs:147`). 0 이면 **공짜로 나간다.**
#   ⑤ 5.99 표의 무기이고(하데스 자체 영문 아이템·`치장아이템` 제외)
#   ⑥ 때릴 수 있어야 한다(최대피해 10 이상 — 돌괭이·K4 따위 1~2 는 곡괭이다)
#
# 남는 것은 **에페**(1레벨·500골드·15~20) 하나다. 에페는 `Class` 가 공용이라 이미 `전사무기` 목록에 들어
# 있어 실제로 느는 물건은 없지만, "무도가가 1레벨부터 살 무기가 있다"가 규칙으로 지켜진다.
#
# **빠지는 것: 용의발톱.** 서버의 무도가 전용 무기 6개 중 25레벨 이하는 이것 하나뿐인데 ④에 걸린다 —
# `Value 0` 이라 공짜고, 피해가 180~200 으로 에페의 12배다. 같은 이름을 노바 팩은 **99레벨**로 적었다
# (5.99 의 오기로 보인다 — gear-plan 조사 ④). 값을 고치는 일은 이 작업 밖이라 **넣지 않는다.**
#
# **원작이 뒷받침한다.** `data/game-data/items.json`(ItemInfo0~11)에서 무도가 전용(`who=Monk`)은 **51레벨
# 장갑·팔찌부터**고 무기는 하나도 없다. 무기 갈래(Swords·Dagger·Staff·Soori·Crude)는 전부 `who=All` —
# 원작에서도 무도가는 공용 무기를 쓴다.
#
# 제대로 된 너클 사다리는 노바 팩에만 있다(글러브1 1레벨 80~100 · 견습자의글러브 11 · 숙련자의글러브 41 ·
# 지존의글러브 71). 그 가운데 **1~25레벨 두 칸**을 `scripts/build-nova-knuckles.py` 가 들여왔고, 여기서는
# 아래 `SHOPS` 의 `너클` 칸이 그것을 델란 목록에 넣는다. 41·71레벨 두 칸은 아직 들이지 않았다.
MONK_WEAPON_MIN_DAMAGE = 10
MONK_WEAPON_GROUPS_OUT = ("치장아이템",)

# ── 세울 상점 ───────────────────────────────────────────────────────────
# 자리·이름·그림·인사말은 혼든 팩에서 읽는다(아래 `혼든`). 물목은 5.99 팩의 목록 이름으로 고른다.
SHOPS = [
    {
        "혼든": "델란",
        "갈래": "무기",
        "목록": ["전사무기", "도적무기", "마법사무기", "성직자무기"],
        "혼든물목": True,        # 혼든 델란 목록 — 서버에 있는 것만 더한다
        "무도가무기": True,      # 5.99 에 목록이 없어 위 규칙으로 채운다
        # 노바 팩에서 들여온 무도가 너클 사다리의 아래 두 칸(`scripts/build-nova-knuckles.py`).
        # 무도가가 1~20레벨에 들 것이 공용 에페 하나뿐이던 자리를 메운다.
        "너클": ["글러브1", "견습자의글러브"],
        "옮김": None,
    },
    {
        "혼든": "드보이",
        "갈래": "갑옷",
        "목록": ["전사갑옷사기", "도적갑옷사기", "마법사갑옷사기", "성직자갑옷사기", "무도가갑옷사기"],
        "혼든물목": True,        # **1~20레벨 옷이 여기서 온다** — 5.99 갑옷은 전부 21레벨부터다
        "무도가무기": False,
        "너클": [],
        # 혼든은 (7,2) 인데 5.99 의 `스웨인@노비스무기방어구상점#7,2` 가 이미 그 칸에 서 있다.
        # 스웨인을 지우지 않으려고 한 칸 옆으로 옮긴다. (6,2) 도 걸을 수 있는 칸이다.
        "옮김": (6, 2),
    },
]


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def honden_npcs():
    """혼든 `*_npc.txt` — `{ 이름 … 이미지 … 말하기 … }` 블록. 이름 → (그림 번호, 인사말)."""
    out = {}
    text = HONDEN_NPC.read_text(encoding="utf-8", errors="replace")
    for block in re.findall(r"\{(.*?)\n\}", text, re.S):
        name = image = None
        speech = []
        for line in block.splitlines():
            cols = [c for c in line.strip().split("\t") if c != ""]
            if not cols:
                continue
            if cols[0] == "이름" and len(cols) > 1:
                name = cols[1].strip()
            elif cols[0] == "이미지" and len(cols) > 1:
                image = int(cols[1])
            elif cols[0] == "말하기" and len(cols) > 2:
                speech.append(cols[2].strip())
        if name:
            out[name] = (image, speech)
    return out


def honden_shops():
    """혼든 `*_shop.txt` — `물건사기<탭>이름<탭>{ … 추가<탭>물건 … }`. 이름 → 파는 것 차례대로."""
    out = {}
    text = HONDEN_SHOP.read_text(encoding="utf-8", errors="replace")
    for name, block in re.findall(r"물건사기\t([^\t\n]+)\t\{(.*?)\n\}", text, re.S):
        goods = []
        for line in block.splitlines():
            cols = [c for c in line.strip().split("\t") if c != ""]
            if len(cols) > 1 and cols[0] == "추가":
                goods.append(cols[1].strip())
        out[name.strip()] = goods
    return out


def honden_spawns():
    """혼든 `*_spawn.txt` — `맵,x,y,방향,이름,갈래`. 이름 → (맵, x, y, 방향)."""
    out = {}
    for line in HONDEN_SPAWN.read_text(encoding="utf-8", errors="replace").splitlines():
        cols = line.strip().split(",")
        if len(cols) < 5:
            continue
        out[cols[4].strip()] = (cols[0].strip(), int(cols[1]), int(cols[2]), int(cols[3]))
    return out


def area_ids():
    """맵 이름 → 전역 번호. `database/server/areas/*.json` 이 단일 출처다."""
    out = {}
    for path in AREAS.glob("*.json"):
        try:
            area = read_json(path)
        except json.JSONDecodeError:
            continue
        if area.get("Name") and area.get("Id") is not None:
            out.setdefault(area["Name"], int(area["Id"]))
    return out


def item_templates():
    out = {}
    for path in ITEMS.glob("*.json"):
        try:
            item = read_json(path)
        except json.JSONDecodeError:
            continue
        if item.get("Name"):
            out[item["Name"]] = item
    return out


def pack599_stock():
    return {s["이름"]: s["아이템"] for s in read_json(PACK599_SHOPS)}


def monk_weapons(items):
    """위 ①~⑥ 규칙으로 무도가가 1~25레벨에 살 무기를 고른다."""
    keep = []
    for name, it in sorted(items.items()):
        if it.get("EquipmentSlot") != 1:
            continue
        if (it.get("LevelRequired") or 0) > EARLY_LEVEL:
            continue
        if it.get("Class") not in (0, 5):
            continue
        if (it.get("Value") or 0) <= 0:
            continue
        group = it.get("Group") or ""
        if not group.startswith("5.99표/무기"):
            continue
        if any(bad in group for bad in MONK_WEAPON_GROUPS_OUT):
            continue
        if (it.get("DmgMax") or 0) < MONK_WEAPON_MIN_DAMAGE:
            continue
        keep.append(name)
    return keep


def occupied(area_id, skip_names):
    """이미 그 맵에 서 있는 NPC 의 칸. 같은 칸에 둘을 세우면 클라이언트가 하나만 그린다."""
    taken = {}
    for path in MUNDANES.glob("*.json"):
        try:
            npc = read_json(path)
        except json.JSONDecodeError:
            continue
        if npc.get("AreaID") != area_id or npc.get("Name") in skip_names:
            continue
        taken[(npc.get("X"), npc.get("Y"))] = npc.get("Name")
    return taken


def mundane_json(name, area_name, area_id, x, y, direction, image, speech, stock):
    """기존 상점 템플릿 23장과 같은 생김새. 칸 이름·차례까지 그대로 둔다."""
    return {
        "Name": f"{name}@{area_name}#{x},{y}",
        "AreaID": area_id,
        "X": x,
        "Y": y,
        "Direction": direction,
        "Image": SPRITE_BASE + image,
        "Level": 1,
        "MaximumHp": 1000,
        "MaximumMp": 1000,
        "Speech": speech,
        "ScriptKey": SHOP_SCRIPT,
        "DefaultMerchantStock": stock,
        "EnableWalking": False,
        "EnableTurning": False,
        "EnableAttacking": False,
        "EnableCasting": False,
        "WalkRate": 0,
        "TurnRate": 0,
        "CastRate": 0,
        "ChatRate": 0,
        "PathQualifer": 1,
        "ViewingQualifer": 1,
    }


def coverage(items, stock, slot, level):
    """직업별로 <paramref>level</paramref> 레벨까지 살 수 있는 것. 공용(0)은 다섯 직업 모두에 센다."""
    out = {c: [] for c in (1, 2, 3, 4, 5)}
    for name in stock:
        it = items.get(name)
        if not it or it.get("EquipmentSlot") != slot:
            continue
        if (it.get("LevelRequired") or 0) > level:
            continue
        cls = it.get("Class")
        for c in out:
            if cls == c or cls == 0:
                out[c].append(name)
    return out


def main():
    parser = argparse.ArgumentParser(description="노비스마을 장비 상점 둘을 세운다")
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    args = parser.parse_args()

    npcs, spawns, ids, items, lists, honden = (
        honden_npcs(), honden_spawns(), area_ids(), item_templates(), pack599_stock(), honden_shops())

    trouble = []
    for shop in SHOPS:
        who = shop["혼든"]
        if who not in npcs or who not in spawns:
            trouble.append(f"혼든 팩에 {who} 가 없다")
            continue

        image, speech = npcs[who]
        area_name, hx, hy, direction = spawns[who]
        if area_name not in ids:
            trouble.append(f"하데스에 {area_name} 맵이 없다")
            continue
        area_id = ids[area_name]

        # 물목 — 5.99 목록을 차례대로 잇고 겹치는 것은 한 번만 둔다.
        names, seen = [], set()
        for key in shop["목록"]:
            if key not in lists:
                trouble.append(f"5.99 팩에 {key} 목록이 없다")
                continue
            for name in lists[key]:
                if name not in seen:
                    seen.add(name)
                    names.append(name)
        if shop["무도가무기"]:
            for name in monk_weapons(items):
                if name not in seen:
                    seen.add(name)
                    names.append(name)
        for name in shop["너클"]:
            if name not in items:
                trouble.append(f"{who}: 너클 {name} 이 아이템 템플릿에 없다 — python3 scripts/build-nova-knuckles.py --쓰기")
            elif name not in seen:
                seen.add(name)
                names.append(name)

        missing = [n for n in names if n not in items]
        if missing:
            trouble.append(f"{who}: 아이템 템플릿에 없는 물건 {missing}")
        names = [n for n in names if n in items]

        # 혼든 목록은 **서버에 있는 것만** 더한다 — 혼든에만 있는 이름(목도·셔츠 …)은 조용히 뺀다.
        # 여기서 1~20레벨 옷(레더튜닉·도복·스카웃튜닉 …)이 들어온다.
        from_honden = []
        if shop["혼든물목"]:
            if who not in honden:
                trouble.append(f"혼든 `노비스마을_shop.txt` 에 {who} 목록이 없다")
            for name in honden.get(who, []):
                if name in items and name not in seen:
                    seen.add(name)
                    names.append(name)
                    from_honden.append(name)

        # 낮은 레벨이 먼저 보이게 놓는다 — 50줄짜리 목록에서 1레벨짜리를 끝까지 넘겨 찾지 않도록.
        names.sort(key=lambda n: (items[n].get("LevelRequired") or 1, items[n].get("Value") or 0, n))

        x, y = shop["옮김"] or (hx, hy)
        full = f"{who}@{area_name}#{x},{y}"
        taken = occupied(area_id, {full})
        if (x, y) in taken:
            trouble.append(f"{who}: {area_name} ({x},{y}) 에 {taken[(x, y)]} 가 이미 서 있다")

        out = MUNDANES / f"{full}.json"
        body = mundane_json(who, area_name, area_id, x, y, direction, image, speech, names)
        before = read_json(out) if out.exists() else None
        if args.writing:
            out.write_text(json.dumps(body, ensure_ascii=False, indent=2), encoding="utf-8")

        moved = "" if shop["옮김"] is None else f"  (혼든은 {hx},{hy} — 그 칸엔 5.99 스웨인이 있다)"
        print(f"\n■ {full}  그림 {SPRITE_BASE + image}(혼든 이미지 {image}) · {shop['갈래']} {len(names)}종{moved}")
        print(f"   인사말 {len(speech)}줄 · 스크립트 {SHOP_SCRIPT} · {'고침' if before else '새로 씀'}"
              f"{'' if args.writing else ' (아직 안 씀 — --쓰기)'}")
        print(f"   물목: {', '.join(names)}")
        if from_honden:
            print(f"   혼든에서 더한 것 {len(from_honden)}종: {', '.join(from_honden)}")

        slot = 1 if shop["갈래"] == "무기" else 2
        for cls, got in coverage(items, names, slot, STARTER_LEVEL).items():
            mark = "○" if got else "✗"
            print(f"   {mark} {CLASS_NAME[cls]} 1~{STARTER_LEVEL}레벨: "
                  + (", ".join(f"{n}(Lv{items[n]['LevelRequired']}·{items[n]['Value']}골드)" for n in got) or "없다"))
            if not got:
                trouble.append(f"{who}: {CLASS_NAME[cls]} 가 1~{STARTER_LEVEL}레벨에 살 {shop['갈래']}가 없다")

    if trouble:
        print("\n막는 것:")
        for line in trouble:
            print("  -", line)
        return 1
    print(f"\n끊긴 참조 0 · 다섯 직업 모두 1~{STARTER_LEVEL}레벨에 입고 들 것이 있다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
