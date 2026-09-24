#!/usr/bin/env python3
"""지금 구현된 지역(노비스·수오미)의 맵이 서로 어떻게 이어지는지 뽑는다.

전체 월드맵은 `build-world-map-data.py` 가 따로 만든다 — **이 파일은 그것을 건드리지 않는다.**
여기서 보는 것은 "지금 걸어 다닐 수 있는 곳만" 이고, 묻는 것은 세 가지다.

  · 마을에서 걸어서 닿나 (닿지 않으면 그 맵은 지금 죽은 맵이다)
  · 한 방향으로만 나 있나 (들어가면 못 나오는 곳)
  · 그 맵에 괴물·NPC 가 있나 (문만 있고 아무것도 없는 맵을 가려내려고)

  쓰는 법: python3 scripts/build-region-warp-data.py   → docs/region-warps-data.js
"""
import json
import re
import subprocess
from collections import defaultdict, deque
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
OUT = ROOT / "docs" / "region-warps-data.js"

# 지역과 그 지역의 출발점. 걸어서 닿는지는 여기서부터 잰다.
REGIONS = {"노비스": "노비스마을", "수오미": "수오미마을"}

# 원작 팩의 몬스터 젠 표. 던전인데 괴물이 하나도 없으면 원작도 그런지 여기서 확인한다
# (형제 방은 있는데 이 방만 없으면 "원작도 빈 방", 팩 자체에 이 던전이 없으면 증거 없음).
PACK_SPAWNS = {
    "5.99": ROOT / "data/server-packs/5.99-server/db/mob/Novice/Novice_Spawn.txt",
    "novaonline": ROOT / "data/server-packs/novaonline/db/mob/노비스/spawn.txt",
}

# 맵 이름으로 갈래를 가른다. 위에서부터 먼저 맞는 것을 쓴다.
KINDS = [
    ("마을", ("마을",)),
    ("던전", ("지하던전", "지하동굴", "던전")),
    ("사냥터", ("평원", "사냥터", "숲")),
    ("가게", ("상점", "무기점", "방어구점", "주점", "여관", "음식점", "식당", "잡화")),
    ("집", ("민가", "의집", "대련장")),
]


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def kind_of(name):
    for kind, marks in KINDS:
        if any(mark in name for mark in marks):
            return kind
    return "그밖"


def pack_spawn_maps(path):
    """그 팩 젠 표에 몬스터가 한 마리라도 적힌 맵 이름 집합. 팩이 없으면 빈 집합(증거 없음과 같다)."""
    if not path.exists():
        return set()
    names = set()
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        line = line.strip()
        if not line or line.startswith("//"):
            continue
        field = line.split(",")[0].strip()
        if field:
            names.add(field)
    return names


def dungeon_zone(name):
    """맵 이름 끝의 'C1'·'A3' 같은 영문 한 글자 + 숫자를 떼어 그 던전 묶음 이름을 얻는다."""
    return re.sub(r"[A-Za-z]\d+$", "", name)


def empty_room_evidence(name, spawn_sets):
    """이 던전 방이 원작 팩에도 비어 있는지. 팩이 같은 던전의 형제 방은 담고 있는데 이 방만
    없으면 원작도 빈 방이라는 뜻 — 팩이 이 던전 자체를 안 실었으면(형제 방도 없으면) 증거가 아니다."""
    zone = dungeon_zone(name)
    corroborating = []
    for pack, spawn_map in spawn_sets.items():
        zones = {dungeon_zone(n) for n in spawn_map}
        if zone in zones and name not in spawn_map:
            corroborating.append(pack)
    if not corroborating:
        return None
    return {
        "원작도빈방": True,
        "팩": corroborating,
        "근거": [f"{pack}: {PACK_SPAWNS[pack].relative_to(ROOT)}" for pack in corroborating],
    }


def region_of(name):
    for region in REGIONS:
        if name.startswith(region):
            return region
    return None


