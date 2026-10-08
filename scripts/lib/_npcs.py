"""NPC 가 하는 일 — 역할 한 낱말·한 줄 설명·가르치는 기술·파는 서클 폭. 길 안내(`scripts/gen/client/build-client-guide.py` → 앱
`guide.txt`)와 게임 볼트·그래프(`scripts/gen/vault/build-game-vault.py`)가 같이 쓴다 — 두 곳의 NPC 설명이 어긋나지 않게
(사용자 2026-10-09 「엔피시나 기타 게임데이터 graphify 랑 볼트로 정리한 데이터끼리 연결고리 정리해서」).
"""
import re

from lib._paths import ROOT

SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"


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



def taught(npc: dict) -> list:
    """그 NPC 가 가르치는 기술·마법 이름 전부(대본의 skill_add·spell_add) — 차례 그대로, 겹침 없이."""
    key = npc.get("ScriptKey") or ""
    script = SERVER / "scripts" / "Pack599" / "Npcs" / f"{key[len('NPC_'):]}.cs"
    if not key.startswith("NPC_") or not script.exists():
        return []
    text = script.read_text(encoding="utf-8-sig")
    return list(dict.fromkeys(re.findall(r'Call\("(?:skill|spell)_add2?",\s*\(V\)"([^"]+)"\)', text)))
