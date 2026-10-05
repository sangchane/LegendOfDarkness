#!/usr/bin/env python3
"""구광산 — 마인마을 10시 방향 출구로 들어가는 29층 광산을 하데스에 넣는다(맵·워프).

원작의 구광산은 카스마늄 갱도가 나오기 전 마인마을 서쪽(10시) 출구에 있던 광산이다(사용자 2026-10-02).
5.99 팩은 그 출구를 카스마늄광산진입로로 바꿨고 층은 「광산N층」 13장만 남겼다(지금 하데스의 광산대기실·광산1~16층).
혼든 커뮤니티 팩의 「공식길드전용던전」이 구광산 맵 36장을 통째로 쓴다 — 대기실 + 1-1 ~ 29-1(갈래층 포함).
5.99 의 13장은 그중 13장과 바이트까지 같다. 8-1 은 혼든에도 없어(혼든이 카스마늄제4광산8-1 로 메꿨다) 넣지 않는다.

  - 맵: 36장 모두 새 번호로 넣는다. 하데스의 「광산N층」·「리파이너의던전N층」이 같은 맵 파일을 쓰지만 건드리지 않는다 —
    리파이너의던전은 살아 있는 던전이고, 혼든 안에서도 22-1=3-2 · 24-1=17-1 처럼 한 파일을 두 층이 쓰므로 번호를 나누면 층이 엉킨다.
  - 워프: 혼든 `db/warp/길드성/공식길드전용던전.txt` 를 그대로(8-1 로 가는 줄만 뺀다).
  - 월드맵 사냥터 카드 「구광산」 — 대기실로, 층마다 바로 가기(아벨해안·뤼케시온해안처럼). 레벨은 마인마을 입구 99 를 따른다.
  - 드나드는 길: 마인마을(0,49~54) → 구광산대기실(18,47) **99레벨부터**, 구광산대기실(18,49) → 마인마을(2,51).
    대기실 칸은 노바 팩 `Titan.txt`(광산대기실 18,47 도착 · 18,49 나감), 마인마을 칸은 5.99 `Casmanum_Warp.txt` 의 10시 출구.
  - 괴물: 배치(어느 층에 무엇이 몇 마리)는 혼든 `db/mob/공식길드전용던전/` 76줄 그대로. 수치는 **우리가 정했다**
    (사용자 2026-10-02 「층마다 올라가게 · 99레벨부터 · 기존 광산 몬스터를 레퍼런스로 · 아벨과는 비교 안 됨」):
      1층 = 노바 팩 광산1층 수치(`novaonline/db/mob/mine.txt`) — 그림록 77,760 · 공격 2,409~9,852.
      29층으로 갈수록 5.99 카스마늄 갱도(나중에 나온 더 깊은 광산, 체력 45만 · 공격 5,200~ · 방어 -70)에 다가간다:
      체력 ×1→×3, 최소 공격 2,409→5,200, 방어 → -70. 경험치는 카스마늄의 경험치/체력(1,309,240/450,000)에
      하데스 나눗수 7.3(tools/pack-import EXPERIENCE_DIVISOR). 드라코는 노바 값(2,375만)이 튀어서 혼든 비율(그림록가 아니라 헬 바로 아래 — 헬 × 혼든 드라코/헬 비율).
      금화는 한 마리에 최소 10,000전(사용자) — 템플릿 `GoldMinimum`, 그 위로는 경험치 비례식(Formulas/monsterexp.cs).
      드랍은 노바 방식(사용자): 그림록퀸 → 그림록퀸홀, 헬 직업 → 그 직업 헬옷(노바 `mine.txt` 헬불씨프1·2 처럼 갈래마다
      한 벌씩이던 것을 한 괴물 목록으로 모았다). 확률은 노바(15%)가 아니라 **0.5% 아래**, 층이 깊을수록 조금 더(괴물 DropRate 0.30%→0.48%)(사용자 — 원작 헬옷은 0.5% 도 안 된다).
      하데스는 목록에서 하나를 고르고 그 아이템의 DropRate × 1.5 를 굴리므로 DropRate 0.003 — 한 마리에 0.45%,
      목록이 넷이면 한 벌에 0.11%. 다른 곳의 그림록퀸도 같은 그림록퀸홀(전에는 0.01)을 쓰므로 함께 0.45% 가 된다.
      일반 괴물(노바는 3백만·5백만골드 주머니)은 금화만. 드라코(노바는 캐시템 드라코의발톱)는 아직 없음.

  쓰는 법: python3 scripts/gen/world/build-old-mine.py            # 무엇이 바뀌는지만
           python3 scripts/gen/world/build-old-mine.py --쓰기     # 서버 자료에 쓴다
"""
import json
import re
import shutil
import sys
from pathlib import Path

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._world import AREAS, EXP_PER_HP, LOOT_NONE, LOOT_RANDOM, MAPS, SERVER, WARPS, WORLDMAP, text, warp, world_card

