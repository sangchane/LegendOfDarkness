#!/usr/bin/env python3
"""어느 NPC 가 어느 상점 목록을 여는지 복원한다.

상점 이름 47종은 `각반사기`·`체력포션` 같은 **목록 이름**이지 NPC 이름이 아니다 — NPC 정의
31종과 한 글자도 안 겹친다. 결합은 팩 스크립트 안에 있다:

    shop 0, "체력포션", "어느 물약을 구매 하겠습니까?"
         ^      ^              ^
      0=사기  목록 이름      말풍선
      1=팔기

그리고 **스크립트 이름이 곧 NPC 이름**이다. NPC 젠이 부르는 "정의 없는" 이름 95개가
바로 이 스크립트들이다. 그래서 젠 → 스크립트 → shop 호출로 이어진다.

  쓰는 법: python3 scripts/build-shop-binding.py   → plans/5.99-상점결합.tsv
"""
import json, re
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PACK = ROOT / "data" / "server-packs"
SCRIPTS = PACK / "5.99-server" / "db" / "script"
EXTRACTED = PACK / "extracted" / "5.99-server"
OUT = ROOT / "plans" / "5.99-상점결합.tsv"

# 머리말<TAB>이름<TAB>{ 본문 } — build-server-pack-data.py 와 같은 잣대다.
HEAD = re.compile(r"^(?P<head>[^\t]*)\t(?P<name>[^\t{]+?)\s*\{\s*$")
CALL = re.compile(r'\bshop\s+(\d+)\s*,\s*"([^"]+)"')


def scripts_with_shops():
    """스크립트 이름 → [(갈래, 상점목록)]. 한 NPC 가 여러 목록을 열기도 한다."""
    found = defaultdict(list)
    for f in sorted(SCRIPTS.rglob("*.txt")):
        lines = f.read_text(encoding="utf-8", errors="replace").splitlines()
        i = 0
        while i < len(lines):
            m = HEAD.match(lines[i].rstrip())
            if not m:
                i += 1
                continue
            name, depth, body, i = m.group("name").strip(), 1, [], i + 1
            while i < len(lines) and depth > 0:
                depth += lines[i].count("{") - lines[i].count("}")
                if depth > 0:
                    body.append(lines[i])
                i += 1
            for kind, shop in CALL.findall("\n".join(body)):
                found[name].append(("물건팔기" if kind == "1" else "물건사기", shop, f.name))
    return found


def main():
    load = lambda k: json.loads((EXTRACTED / f"{k}.json").read_text(encoding="utf-8"))
    shops = {s["이름"]: s for s in load("shops")}
    spawns = load("npc_spawns")
    defined = {n["이름"] for n in load("npcs")}
    items = {i["이름"] for i in load("items")}

    opens = scripts_with_shops()
    placed = defaultdict(list)
    for s in spawns:
        placed[s["NPC"]].append((s["맵"], s["좌표"][0], s["좌표"][1]))

    rows, unplaced, unknown_shop = [], [], set()
    for npc, calls in sorted(opens.items()):
        where = placed.get(npc)
        if not where:
            unplaced.append(npc)
            continue
        for kind, shop, src in calls:
            if shop not in shops:
                unknown_shop.add(shop)
            goods = shops.get(shop, {}).get("아이템", [])
            missing = [g for g in goods if g not in items]
            for m, x, y in where:
                rows.append([npc, m, x, y, kind, shop, str(len(goods)), str(len(missing)), src])

    out = [
        "# 어느 NPC 가 어느 상점 목록을 여는가 — 팩 스크립트의 shop 호출에서 복원했다.",
        "# 상점 이름은 목록 이름이지 NPC 이름이 아니다. 스크립트 이름이 곧 NPC 이름이다.",
        "# 물건팔기는 NPC 가 사 주는 목록이라 DefaultMerchantStock 이 아니다 - shop1.cs 의",
        "# 사는 쪽은 목록을 안 쓰고 인벤토리를 받는다. 넣지 않는다.",
        "#",
        "# 칸: NPC / 맵 / x / y / 갈래 / 상점목록 / 물건수 / 그중없는것 / 근거스크립트",
        "",
    ] + ["\t".join(r) for r in rows]
    OUT.write_text("\n".join(out) + "\n", encoding="utf-8")

    buy = [r for r in rows if r[4] == "물건사기"]
    print(f"shop 을 쓰는 스크립트 {len(opens)} · 그중 젠에 있는 것 {len(opens) - len(unplaced)}")
    print(f"결합 {len(rows)}줄 (물건사기 {len(buy)} · 물건팔기 {len(rows) - len(buy)})")
    print(f"서로 다른 NPC {len({r[0] for r in rows})} · 상점 목록 {len({r[5] for r in rows})}")
    print(f"젠에 없는 NPC {len(unplaced)}: {', '.join(unplaced)}")
    if unknown_shop:
        print(f"shops.json 에 없는 목록 {len(unknown_shop)}: {', '.join(sorted(unknown_shop))}")
    print(f"→ {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
