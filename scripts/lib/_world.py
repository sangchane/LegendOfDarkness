"""맵·워프·괴물 생성기가 같이 쓰는 것 — 서버 자료 경로, 팩 글 읽기, 워프 템플릿, 경험치·드랍 갈래."""
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