HONDEN = Path.home() / "Downloads" / "혼든커뮤니티팩2"
PACK_NAME = "공식길드전용던전"
NAME = "구광산"
FLAGS = 106240           # 하데스 기존 맵과 같은 값(tools/pack-import DEFAULT_MAP_FLAGS)
TOWN = "마인마을"
TOWN_EXIT = [(0, y) for y in range(49, 55)]
LOBBY_IN, LOBBY_OUT, TOWN_IN = (18, 47), (18, 49), (2, 51)
MONSTERS = SERVER / "templates" / "monsters" / NAME
ENTRY_LEVEL = 99              # 99레벨부터 사냥하는 곳(사용자 2026-10-02) — 아벨해안처럼 들어가는 문에만 건다
CARD_POINT = (120, 130)       # 월드맵 사냥터 카드(층 바로 가기, 사용자 2026-10-02) — 원작 field001 마인(137,113) 옆

# 노바 광산 괴물 — (체력, 최대 공격, 방어, 그림). 혼든 이름에서 「길드던전」 을 뗀 이름으로 찾는다.
NOVA = {
    "그림록": (77760, 9852, -30, 8), "그림록병사": (138240, 9852, -30, 10), "그림록근위병": (205335, 9852, -35, 9),
    "그림록퀸": (337500, 9852, -76, 11), "오크병사": (65340, 9852, -30, 148),
    # 드라코는 헬 직업·그림록퀸 바로 아랫 단계다(사용자 2026-10-02 — 헬몽크아머·그림록퀸홀 아래). 노바 값(2,375만)은 튀어서,
    # 헬(노바 337,500)에 혼든의 드라코/헬 평균 비율(322,172 / 328,516)을 곱한다 — 일반 괴물과는 크게 벌어지고 헬보다 조금 아래.
    "드라코": (round(337500 * 322172 / 328516), 12412, -69, 151),
    "헬몽크": (337500, 12340, -78, 238), "헬불씨프": (337500, 12340, -78, 155), "헬블루나이트": (337500, 12340, -78, 156),
    "헬소서러": (337500, 12340, -78, 68), "헬소서리스": (337500, 12340, -78, 158),
}
LAST_FLOOR = 29
DMG_MIN = (2409, 5200)        # 1층(노바) → 29층(카스마늄)
AC_DEEP = -70
GOLD_MINIMUM = 10000
ITEMS = SERVER / "templates" / "items"
DROP_RATE = 0.003             # × DropBoost 1.5 = 0.45% — 헬옷은 0.5% 도 안 된다(사용자 2026-10-02)
# 괴물마다 확률(템플릿 DropRate) — 깊은 층일수록 조금 더(사용자 2026-10-02 「강한 몹일수록 드랍률이 조금이라도 높게」).
# 1층 0.002 → 29층 0.0032, × 1.5 = 0.30% → 0.48%. 아이템의 DROP_RATE 는 다른 곳 그림록퀸홀 몫으로 그대로 둔다.
MONSTER_DROP_RATE = (0.002, 0.0032)
DROPS = {
    "그림록퀸": ["그림록퀸홀"],
    "헬몽크": ["헬몽크아머"],
    "헬불씨프": ["헬씨프아머", "헬씨프투구"],
    "헬블루나이트": ["헬나이트아머", "헬나이트투구"],
    "헬소서러": ["헬소서러로브", "헬소서러후드", "헬소서리스로브", "매직베일"],
    "헬소서리스": ["헬클레릭로브", "헬클레릭후드", "홀리베일", "헬프리스트로브"],
}


