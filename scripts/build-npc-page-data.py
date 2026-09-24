#!/usr/bin/env python3
"""NPC(mundane) 154명이 누구고 어디 서 있고 무슨 일을 하는지 한 덩어리로 뽑는다.

"무슨 일을 하는지"는 짐작하지 않는다 — 템플릿의 `ScriptKey` 가 가리키는 실제 스크립트를 읽어
그 안에서 부르는 명령(`skill_add`·`spell_add`·`set_hair`·`legend_add` …)으로 역할을 가른다.
5.99 팩 NPC 대화(`scripts/build-pack-npcs.py` 가 옮긴 `scripts/Pack599/Npcs/*.cs`)는 그 명령이 곧
원작 근거이기도 하다 — 무엇을 배우는지는 그 대화가 부르는 `skill_add`/`spell_add` 인자 그대로다.

"지금 서 있다"는 그 NPC 의 맵이 노비스·수오미 마을에서 걸어서 닿는 맵일 때만이다
(`build-region-warp-data.py` 와 같은 셈). 나머지 125명은 정의는 있지만 아직 갈 수 없는 마을에 있다 —
따로 센다.

  쓰는 법: python3 scripts/build-npc-page-data.py   → docs/npcs-data.js
"""
import json
import re
import subprocess
from collections import defaultdict, deque
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
PACK599 = SERVER / "scripts/Pack599/Npcs"
OUT = ROOT / "docs" / "npcs-data.js"

# 지금 모바일로 실제 돌아다닐 수 있는 지역 — build-region-warp-data.py 와 같다.
REGIONS = {"노비스": "노비스마을", "수오미": "수오미마을"}

#: 5.99 대화가 부르는 명령 가운데 역할을 가르는 것들. 위에서부터 먼저 맞는 것을 쓴다.
ROLE_CALLS = [
    ("기술/마법 사범", {"skill_add", "spell_add"}),
    ("꾸밈", {"set_hair", "set_haircolor", "set_face"}),
    ("승급/전직", {"set_class_sub", "set_class"}),
    ("퀘스트", {"legend_add"}),
    ("이동", {"warp", "warp_create", "group_warp"}),
]


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def areas():
    out = {}
    for path in (SERVER / "areas").glob("*.json"):
        try:
            data = read(path)
        except Exception:
            continue
        out[data["Id"]] = data["Name"]
    return out


def reachable_set(names):
    """노비스·수오미 마을에서 워프로 걸어서 닿는 맵 번호 집합. region-warp 과 같은 BFS."""
    wanted = {i: n for i, n in names.items() if any(n.startswith(r) for r in REGIONS)}
    by_name = {n: i for i, n in wanted.items()}
    nexts = defaultdict(set)
    for path in (SERVER / "templates/warps").glob("*.json"):
        try:
            data = read(path)
        except Exception:
            continue
        target = (data.get("To") or {}).get("AreaID")
        if not target:
            continue
        for activation in data.get("Activations") or []:
            source = activation.get("AreaID")
            if source not in wanted and target not in wanted:
                continue
            nexts[source].add(target)

    reached = set()
    for start in REGIONS.values():
        queue = deque([by_name.get(start)])
        while queue:
            here = queue.popleft()
            if here is None or here in reached:
                continue
            reached.add(here)
            queue.extend(nexts.get(here, ()))
    return reached


def town_anchors(names):
    """'노비스마을' → '노비스' 처럼 마을 이름에서 접미사 뗀 것들. 필터의 '마을' 값이다."""
    return sorted({n[:-2] for n in names.values() if n.endswith("마을") and len(n) > 2},
                  key=len, reverse=True)


def town_of(area_name, anchors):
    for anchor in anchors:
        if area_name.startswith(anchor):
            return anchor
    return area_name


def item_kind(item):
    if item.get("HealthRestore") or item.get("ManaRestore"):
        return "시약"
    if item.get("EquipmentSlot"):
        return "장비"
    return "재료"


def item_facts():
    out = {}
    for path in (SERVER / "templates/items").glob("*.json"):
        try:
            data = read(path)
        except Exception:
            continue
        out[data.get("Name")] = data
    return out


def pack599_scripts():
    """스크립트 이름(파일 이름, 'NPC_' 뗀 것) → {calls, 원본, 가르침}. 5.99 대화를 그대로 옮긴 것들이다."""
    out = {}
    for path in PACK599.glob("*.cs"):
        text = path.read_text(encoding="utf-8-sig")
        calls = set(re.findall(r'Call\("([a-z_]+)"', text))
        source = re.search(r"5\.99 `(Npc_[A-Za-z_]+\.txt)`", text)
        taught = re.findall(r'Call\("(?:skill|spell)_add",\s*\(V\)"([^"]+)"\)', text)
        out[path.stem] = {
            "명령": calls,
            "원본": source.group(1) if source else None,
            "가르침": list(dict.fromkeys(taught)),  # 나온 차례를 지키며 중복만 뺀다
        }
    return out


