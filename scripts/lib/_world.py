"""맵·워프·괴물 생성기가 같이 쓰는 것 — 서버 자료 경로, 팩 글 읽기, 워프 템플릿, 월드맵 카드, 경험치·드랍 갈래."""
import json
from pathlib import Path

from lib._paths import ROOT

SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
AREAS, MAPS, WARPS = SERVER / "areas", SERVER / "maps", SERVER / "templates" / "warps"
# 경험치는 카스마늄 갱도의 경험치/체력(1,309,240/450,000)에 하데스 나눗수 7.3(tools/pack-import EXPERIENCE_DIVISOR) — 구광산·뤼케시온해안
EXP_PER_HP = 1309240 / 450000 / 7.3
LOOT_RANDOM, LOOT_NONE = 1 << 1, 256        # Random 이어야 DropRate 가 굴러간다(GearDropTests). 금화는 깃발과 상관없이 늘 준다


def text(path: Path) -> str:
    """팩 글 — UTF-8 이 아니면 CP949."""
    raw = path.read_bytes()
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("cp949", errors="replace")


def warp(src_name, src_id, at, dst_name, dst_id, to, level=1):
    """맵 워프 템플릿 하나 — (파일 이름, 내용)."""
    name = f"warp {src_name}({at[0]},{at[1]}) to {dst_name}({to[0]},{to[1]})"
    return name, {
        "ActivationMapId": src_id,
        "Activations": [{"AreaID": src_id, "Location": {"X": at[0], "Y": at[1]}, "PortalKey": 0}],
        "LevelRequired": level,
        "To": {"AreaID": dst_id, "Location": {"X": to[0], "Y": to[1]}, "PortalKey": 0},
        "WarpRadius": 0, "WarpType": "Map", "WorldResetWarpId": 0, "WorldTransionWarpId": 0,
        "Description": None, "Group": None, "Name": name,
    }


WORLDMAP = SERVER / "templates" / "worldmaps" / "temuair.json"


def world_card(display, lobby_id, arrival, point, zone_ids, warps):
    """월드맵 사냥터 카드 하나를 넣은(같은 이름은 바꾼) 월드맵 — 카드는 대기실로, 구역마다 그 구역으로 드는 워프의
    도착 칸 하나(워프 이름 순으로 처음 것)로 바로 간다. 레벨은 서버가 대기실로 드는 워프에서 읽는다(WorldMapRefusal)."""
    arrivals = {}
    for _, w in sorted(warps, key=lambda nw: nw[0]):
        to = w["To"]
        if to["AreaID"] in zone_ids:
            arrivals.setdefault(to["AreaID"], (to["Location"]["X"], to["Location"]["Y"]))
    zones = [{"AreaID": z, "Location": {"X": arrivals[z][0], "Y": arrivals[z][1]}, "PortalKey": 0} for z in zone_ids]
    card = {"Destination": {"AreaID": lobby_id, "Location": {"X": arrival[0], "Y": arrival[1]}, "PortalKey": 0},
            "DisplayName": display, "PointX": point[0], "PointY": point[1], "Zones": zones}
    world = json.loads(WORLDMAP.read_text(encoding="utf-8-sig"))
    world["Portals"] = [p for p in world["Portals"] if p["DisplayName"] != display] + [card]
    return world
