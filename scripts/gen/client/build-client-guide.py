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
  role <맵> <x> <y> <역할>            — 그 NPC 의 역할 한 낱말(미니맵·길 찾기 창·머리 위 아이콘, `ROLES`)
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


#: 역할 낱말 — 알맹이 `NpcRoles` 가 읽는다. 대본 명령 → 역할, 위쪽이 이긴다(사용자 2026-10-08 「마을마다 어느 npc가 뭐하는지 잘
#: 모르겠던데 직관적으로」, autopilot/npc-roles/SPEC.md). 전직이 퀘스트·가르침보다, 제작은 그 셋보다 아래 — 밀레스신전 선진은
#: 전직·가르침·퀘스트·물건 주고받기를 다 한다.
ROLES = [
    ({"set_class"}, "전직"), ({"set_class_sub"}, "전직"),
    ({"legend_add"}, "퀘스트"),
    ({"TEACH"}, "기술"),
    ({"set_basevita"}, "체력"), ({"set_basemana"}, "체력"), ({"set_bodyvita"}, "체력"), ({"set_bodymana"}, "체력"),
    ({"set_str"}, "능력치"),
    ({"set_hair"}, "미용"),
    ({"item_add", "item_del"}, "제작"),
    ({"call_func"}, "게시판"),
    ({"warp"}, "이동"), ({"group_warp"}, "이동"),
]


def kind_of(item: dict) -> str:
    """상점 물건 하나의 갈래 — 무기(자리 1) · 방어구(2) · 장신구(3~13) · 물약(소모품) · 잡화."""
    slot = item.get("EquipmentSlot") or 0
    return ("무기" if slot == 1 else "방어구" if slot == 2 else "장신구" if slot >= 3
            else "물약" if (item.get("Flags") or 0) & 256 else "잡화")


def role(npc: dict, items: dict) -> str:
    """그 NPC 의 역할 한 낱말 — 상점은 물목의 과반(무기 자리 1 · 갑옷 2 · 장신구 3~13 · 소모품), 과반이 없으면 잡화."""
    key = npc.get("ScriptKey") or ""
    stock = npc.get("DefaultMerchantStock") or []

    if key in ("shop1", "shop2") and stock:
        # 역할에서는 무기 밖 입는 것을 모두 방어구로 — 2026-10-08 부터 방어구상이 장신구까지 판다(build-circle-gear-shops.py).
        kinds = ["방어구" if kind == "장신구" else kind for kind in (kind_of(items.get(name) or {}) for name in stock)]
        top = max(set(kinds), key=kinds.count)
        return top if kinds.count(top) * 2 > len(kinds) else "잡화"
    if key == "Banker":
        return "은행"
    if key == "gem_crafter":
        return "제작"
    if key == "Class Chooser":
        return "전직"

    script = SERVER / "scripts" / "Pack599" / "Npcs" / f"{key[len('NPC_'):]}.cs"
    if key.startswith("NPC_") and script.exists():
        text = script.read_text(encoding="utf-8-sig")
        calls = set(re.findall(r'Call\("([a-z_]+)"', text))
        if re.search(r'Call\("(?:skill|spell)_add2?"', text):
            calls.add("TEACH")
        return next((word for needed, word in ROLES if needed <= calls), "안내")
    return "안내"


#: 서클 레벨 폭 — `docs/item-prices-by-circle.md`, `build-circle-gear-shops.py` 와 같다.
CIRCLES = [(1, 10), (11, 40), (41, 70), (71, 98), (99, 99)]


def circle_span(npc: dict, items: dict) -> tuple:
    """상점이 파는 입는 물건이 든 서클들의 레벨 폭 — 봇이 제 레벨 가게만 들르게. 입는 물건이 없으면 (0, 0)."""
    levels = [int((items.get(name) or {}).get("LevelRequired") or 1) for name in npc.get("DefaultMerchantStock") or []
              if ((items.get(name) or {}).get("EquipmentSlot") or 0) > 0]
    if not levels:
        return 0, 0
    ring = lambda level: next((lo, hi) for lo, hi in CIRCLES if level <= hi or hi == 99)
    return ring(min(levels))[0], ring(max(levels))[1]


#: 가르침 목록을 이만큼까지 이름으로, 넘으면 「외 N개」.
TAUGHT_SHOWN = 4


def about(npc: dict, items: dict) -> str:
    """그 NPC 가 하는 일 한 줄 — 짐작하지 않고 템플릿·대본에 적힌 것만. 상점은 물건 이름을 늘어놓지 않고 갈래별 가짓수와
    레벨 폭만(사용자 2026-10-08 「아이템 리스트 텍스트 저렇게 나열 하는건 의미 없는거 같아 … 큰 맥락만」)."""
    key = npc.get("ScriptKey") or ""
    stock = npc.get("DefaultMerchantStock") or []

    if key in ("shop1", "shop2") and stock:
        kinds = [kind_of(items.get(name) or {}) for name in stock]
        parts = [f"{kind} {kinds.count(kind)}종" for kind in sorted(set(kinds), key=lambda k: (-kinds.count(k), k))]
        levels = [int((items.get(name) or {}).get("LevelRequired") or 1) for name in stock
                  if ((items.get(name) or {}).get("EquipmentSlot") or 0) > 0]
        if levels:
            parts.append(f"레벨 {min(levels)}" if min(levels) == max(levels) else f"레벨 {min(levels)}~{max(levels)}")
        return "판매: " + " · ".join(parts)
    if key == "Class Chooser":
        return "직업을 고른다"
    if key == "Banker":
        return "은행: 물건·금화를 맡기고 찾는다"

    said = []
    script = SERVER / "scripts" / "Pack599" / "Npcs" / f"{key[len('NPC_'):]}.cs"

    if key.startswith("NPC_") and script.exists():
        text = script.read_text(encoding="utf-8-sig")
        calls = set(re.findall(r'Call\("([a-z_]+)"', text))
        taught = list(dict.fromkeys(re.findall(r'Call\("(?:skill|spell)_add2?",\s*\(V\)"([^"]+)"\)', text)))
        if taught:
            more = f" 외 {len(taught) - TAUGHT_SHOWN}개" if len(taught) > TAUGHT_SHOWN else ""
            said.append("가르침: " + ", ".join(taught[:TAUGHT_SHOWN]) + more)
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
        area = int(npc.get("AreaID") or 0)

        npcs.add((area, int(npc["X"]), int(npc["Y"]), npc["Name"].split("@")[0], about(npc, items), role(npc, items)))
        if area in drawn and (npc.get("ScriptKey") or "") in ("shop1", "shop2") and npc.get("DefaultMerchantStock"):
            lo, hi = circle_span(npc, items)
            stocks.append((area, int(npc["X"]), int(npc["Y"]), role(npc, items), lo, hi, ", ".join(npc["DefaultMerchantStock"])))

    standing = {}
    roles_in = {}

    for path in sorted((SERVER / "templates" / "mundanes").glob("*.json")):
        npc = json.loads(path.read_text(encoding="utf-8-sig"))
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