def our_name(pack_name):
    return NAME + pack_name[len(PACK_NAME):]


def pack_maps():
    """혼든 맵 정의 — 이름 → (너비, 높이, 배경음, 맵 파일)."""
    out = {}
    for f in (HONDEN / "db" / "maps").glob("**/*.txt"):
        for block in text(f).split("{")[1:]:
            field = lambda k: (re.search(rf"^{k}\s+(.+)$", block, re.M) or [None, ""])[1].strip()
            name = field("이름")
            if name.startswith(PACK_NAME):
                out[name] = (int(field("너비")), int(field("높이")), int(field("배경음") or 0), HONDEN / field("맵파일"))
    return out


def hades_areas():
    out = []
    for path in AREAS.glob("*.json"):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        out.append((path, data))
    return out


def floor_of(name):
    m = re.search(r"(\d+)-\d+$", name)
    return int(m.group(1)) if m else 1


def monster(kind, count, map_name, area):
    hp, dmg_max, ac, image = NOVA[kind]
    step = (floor_of(map_name) - 1) / (LAST_FLOOR - 1)
    hp = round(hp * (1 + 2 * step))
    ac = round(ac + (AC_DEEP - ac) * step) if ac > AC_DEEP else ac
    return {
        "Name": kind, "BaseName": kind, "AreaID": area,
        "SpawnMax": count, "SpawnType": 2, "SpawnRate": 30, "SpawnSize": 0, "SpawnOnlyOnActiveMaps": False,
        "Image": 0x4000 + image, "ImageVarience": 0,
        "MaximumHP": hp, "MaximumMP": 0, "Exp": round(hp * EXP_PER_HP),
        "DmgMin": round(DMG_MIN[0] + (DMG_MIN[1] - DMG_MIN[0]) * step), "DmgMax": dmg_max, "Ac": ac,
        "Level": 1, "MovementSpeed": 1500, "EngagedWalkingSpeed": 1500, "AttackSpeed": 1000, "CastSpeed": 8000,
        "MoodType": 2, "PathQualifer": 1,   # 99레벨 사냥터는 모두 선공(사용자 2026-10-05) — 4 는 스폰 때 반반
        "LootType": LOOT_RANDOM if kind in DROPS else LOOT_NONE, "Drops": {"$values": DROPS.get(kind, [])},
        "ScriptName": "Common Monster", "UpdateMapWide": True, "UpdateRate": 1000.0,
        "Grow": False, "IgnoreCollision": False, "GoldMinimum": GOLD_MINIMUM,
        **({"DropRate": round(MONSTER_DROP_RATE[0] + (MONSTER_DROP_RATE[1] - MONSTER_DROP_RATE[0]) * step, 5)} if kind in DROPS else {}),
    }


