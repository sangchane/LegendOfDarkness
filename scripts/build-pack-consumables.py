#!/usr/bin/env python3
"""5.99 서버팩의 소모품(물약·음식·귀환 주문서)을 하데스 아이템 템플릿으로 옮긴다.

상점 NPC 24곳의 판매 목록 180종 중 131종이 서버에 없었다 — 대화창으로 상점을 열어도 물약 하나 살 수 없었다. 그중
플레이에 가장 급한 것부터(2026-09-17): `item/Potion.txt` · `item/Hungry.txt` · `item/Recoll.txt`.

**5.99 소모품은 스크립트가 아니라 칸으로 움직인다:**
  체력변화 +1000 → HealthRestore (쓰면 그만큼 돌아온다 — 장비의 같은 칸은 최대치 보너스라 HealthModifer 로 간다)
  마력변화 → ManaRestore · 이동맵 + 이동좌표 → RecallArea(맵 번호표 plans/5.99-맵번호표.tsv) · RecallX · RecallY
  이름 → Name · 이미지 → DisplayImage = 0x8000 + 이미지(아이콘, 장비와 같은 체계) · 판매가격 → Value
  ScriptName Consumable(scripts/Items/Consumable.cs) · Flags 쌓임·소모·거래·보관·판매 + 떨굼여부 0 이 아니면 버리기
  MaxStack 100 — 하데스 소모품에서 가장 흔한 값이다(팩에는 한 칸에 겹치는 수가 따로 없다).

**옮기지 않은 것:** 배고픔변화(하데스에 배고픔이 없다), 사용펄숫(부르는 스크립트가 팩에 없다 — 코마디움 둘만 있어
그 둘은 전용 스크립트가 필요해 뺀다), 맵 번호표에 없는 맵으로 가는 귀환 주문서.

  쓰는 법: python3 scripts/build-pack-consumables.py [--쓰기]
"""
import json
import sys
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
ITEMS = ROOT / "data" / "server-packs" / "extracted" / "5.99-server" / "items.json"
MAP_IDS = ROOT / "plans" / "5.99-맵번호표.tsv"
OUT = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates" / "items"

SOURCES = {"Potion.txt": "물약", "Hungry.txt": "음식", "Recoll.txt": "귀환"}

#: 사용펄숫이 실제 스크립트(부활)인 것 — 칸만으로는 할 수 없다.
NEEDS_OWN_SCRIPT = {"코마디움", "엑스코마디움"}

# ItemFlags (Types/ItemFlags.cs)
TRADEABLE, DROPABLE, BANKABLE, SELLABLE, STACKABLE, CONSUMABLE = 4, 8, 16, 32, 1 << 7, 1 << 8

configure_utf8_stdio(sys.stdout, sys.stderr)


def text(fields, key, default=""):
    value = fields.get(key, default)
    if isinstance(value, list):
        value = value[0] if value else default
    return (value or default).strip()


def number(fields, key, default=0):
    """`+1000` 처럼 부호가 붙어 있다. 앞의 숫자만 읽는다."""
    digits = ""
    for ch in text(fields, key):
        if ch.isdigit() or (ch in "+-" and not digits):
            digits += ch
        else:
            break
    return int(digits) if digits.strip("+-") else default


def map_ids():
    ids = {}
    for line in MAP_IDS.read_text(encoding="utf-8").splitlines():
        if line and not line.startswith("#"):
            cells = line.split("\t")
            ids.setdefault(cells[1], int(cells[2]))
    return ids


def template(item, kind, ids):
    f = item["fields"]
    flags = STACKABLE | CONSUMABLE | TRADEABLE | BANKABLE | SELLABLE
    if text(f, "떨굼여부", "1") != "0":
        flags |= DROPABLE

    made = {
        "$type": "Darkages.Types.ItemTemplate, Darkages.Server",
        "Name": text(f, "이름"),
        "DisplayImage": 0x8000 + number(f, "이미지"),
        "ScriptName": "Consumable",
        "Flags": flags,
        "CanStack": True,
        "MaxStack": 100,
        "Value": max(0, number(f, "판매가격")),
    }
    if number(f, "체력변화"):
        made["HealthRestore"] = number(f, "체력변화")
    if number(f, "마력변화"):
        made["ManaRestore"] = number(f, "마력변화")
    if text(f, "이동맵"):
        x, _, y = text(f, "이동좌표").partition(",")
        made["RecallArea"] = ids[text(f, "이동맵")]
        made["RecallX"], made["RecallY"] = int(x), int(y)
    made["Group"] = f"5.99표/{kind}"
    return made


def main():
    write = "--쓰기" in sys.argv
    ids = map_ids()
    made, skipped, replaced = 0, [], []
    for item in json.loads(ITEMS.read_text(encoding="utf-8")):
        kind = SOURCES.get(Path(item["출처"]).name)
        if kind is None:
            continue
        name = item["이름"]
        if name in NEEDS_OWN_SCRIPT:
            skipped.append(f"{name}(전용 스크립트)")
            continue
        where = text(item["fields"], "이동맵")
        if where and where not in ids:
            skipped.append(f"{name}(맵 {where} 이 번호표에 없다)")
            continue
        path = OUT / f"{name}.json"
        if path.exists():
            replaced.append(name)
        if write:
            path.write_text(json.dumps(template(item, kind, ids), ensure_ascii=False, indent=2), encoding="utf-8")
        made += 1

    print(f"소모품 {made}종 {'씀' if write else '(세어만 봄 — --쓰기 로 쓴다)'} → {OUT.relative_to(ROOT)}")
    if replaced:
        print(f"  이미 있던 것을 덮음 {len(replaced)}: {', '.join(replaced)}")
    if skipped:
        print(f"  뺀 것 {len(skipped)}: {', '.join(skipped)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
