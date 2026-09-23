#!/usr/bin/env python3
"""수오미마을과 우드랜드입구에 장비 상점을 세운다 — 무기·갑옷·장신구.

  python3 scripts/build-town-gear-shops.py          # 무엇이 바뀌는지만 본다
  python3 scripts/build-town-gear-shops.py --쓰기    # 서버 정의에 적는다

**노비스 다음 자리를 채운다.** `scripts/build-novice-gear-shops.py` 가 노비스마을에 델란(무기)·드보이(갑옷)를
세워 1~25레벨이 처음 입고 들 것을 만들었다. 그 뒤 초반 동선인 **수오미마을**과 **우드랜드입구**에는 아직
장비를 파는 사람이 없고, 5.99 팩의 상점 목록 25개가 여전히 NPC 에 안 묶인 채 남아 있었다
(`plans/5.99-상점결합.tsv`). 그 25개 중 **장신구 7개 목록**은 게임 어디에서도 살 수 없었다.

**어디서 무엇을 가져오나**
  - **무엇을 파나 → 5.99 팩.** `data/server-packs/extracted/5.99-server/shops.json` 의 목록 그대로.
    · 가이(무기)  = 전사·도적·마법사·성직자무기 4개 목록
    · 아돌(갑옷)  = 전사·도적·마법사·성직자·무도가갑옷사기 5개 목록
    · 보석상여주인(장신구) = **안 묶인 7개** 각반·신발·벨트·귀걸이·방패·반지·전사투구 + 장갑사기
      (장갑사기만 이미 `시장마스터@시장은행` 에 묶여 있다. 시장은행은 초반 동선이 아니라서 같이 판다 —
       팩에서도 베이가가 같은 목록 4개를 마을 8곳에서 되판다. 겹쳐 파는 것이 이 게임의 꼴이다.)
  - **어디서 누가 파나 → 혼든 팩.** `data/server-packs/honden-community/db/npc/마이소시아/수오미마을_{npc,spawn,shop}.txt`
    · `가이` — 혼든 수오미의 무기상. 이름·그림 30·인사말 3줄.
    · `아돌` — 혼든 수오미의 갑옷상. 이름·그림 30·인사말 3줄.
    · `보석상여주인` — 혼든 수오미의 장신구상. 이름·그림 163·인사말 5줄("제가 만드는 장신구들은 …").
  - **혼든 물목도 서버에 있는 것만 더한다.** 혼든 가이·아돌 목록에서 이 상점 칸에 맞는 것만 집는다
    (가이 → 광단검 · 아돌 → 카스뮴아머·팔루텐 … · 장신구 → 철방패·동장갑·동각반·쌍금귀걸이 …).
    혼든에만 있는 이름(체인메일·풀플레이트·은제방패 …)은 조용히 뺀다 → 끊긴 참조는 늘 0이다.
  - 값은 아이템 템플릿의 `Value` 를 그대로 쓴다. **이 생성기는 아이템을 고치지 않는다.**

**자리는 잰 것이다.**
  - `가이@수오미무기점#7,7` · `아돌@수오미방어구점#4,5` — 혼든 `수오미마을_spawn.txt` 의
    `만남의광장무기점,7,7,2,가이` · `만남의광장방어구점,4,5,2,아돌` 줄이다. 혼든은 같은 두 사람을
    `수오미대장간A,3,2` · `수오미대장간B,3,6` 에도 세워 두었는데 대장간B 는 x 가 18까지 가는 큰 맵이라
    하데스의 12x12 수오미방어구점이 아니다. **만남의광장 쪽 좌표가 하데스 맵과 맞는다** — 5.99 가 세운
    `스웨인@수오미무기점#8,7` · `스웨인@수오미방어구점#3,5` 가 바로 옆 칸이다. 같은 계산대 뒤다.
  - `보석상여주인@우드랜드입구#10,15` — 혼든에는 우드랜드 상인이 **없다.** 그래서 5.99 팩이 그 맵에 세운
    단 하나의 NPC 자리를 쓴다: `npc_spawns.json` 의 `우드랜드입구,10,15,2,부자아져찌`. 팩이 이름·그림을
    안 적어 두어 하데스가 안 들여온 자리다. 세계지도에서 내려서는 칸(9~11,23)에서 곧장 북쪽이다.
  - **셋 다 아래 `자리를_잰다` 가 서버 벽 규칙(`Area.ParseMapWalls` + `static/sotp.dat`)으로 다시 잰다.**
    손님이 문(또는 내려서는 칸)에서 걸어 닿는 칸과 상인 사이가 **3칸 이내**여야 한다. 서버 상점 12곳이
    하나같이 계산대 뒤 3칸이고(그래서 상인 칸까지는 못 걸어간다), 누를 때 거리를 재지 않으며
    (`GameServerHandlers.Format43Handler`) 12칸까지 보인다(`LoruleConfig.json` `WithinRangeProximity`).

**이미 선 NPC 는 건드리지 않는다.** 5.99 스웨인 둘은 그대로 두고 옆 칸에 선다. 겹치면 멈춘다.

**지우지 않는다.** 이 생성기는 자기가 쓰는 세 파일만 만들고 고친다. 다른 NPC·아이템은 읽기만 한다.
"""