def main():
    names = {}
    for path in (SERVER / "areas").glob("*.json"):
        try:
            data = read(path)
        except Exception:
            continue
        names[data["Id"]] = data["Name"]

    wanted = {i: n for i, n in names.items() if region_of(n)}
    spawn_sets = {pack: pack_spawn_maps(path) for pack, path in PACK_SPAWNS.items()}

    # 그 맵에 무엇이 서 있나. 괴물은 템플릿의 AreaID, NPC 는 파일 이름의 "@맵이름" 이 근거다.
    monsters = defaultdict(int)
    for folder in (SERVER / "templates/monsters").iterdir():
        if not folder.is_dir():
            continue
        for path in folder.glob("*.json"):
            try:
                data = read(path)
            except Exception:
                continue
            if data.get("AreaID") in wanted:
                monsters[data["AreaID"]] += 1

    by_name = {n: i for i, n in wanted.items()}
    npcs = defaultdict(int)
    for path in (SERVER / "templates/mundanes").glob("*.json"):
        hit = re.search(r"@([^#]+)#", path.stem)
        if hit and hit.group(1) in by_name:
            npcs[by_name[hit.group(1)]] += 1

    # 워프. 한 칸마다 파일 하나라 같은 두 맵 사이에 여러 칸이 생긴다 — 묶어서 센다.
    tiles = defaultdict(int)
    worldmap = defaultdict(int)
    for path in (SERVER / "templates/warps").glob("*.json"):
        try:
            data = read(path)
        except Exception:
            continue
        target = (data.get("To") or {}).get("AreaID")
        for activation in data.get("Activations") or []:
            source = activation.get("AreaID")
            if source not in wanted and target not in wanted:
                continue
            if not target:  # To.AreaID 0 = 월드맵으로 나간다
                if source in wanted:
                    worldmap[source] += 1
                continue
            tiles[(source, target)] += 1

    result = {}
    for region, start in REGIONS.items():
        ids = {i for i, n in wanted.items() if region_of(n) == region}
        edges = [{"부터": s, "까지": t, "칸": c, "밖으로": t not in ids}
                 for (s, t), c in tiles.items() if s in ids or t in ids]

        # 왕복인지 한 방향인지. 되돌아오는 칸이 없으면 들어가면 못 나온다.
        pairs = {(e["부터"], e["까지"]) for e in edges}
        for edge in edges:
            edge["왕복"] = (edge["까지"], edge["부터"]) in pairs

        # 출발점에서 걸어서 닿는 맵. 워프 방향을 따라간다.
        nexts = defaultdict(set)
        for edge in edges:
            nexts[edge["부터"]].add(edge["까지"])
        reached, queue = set(), deque([by_name.get(start)])
        while queue:
            here = queue.popleft()
            if here is None or here in reached:
                continue
            reached.add(here)
            queue.extend(nexts[here])

        nodes = []
        for area in sorted(ids):
            name = wanted[area]
            monster_count = monsters.get(area, 0)
            node = {
                "번호": area,
                "이름": name,
                "갈래": kind_of(name),
                "출발점": name == start,
                "닿음": area in reached,
                "괴물": monster_count,
                "NPC": npcs.get(area, 0),
                "월드맵": worldmap.get(area, 0),
                "나가는곳": len(nexts.get(area, ())),
            }
            if monster_count == 0 and node["갈래"] == "던전":
                node["빈방"] = empty_room_evidence(name, spawn_sets)
            nodes.append(node)

        # 지역 밖으로 이어지는 곳 — 다음에 무엇을 채워야 하는지가 여기 보인다.
        outside = sorted({(e["부터"], e["까지"]) for e in edges if e["밖으로"]})
        result[region] = {
            "출발점": by_name.get(start),
            "맵": nodes,
            "연결": edges,
            "밖": [{"부터": names.get(s, s), "까지": names.get(t, t)} for s, t in outside],
            "셈": {
                "맵": len(nodes),
                "닿음": sum(1 for n in nodes if n["닿음"]),
                "고아": sum(1 for n in nodes if not n["닿음"]),
                "한방향": sum(1 for e in edges if not e["왕복"]),
            },
        }

    try:
        pointer = subprocess.run(
            ["git", "-C", str(SERVER.parents[1]), "rev-parse", "--short", "HEAD"],
            capture_output=True, text=True, check=True).stdout.strip()
    except Exception:
        pointer = ""

    payload = {
        "생성": "scripts/build-region-warp-data.py",
        "서버포인터": pointer,
        "이름": {str(i): n for i, n in names.items()},
        "지역": result,
    }
    OUT.write_text(
        "window.LOD_REGION_WARPS = " + json.dumps(payload, ensure_ascii=False) + ";\n",
        encoding="utf-8")

    for region, data in result.items():
        c = data["셈"]
        print(f"{region}: 맵 {c['맵']} · 닿음 {c['닿음']} · 고아 {c['고아']} · 한방향 {c['한방향']}")
    print(f"→ {OUT.relative_to(ROOT)}  ({OUT.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
