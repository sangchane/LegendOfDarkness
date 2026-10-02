#!/usr/bin/env python3
"""구광산 — 마인마을 10시 방향 출구로 들어가는 29층 광산을 하데스에 넣는다(맵·워프).

원작의 구광산은 카스마늄 갱도가 나오기 전 마인마을 서쪽(10시) 출구에 있던 광산이다(사용자 2026-10-02).
5.99 팩은 그 출구를 카스마늄광산진입로로 바꿨고 층은 「광산N층」 13장만 남겼다(지금 하데스의 광산대기실·광산1~16층).
혼든 커뮤니티 팩의 「공식길드전용던전」이 구광산 맵 36장을 통째로 쓴다 — 대기실 + 1-1 ~ 29-1(갈래층 포함).
5.99 의 13장은 그중 13장과 바이트까지 같다. 8-1 은 혼든에도 없어(혼든이 카스마늄제4광산8-1 로 메꿨다) 넣지 않는다.

  - 맵: 36장 모두 새 번호로 넣는다. 하데스의 「광산N층」·「리파이너의던전N층」이 같은 맵 파일을 쓰지만 건드리지 않는다 —
    리파이너의던전은 살아 있는 던전이고, 혼든 안에서도 22-1=3-2 · 24-1=17-1 처럼 한 파일을 두 층이 쓰므로 번호를 나누면 층이 엉킨다.
  - 워프: 혼든 `db/warp/길드성/공식길드전용던전.txt` 를 그대로(8-1 로 가는 줄만 뺀다).
  - 드나드는 길: 마인마을(0,49~54) → 구광산대기실(18,47), 구광산대기실(18,49) → 마인마을(2,51).
    대기실 칸은 노바 팩 `Titan.txt`(광산대기실 18,47 도착 · 18,49 나감), 마인마을 칸은 5.99 `Casmanum_Warp.txt` 의 10시 출구.
  - 괴물은 아직 넣지 않는다 — 혼든 수치는 길드 최고 레벨용이라(체력 6만~40만) 사용자가 정한다.

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
from lib._paths import ROOT

HONDEN = Path.home() / "Downloads" / "혼든커뮤니티팩2"
SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
AREAS, MAPS, WARPS = SERVER / "areas", SERVER / "maps", SERVER / "templates" / "warps"
PACK_NAME = "공식길드전용던전"
NAME = "구광산"
FLAGS = 106240           # 하데스 기존 맵과 같은 값(tools/pack-import DEFAULT_MAP_FLAGS)
TOWN = "마인마을"
TOWN_EXIT = [(0, y) for y in range(49, 55)]
LOBBY_IN, LOBBY_OUT, TOWN_IN = (18, 47), (18, 49), (2, 51)


def text(path):
    raw = path.read_bytes()
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("cp949", errors="replace")


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


def warp(src_name, src_id, at, dst_name, dst_id, to):
    name = f"warp {src_name}({at[0]},{at[1]}) to {dst_name}({to[0]},{to[1]})"
    return name, {
        "ActivationMapId": src_id,
        "Activations": [{"AreaID": src_id, "Location": {"X": at[0], "Y": at[1]}, "PortalKey": 0}],
        "LevelRequired": 1,
        "To": {"AreaID": dst_id, "Location": {"X": to[0], "Y": to[1]}, "PortalKey": 0},
        "WarpRadius": 0, "WarpType": "Map", "WorldResetWarpId": 0, "WorldTransionWarpId": 0,
        "Description": None, "Group": None, "Name": name,
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
        warps.append(warp(TOWN, town, at, lobby_name, lobby, LOBBY_IN))
    warps.append(warp(lobby_name, lobby, LOBBY_OUT, TOWN, town, TOWN_IN))

    fresh = [(n, w) for n, w in warps if not (WARPS / f"{n}.json").exists()]
    if write:
        for n, w in fresh:
            (WARPS / f"{n}.json").write_text(json.dumps(w, ensure_ascii=False, indent=2), encoding="utf-8")

    for kind, before, after, number in plan:
        print(f"  {kind:4} {number}  {before} → {after}")
    print(f"맵 {len(ids)}장(새 {len(plan)}) · "
          f"워프 {len(warps)}장(새 {len(fresh)})" + ("" if write else "  — 미리보기, --쓰기 로 쓴다"))


if __name__ == "__main__":
    main()