import argparse
import json
import pathlib
import re
import struct
import sys
from collections import deque

ROOT = pathlib.Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
AREAS = SERVER / "areas"
MAPS = SERVER / "maps"
SOTP = SERVER / "static/sotp.dat"
ITEMS = SERVER / "templates/items"
MUNDANES = SERVER / "templates/mundanes"

PACK599_SHOPS = ROOT / "data/server-packs/extracted/5.99-server/shops.json"
PACK599_SPAWNS = ROOT / "data/server-packs/extracted/5.99-server/npc_spawns.json"
HONDEN = ROOT / "data/server-packs/honden-community/db/npc/마이소시아"
HONDEN_NPC = HONDEN / "수오미마을_npc.txt"
HONDEN_SPAWN = HONDEN / "수오미마을_spawn.txt"
HONDEN_SHOP = HONDEN / "수오미마을_shop.txt"

#: NPC 그림은 괴물과 같은 번호 체계로 간다(ServerFormat07). `tools/pack-import/import.py` 의 MONSTER_IMAGE_BASE 와 같은 값.
SPRITE_BASE = 0x4000

#: shop1.cs 가 사기·팔기·수리를 다 한다. 붙이는 것은 이 이름 하나뿐 — 코드는 짜지 않는다.
SHOP_SCRIPT = "shop1"

#: 1차 직업 동선. 다섯 직업이 이 구간에 살 것이 있어야 한다.
EARLY_LEVEL = 25

#: 계산대 뒤 몇 칸까지 서도 되나. 서버 상점 12곳이 전부 3칸이다(`docs` 와 NoviceGearShopTests 참고).
COUNTER_REACH = 3

CLASS_NAME = {0: "공용", 1: "전사", 2: "도적", 3: "마법사", 4: "성직자", 5: "무도가"}

#: `ItemTemplate.EquipmentSlot` — 무기 1 · 갑옷 2 · 나머지가 장신구다.
WEAPON_SLOT = {1}
ARMOUR_SLOT = {2}
#: 방패 3 · 투구 4 · 귀걸이 5 · 목걸이 6 · 반지 7 · 장갑 9 · 벨트 11 · 각반 12 · 신발 13.
ACCESSORY_SLOTS = {3, 4, 5, 6, 7, 9, 11, 12, 13}

