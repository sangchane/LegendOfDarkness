#!/usr/bin/env python3
"""길 찾기 창(원작 Tab 지도)에 찍을 출구·NPC 자리 — 서버 자료에서 뽑는다.

원작 클라이언트는 워프 칸을 모른다(서버가 밟았을 때만 옮겨 준다). 그래서 모바일 길 찾기 창이 "어디로 나가나"를
말하려면 서버의 워프 템플릿을 미리 뽑아 둬야 한다. 같은 이유로 멀리 있어 아직 안 보이는 NPC 자리도 함께 적는다.

  쓰는 법: python3 scripts/gen/client/build-client-guide.py
  산출물:  mobile/client/assets/world/guide.txt   (배치 글이 있는 맵 — map<번호>.txt — 만)

줄 모양(알맹이 `MapGuide.Read` 가 읽는다):
  exit <맵> <x> <y> <간 곳 이름>      — 워프 칸 하나. 이어 붙은 칸은 알맹이가 한 출구로 묶는다
  npc  <맵> <x> <y> <이름>            — mundanes 템플릿의 NPC 자리
  about <맵> <x> <y> <설명>           — 그 NPC 가 하는 일 한 줄(길 찾기 창 NPC 목록 팝업). 상점은 파는 것, 5.99 대화는
                                        부르는 명령으로(build-npc-page-data.py 와 같은 근거), 없으면 대사 첫 줄
  room <맵> <간 곳>|<NPC — 설명 / …>  — 출구 너머 맵(상점 건물 등)에 선 NPC 들. 마을 지도에서 상점 안을 말하려고
  area <맵> <입장 레벨> <town|field> <이름> — 월드맵이 내려 주는 맵(카드에 적는다). 레벨은 그 맵으로 드는 워프의
                                        LevelRequired 중 가장 큰 것(서버도 가장 엄한 것을 쓴다), 마을은 이름에 "마을"이 든 곳
  zone <카드 맵> <구역 맵> <입장 레벨> <이름> — 사냥터 카드 아래 구역(월드맵 Portals[].Zones). 고르면 바로 그 구역으로 간다.
                                        레벨은 구역과 카드 중 큰 것 — 서버가 구역에도 입구(카드)의 제한을 건다
"""
import json
import re

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from script_warps import script_warp_destination

from lib._paths import ROOT
SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
OUT = ROOT / "mobile" / "client" / "assets" / "world"


#: 5.99 대화가 부르는 명령 → 하는 일. 위에서부터 맞는 것을 모두 적는다.
DOES = [
    ({"set_basevita", "exp_del"}, "경험치로 체력을 산다"),
    ({"set_basemana", "exp_del"}, "경험치로 마력을 산다"),
    ({"set_str"}, "능력치를 다시 나눈다"),
    ({"set_hair"}, "머리 모양·색을 바꾼다"),
    ({"set_class_sub"}, "승급"),
    ({"set_class"}, "전직"),
    ({"legend_add"}, "퀘스트"),
    ({"call_func"}, "게시판"),
    ({"item_add", "item_del"}, "물건을 만들어 준다(재료 → 물건)"),
    ({"warp"}, "다른 곳으로 보내 준다"),
    ({"group_warp"}, "파티를 데려간다"),
]


def about(npc: dict) -> str:
    """그 NPC 가 하는 일 한 줄 — 짐작하지 않고 템플릿·대본에 적힌 것만."""
    key = npc.get("ScriptKey") or ""
    stock = npc.get("DefaultMerchantStock") or []

    if key in ("shop1", "shop2") and stock:
        return "판매: " + ", ".join(stock)
    if key == "Class Chooser":
        return "직업을 고른다"

    said = []
    script = SERVER / "scripts" / "Pack599" / "Npcs" / f"{key[len('NPC_'):]}.cs"

    if key.startswith("NPC_") and script.exists():
        text = script.read_text(encoding="utf-8-sig")
        calls = set(re.findall(r'Call\("([a-z_]+)"', text))
        taught = list(dict.fromkeys(re.findall(r'Call\("(?:skill|spell)_add2?",\s*\(V\)"([^"]+)"\)', text)))
        if taught:
            said.append("가르침: " + ", ".join(taught))
        said += [words for needed, words in DOES if needed <= calls]

    if not said:
        speech = [line for line in npc.get("Speech") or [] if line.strip()]
        said.append(speech[0] if speech else "안내")

    return " · ".join(said).replace("\n", " ")


