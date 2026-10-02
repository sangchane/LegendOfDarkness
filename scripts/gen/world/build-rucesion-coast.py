#!/usr/bin/env python3
"""뤼케시온해안 — 71~98레벨 사냥터(사용자 2026-10-02). 하데스에 맵 12장(대기실·1-A~4-C)만 있고 워프·괴물이 없던 것을 채운다.

  - 맵: 그대로 둔다. 하데스 lod20455~20466 이 혼든 `db/maps/뤼케해안/` 12장과 바이트까지 같다.
  - 들어가는 길: **월드맵 사냥터 카드**(아벨해안 카드처럼, 구역 1-A~4-C 바로 가기). 노바 팩도 대기실 문(41,42~43)이
    월드맵으로 나간다. 혼든은 마을에서 들어가지만 그 마을은 120칸짜리 다른 맵이고, 하데스 뤼케시온마을은 아직 닫아 둔
    마을이다(건물·상점 없음 — tests WorldMapTests.Reopened). 카드 자리는 원작 field001 의 뤼케시온(317,398) 옆.
  - 입장 레벨 71: 서버는 카드 맵(대기실)으로 드는 워프 중 가장 엄한 제한을 월드맵에도 건다(GameServerHandlers.WorldMapRefusal).
    대기실로 드는 워프는 해안 안쪽 두 갈래(1-B·2-C → 대기실)뿐이라 그 둘에 71을 건다 — 아벨해안처럼 입구에만.
  - 해안 안 워프: 혼든 `db/warp/뤼케해안.txt` 에서 양 끝이 모두 해안인 줄(레벨 99 는 혼든 값이라 버린다).
  - 괴물: 배치는 노바 `mob/뤼케해안/뤼케해안스폰.txt`(혼든과 같은 자리·마릿수, 혼든은 이름 앞에 「변이된」).
    수치는 사용자 2026-10-02 「노바 안에서 구광산에 맞춤」: 노바 값의 강약은 그대로 두고, 가장 센 일렉코아틀(98)이
    노바 광산1층 그림록(99 — 구광산 1층, 체력 77,760 · 최소 공격 2,409) 바로 아래 오게 체력·공격을 같은 배율로 올린다.
    경험치는 구광산과 같은 체력 비례(build-old-EXP_PER_HP). 방어는 노바 -45 그대로.
    3층의 「에스코모이드」는 뺀다 — 노바·혼든 모두 스폰 줄만 있고 정의가 없어(아벨해안의 애스코모이드뿐) 그 서버들에서도 안 나왔다.
  - 드랍: 노바는 일반 괴물이 3천골드·골드아쿠아링 3%, 보스 에리얼이 에리얼의팬던트 40%(하데스에 없음). 모두 골드아쿠아링 —
    일반 괴물은 괴물 템플릿 DropRate 로 2%~4%(센 괴물일수록), 보스는 아이템 확률(60%, 아벨 킹아크퍼스와 같음).

  쓰는 법: python3 scripts/gen/world/build-rucesion-coast.py            # 무엇이 바뀌는지만
           python3 scripts/gen/world/build-rucesion-coast.py --쓰기     # 서버 자료에 쓴다
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))   # scripts/ — lib/
from lib._paths import ROOT
from lib._world import AREAS, EXP_PER_HP, LOOT_RANDOM, SERVER, WARPS, WORLDMAP, text, warp, world_card

HONDEN = Path.home() / "Downloads" / "혼든커뮤니티팩2"
NOVA = ROOT / "data" / "server-packs" / "extracted" / "novaonline"
MONSTERS = SERVER / "templates" / "monsters" / "뤼케시온해안"
NAME = "뤼케시온해안"
LOBBY = f"{NAME}대기실"
LOBBY_DOOR = [(41, 42), (41, 43)]          # 노바: 대기실 → 월드맵
LOBBY_ARRIVAL = (28, 25)                   # 혼든: 마을·해안가가는길 → 대기실 도착 칸
CARD_POINT = (305, 410)                    # 원작 field001 뤼케시온(317,398) 옆
ENTRY_LEVEL = 71
HP_SCALE = 0.99 * 77760 / 5300             # 노바 일렉코아틀(체력 5,300) = 그림록의 99%
DMG_SCALE = 0.99 * 2409 / 220              # 노바 일렉코아틀(최소 공격 220) = 그림록 최소 공격의 99%
AC = -45
# 일반 괴물도 골드아쿠아링 — 괴물마다 확률(템플릿 DropRate), 센 괴물일수록 조금 더(사용자 2026-10-02):
# 체력에 비례해 블루하콘 2% → 일렉코아틀 4%(× DropBoost 1.5 를 거꾸로). 보스 에리얼은 아이템 것(0.4 × 1.5 = 60%).
RING = "골드아쿠아링"
RING_CHANCE = ((56154, 0.02), (76982, 0.04))      # (체력, 한 마리 확률)
BOSS = "에리얼"


def ring_rate(hp):
    (lo_hp, lo), (hi_hp, hi) = RING_CHANCE
    return round((lo + (hi - lo) * (hp - lo_hp) / (hi_hp - lo_hp)) / 1.5, 5)


def rows(name):
    data = json.loads((NOVA / name).read_text(encoding="utf-8"))
    return data if isinstance(data, list) else next(iter(data.values()))


def monster(kind, fields, count, area):
    hp = round(int(fields["체력"]) * HP_SCALE)
    speed = int(fields["속도"])
    return {
        "Name": kind, "BaseName": kind, "AreaID": area,
        "SpawnMax": count, "SpawnType": 2, "SpawnRate": 30, "SpawnSize": 0, "SpawnOnlyOnActiveMaps": False,
        "Image": 0x4000 + int(fields["이미지"]), "ImageVarience": 0,
        "MaximumHP": hp, "MaximumMP": 0, "Exp": round(hp * EXP_PER_HP),
        "DmgMin": round(int(fields["최소공격력"]) * DMG_SCALE), "DmgMax": round(int(fields["최대공격력"]) * DMG_SCALE), "Ac": AC,
        "Level": 1, "MovementSpeed": speed, "EngagedWalkingSpeed": speed, "AttackSpeed": 1000, "CastSpeed": 8000,
        "MoodType": 4, "PathQualifer": 1,
        "LootType": LOOT_RANDOM, "Drops": {"$values": [RING]},
        "ScriptName": "Common Monster", "UpdateMapWide": True, "UpdateRate": 1000.0,
        "Grow": False, "IgnoreCollision": False,
        **({} if kind == BOSS else {"DropRate": ring_rate(hp)}),
    }


def main():
    write = "--쓰기" in sys.argv
    ids = {}
    for path in AREAS.glob(f"{NAME}*.json"):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        ids[data["Name"]] = data["Id"]
    assert len(ids) == 12, ids

    warps = []
    for line in text(HONDEN / "db" / "warp" / "뤼케해안.txt").splitlines():
        p = line.strip().split(",")
        if len(p) < 7 or p[1] not in ids or p[4] not in ids:
            continue                                            # 마을·해안가가는길과 잇는 줄
        level = ENTRY_LEVEL if p[4] == LOBBY else 1
        warps.append(warp(p[1], ids[p[1]], (int(p[2]), int(p[3])), p[4], ids[p[4]], (int(p[5]), int(p[6])), level))
    door = f"warp {LOBBY} to world map"
    warps.append((door, {
        "ActivationMapId": ids[LOBBY],
        "Activations": [{"AreaID": ids[LOBBY], "Location": {"X": x, "Y": y}, "PortalKey": 0} for x, y in LOBBY_DOOR],
        "LevelRequired": 1, "To": {"AreaID": 0, "Location": None, "PortalKey": 1},
        "WarpRadius": 0, "WarpType": "World", "WorldResetWarpId": 0, "WorldTransionWarpId": 0,
        "Description": None, "Group": None, "Name": door,
    }))
    fresh = [n for n, _ in warps if not (WARPS / f"{n}.json").exists()]

    zones = [ids[n] for n in sorted(ids) if n != LOBBY]
    world = world_card(NAME, ids[LOBBY], LOBBY_ARRIVAL, CARD_POINT, zones, warps)

    stats = {m["이름"]: m["fields"] for m in rows("mobs.json") if "뤼케해안" in m["출처"]}
    mobs = [(f"{s['괴물']}@{s['맵']}", monster(s["괴물"], stats[s["괴물"]], int(s["마리수"]), ids[s["맵"]]))
            for s in rows("mob_spawns.json") if s["맵"] in ids and s["괴물"] in stats]

    if write:
        for n, w in warps:
            (WARPS / f"{n}.json").write_text(json.dumps(w, ensure_ascii=False, indent=2), encoding="utf-8")
        WORLDMAP.write_text(json.dumps(world, ensure_ascii=False, indent=2), encoding="utf-8")
        MONSTERS.mkdir(parents=True, exist_ok=True)
        for n, m in mobs:
            (MONSTERS / f"{n}.json").write_text(json.dumps(m, ensure_ascii=False, indent=2), encoding="utf-8")

    for kind, fields in stats.items():
        m = monster(kind, fields, 0, 0)
        chance = f"{m['DropRate'] * 1.5:.1%}" if "DropRate" in m else "아이템 것"
        print(f"  {kind:8} 체력 {m['MaximumHP']:>7,} · 공격 {m['DmgMin']:,}~{m['DmgMax']:,} · 경험치 {m['Exp']:,} · 골드아쿠아링 {chance}")
    print(f"워프 {len(warps)}장(새 {len(fresh)}) · 월드맵 카드 구역 {len(zones)} · 괴물 자리 {len(mobs)}"
          + ("" if write else "  — 미리보기, --쓰기 로 쓴다"))


if __name__ == "__main__":
    main()
