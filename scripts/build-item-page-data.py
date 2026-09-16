#!/usr/bin/env python3
"""아이템을 화면에서 볼 수 있게 한 덩어리로 뽑는다 — 슬롯별, 직업별.

978장을 표로 보면 무엇이 어느 자리에 쓰이는지가 안 보인다. 슬롯과 직업으로 나누고
대표 수치 하나만 앞에 내면 보인다. 베이스는 하데스(영문)이고, 한글 이름은
`compare-packs.py` 가 정한 것을 얹는다. `data/아이템-한글이름.tsv` 의 사람이 고친 값은
거기서 이미 최우선으로 반영돼 들어온다 (등급 `HUMAN`).

카페에만 있는 371장(`items-cafe-missing.json`)은 **싣지 않는다.** 게임에 없는 것이라
섞으면 도감을 보고 만든 판단이 틀린다.

  쓰는 법: python3 scripts/build-item-page-data.py   → docs/items-data.js
"""
import json
import sys
from collections import Counter
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
HADES = ROOT / "data" / "game-data" / "items-hades.json"
KOREAN = ROOT / "data" / "pack-compare" / "item-korean-names.json"
OUT = ROOT / "docs" / "items-data.js"

configure_utf8_stdio(sys.stdout, sys.stderr)

# 하데스 `EquipmentSlot` → 사람이 읽는 이름. 0 은 착용하지 않는 것들이다.
SLOTS = {0: "기타", 1: "무기", 2: "옷", 3: "방패", 4: "투구", 5: "귀걸이", 6: "목걸이",
         7: "반지", 8: "반지", 9: "장갑", 10: "장갑", 11: "벨트", 12: "각반", 13: "신발"}

# `Class` 0 은 제한 없음이다. 1~5 는 원작 다섯 직업.
CLASSES = {0: "공용", 1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "무도가"}

# `Gender` 255 는 제한 없음. 자료에 0 도 있는데 그것도 제한이 없는 쪽으로 읽는다.
GENDERS = {1: "남", 2: "여"}

# 카드에 내보낼 수치. 화면에서 스탯 줄을 만들 때 이 차례대로 읽는다.
STATS = [("AcModifer", "방어"), ("MrModifer", "마방"), ("HitModifer", "명중"),
         ("DmgModifer", "공격"), ("HealthModifer", "체력"), ("ManaModifer", "마력"),
         ("StrModifer", "힘"), ("DexModifer", "덱스"), ("IntModifer", "인트"),
         ("WisModifer", "위즈"), ("ConModifer", "콘")]


def stat_value(raw):
    """수정치 칸은 숫자일 때도 있고 `StatusOperator` 객체일 때도 있다.

    하데스는 `{"$type": "...StatusOperator", "Option": 0, "Value": 150}` 로 싣는다. 그대로
    내보내면 화면에 `[object Object]` 가 찍힌다. `Option` 은 더하기/곱하기 구분인데 자료에
    0 밖에 없어 지금은 읽지 않는다 — 다른 값이 나오면 그때 붙인다.
    """
    if isinstance(raw, dict):
        raw = raw.get("Value")
    if isinstance(raw, str):
        raw = raw.strip()            # 몇 장은 수를 따옴표로 싣는다 (`"750"`)
        return int(raw) if raw.lstrip("-").isdigit() else 0
    return raw or 0


def headline(item):
    """카드 앞줄에 낼 대표 수치 하나. 무기면 데미지, 그 밖엔 방어력.

    둘 다 없는 것이 많아서(978 중 방어력 443 · 데미지 96) 없으면 빈 칸으로 둔다 —
    없는 값을 0 으로 채우면 "방어 0" 이 방어구처럼 보인다.
    """
    if item.get("DmgMin") or item.get("DmgMax"):
        return f"{item.get('DmgMin', 0)}~{item.get('DmgMax', 0)}"
    ac = stat_value(item.get("AcModifer"))
    if ac:
        return f"방어 {ac}"
    return ""


def main():
    items = json.loads(HADES.read_text(encoding="utf-8-sig"))
    korean = {}
    if KOREAN.exists():
        for row in json.loads(KOREAN.read_text(encoding="utf-8")):
            if row.get("한글이름"):
                korean[row["영문"]] = (row["한글이름"], row.get("등급") or "")

    rows = []
    for it in items:
        name = it.get("Name")
        if not name:
            continue
        ko, source = korean.get(name, ("", ""))
        stats = [[label, stat_value(it[key])] for key, label in STATS
                 if it.get(key) and stat_value(it[key])]
        rows.append({
            "en": name,
            "ko": ko,
            "src": source,
            "slot": SLOTS.get(it.get("EquipmentSlot"), "기타"),
            "cls": CLASSES.get(it.get("Class"), "공용"),
            "sex": GENDERS.get(it.get("Gender"), ""),
            "lv": it.get("LevelRequired") or 0,
            "head": headline(it),
            "img": (it.get("DisplayImage") or 0) & 0x7FFF or it.get("Image") or 0,
            "val": it.get("Value") or 0,
            "dur": it.get("MaxDurability") or 0,
            "stats": stats,
        })

    # 슬롯 → 직업 → 레벨 차례로 정렬해 둔다. 화면은 이 차례를 그대로 쓴다.
    slot_order = {n: i for i, n in enumerate(["무기", "옷", "방패", "투구", "귀걸이", "목걸이",
                                              "반지", "장갑", "벨트", "각반", "신발", "기타"])}
    cls_order = {n: i for i, n in enumerate(["공용", "전사", "도적", "마법사", "사제", "무도가"])}
    rows.sort(key=lambda r: (slot_order.get(r["slot"], 99), cls_order.get(r["cls"], 99),
                             r["lv"], r["en"]))

    payload = {
        "생성": "scripts/build-item-page-data.py",
        "총": len(rows),
        "한글": sum(1 for r in rows if r["ko"]),
        "슬롯": [s for s in slot_order if any(r["slot"] == s for r in rows)],
        "직업": [c for c in cls_order if any(r["cls"] == c for r in rows)],
        "목록": rows,
    }
    OUT.write_text("window.LOD_ITEMS = " + json.dumps(payload, ensure_ascii=False) + ";\n",
                   encoding="utf-8")

    print(f"아이템 {len(rows)}장 · 한글 이름 {payload['한글']}장 → {OUT.relative_to(ROOT)}")
    print("  슬롯별: " + " · ".join(f"{s} {c}" for s, c in
                                 sorted(Counter(r["slot"] for r in rows).items(),
                                        key=lambda kv: slot_order.get(kv[0], 99))))
    print("  등급별: " + " · ".join(f"{s or '없음'} {c}" for s, c in
                                 Counter(r["src"] for r in rows).most_common()))


if __name__ == "__main__":
    main()
