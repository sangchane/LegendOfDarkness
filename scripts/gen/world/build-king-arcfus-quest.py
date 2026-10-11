#!/usr/bin/env python3
"""아벨해안 킹아크퍼스 퀘스트(3서클 이벤트) — 사용자 2026-10-11, 명세 `autopilot/king-arcfus-quest/SPEC.md`.

  - 맵: 원작 `database/assets/MapFiles` 에서 서버가 안 쓰던 신해안 맵(혼든 `metadat/TMap5.txt` 에 「킹아크퍼스의본거지」「뤼케시온신해안외딴길」이
    이 번호대). 이름↔번호는 공략 그림 좌표·모양으로 어림했다 — 외딴길(4747, 40×60) 은 그림 좌표 (23,53)·(22,57) 이 모래길 위다.
    외딴곳만 상설(아벨해안3-C 숨은 칸으로 간다), 나머지는 사본 원본(`static/pack599-mapfiles.tsv` 에 더한다 — 대본이 `map_create` 로 찾는다).
  - 숨은 칸(3-C (20,37))은 밟으면 대본이 도는 워프 — 대지의인장이 있으면 외딴곳으로(자동 사냥·봇이 우연히 빠지지 않게, 리뷰).
    `Description` 「숨은 칸」이라 앱 길잡이(`build-client-guide.py`)가 출구로 적지 않는다.
  - 4-C 암초는 밟으면 대본이 도는 워프(호러캐슬 꼴, `ScriptNpc`). 대본을 도는 진행자 NPC(암초·코마)와 미레 템플릿은 사람이 안 가는
    외딴길 원본에 둔다 — 서버가 어느 맵이든 같은 스크립트의 NPC 를 찾아 돌린다(`GameClient.WarpTo`).
  - 재료: 껍질 셋(터틀·크라켄·퐁퐁이 — 원작 1존 셋 「터틀·크래커·퐁퐁이」) · 거대문어 → 문어다리 · 문어다리를 든 사람이 문어를 잡으면 문어의원한.
    `static/quest-drops.tsv` 에서 제 줄만 바꾼다(에리얼 생성기와 나눠 쓴다).

  쓰는 법: python3 scripts/gen/world/build-king-arcfus-quest.py            # 무엇이 바뀌는지만
           python3 scripts/gen/world/build-king-arcfus-quest.py --쓰기     # 서버 자료에 쓴다
"""
import json
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))   # scripts/ — lib/
from lib._world import AREAS, MAPS, SERVER, WARPS, warp

ORIGINAL = SERVER.parent / "assets" / "MapFiles"
MAPFILES = SERVER / "static" / "pack599-mapfiles.tsv"
QUEST_DROPS = SERVER / "static" / "quest-drops.tsv"
TEMPLATES = SERVER / "templates"
MONSTERS = TEMPLATES / "monsters" / "아벨해안"
ABEL_599 = TEMPLATES / "monsters" / "5.99"
NOOK, TRAIL, ROAD_A, ROAD_B, FAKE, LAIR = ("아벨신해안외딴곳", "아벨신해안외딴길", "신해안의숨겨진길A", "신해안의숨겨진길B",
                                          "거대문어의본거지", "킹아크퍼스의본거지")
