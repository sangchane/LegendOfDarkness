#!/usr/bin/env python3
"""생태계 봇이 맵 사이를 걸어서 가는 길 — 워프 칸과 월드맵(설계 autopilot/eco-bots/walk-SPEC.md, 결정 18).
guide.txt 의 exit 줄에는 도착 맵·칸이 없어 서버 워프 템플릿에서 바로 뽑는다. 레벨은 서버가 보는 그대로 —
Map 워프는 그 워프의 LevelRequired·LevelMaximum(GameClient.WarpTo), 월드맵에서 고르는 곳은 그 맵으로 드는 워프 중 가장 엄한
최소·최대(GameServerHandlers.WorldMapRefusal), 구역은 그 카드(입구)의 것도 함께 지킨다(TraverseWorldMap).
ScriptNpc 가 붙은 워프는 NPC 스크립트가 옮겨 도착지가 정해져 있지 않아 길로 쓰지 않고, 걷다 밟지 않게 block 줄로만 적는다.
  쓰는 법: python3 scripts/gen/eco/build-eco-links.py [--쓰기]   (없으면 줄 수만 보여 준다)
  입력:   서버 templates/warps · templates/worldmaps
  산출물: mobile/client/assets/world/links.txt — link·gate·field·block 줄(알맹이 EcoLinks.Read)
"""
import json
import sys as _sys, pathlib as _pathlib
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT

TEMPLATES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates"
OUT = ROOT / "mobile" / "client" / "assets" / "world" / "links.txt"


def load(folder):
    return [json.loads(path.read_text(encoding="utf-8-sig")) for path in sorted((TEMPLATES / folder).glob("*.json"))]


def refusal(into, area):
    """서버 WorldMapRefusal — (가장 엄한 최소, 가장 엄한 최대 · 0 = 없음). 드는 워프가 없으면 (0, 0)."""
    warps = into.get(area, [])
    low = max((int(w.get("LevelRequired") or 0) for w in warps), default=0)
    highs = [int(w.get("LevelMaximum") or 0) for w in warps if int(w.get("LevelMaximum") or 0) > 0]
    return low, min(highs, default=0)


def main():
    warps = load("warps")
    into = {}
    for w in warps:
        into.setdefault(int(w["To"]["AreaID"]), []).append(w)

    links, gates, blocks = set(), set(), set()
    for w in warps:
        for a in w["Activations"]:
            spot = (int(a["AreaID"]), int(a["Location"]["X"]), int(a["Location"]["Y"]))
            if w.get("ScriptNpc"):
                blocks.add(spot)
            elif w["WarpType"] == "Map" and w["To"].get("Location"):
                to = w["To"]
                links.add((*spot, int(to["AreaID"]), int(to["Location"]["X"]), int(to["Location"]["Y"]),
                           int(w.get("LevelRequired") or 0), int(w.get("LevelMaximum") or 0)))
            elif w["WarpType"] != "Map":
                gates.add(spot)

    fields = {}
    for world in load("worldmaps"):
        for card in world["Portals"]:
            card_low, card_high = refusal(into, int(card["Destination"]["AreaID"]))
            for dest, is_zone in [(card["Destination"], False)] + [(zone, True) for zone in card.get("Zones") or []]:
                area = int(dest["AreaID"])
                low, high = refusal(into, area)
                if is_zone:
                    low = max(low, card_low)
                    high = min(h for h in (high, card_high) if h) if (high or card_high) else 0
                fields.setdefault(area, (area, int(dest["Location"]["X"]), int(dest["Location"]["Y"]), low, high))

    lines = ["# scripts/gen/eco/build-eco-links.py 가 만든다. 손으로 고치지 말 것.",
             "# link 맵 x y 갈맵 갈x 갈y 최소레벨 최대레벨(0=없음) · gate 맵 x y(월드맵이 열리는 칸) · field 갈맵 갈x 갈y 최소레벨 최대레벨(월드맵에서 고르는 곳)"
             " · block 맵 x y(NPC 스크립트 워프 — 밟지 않는다)"]
    lines += ["link " + " ".join(map(str, row)) for row in sorted(links)]
    lines += ["gate " + " ".join(map(str, row)) for row in sorted(gates)]
    lines += ["field " + " ".join(map(str, row)) for row in sorted(fields.values())]
    lines += ["block " + " ".join(map(str, row)) for row in sorted(blocks)]
    print(f"links.txt: 워프 칸 {len(links)} · 월드맵 칸 {len(gates)} · 월드맵 곳 {len(fields)} · 스크립트 워프 칸 {len(blocks)}")
    if "--쓰기" in _sys.argv:
        OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(f"썼습니다 — {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