def main():
    write = "--쓰기" in sys.argv
    maps = pack_maps()
    areas = hades_areas()
    by_name = {data["Name"]: (path, data) for path, data in areas}
    used = {data["Id"] for _, data in areas}
    next_id = max(i for i in used if i < 65536) + 1     # 맵 번호는 전선에서 16비트다

    ids, plan = {}, []
    for pack_name in sorted(maps):
        cols, rows, music, file = maps[pack_name]
        name = our_name(pack_name)
        if name in by_name:                                     # 이미 넣었다 — 다시 돌려도 같은 번호
            ids[pack_name] = by_name[name][1]["Id"]
            continue
        number = next_id
        next_id += 1
        ids[pack_name] = number
        plan.append(("새 맵", pack_name, name, number))
        if write:
            shutil.copyfile(file, MAPS / f"lod{number}.map")
            area = {"FilePath": f"../../database/server/maps/lod{number}.map", "Cols": cols, "ContentName": None,
                    "Flags": FLAGS, "Id": number, "Music": music, "Name": name, "Rows": rows,
                    "Blocks": [], "ScriptKey": None, "ID": number}
            (AREAS / f"{name}.json").write_text(json.dumps(area, ensure_ascii=False, indent=2), encoding="utf-8")

    warps = []
    for line in text(HONDEN / "db" / "warp" / "길드성" / f"{PACK_NAME}.txt").splitlines():
        parts = line.strip().split(",")
        if len(parts) < 7 or line.startswith("//") or parts[1] not in ids or parts[4] not in ids:
            continue                                            # 8-1(카스마늄제4광산8-1) 로 가는 줄
        warps.append(warp(our_name(parts[1]), ids[parts[1]], (int(parts[2]), int(parts[3])),
                          our_name(parts[4]), ids[parts[4]], (int(parts[5]), int(parts[6]))))
    town = by_name[TOWN][1]["Id"]
    lobby_name, lobby = f"{NAME}대기실", ids[f"{PACK_NAME}대기실"]
    for at in TOWN_EXIT:
        warps.append(warp(TOWN, town, at, lobby_name, lobby, LOBBY_IN, ENTRY_LEVEL))
    warps.append(warp(lobby_name, lobby, LOBBY_OUT, TOWN, town, TOWN_IN))

    fresh = [(n, w) for n, w in warps if not (WARPS / f"{n}.json").exists()]
    floors = sorted((n for n in ids if n != f"{PACK_NAME}대기실"), key=lambda n: (floor_of(n), n))
    world = world_card(NAME, lobby, LOBBY_IN, CARD_POINT, [ids[n] for n in floors], warps)
    if write:
        for n, w in warps:
            (WARPS / f"{n}.json").write_text(json.dumps(w, ensure_ascii=False, indent=2), encoding="utf-8")
        WORLDMAP.write_text(json.dumps(world, ensure_ascii=False, indent=2), encoding="utf-8")

    counts = {}                                                 # 혼든에 같은 층·같은 괴물 줄이 두 번 있는 곳이 있다 — 마릿수를 합친다
    for line in text(HONDEN / "db" / "mob" / PACK_NAME / f"{PACK_NAME}_spawn.txt").splitlines():
        parts = line.strip().split(",")
        if len(parts) >= 3 and parts[0] in ids:
            key = (parts[1].replace("길드던전", ""), parts[0])
            counts[key] = counts.get(key, 0) + int(parts[2])
    spawns = [monster(kind, n, our_name(where), ids[where]) for (kind, where), n in counts.items()]
    if write:
        MONSTERS.mkdir(parents=True, exist_ok=True)
        for m in spawns:
            map_name = next(our_name(n) for n, i in ids.items() if i == m["AreaID"])
            (MONSTERS / f"{m['Name']}@{map_name}.json").write_text(json.dumps(m, ensure_ascii=False, indent=2), encoding="utf-8")

    changed_items = []
    for item in sorted({i for names in DROPS.values() for i in names}):
        path = ITEMS / f"{item}.json"
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if data.get("DropRate") != DROP_RATE:
            changed_items.append(f"{item} {data.get('DropRate')}→{DROP_RATE}")
            if write:
                data["DropRate"] = DROP_RATE
                path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    if changed_items:
        print("  아이템 확률: " + " · ".join(changed_items))

    for kind, before, after, number in plan:
        print(f"  {kind:4} {number}  {before} → {after}")
    print(f"맵 {len(ids)}장(새 {len(plan)}) · "
          f"워프 {len(warps)}장(새 {len(fresh)}) · 괴물 자리 {len(spawns)}" + ("" if write else "  — 미리보기, --쓰기 로 쓴다"))


if __name__ == "__main__":
    main()
