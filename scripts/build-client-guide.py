#!/usr/bin/env python3
"""길 찾기 창(원작 Tab 지도)에 찍을 출구·NPC 자리 — 서버 자료에서 뽑는다.

원작 클라이언트는 워프 칸을 모른다(서버가 밟았을 때만 옮겨 준다). 그래서 모바일 길 찾기 창이 "어디로 나가나"를
말하려면 서버의 워프 템플릿을 미리 뽑아 둬야 한다. 같은 이유로 멀리 있어 아직 안 보이는 NPC 자리도 함께 적는다.

  쓰는 법: python3 scripts/build-client-guide.py
  산출물:  mobile/client/assets/world/guide.txt   (배치 글이 있는 맵 — map<번호>.txt — 만)

줄 모양(알맹이 `MapGuide.Read` 가 읽는다):
  exit <맵> <x> <y> <간 곳 이름>      — 워프 칸 하나. 이어 붙은 칸은 알맹이가 한 출구로 묶는다
  npc  <맵> <x> <y> <이름>            — mundanes 템플릿의 NPC 자리
  area <맵> <입장 레벨> <town|field> <이름> — 월드맵이 내려 주는 맵(카드에 적는다). 레벨은 그 맵으로 드는 워프의
                                        LevelRequired 중 가장 작은 것, 마을은 이름에 "마을"이 든 곳
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
OUT = ROOT / "mobile" / "client" / "assets" / "world"


def main() -> None:
    drawn = {int(p.stem[3:]) for p in OUT.glob("map*.txt") if p.stem[3:].isdigit()}
    names = {}

    for path in (SERVER / "areas").glob("*.json"):
        area = json.loads(path.read_text(encoding="utf-8-sig"))
        names[int(area["ID"])] = area["Name"]

    lines = ["# tools: scripts/build-client-guide.py 가 서버 워프·NPC 템플릿에서 만든다. 손으로 고치지 말 것."]
    exits = set()

    for path in sorted((SERVER / "templates" / "warps").glob("*.json")):
        warp = json.loads(path.read_text(encoding="utf-8-sig"))
        to = warp.get("To") or {}
        world = warp.get("WarpType") == "World" or not to.get("AreaID")
        where = "월드맵" if world else names.get(int(to["AreaID"]), str(to["AreaID"]))

        for step in warp.get("Activations") or []:
            spot = step.get("Location") or {}
            area = int(step.get("AreaID") or 0)

            if area in drawn and "X" in spot:
                exits.add((area, int(spot["X"]), int(spot["Y"]), where))

    lines += [f"exit {a} {x} {y} {w}" for a, x, y, w in sorted(exits)]

    # 월드맵 카드 — 월드맵이 내려 주는 맵마다 이름·마을인지·입장 레벨.
    levels = {}

    for path in sorted((SERVER / "templates" / "warps").glob("*.json")):
        warp = json.loads(path.read_text(encoding="utf-8-sig"))
        to = int((warp.get("To") or {}).get("AreaID") or 0)

        if to:
            levels[to] = min(levels.get(to, 999), int(warp.get("LevelRequired") or 1))

    places = set()

    for path in sorted((SERVER / "templates" / "worldmaps").glob("*.json")):
        field = json.loads(path.read_text(encoding="utf-8-sig"))

        for portal in field.get("Portals") or []:
            area = int(((portal.get("Destination") or {}).get("AreaID")) or 0)

            if area in names:
                name = names[area]
                places.add((area, max(1, levels.get(area, 1)), "town" if "마을" in name else "field", name))

    lines += [f"area {a} {lv} {kind} {n}" for a, lv, kind, n in sorted(places)]

    npcs = set()

    for path in sorted((SERVER / "templates" / "mundanes").glob("*.json")):
        npc = json.loads(path.read_text(encoding="utf-8-sig"))
        area = int(npc.get("AreaID") or 0)

        if area in drawn:
            npcs.add((area, int(npc["X"]), int(npc["Y"]), npc["Name"].split("@")[0]))

    lines += [f"npc {a} {x} {y} {n}" for a, x, y, n in sorted(npcs)]

    (OUT / "guide.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"guide.txt: 맵 {len(drawn)} · 출구 칸 {len(exits)} · NPC {len(npcs)} · 월드맵 맵 {len(places)}")


if __name__ == "__main__":
    main()
