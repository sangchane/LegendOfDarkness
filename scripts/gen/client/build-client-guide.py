#!/usr/bin/env python3
"""길 찾기 창(원작 Tab 지도)에 찍을 출구·NPC 자리 — 서버 자료에서 뽑는다.

원작 클라이언트는 워프 칸을 모른다(서버가 밟았을 때만 옮겨 준다). 그래서 모바일 길 찾기 창이 "어디로 나가나"를
말하려면 서버의 워프 템플릿을 미리 뽑아 둬야 한다. 같은 이유로 멀리 있어 아직 안 보이는 NPC 자리도 함께 적는다.

  쓰는 법: python3 scripts/gen/client/build-client-guide.py
  산출물:  mobile/client/assets/world/guide.txt   (배치 글이 있는 맵 — map<번호>.txt — 만)

줄 모양(알맹이 `MapGuide.Read` 가 읽는다):
  exit <맵> <x> <y> <간 곳 이름>      — 워프 칸 하나. 이어 붙은 칸은 알맹이가 한 출구로 묶는다
  npc  <맵> <x> <y> <이름>            — mundanes 템플릿의 NPC 자리. 앱 지도가 없는 맵(세오신전·칸신전 …)도 적는다 — 생태계 봇이
                                        이름표로 세오·칸·뮤레칸을 찾는다(사용자 2026-10-08 「모든 정보나 봇들을 라벨링해서 데이터 수정하면
                                        거기에 맞게 적용되도록」). about·role 줄도 같다.
  about <맵> <x> <y> <설명>           — 그 NPC 가 하는 일 한 줄(길 찾기 창 NPC 목록 팝업). 상점은 파는 갈래·가짓수·레벨 폭, 5.99 대화는
                                        부르는 명령으로(build-npc-page-data.py 와 같은 근거), 없으면 대사 첫 줄
  role <맵> <x> <y> <역할>            — 그 NPC 의 역할 한 낱말(미니맵·길 찾기 창·머리 위 아이콘, `scripts/lib/_npcs.py` ROLES)
  stock <맵> <x> <y> <역할> <최저> <최고> <이름, …> — 상점 물목 그대로(생태계 봇 `EcoWorld.Stops` 가 역할로 가게를 고른다 — about 은
                                        사람이 읽게 요약한다). 최저·최고는 입는 물건이 든 서클의 레벨 폭(1~10 · 11~40 · 41~70 · 71~98 · 99),
                                        없으면 0 0. 봇이 걸어 다닐 수 있게 앱 지도가 있는 맵만
  room <맵> <간 곳>|<NPC — 설명 / …>|<역할,…> — 출구 너머 맵(상점 건물 등)에 선 NPC 들과 그 역할. 마을 지도에서 상점 안을 말하려고
  area <맵> <입장 레벨> <town|field> <이름> — 월드맵이 내려 주는 맵(카드에 적는다). 레벨은 그 맵으로 드는 워프의
                                        LevelRequired 중 가장 큰 것(서버도 가장 엄한 것을 쓴다), 마을은 이름에 "마을"이 든 곳
  zone <카드 맵> <구역 맵> <입장 레벨> <이름> — 사냥터 카드 아래 구역(월드맵 Portals[].Zones). 고르면 바로 그 구역으로 간다.
                                        레벨은 구역과 카드 중 큰 것 — 서버가 구역에도 입구(카드)의 제한을 건다
"""
import json

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from script_warps import script_warp_destination

from lib._paths import ROOT
from lib._npcs import about, circle_span, role
SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
OUT = ROOT / "mobile" / "client" / "assets" / "world"


#: 길 안내에 싣지 않는 NPC 스크립트 — 운영자만 거래하는 상인(`build-operator-shop.py`, 사용자 2026-10-09). 손님 목록·미니맵·봇 동선에 안 나온다.
HIDDEN_SCRIPTS = {"operator_shop"}


def main() -> None:
    drawn = {int(p.stem[3:]) for p in OUT.glob("map*.txt") if p.stem[3:].isdigit()}
    names = {}

    for path in (SERVER / "areas").glob("*.json"):
        area = json.loads(path.read_text(encoding="utf-8-sig"))
        names[int(area["ID"])] = area["Name"]

    lines = ["# tools: scripts/gen/client/build-client-guide.py 가 서버 워프·NPC 템플릿에서 만든다. 손으로 고치지 말 것."]
    items = {}
    for path in (SERVER / "templates" / "items").rglob("*.json"):
        try:
            item = json.loads(path.read_text(encoding="utf-8-sig"))
        except json.JSONDecodeError:
            continue
        if item.get("Name"):
            items[item["Name"]] = item
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
    stocks = []

    for path in sorted((SERVER / "templates" / "mundanes").glob("*.json")):
        npc = json.loads(path.read_text(encoding="utf-8-sig"))
        if npc.get("ScriptKey") in HIDDEN_SCRIPTS:
            continue
        area = int(npc.get("AreaID") or 0)

        npcs.add((area, int(npc["X"]), int(npc["Y"]), npc["Name"].split("@")[0], about(npc, items), role(npc, items)))
        if area in drawn and (npc.get("ScriptKey") or "") in ("shop1", "shop2") and npc.get("DefaultMerchantStock"):
            lo, hi = circle_span(npc, items)
            stocks.append((area, int(npc["X"]), int(npc["Y"]), role(npc, items), lo, hi, ", ".join(npc["DefaultMerchantStock"])))

    standing = {}
    roles_in = {}

    for path in sorted((SERVER / "templates" / "mundanes").glob("*.json")):
        npc = json.loads(path.read_text(encoding="utf-8-sig"))
        if npc.get("ScriptKey") in HIDDEN_SCRIPTS:
            continue
        # room 줄은 | 로 칸을 가른다 — 설명에 든 | 는 / 로(역할 칸이 밀리지 않게).
        standing.setdefault(int(npc.get("AreaID") or 0), []).append(f"{npc['Name'].split('@')[0]} — {about(npc, items)}".replace("|", "/"))
        roles_in.setdefault(int(npc.get("AreaID") or 0), []).append(role(npc, items))

    # 역할 아이콘은 건물 문에만 — 상점 안에서 마을·사냥터(월드맵이 내려 주는 곳)로 나가는 문에 마을 NPC 들의 아이콘을 달면
    # 그 문 너머가 상점처럼 읽힌다.
    landing = {a for a, _, _, _ in places}
    rooms = [(a, w, " / ".join(standing[to]), "" if to in landing else ",".join(dict.fromkeys(roles_in[to])))
             for (a, w), to in sorted(leads.items()) if to in standing]

    lines += [f"npc {a} {x} {y} {n}" for a, x, y, n, _, _ in sorted(npcs)]
    lines += [f"room {a} {w}|{t}|{r}" for a, w, t, r in rooms]
    lines += [f"about {a} {x} {y} {t}" for a, x, y, _, t, _ in sorted(npcs)]
    lines += [f"role {a} {x} {y} {r}" for a, x, y, _, _, r in sorted(npcs)]
    lines += [f"stock {a} {x} {y} {r} {lo} {hi} {names}" for a, x, y, r, lo, hi, names in sorted(stocks)]

    (OUT / "guide.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"guide.txt: 맵 {len(drawn)} · 출구 칸 {len(exits)} · NPC {len(npcs)} · 건물 {len(rooms)} · 월드맵 맵 {len(places)} · 구역 {len(zones)}")


if __name__ == "__main__":
    main()
