#!/usr/bin/env python3
"""생태계 봇의 사냥터 표 — 맵마다 적정 레벨(설계 autopilot/eco-bots/, FR-002).
괴물 템플릿의 Level 칸은 모두 1이라 쓸 수 없다. 사용자 기준 「약 50마리에 한 레벨」(우드랜드 서·북 경험치, 2026-10-04)을 거꾸로 써서,
그 맵 괴물 경험치 중앙값의 50배로 한 레벨을 올릴 수 있는 가장 높은 레벨을 적정 레벨로 삼는다(서버 ExperienceCurve 표).
봇은 순간이동(0xF1 8)으로 들어가 워프의 입장 레벨 검사를 거치지 않으므로, 구역의 입장 레벨(guide.txt zone)보다 낮게 적지 않는다.
경험치만 보면 위험이 안 보인다(서의우드랜드1-1 은 경험치로 2레벨인데 공격 55~60 말벌이 섞여 2레벨 봇이 몇 초 만에 죽었다) —
가장 센 괴물에게 HITS_TO_SURVIVE 대를 버티는 레벨(최대 체력 ≈ 150 + 33×(레벨−1), 서버 Levelup 의 콘+30 에서 콘을 3으로 낮게 잡음)보다도 낮게 적지 않는다.
월드맵 구역 말고 노비스 사냥터(노비스평원·노비스지하던전, 서버 areas 이름)도 넣는다 — 새 캐릭터가 서의우드랜드1-1 에서 바로 죽었다(2026-10-06 격리 서버).
  쓰는 법: python3 scripts/gen/eco/build-eco-grounds.py
  입력:   mobile/client/assets/world/guide.txt 의 zone 줄(월드맵 사냥터 구역) · 서버 areas(노비스 사냥터 이름) · templates/monsters
  산출물: mobile/client/assets/world/eco-grounds.txt — 줄마다 「맵 적정레벨 이름」(알맹이 EcoGrounds.Read)
"""
import json
import statistics
import sys as _sys, pathlib as _pathlib
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT

SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
WORLD = ROOT / "mobile" / "client" / "assets" / "world"
KILLS_PER_LEVEL = 50
HITS_TO_SURVIVE = 8

# 서버 src/Hades.Server.Base/Types/ExperienceCurve.cs — Table[L+1] 이 L 에서 L+1 로 가는 데 드는 경험치.
CURVE = [
    0, 0, 600, 2400, 5400, 9600, 15450, 22248, 30282, 39552,
    50058, 61800, 74778, 88992, 104442, 121128, 139050, 158208, 178602, 200232,
    223098, 247200, 272538, 299112, 326922, 355968, 386250, 417768, 450522, 484512,
    519738, 556200, 593898, 632832, 673002, 714408, 757050, 800928, 846042, 892392,
    939978, 988800, 1038858, 1090152, 1142682, 1196448, 1251450, 1307688, 1365162, 1423872,
    1484872, 1547682, 1612112, 1678232, 1746220, 1814208, 1883931, 1955573, 2029306, 2105199,
    2183331, 2263802, 2346683, 2432048, 2520020, 2610641, 2704073, 2800417, 2899774, 3030646,
    3164727, 3302150, 3442996, 3587478, 3735583, 3887556, 4043377, 4202736, 4366198, 4533823,
    4705775, 4882130, 5062983, 5248444, 5438595, 5633552, 5833406, 6038257, 6248219, 6463368,
    6683821, 6909673, 7141024, 7377982, 7620645, 7869109, 8123470, 8383835, 8582713, 8990567,
]


def level_for(exp: int) -> int:
    """괴물 하나 경험치 → 50마리로 한 레벨이 오르는 가장 높은 레벨(1~99)."""
    best = 1
    for level in range(1, 99):
        if CURVE[level + 1] <= exp * KILLS_PER_LEVEL:
            best = level
    return 99 if CURVE[99] <= exp * KILLS_PER_LEVEL else best


def safe_level(hit: int) -> int:
    """가장 센 괴물 한 대(DmgMax) → 그것을 HITS_TO_SURVIVE 대 버티는 가장 낮은 레벨(1~99)."""
    for level in range(1, 100):
        if 150 + 33 * (level - 1) >= hit * HITS_TO_SURVIVE:
            return level
    return 99


def main() -> None:
    exps: dict[int, list[int]] = {}
    hits: dict[int, int] = {}
    for path in sorted((SERVER / "templates" / "monsters").rglob("*.json")):
        try:
            monster = json.loads(path.read_text(encoding="utf-8-sig"))
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
        area, exp = int(monster.get("AreaID") or 0), int(monster.get("Exp") or 0)
        if area and exp > 0 and int(monster.get("SpawnMax") or 0) > 0:
            exps.setdefault(area, []).append(exp)
            hits[area] = max(hits.get(area, 0), int(monster.get("DmgMax") or 0))

    zones = {}
    for line in (WORLD / "guide.txt").read_text(encoding="utf-8").splitlines():
        part = line.split(" ", 4)
        if part[0] == "zone" and len(part) == 5:
            zones[int(part[2])] = (int(part[3]), part[4])

    for path in (SERVER / "areas").glob("*.json"):
        area = json.loads(path.read_text(encoding="utf-8-sig"))
        if str(area["Name"]).startswith(("노비스평원", "노비스지하던전")):
            zones.setdefault(int(area["ID"]), (1, area["Name"]))

    rows = sorted((max(entry, level_for(int(statistics.median(exps[m]))), safe_level(hits[m])), m, name)
                  for m, (entry, name) in zones.items() if m in exps)
    lines = ["# scripts/gen/eco/build-eco-grounds.py 가 만든다. 손으로 고치지 말 것.",
             "# 맵 적정레벨 이름 — 적정 레벨 = 그 맵 괴물 경험치 중앙값 × 50 으로 한 레벨이 오르는 가장 높은 레벨, 입장 레벨·가장 센 괴물 8대를 버티는 레벨보다 낮지 않게"]
    lines += [f"{m} {level} {name}" for level, m, name in rows]
    (WORLD / "eco-grounds.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"eco-grounds.txt: 사냥터 {len(rows)} (구역 {len(zones)} 중 괴물 있는 맵)")


if __name__ == "__main__":
    main()