SHOPS = [
    {
        "혼든": "가이",
        "혼든자리": "만남의광장무기점",   # 혼든은 같은 사람을 수오미대장간A 에도 세운다 — 위 주석 참고
        "맵": "수오미무기점",
        "갈래": "무기",
        "칸": WEAPON_SLOT,
        "목록": ["전사무기", "도적무기", "마법사무기", "성직자무기"],
        # 노바 팩에서 들여온 무도가 너클 사다리의 아래 두 칸(`scripts/build-nova-knuckles.py`).
        # 5.99 상점 47개에 무도가 무기 목록이 없어 노비스 델란도 같은 두 칸으로 메운다.
        "너클": ["글러브1", "견습자의글러브"],
        "문": (6, 13),   # warp 수오미마을(11,55) to 수오미무기점(6,13)
    },
    {
        "혼든": "아돌",
        "혼든자리": "만남의광장방어구점",
        "맵": "수오미방어구점",
        "갈래": "갑옷",
        "칸": ARMOUR_SLOT,
        "목록": ["전사갑옷사기", "도적갑옷사기", "마법사갑옷사기", "성직자갑옷사기", "무도가갑옷사기"],
        "너클": [],
        "문": (10, 8),   # warp 수오미마을(19,48) to 수오미방어구점(10,8)
    },
    {
        "혼든": "보석상여주인",
        "혼든자리": None,                 # 혼든에 우드랜드 상인이 없다 — 자리는 5.99 팩에서 온다
        "팩자리": ("우드랜드입구", "부자아져찌"),
        "맵": "우드랜드입구",
        "갈래": "장신구",
        "칸": ACCESSORY_SLOTS,
        # 안 묶인 7개 + 장갑사기(시장은행에만 있다).
        "목록": ["장갑사기", "각반사기", "신발사기", "벨트사기", "귀걸이사기", "방패사기", "반지사기", "전사투구사기"],
        "너클": [],
        "문": (10, 23),  # warp 우드랜드입구 to world map — 세계지도에서 내려서는 칸
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
    """혼든 `*_spawn.txt` — `맵,x,y,방향,이름,갈래`. 한 사람이 여러 맵에 서므로 (맵, 이름) 으로 센다."""
    out = {}
    for line in HONDEN_SPAWN.read_text(encoding="utf-8", errors="replace").splitlines():
        cols = line.strip().split(",")
        if len(cols) < 5:
            continue
        out[(cols[0].strip(), cols[4].strip())] = (int(cols[1]), int(cols[2]), int(cols[3]))
    return out


def pack599_spawns():
    """5.99 `npc_spawns.json` — `raw` 가 `맵,x,y,방향,이름,스크립트`. (맵, 이름) → (x, y, 방향)."""
    out = {}
    for row in read_json(PACK599_SPAWNS):
        raw = row.get("raw") or []
        if len(raw) < 5:
            continue
        try:  # 팩 줄이 다 같은 꼴은 아니다 — 숫자가 아닌 줄은 건너뛴다.
            spot = (int(raw[1]), int(raw[2]), int(raw[3]))
        except ValueError:
            continue
        out[(raw[0].strip(), raw[4].strip())] = spot
    return out


def areas():
    """맵 이름 → 그 맵 정의. `database/server/areas/*.json` 이 단일 출처다."""
    out = {}
    for path in AREAS.glob("*.json"):
        try:
            area = read_json(path)
        except json.JSONDecodeError:
            continue
        if area.get("Name") and area.get("Id") is not None:
            out.setdefault(area["Name"], area)
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


def walls(area):
    """
    서버의 `Area.OnLoaded`·`Area.ParseMapWalls` 그대로다
    (`src/Hades.Server.Base/Types/Area.cs:165-250`): 칸마다 6바이트 — 앞 2바이트를 건너뛰고 short 둘을
    작은 끝 먼저로 읽어 지형표(`static/sotp.dat`)에서 0x0F 면 벽. 파일이 모자란 칸은 벽으로 친다.
    맵 정의의 `Blocks` 도 서버가 덮으므로 같이 막는다.
    """
    cols, rows = int(area["Cols"]), int(area["Rows"])
    sotp = SOTP.read_bytes()
    body = (MAPS / f"lod{int(area['Id'])}.map").read_bytes()

    wall = [[True] * rows for _ in range(cols)]
    at = 0
    for y in range(rows):
        for x in range(cols):
            if at + 6 > len(body):
                at += 6
                continue
            left = struct.unpack_from("<h", body, at + 2)[0]
            right = struct.unpack_from("<h", body, at + 4)[0]
            at += 6
            if left == 0 and right == 0:
                blocked = False
            elif left == 0:
                blocked = sotp[right - 1] == 0x0F
            elif right == 0:
                blocked = sotp[left - 1] == 0x0F
            else:
                blocked = sotp[left - 1] == 0x0F or sotp[right - 1] == 0x0F
            wall[x][y] = blocked

    for block in area.get("Blocks") or []:
        wall[int(block["X"])][int(block["Y"])] = True

    return cols, rows, wall


def walkable_from(cols, rows, wall, start):
    """문(또는 내려서는 칸)에서 걸어 닿는 칸 전부. 서버와 같이 상하좌우만 본다."""
    if wall[start[0]][start[1]]:
        return set()
    seen = {start}
    queue = deque([start])
    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < cols and 0 <= ny < rows and not wall[nx][ny] and (nx, ny) not in seen:
                seen.add((nx, ny))
                queue.append((nx, ny))
    return seen


def 자리를_잰다(area, door, where):
    """상인이 설 칸이 성한지, 손님 칸에서 몇 칸인지. (탈, 손님칸, 거리) 를 돌려준다."""
    cols, rows, wall = walls(area)
    x, y = where
    if not (0 <= x < cols and 0 <= y < rows):
        return f"({x},{y}) 가 {area['Name']} {cols}x{rows} 밖이다", None, None
    if wall[x][y]:
        return f"({x},{y}) 가 벽이다 — 상인이 설 수 없다", None, None

    customers = walkable_from(cols, rows, wall, door)
    if not customers:
        return f"문 {door} 이 벽이라 손님이 못 들어온다", None, None

    best = min(customers, key=lambda t: abs(t[0] - x) + abs(t[1] - y))
    far = abs(best[0] - x) + abs(best[1] - y)
    if far > COUNTER_REACH:
        return f"손님이 닿는 가장 가까운 칸 {best} 가 {far}칸 — 계산대 {COUNTER_REACH}칸을 넘는다", best, far
    return None, best, far


def occupied(area_id, skip):
    """이미 그 맵에 서 있는 NPC 의 칸. 같은 칸에 둘을 세우면 클라이언트가 하나만 그린다."""
    taken = {}
    for path in MUNDANES.glob("*.json"):
        try:
            npc = read_json(path)
        except json.JSONDecodeError:
            continue
        if npc.get("AreaID") != area_id or npc.get("Name") in skip:
            continue
        taken[(npc.get("X"), npc.get("Y"))] = npc.get("Name")
    return taken


def mundane_json(name, area_name, area_id, x, y, direction, image, speech, stock):
    """기존 상점 템플릿과 같은 생김새. 칸 이름·차례까지 그대로 둔다."""
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


def coverage(items, stock, slots, level):
    """직업별로 <level> 레벨까지 살 수 있는 것. 서버는 자기 직업이거나 공용(0)일 때만 끼워 준다."""
    out = {c: [] for c in (1, 2, 3, 4, 5)}
    for name in stock:
        it = items.get(name)
        if not it or it.get("EquipmentSlot") not in slots:
            continue
        if (it.get("LevelRequired") or 0) > level:
            continue
        cls = it.get("Class")
        for c in out:
            if cls == c or cls == 0:
                out[c].append(name)
    return out


def main():
    parser = argparse.ArgumentParser(description="수오미·우드랜드에 장비 상점을 세운다")
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    args = parser.parse_args()

    npcs = honden_npcs()
    hspawns = honden_spawns()
    pspawns = pack599_spawns()
    maps = areas()
    items = item_templates()
    lists = pack599_stock()
    honden = honden_shops()

    trouble = []
    for shop in SHOPS:
        who = shop["혼든"]
        if who not in npcs:
            trouble.append(f"혼든 팩에 {who} 가 없다")
            continue
        image, speech = npcs[who]

        area_name = shop["맵"]
        if area_name not in maps:
            trouble.append(f"하데스에 {area_name} 맵이 없다")
            continue
        area = maps[area_name]
        area_id = int(area["Id"])

        # 자리 — 혼든 spawn 이 있으면 그것, 없으면 5.99 팩이 그 맵에 세운 자리.
        if shop["혼든자리"]:
            key = (shop["혼든자리"], who)
            if key not in hspawns:
                trouble.append(f"혼든 `수오미마을_spawn.txt` 에 {key} 가 없다")
                continue
            x, y, direction = hspawns[key]
            source = f"혼든 {shop['혼든자리']},{x},{y},{direction},{who}"
        else:
            key = shop["팩자리"]
            if key not in pspawns:
                trouble.append(f"5.99 `npc_spawns.json` 에 {key} 가 없다")
                continue
            x, y, direction = pspawns[key]
            source = f"5.99 {key[0]},{x},{y},{direction},{key[1]}"

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

        # 혼든 물목은 **서버에 있고 이 상점 칸에 맞는 것만** 더한다.
        from_honden = []
        for giver in ("가이", "아돌"):
            for name in honden.get(giver, []):
                it = items.get(name)
                if it and it.get("EquipmentSlot") in shop["칸"] and name not in seen:
                    seen.add(name)
                    names.append(name)
                    from_honden.append(name)

        # 낮은 레벨이 먼저 보이게 놓는다 — 긴 목록에서 1레벨짜리를 끝까지 넘겨 찾지 않도록.
        names.sort(key=lambda n: (items[n].get("LevelRequired") or 1, items[n].get("Value") or 0, n))

        full = f"{who}@{area_name}#{x},{y}"

        taken = occupied(area_id, {full})
        if (x, y) in taken:
            trouble.append(f"{who}: {area_name} ({x},{y}) 에 {taken[(x, y)]} 가 이미 서 있다")

        bad, counter, far = 자리를_잰다(area, shop["문"], (x, y))
        if bad:
            trouble.append(f"{who}: {bad}")

        out = MUNDANES / f"{full}.json"
        body = mundane_json(who, area_name, area_id, x, y, direction, image, speech, names)
        before = out.exists()
        if args.writing:
            out.write_text(json.dumps(body, ensure_ascii=False, indent=2), encoding="utf-8")

        print(f"\n■ {full}  그림 {SPRITE_BASE + image}(혼든 이미지 {image}) · {shop['갈래']} {len(names)}종")
        print(f"   자리 근거: {source}")
        print(f"   계산대: 문 {shop['문']} 에서 걸어 닿는 가장 가까운 칸 {counter} — {far}칸")
        print(f"   인사말 {len(speech)}줄 · 스크립트 {SHOP_SCRIPT} · {'고침' if before else '새로 씀'}"
              f"{'' if args.writing else ' (아직 안 씀 — --쓰기)'}")
        print(f"   물목: {', '.join(names)}")
        if from_honden:
            print(f"   혼든에서 더한 것 {len(from_honden)}종: {', '.join(from_honden)}")

        for cls, got in coverage(items, names, shop["칸"], EARLY_LEVEL).items():
            mark = "○" if got else "✗"
            print(f"   {mark} {CLASS_NAME[cls]} 1~{EARLY_LEVEL}레벨: "
                  + (", ".join(f"{n}(Lv{items[n]['LevelRequired']}·{items[n]['Value']}골드)" for n in got) or "없다"))
            if not got:
                trouble.append(f"{who}: {CLASS_NAME[cls]} 가 1~{EARLY_LEVEL}레벨에 살 {shop['갈래']}가 없다")

    if trouble:
        print("\n막는 것:")
        for line in trouble:
            print("  -", line)
        return 1
    print(f"\n끊긴 참조 0 · 상인 셋 모두 계산대 {COUNTER_REACH}칸 안 · 다섯 직업이 1~{EARLY_LEVEL}레벨에 살 것이 있다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
