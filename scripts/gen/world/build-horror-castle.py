#!/usr/bin/env python3
"""호러캐슬 — 원작식 파티 방(사용자 2026-10-04). SPEC `plans/horror-castle-spec-2026-10-04.md`.

입구5 `호러캐슬입장`(99레벨·3천골드)이 메인홀(20719)로 보내지만, 메인홀에서 방으로 드는 길이 없었다. 방은 5.99 스크립트
`호러캐슬`(Pack599/Npcs, 그대로)이 그룹마다 사본(원본 호러캐슬1 = lod0001, 15×15)을 짓고 괴물 17마리를 세운다(`mob_spawn`).
다 잡고 위 문(9,0)을 밟으면 `호러캐슬다음방` 이 같은 사본에 다시 17마리.

  - 메인홀 문: 5.99 팩에는 이 스크립트를 부르는 워프 줄이 없다(`Warp_script.txt`). 같은 맵 파일(lod6002)을 쓰는 혼든
    `warp/마이소시아/호러캐슬.txt` 의 안쪽 문 (22~26,10) 을 종류 3(스크립트 워프, 99레벨)으로, 아래 문 (22~26,19) → 입구5(9,4).
  - 스크립트 주인 NPC: 종류 3 워프는 같은 맵에 선 그 스크립트의 NPC 가 돌린다(포테의숲오솔길입장처럼). 메인홀 위 벽에 둘 —
    `호러캐슬` 은 메인홀 문이, `호러캐슬다음방` 은 사본의 (9,0) 이 부른다(사본에는 NPC 가 없어 서버가 어디서든 찾는다).
  - 괴물 12종(5.99 `mob/HorrorCastle/HorrorCastle_Monster.txt`): AreaID 20714 · SpawnMax 0 — 스스로는 서지 않고 스크립트만 세운다.
    수치 「노바 안에서 맞춤」: …1 = 노바 `mob/호러캐슬/호러캐슬.txt` 의 체력·공격·방어 그대로, …2 = 노바 × 5.99 의 …2/…1 비율.
    경험치 = 5.99 ÷ 7.3(하데스 나눗수). 금화 최소 10,000(구광산). 그림·속도는 5.99.
    드랍 = 5.99 목록 중 하데스 아이템 템플릿에 있는 것, LootType Random, 괴물 DropRate 0.003(…1)·0.0032(…2) — × 1.5 해도 0.5% 아래
    (구광산 규칙). 아이템 쪽 DropRate(헬옷 0.003, build-gear-drops)는 건드리지 않는다.

  쓰는 법: python3 scripts/gen/world/build-horror-castle.py            # 무엇이 바뀌는지만
           python3 scripts/gen/world/build-horror-castle.py --쓰기     # 서버 자료에 쓴다
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))   # scripts/ — lib/
from lib._paths import ROOT
from lib._world import AREAS, LOOT_RANDOM, SERVER, WARPS, warp

PACKS = ROOT / "data" / "server-packs" / "extracted"
MONSTERS = SERVER / "templates" / "monsters" / "호러캐슬"
MUNDANES = SERVER / "templates" / "mundanes"
ITEMS = SERVER / "templates" / "items"
HALL, ROOM, LOBBY = "호러캐슬메인홀", "호러캐슬1", "죽음의마을입구5"
DOOR = [(x, 10) for x in range(22, 27)]        # 혼든 메인홀 안쪽 문
EXIT = [(x, 19) for x in range(22, 27)]        # 혼든 메인홀 아래 문 → 입구5(9,4)
EXIT_TO = (9, 4)
ENTRY_LEVEL = 99
HOSTS = {"호러캐슬": (0, 0), "호러캐슬다음방": (1, 0)}   # 메인홀 위 벽 — 스크립트 주인(그림 없는 NPC)
EXPERIENCE_DIVISOR = 7.3                       # tools/pack-import EXPERIENCE_DIVISOR
GOLD_MINIMUM = 10000
DROP_RATE = {"1": 0.003, "2": 0.0032}          # × DropBoost 1.5 = 0.45% · 0.48%
STATS = ("MaximumHP", "DmgMin", "DmgMax", "Ac")
FIELDS = ("체력", "최소공격력", "최대공격력", "방어력")


def mobs(pack, where):
    data = json.loads((PACKS / pack / "mobs.json").read_text(encoding="utf-8"))
    rows = data if isinstance(data, list) else next(iter(data.values()))
    return {m["이름"]: m["fields"] for m in rows if where in m["출처"]}


def drop_names(listed):
    """5.99 드롭 칸 ["3", "헬나이트아머", ["3", "헬씨프아머"], …] 의 이름들."""
    out = []
    for cell in listed or []:
        if isinstance(cell, list):
            out += drop_names(cell)
        elif not cell.isdigit():
            out.append(cell)
    return out


def area_id(name):
    return json.loads((AREAS / f"{name}.json").read_text(encoding="utf-8-sig"))["Id"]


def script_warp(src_name, src_id, at, npc):
    name = f"warp {src_name}({at[0]},{at[1]}) runs {npc}"
    spot = {"AreaID": src_id, "Location": {"X": at[0], "Y": at[1]}, "PortalKey": 0}
    return name, {"ActivationMapId": src_id, "Activations": [spot], "LevelRequired": ENTRY_LEVEL, "LevelMaximum": 0,
                  "ScriptNpc": f"NPC_{npc}", "To": spot, "WarpRadius": 0, "WarpType": "Map",
                  "WorldResetWarpId": 0, "WorldTransionWarpId": 0, "Description": None, "Group": None, "Name": name}


def host(npc, hall, at):
    name = f"{npc}@{HALL}#{at[0]},{at[1]}"
    return name, {"Name": name, "AreaID": hall, "X": at[0], "Y": at[1], "Direction": 2, "Image": 16384, "Level": 1,
                  "MaximumHp": 1000, "MaximumMp": 1000, "Speech": [], "ScriptKey": f"NPC_{npc}", "DefaultMerchantStock": [],
                  "EnableWalking": False, "EnableTurning": False, "EnableAttacking": False, "EnableCasting": False,
                  "WalkRate": 0, "TurnRate": 0, "CastRate": 0, "ChatRate": 0, "PathQualifer": 1, "ViewingQualifer": 1}


def monsters(room):
    old, nova = mobs("5.99-server", "HorrorCastle/"), mobs("novaonline", "/호러캐슬/호러캐슬.txt")
    have = {p.stem for p in ITEMS.glob("*.json")}
    out = []
    for name, f in sorted(old.items()):
        base, tier = name[:-1], name[-1]
        n = nova[base]
        scale = {k: int(f[k]) / int(old[base + "1"][k]) if tier == "2" else 1 for k in FIELDS}
        stats = {s: round(int(n[k]) * scale[k]) for s, k in zip(STATS, FIELDS)}
        drops = [d for d in drop_names(f.get("드롭아이템")) if d in have]
        speed = int(f.get("속도") or 1000)
        out.append({
            "Name": name, "BaseName": name, "AreaID": room,
            "SpawnMax": 0, "SpawnType": 2, "SpawnRate": int(f.get("젠타임") or 20), "SpawnSize": 0, "SpawnOnlyOnActiveMaps": False,
            "Image": 0x4000 + int(f["이미지"]), "ImageVarience": 0,
            **stats, "MaximumMP": 0, "Exp": round(int(f["경험치"]) / EXPERIENCE_DIVISOR),
            "Level": 1, "MovementSpeed": speed, "EngagedWalkingSpeed": speed, "AttackSpeed": 1000, "CastSpeed": 8000,
            "MoodType": 2, "PathQualifer": 1,   # 99레벨 사냥터는 모두 선공(사용자 2026-10-05) — 4 는 스폰 때 반반
            "LootType": LOOT_RANDOM, "Drops": {"$values": drops}, "DropRate": DROP_RATE[tier],
            "ScriptName": "Common Monster", "UpdateMapWide": True, "UpdateRate": 1000.0,
            "Grow": False, "IgnoreCollision": False, "GoldMinimum": GOLD_MINIMUM,
        })
    return out


def main():
    write = "--쓰기" in sys.argv
    hall, room, lobby = area_id(HALL), area_id(ROOM), area_id(LOBBY)

    warps = [script_warp(HALL, hall, at, "호러캐슬") for at in DOOR]
    warps += [warp(HALL, hall, at, LOBBY, lobby, EXIT_TO) for at in EXIT]
    hosts = [host(npc, hall, at) for npc, at in HOSTS.items()]
    mobs_out = monsters(room)

    if write:
        for n, w in warps:
            (WARPS / f"{n}.json").write_text(json.dumps(w, ensure_ascii=False, indent=2), encoding="utf-8")
        for n, h in hosts:
            (MUNDANES / f"{n}.json").write_text(json.dumps(h, ensure_ascii=False, indent=2), encoding="utf-8")
        MONSTERS.mkdir(parents=True, exist_ok=True)
        for m in mobs_out:
            (MONSTERS / f"{m['Name']}@{ROOM}.json").write_text(json.dumps(m, ensure_ascii=False, indent=2), encoding="utf-8")

    for m in mobs_out:
        print(f"  {m['Name']:8} 체력 {m['MaximumHP']:>6,} · 공격 {m['DmgMin']}~{m['DmgMax']} · 방어 {m['Ac']} · 경험치 {m['Exp']:,}"
              f" · 드랍 {m['DropRate']} {', '.join(m['Drops']['$values'])}")
    print(f"메인홀 문 {len(DOOR)}(스크립트) · 나가는 문 {len(EXIT)} · 스크립트 NPC {len(hosts)} · 괴물 {len(mobs_out)}"
          + ("" if write else "  — 미리보기, --쓰기 로 쓴다"))


if __name__ == "__main__":
    main()