# (구역 이름, 원작 맵 번호, 가로, 세로, 대본이 map_create 에 적는 맵 파일 — 상설이면 None)
NEW_AREAS = [
    (NOOK, 4746, 10, 10, None),
    (TRAIL, 4747, 40, 60, "db/maps/kingarcfus/lod4747.map"),
    (ROAD_A, 4748, 14, 14, "db/maps/kingarcfus/lod4748.map"),
    (ROAD_B, 4750, 13, 13, "db/maps/kingarcfus/lod4750.map"),
    (FAKE, 4758, 20, 20, "db/maps/kingarcfus/lod4758.map"),
    (LAIR, 4749, 12, 12, "db/maps/kingarcfus/lod4749.map"),
]
COAST_3C, COAST_4C = "아벨해안3-c", "아벨해안4-c"           # 구역 파일 이름
HOLE = (20, 37)                                            # 3-C 숨은 칸(원작 그림 (18,37) 곁 검은 구멍) — 밟으면 NPC_킹아크퍼스숨은칸(인장이 있으면 외딴곳)
NOOK_EXIT = [((9, 8), (21, 38)), ((9, 9), (21, 38))]       # 외딴곳 동쪽 끝 → 3-C 구멍 곁(브라토를 안 누르고도 나가게)
REEF = (10, 39)                                            # 4-C 바위 틈(원작 그림 「무도가」 자리) — 밟으면 NPC_킹아크퍼스암초
ENTRY_LEVEL = 41
# (이름, 맵, x, y, 그림, 방향, 대사) — 진행자 둘·미레는 외딴길 원본(사람이 안 감)의 바다 구석(호러캐슬 진행자와 같이).
NPCS = [
    ("브라토", NOOK, 6, 5, 16384 + 249, 2, ["바다 밑에서 건진 팬던트가 있다네."]),
    ("대장장이", "아벨무기점", 5, 4, 16414, 2, ["망치 소리가 그칠 날이 없구먼."]),
    ("제단", "밀레스신전", 9, 4, 16384, 2, ["오래된 제단이다. 무언가를 바칠 수 있을 것 같다."]),
    ("미레", TRAIL, 0, 0, 16384 + 61, 2, ["살려 주세요!"]),
    ("킹아크퍼스암초", TRAIL, 1, 0, 16384, 2, []),
    ("킹아크퍼스코마", TRAIL, 2, 0, 16384, 2, []),
    ("킹아크퍼스숨은칸", TRAIL, 3, 0, 16384, 2, []),
]
# (이름, 그림, 떨구는 괴물들, 확률, 잡은 사람이 가져야 할 것)
ITEMS = [
    ("터틀껍질", 33336, ["터틀1", "터틀2"], 0.1, None),
    ("게껍질", 33331, ["크라켄1", "크라켄2"], 0.1, None),
    ("퐁퐁이조각", 33330, ["퐁퐁이1", "퐁퐁이2"], 0.1, None),
    ("문어다리", 33327, ["거대문어"], 1.0, None),
    ("문어의원한", 46242, ["문어"], 1.0, "문어다리"),
    ("대지의인장", 33431, [], 0, None),
    ("해저의팬던트", 33436, [], 0, None),
    ("원한의에센스", 46243, [], 0, None),
]
# (새 괴물, 원본 템플릿, 사는 사본 원본, 가짜인가) — 가짜는 문어다리만(드랍 표), 진짜는 원본 드랍 그대로.
BOSSES = [("거대문어", "킹아크퍼스2@아벨해안4-c", FAKE, True), ("킹아크퍼스", "킹아크퍼스1@아벨해안4-c", LAIR, False)]


def load_areas():
    return {path.stem: json.loads(path.read_text(encoding="utf-8-sig")) for path in AREAS.glob("*.json")}


def write_json(path, data, write, changes):
    text = json.dumps(data, ensure_ascii=False, indent=2)
    old = path.read_text(encoding="utf-8-sig") if path.exists() else None
    if old is not None and json.loads(old) == data:
        return
    changes.append(f"{'새' if old is None else '고침'} {path.relative_to(TEMPLATES.parent)}")
    if write:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")


def npc(name, area, x, y, image, direction, speech):
    return {"Name": f"{name}@{area[0]}#{x},{y}", "AreaID": area[1], "X": x, "Y": y, "Direction": direction, "Image": image,
            "Level": 1, "MaximumHp": 1000, "MaximumMp": 1000, "Speech": speech, "ScriptKey": f"NPC_{name}",
            "DefaultMerchantStock": [], "EnableWalking": False, "EnableTurning": False, "EnableAttacking": False,
            "EnableCasting": False, "WalkRate": 0, "TurnRate": 0, "CastRate": 0, "ChatRate": 0,
            "PathQualifer": 1, "ViewingQualifer": 1}