def main() -> None:
    drawn = {int(p.stem[3:]) for p in OUT.glob("map*.txt") if p.stem[3:].isdigit()}
    names = {}

    for path in (SERVER / "areas").glob("*.json"):
        area = json.loads(path.read_text(encoding="utf-8-sig"))
        names[int(area["ID"])] = area["Name"]

    lines = ["# tools: scripts/gen/client/build-client-guide.py 가 서버 워프·NPC 템플릿에서 만든다. 손으로 고치지 말 것."]
    exits = set()
    leads = {}

    for path in sorted((SERVER / "templates" / "warps").glob("*.json")):
        warp = json.loads(path.read_text(encoding="utf-8-sig"))
        to = warp.get("To") or {}
        world = warp.get("WarpType") == "World" or not to.get("AreaID")
        where = "월드맵" if world else names.get(int(to["AreaID"]), str(to["AreaID"]))
        # 밟으면 스크립트가 도는 워프는 To 가 제자리다 — 스크립트가 보내는 곳(개인 던전)을 적는다.
        scripted = script_warp_destination(SERVER, warp)
        if scripted:
            where = scripted[1]

        for step in warp.get("Activations") or []:
            spot = step.get("Location") or {}
            area = int(step.get("AreaID") or 0)

            if area in drawn and "X" in spot:
                exits.add((area, int(spot["X"]), int(spot["Y"]), where))
                if not world and not scripted and int(to["AreaID"]) != area:
                    leads[(area, where)] = int(to["AreaID"])

    lines += [f"exit {a} {x} {y} {w}" for a, x, y, w in sorted(exits)]

    # 월드맵 카드 — 월드맵이 내려 주는 맵마다 이름·마을인지·입장 레벨.
    levels = {}

    for path in sorted((SERVER / "templates" / "warps").glob("*.json")):
        warp = json.loads(path.read_text(encoding="utf-8-sig"))
        to = int((warp.get("To") or {}).get("AreaID") or 0)

        if to:
            levels[to] = max(levels.get(to, 1), int(warp.get("LevelRequired") or 1))

    places = set()
    zones = []

    for path in sorted((SERVER / "templates" / "worldmaps").glob("*.json")):
        field = json.loads(path.read_text(encoding="utf-8-sig"))

        for portal in field.get("Portals") or []:
            area = int(((portal.get("Destination") or {}).get("AreaID")) or 0)

            if area in names:
                name = names[area]
                places.add((area, max(1, levels.get(area, 1)), "town" if name.endswith("마을") else "field", name))

            # 구역은 자료에 적힌 차례 그대로.
            for zone in portal.get("Zones") or []:
                inner = int(zone.get("AreaID") or 0)

                if inner in names:
                    zones.append((area, inner, max(levels.get(inner, 1), levels.get(area, 1)), names[inner]))

    lines += [f"area {a} {lv} {kind} {n}" for a, lv, kind, n in sorted(places)]
    lines += [f"zone {a} {z} {lv} {n}" for a, z, lv, n in zones]

    npcs = set()

    for path in sorted((SERVER / "templates" / "mundanes").glob("*.json")):
        npc = json.loads(path.read_text(encoding="utf-8-sig"))
        area = int(npc.get("AreaID") or 0)

        if area in drawn:
            npcs.add((area, int(npc["X"]), int(npc["Y"]), npc["Name"].split("@")[0], about(npc)))

    standing = {}

    for path in sorted((SERVER / "templates" / "mundanes").glob("*.json")):
        npc = json.loads(path.read_text(encoding="utf-8-sig"))
        standing.setdefault(int(npc.get("AreaID") or 0), []).append(f"{npc['Name'].split('@')[0]} — {about(npc)}")

    rooms = [(a, w, " / ".join(standing[to])) for (a, w), to in sorted(leads.items()) if to in standing]

    lines += [f"npc {a} {x} {y} {n}" for a, x, y, n, _ in sorted(npcs)]
    lines += [f"room {a} {w}|{t}" for a, w, t in rooms]
    lines += [f"about {a} {x} {y} {t}" for a, x, y, _, t in sorted(npcs)]

    (OUT / "guide.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"guide.txt: 맵 {len(drawn)} · 출구 칸 {len(exits)} · NPC {len(npcs)} · 건물 {len(rooms)} · 월드맵 맵 {len(places)} · 구역 {len(zones)}")


if __name__ == "__main__":
    main()
