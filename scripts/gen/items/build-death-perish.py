#!/usr/bin/env python3
"""죽으면 사라지는 장비(ItemFlags.Perishable)를 사용자 규칙대로 켜고 끈다.

  python3 scripts/gen/items/build-death-perish.py          # 켤 것·끌 것 수와 예시만 본다
  python3 scripts/gen/items/build-death-perish.py --쓰기    # 아이템 템플릿 Flags 를 고친다

**사용자 결정(2026-10-11, 명세 `autopilot/death-penalty-gear/SPEC.md`)**: 「사망하면 접두사 붙은 아이템 사라지게」 — 확인 답
「사라지는 것 = 신 이름 접두사 전부 + 수토풍화 속성 목걸이·벨트, 크리스탈목걸이는 바닥에」. 서버(`Aisling.SendToHell`)는
이 표시 하나만 보고 없앨지 바닥에 둘지 정한다.
  - 이름이 신 이름 접두사(세토아의·메투스의·칸의 …)로 시작하는 장비
  - 목걸이(6)·벨트(11) 자리이고 OffenseElement·DefenseElement 중 하나가 0 이 아니며 이름에 「크리스탈」이 없는 것
  - 그 밖의 장비(Flags 에 Equipable)는 Perishable 을 끈다. 장비가 아닌 물건은 건드리지 않는다.

Flags 는 숫자(61)나 글자(「NormalEquipPerish」·「A, B」) 꼴이 섞여 있다 — 원래 꼴을 지켜 그 한 줄만 바꾼다(BOM·들여쓰기·나머지 필드 그대로).
"""

import argparse
import json
import re
import sys

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT

ITEMS = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server/templates/items"

PREFIX = re.compile(r"^(세토아|메투스|칸|셔스|로오|이아|세오|뮤레칸|풍요|축복|체력|마력)의")
NECK, BELT = 6, 11  # ItemSlots.Necklace · ItemSlots.Waist

# 서버 ItemFlags.cs 와 같은 값 — 글자 꼴 Flags 를 셈하려고.
FLAG = {
    "Equipable": 1, "Perishable": 1 << 1, "Tradeable": 1 << 2, "Dropable": 1 << 3, "Bankable": 1 << 4,
    "Sellable": 1 << 5, "Repairable": 1 << 6, "Stackable": 1 << 7, "Consumable": 1 << 8, "PerishIFEquipped": 1 << 9,
    "Elemental": 1 << 10, "QuestRelated": 1 << 11, "Upgradeable": 1 << 12, "TwoHanded": 1 << 13,
    "LongRanged": 1 << 14, "Trap": 1 << 15,
}
FLAG["NormalEquipment"] = sum(FLAG[n] for n in ("Equipable", "Repairable", "Tradeable", "Sellable", "Bankable", "Dropable"))
FLAG["NormalEquipPerish"] = FLAG["NormalEquipment"] | FLAG["Perishable"]
FLAG["NormalEquipElementPerish"] = FLAG["NormalEquipPerish"] | FLAG["Elemental"]
FLAG["NormalConsumable"] = sum(FLAG[n] for n in ("Consumable", "Stackable", "Dropable", "Sellable", "Tradeable", "Bankable"))
PERISH = FLAG["Perishable"]

FLAGS_LINE = re.compile(r'^(\s*"Flags"\s*:\s*)("[^"]*"|-?\d+)', re.M)


def perishes(body):
    """이 장비가 죽을 때 사라져야 하나 — 사용자 규칙(2026-10-11)."""
    name = body.get("Name") or ""
    if PREFIX.match(name):
        return True
    elemental = (body.get("OffenseElement") or 0) != 0 or (body.get("DefenseElement") or 0) != 0
    return body.get("EquipmentSlot") in (NECK, BELT) and elemental and "크리스탈" not in name


def value(flags):
    """숫자든 「A, B」 글자든 정수로."""
    if isinstance(flags, int):
        return flags
    return sum(FLAG[part.strip()] for part in flags.split(",") if part.strip())


def toggled(flags, on):
    """Perishable 을 켜거나 끈 Flags — 원래 꼴(숫자·글자)대로."""
    if isinstance(flags, int):
        return flags | PERISH if on else flags & ~PERISH
    parts = [part.strip() for part in flags.split(",") if part.strip()]
    if on:
        parts = ["NormalEquipPerish" if p == "NormalEquipment" else p for p in parts]
        if not value(", ".join(parts)) & PERISH:
            parts.append("Perishable")
    else:
        swap = {"NormalEquipPerish": ["NormalEquipment"], "NormalEquipElementPerish": ["NormalEquipment", "Elemental"]}
        parts = [q for p in parts if p != "Perishable" for q in swap.get(p, [p])]
    text = ", ".join(parts)
    assert bool(value(text) & PERISH) == on and value(text) & ~PERISH == value(flags) & ~PERISH, (flags, text)
    return text


def main():
    parser = argparse.ArgumentParser(description="죽으면 사라지는 장비")
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    args = parser.parse_args()

    turn_on, turn_off, kept_on = [], [], 0
    for path in sorted(ITEMS.glob("*.json")):
        raw = path.read_bytes()
        bom = raw.startswith(b"\xef\xbb\xbf")
        text = raw.decode("utf-8-sig")
        body = json.loads(text)
        flags = body.get("Flags")
        if flags is None or not value(flags) & FLAG["Equipable"]:
            continue
        want, have = perishes(body), bool(value(flags) & PERISH)
        if want == have:
            kept_on += want
            continue
        (turn_on if want else turn_off).append(body["Name"])
        if args.writing:
            new = toggled(flags, want)
            shown = json.dumps(new, ensure_ascii=False) if isinstance(new, str) else str(new)
            text, n = FLAGS_LINE.subn(lambda m: m.group(1) + shown, text, count=1)
            assert n == 1, path
            path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))

    note = "" if args.writing else " (아직 안 씀 — --쓰기)"
    print(f"사라짐 표시: 켤 것 {len(turn_on)} · 끌 것 {len(turn_off)} · 이미 맞게 켜짐 {kept_on}"
          f" → 켜진 장비 {kept_on + len(turn_on)}{note}")
    print(f"  켤 것 예: {', '.join(turn_on[:12])}")
    print(f"  끌 것 예: {', '.join(turn_off[:12])}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