def main():
    write = "--쓰기" in sys.argv
    areas = load_areas()
    coast = areas[COAST_4C]
    by_name = {a["Name"]: a for a in areas.values()}
    next_id = max(a["Id"] for a in areas.values() if a["Id"] < 60000) + 1

    ids, changes = {}, []
    for name, original, cols, rows, _ in NEW_AREAS:
        if name in by_name:                                   # 이미 넣었다 — 다시 돌려도 같은 번호
            ids[name] = by_name[name]["Id"]
        else:
            ids[name] = next_id
            next_id += 1
            changes.append(f"새 구역 {name} = {ids[name]} (원작 lod{original}, {cols}×{rows})")
        number = ids[name]
        source = ORIGINAL / f"lod{original}.map"
        assert source.stat().st_size == cols * rows * 6, f"lod{original} 크기가 {cols}×{rows} 와 다르다"
        area = {"FilePath": f"../../database/server/maps/lod{number}.map", "Cols": cols, "ContentName": None,
                "Flags": coast["Flags"], "Id": number, "Music": coast["Music"], "Name": name, "Rows": rows,
                "Blocks": [], "ScriptKey": None, "ID": number}
        if write:
            shutil.copyfile(source, MAPS / f"lod{number}.map")
        write_json(AREAS / f"{name}.json", area, write, changes)

    lines = MAPFILES.read_text(encoding="utf-8").splitlines()
    kept = [line for line in lines if not line.startswith("db/maps/kingarcfus/")]
    mapfiles = kept + [f"{pack_file}\t{ids[name]}" for name, _, _, _, pack_file in NEW_AREAS if pack_file]
    if sorted(mapfiles) != sorted(lines):              # 표는 두 퀘스트 생성기가 나눠 쓴다 — 줄 순서는 따지지 않는다
        changes.append("맵 대응표")
        if write:
            MAPFILES.write_text("\n".join(mapfiles) + "\n", encoding="utf-8")

    c3, c4, nook = areas[COAST_3C], areas[COAST_4C], ids[NOOK]
    for area, at, host, about in ((c3, HOLE, "킹아크퍼스숨은칸", "숨은 칸"), (c4, REEF, "킹아크퍼스암초", None)):
        name = f"warp {area['Name']}({at[0]},{at[1]}) runs {host}"
        spot = {"AreaID": area["Id"], "Location": {"X": at[0], "Y": at[1]}, "PortalKey": 0}
        write_json(WARPS / f"{name}.json", {"ActivationMapId": area["Id"], "Activations": [spot], "LevelRequired": ENTRY_LEVEL,
                                            "LevelMaximum": 0, "ScriptNpc": f"NPC_{host}", "To": spot, "WarpRadius": 0,
                                            "WarpType": "Map", "WorldResetWarpId": 0, "WorldTransionWarpId": 0,
                                            "Description": about, "Group": None, "Name": name}, write, changes)   # 「숨은 칸」은 앱 길잡이가 출구로 적지 않는다
    for at, to in NOOK_EXIT:
        file, data = warp(NOOK, nook, at, c3["Name"], c3["Id"], to)
        write_json(WARPS / f"{file}.json", data, write, changes)

    grab = json.loads((TEMPLATES / "items" / "그래브의집게.json").read_text(encoding="utf-8-sig"))
    drops = []
    for item, image, monsters, chance, needs in ITEMS:
        data = {**grab, "Name": item, "DisplayImage": image, "Group": "킹아크퍼스퀘스트/기타"}
        data.pop("DropRate", None)                     # 괴물 목록에 없다 — QuestDrops 가 굴린다
        write_json(TEMPLATES / "items" / f"{item}.json", data, write, changes)
        drops += [f"{monster}\t{item}\t{chance}" + (f"\t{needs}" if needs else "") for monster in monsters]

    ours = {line.split("\t")[0] for line in drops}
    old = QUEST_DROPS.read_text(encoding="utf-8").splitlines() if QUEST_DROPS.exists() else []
    table = [line for line in old if line.split("\t")[0] not in ours] + drops
    if table != old:
        changes.append(f"재료 표 {QUEST_DROPS.name}")
        if write:
            QUEST_DROPS.write_text("\n".join(table) + "\n", encoding="utf-8")

    for monster, source, home, fake in BOSSES:
        data = json.loads((ABEL_599 / f"{source}.json").read_text(encoding="utf-8-sig"))
        data.update({"Name": monster, "BaseName": monster, "AreaID": ids[home], "SpawnMax": 0})
        if fake:
            data["Drops"] = {"$values": []}
        write_json(MONSTERS / f"{monster}@{home}.json", data, write, changes)

    for name, area, x, y, image, way, speech in NPCS:
        number = ids.get(area) or by_name[area]["Id"]
        write_json(TEMPLATES / "mundanes" / f"{name}@{area}#{x},{y}.json",
                   npc(name, (area, number), x, y, image, way, speech), write, changes)

    print("\n".join(changes) or "바뀐 것 없음")
    if not write and changes:
        print("(--쓰기 로 실제로 쓴다)")


if __name__ == "__main__":
    main()
