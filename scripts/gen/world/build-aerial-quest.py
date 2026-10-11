#!/usr/bin/env python3
"""뤼케시온해안 에리얼 퀘스트(4서클 이벤트) — 사용자 2026-10-11, 명세 `autopilot/aerial-quest/SPEC.md`.

  - 맵: 원작 3-D·게길·대기실·에리얼 방 맵은 가진 자료 어디에도 없다(서버 뤼케시온해안 12장도 아벨해안 복사본). 사용자 「닮은 원작 맵으로」:
    3-D = 원작 `database/assets/MapFiles/lod6625`(80×50 야자수 모래사장, 상설 사냥터) · 게길 = `lod6627`(70×30 바다를 낀 긴 모래길,
    사본 원본으로만 쓴다) · 대기실·에리얼 방 = 3-D 사본. 사본은 5.99 `map_create` 가 팩 맵 파일 이름으로 찾으므로
    `static/pack599-mapfiles.tsv` 에 두 줄을 더한다(팩에는 없는 이름 — 우리 대본만 쓴다).
  - 바다: 원작 두 맵은 바다가 바닥 타일뿐이라 걸어 들어갈 수 있다 → 바다 타일 칸을 구역 JSON `Blocks` 로 막는다(서버 `Area.cs`,
    사본도 그대로 복사 · 앱 맵 자료는 `build-client-maps.py` 가 같은 칸을 막는다).
  - 3-D 로 드는 길: 3-C 는 동쪽 끝이 바다라 가장자리 출구 대신 동쪽 해변 칸에 문을 둔다. 3-D 괴물은 3-C 와 같은 줄(구역만 바꿈).
  - 재료: 블루하콘무기·레드하콘다리(그림은 기존 잡템 251 푸른 칼날 · 563 빨간 다리) — 하콘이 드랍 목록과 따로 굴려 떨군다
    (`static/quest-drops.tsv`, 서버 `Types/QuestDrops.cs`). 목록에 넣으면 칸수로 나뉘어 골드아쿠아링이 옅어진다.
  - 게(그림 그래브, 스크립트 「에리얼게」 = `scripts/Monsters/AerialCrab.cs`) · 입구 NPC · 마레(입구 대본이 에리얼의방 사본에 세운다 —
    템플릿은 사람이 안 가는 게길 원본에 둔다).

  쓰는 법: python3 scripts/gen/world/build-aerial-quest.py            # 무엇이 바뀌는지만
           python3 scripts/gen/world/build-aerial-quest.py --쓰기     # 서버 자료에 쓴다
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
COAST_MONSTERS = TEMPLATES / "monsters" / "뤼케시온해안"
COAST_C = "뤼케시온해안3-c"                       # 3-C 구역 파일 이름(서버 이름은 「뤼케시온해안3-C」)
SHORE, TRAIL = "뤼케시온해안3-D", "뤼케시온해안게길"
# (구역 이름, 원작 맵 번호, 가로, 세로, 대본이 map_create 에 적는 맵 파일)
NEW_AREAS = [
    (SHORE, 6625, 80, 50, "db/maps/aerial/lod6625.map"),
    (TRAIL, 6627, 70, 30, "db/maps/aerial/lod6627.map"),
]
WATER = set(range(7943, 7966)) | {8178, 8179}      # 원작 바다 바닥 타일(두 맵 모두) — 물가 섞인 타일은 빼고 온전한 바다만
# 3-C 동쪽 해변(36,35~38) ↔ 3-D 서쪽 가장자리(0,20~23). 도착은 한 칸 안쪽.
COAST_DOOR = [((36, 35 + i), (2, 20 + i)) for i in range(4)]
SHORE_DOOR = [((0, 20 + i), (34, 35 + i)) for i in range(4)]
ENTRANCE_NPC = ("에리얼입구", 57, 25, 16384 + 249, 3)     # 3-D 동쪽 천막 자리(원작 블로그 지도) · 통 든 사내 그림
MARE_NPC = ("마레", 35, 5, 16384 + 61, 2)                  # 게길 원본(사람이 안 감) — 대본이 사본에 옮겨 세운다 · 분홍 옷 소녀
MATERIALS = [("블루하콘무기", 32768 + 251, "블루하콘"), ("레드하콘다리", 32768 + 563, "레드하콘")]
MATERIAL_CHANCE = 0.15                             # ※ 한 마리 15% — 제물 몇 벌은 금방 모인다
CRAB = "게"
CRAB_HP = 2_000_000                                # 잡아서 길을 트지 못하게(잡아도 상은 없다)


def load_areas():
    return {path.stem: json.loads(path.read_text(encoding="utf-8-sig")) for path in AREAS.glob("*.json")}


def water_blocks(original, cols, rows):
    raw = (ORIGINAL / f"lod{original}.map").read_bytes()
    return [{"X": x, "Y": y} for y in range(rows) for x in range(cols)
            if int.from_bytes(raw[(y * cols + x) * 6:(y * cols + x) * 6 + 2], "little") in WATER]


def write_json(path, data, write, changes):
    text = json.dumps(data, ensure_ascii=False, indent=2)
    old = path.read_text(encoding="utf-8-sig") if path.exists() else None
    if old is not None and json.loads(old) == data:
        return
    changes.append(f"{'새' if old is None else '고침'} {path.relative_to(TEMPLATES.parent)}")
    if write:
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
    coast = areas[COAST_C]
    by_name = {a["Name"]: a for a in areas.values()}
    next_id = max(a["Id"] for a in areas.values() if a["Id"] < 60000) + 1

    ids, changes = {}, []
    for name, original, cols, rows, pack_file in NEW_AREAS:
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
                "Blocks": water_blocks(original, cols, rows), "ScriptKey": None, "ID": number}
        if write:
            shutil.copyfile(source, MAPS / f"lod{number}.map")
        write_json(AREAS / f"{name}.json", area, write, changes)

    lines = MAPFILES.read_text(encoding="utf-8").splitlines()
    kept = [line for line in lines if not line.startswith("db/maps/aerial/")]
    mapfiles = kept + [f"{pack_file}\t{ids[name]}" for name, _, _, _, pack_file in NEW_AREAS]
    if mapfiles != lines:
        changes.append(f"맵 대응표 {len(NEW_AREAS)}줄")
        if write:
            MAPFILES.write_text("\n".join(mapfiles) + "\n", encoding="utf-8")

    coast_name, coast_id, shore = coast["Name"], coast["Id"], ids[SHORE]
    for at, to in COAST_DOOR:
        file, data = warp(coast_name, coast_id, at, SHORE, shore, to)
        write_json(WARPS / f"{file}.json", data, write, changes)
    for at, to in SHORE_DOOR:
        file, data = warp(SHORE, shore, at, coast_name, coast_id, to)
        write_json(WARPS / f"{file}.json", data, write, changes)

    for path in sorted(COAST_MONSTERS.glob("*@뤼케시온해안3-C.json")):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        data["AreaID"] = shore
        write_json(COAST_MONSTERS / path.name.replace("3-C", "3-D"), data, write, changes)

    grab = json.loads((TEMPLATES / "items" / "그래브의집게.json").read_text(encoding="utf-8-sig"))
    for item, image, _ in MATERIALS:
        data = {**grab, "Name": item, "DisplayImage": image, "Group": "에리얼퀘스트/기타"}
        data.pop("DropRate", None)                     # 하콘 목록에 없다 — QuestDrops 가 굴린다
        write_json(TEMPLATES / "items" / f"{item}.json", data, write, changes)

    drops = "".join(f"{monster}\t{item}\t{MATERIAL_CHANCE}\n" for item, _, monster in MATERIALS)
    if not QUEST_DROPS.exists() or QUEST_DROPS.read_text(encoding="utf-8") != drops:
        changes.append(f"재료 표 {QUEST_DROPS.name}")
        if write:
            QUEST_DROPS.write_text(drops, encoding="utf-8")

    crab = json.loads((TEMPLATES / "monsters" / "5.99" / "그래브@아벨해안2-a.json").read_text(encoding="utf-8-sig"))
    crab.update({"Name": CRAB, "BaseName": CRAB, "AreaID": ids[TRAIL], "SpawnMax": 0, "MaximumHP": CRAB_HP, "Exp": 0,
                 "DmgMin": 0, "DmgMax": 0, "MoodType": 0, "MovementSpeed": 2500, "EngagedWalkingSpeed": 2500,
                 "LootType": 256, "Drops": {"$values": []}, "ScriptName": "에리얼게", "Gold": 0, "GoldChance": 0})
    write_json(COAST_MONSTERS / f"{CRAB}@{TRAIL}.json", crab, write, changes)

    name, x, y, image, way = ENTRANCE_NPC
    write_json(TEMPLATES / "mundanes" / f"{name}@{SHORE}#{x},{y}.json",
               npc(name, (SHORE, shore), x, y, image, way, ["해안의 에리얼이 날뛰고 있소. 마레가 붙잡혀 있다오."]), write, changes)
    name, x, y, image, way = MARE_NPC
    write_json(TEMPLATES / "mundanes" / f"{name}@{TRAIL}#{x},{y}.json",
               npc(name, (TRAIL, ids[TRAIL]), x, y, image, way, ["살려 주세요!"]), write, changes)

    print("\n".join(changes) or "바뀐 것 없음")
    if not write and changes:
        print("(--쓰기 로 실제로 쓴다)")


if __name__ == "__main__":
    main()