def role_of(mundane, scripts):
    """역할 하나와 그 근거. 상점부터 보고, 5.99 대화면 부르는 명령으로, 아니면 안내로 둔다."""
    key = mundane.get("ScriptKey") or ""
    if key in ("shop1", "shop2") and (mundane.get("DefaultMerchantStock") or []):
        return "상점", None
    if key == "Class Chooser":
        return "승급/전직", None
    if key.startswith("NPC_"):
        info = scripts.get(key[len("NPC_"):])
        if not info:
            return "안내", None
        for role, needed in ROLE_CALLS:
            if info["명령"] & needed:
                return role, info
        if info["원본"] == "Npc_Making.txt":
            return "제작", info
        return "안내", info
    return "안내", None


def main():
    names = areas()
    reached = reachable_set(names)
    anchors = town_anchors(names)
    facts = item_facts()
    scripts = pack599_scripts()

    rows = []
    role_tally_all = defaultdict(int)
    role_tally_now = defaultdict(int)

    for path in sorted((SERVER / "templates/mundanes").glob("*.json")):
        try:
            data = read(path)
        except Exception:
            continue

        area_id = data.get("AreaID")
        area_name = names.get(area_id, "?")
        role, info = role_of(data, scripts)
        display = (data.get("Name") or "").split("@")[0]
        now = area_id in reached

        row = {
            "이름": display,
            "전체이름": data.get("Name"),
            "맵번호": area_id,
            "맵": area_name,
            "마을": town_of(area_name, anchors),
            "좌표": [data.get("X"), data.get("Y")],
            "닿음": now,
            "스크립트": data.get("ScriptKey"),
            "역할": role,
            "대사": (data.get("Speech") or [None])[0],
            "근거": f"templates/mundanes/{path.name}",
            "원작근거": None,
        }
        if info and info.get("원본"):
            # 스크립트 파일 이름은 ScriptKey 에서 얻는다(mundane 파일 이름과 다를 수 있다 — 달인 처럼 셋이 한 스크립트를 쓴다).
            script_file = (data.get("ScriptKey") or "")[len("NPC_"):]
            row["원작근거"] = f"5.99 팩 db/script/Npc/{info['원본']} (scripts/Pack599/Npcs/{script_file}.cs)"

        if role == "상점":
            stock = data.get("DefaultMerchantStock") or []
            by_kind = defaultdict(int)
            items = []
            for item_name in stock:
                item = facts.get(item_name, {})
                kind = item_kind(item)
                by_kind[kind] += 1
                items.append({"이름": item_name, "갈래": kind, "템플릿있음": item_name in facts})
            row["상점"] = {"개수": len(stock), "갈래별": dict(by_kind), "목록": items}

        if role == "기술/마법 사범" and info:
            row["사범"] = {"가르침": info["가르침"]}

        rows.append(row)
        role_tally_all[role] += 1
        if now:
            role_tally_now[role] += 1

    rows.sort(key=lambda r: (not r["닿음"], r["마을"], r["맵"], r["이름"]))

    towns = sorted({r["마을"] for r in rows})
    maps = sorted({r["맵"] for r in rows})

    try:
        pointer = subprocess.run(
            ["git", "-C", str(SERVER.parents[1]), "rev-parse", "--short", "HEAD"],
            capture_output=True, text=True, check=True).stdout.strip()
    except Exception:
        pointer = ""

    now_count = sum(1 for r in rows if r["닿음"])
    payload = {
        "생성": "scripts/build-npc-page-data.py",
        "서버포인터": pointer,
        "지역": list(REGIONS.keys()),
        "마을": towns,
        "맵": maps,
        "규칙": {
            "닿음": "노비스·수오미 마을에서 워프로 걸어서 닿는 맵의 NPC만 '지금 서 있다' — build-region-warp-data.py 와 같은 셈",
            "역할": "5.99 대화 스크립트가 실제로 부르는 명령으로 가른다(skill_add/spell_add → 사범, set_hair 류 → 꾸밈, "
                    "set_class_sub → 승급/전직, legend_add → 퀘스트, warp 류 → 이동). 상점은 ScriptKey 가 shop1/shop2 이고 "
                    "실제 재고가 있을 때만.",
            "여관창고없음": "이름·맵이 '여관'·'은행'·'창고'인 NPC는 있지만(예: 여관주인@수오미여관) 스크립트가 전부 "
                          "pack_speaker(대사만)라 창고·숙박 기능을 하는 스크립트가 아직 없다 — 그래서 역할 목록에 "
                          "'여관'·'창고'가 없다. Banker.cs(예치·인출)는 존재하지만 어떤 NPC 템플릿도 아직 쓰지 않는다.",
        },
        "NPC": rows,
        "셈": {
            "전체": len(rows),
            "지금서있다": now_count,
            "아직못감": len(rows) - now_count,
            "역할별": dict(role_tally_all),
            "지금역할별": dict(role_tally_now),
        },
    }

    OUT.write_text(
        "window.LOD_NPCS = " + json.dumps(payload, ensure_ascii=False) + ";\n",
        encoding="utf-8")
    print(f"NPC {len(rows)} · 지금 서 있다 {now_count} · 아직 못 감 {len(rows) - now_count}")
    print("역할별(지금):", dict(role_tally_now))
    print(f"→ {OUT.relative_to(ROOT)}  ({OUT.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
