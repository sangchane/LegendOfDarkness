#!/usr/bin/env python3
"""5.99 서버팩의 무기를 하데스 아이템 템플릿으로 옮긴다.

사용자 결정(2026-09-17): 5.99 무기를 서버에 들인다. 하데스 무기 템플릿은 남겨 둔다(사용자 결정). 상점·드롭은 나중에
5.99 NPC·상점과 함께 옮긴다 — 이번에는 템플릿만.

그림 주의: 우리가 가진 5.99 한국 클라이언트는 무기 27개 번호의 그림이 다른 무기로 바뀌어 있지만, 5.99 팩 아이템의
착용 번호는 하데스(영문) 아카이브 그림과 맞는다(3 커틀라스 = 칼, 6 단검 = 단검, 11 매스케이드 = Masquerade 칼).
그래서 옷장 생성기는 무기만 하데스 아카이브를 먼저 본다(`build-client-wardrobe.py` 의 `archives`).

**어느 것이 무기인가:** 5.99 아이템의 `속성` 칸이 장비 자리다 — 0·12·13 무기, 1 갑옷, 2 방패, 3 투구, 4 귀걸이,
5 목걸이, 6 반지, 7 장갑, 8 허리띠, 9 각반, 10 신발, 11 장식. 폴더 이름은 믿을 수 없다(`전사방어구.txt` 에 도끼·곤봉이
있다). 속성 0 에는 재료도 섞여 있어 공격력(`최소공격력1`)과 착용 그림이 있는 것만 무기로 본다.

**칸을 잇는 법:**
  이름 → Name · 착용이미지 → Image(입은 그림, `Aisling.Weapon`) · 이미지 → DisplayImage = 0x8000 + 이미지
    (아이콘 칸 번호 체계가 같다 — 5.99 설단검 91 = 하데스 Dirk 91, 에페 87 = Eppe 87)
  직업제한 → Class (1 전사 · 2 도적 · 3 마법사 · 4 성직자 · 5 도가 — 하데스 `Class` 와 번호가 같다)
  성별제한 0 → Gender 255(둘 다) · 레벨제한 → LevelRequired(없으면 1) · 승급제한 1 → StageRequired Master
  최소/최대공격력1 → DmgMin/DmgMax(`Pack599.AttackPower` 가 둘의 평균을 쓴다) · 내구력 → MaxDurability
  판매가격 → Value · 무게 → CarryWeight
  방어력·마법방어·명중수정·공격수정·체력변화·마력변화·힘/덱스/인트/위즈/콘변화 → *Modifer
    (부호가 같은 뜻이다 — 5.99 레더튜닉 방어력 −10 = 하데스 Leather Tunic `AcModifer` 빼기 10)
  Flags: 장착·거래·보관·판매 + 떨굼여부 0 이 아니면 버리기 + 수리여부 1 이면 수리 + 공격모션 129(양손 휘두르기)면 양손

**옮기지 못한 칸:** 최소/최대공격력2(마법 공격력 — 하데스 템플릿에 칸이 없다), 공격모션(평타 몸 동작 — 하데스 평타는
1 또는 양손 129 만 보낸다), 공격속도, 사운드1·2, 속성(원소), 어빌제한·전직제한, 장착펄숫·장착해제펄숫(끼고 벗을 때 스크립트).

  쓰는 법: python3 scripts/build-pack-weapons.py [--쓰기]
"""
import json
import sys
from collections import Counter
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
ITEMS = ROOT / "data" / "server-packs" / "extracted" / "5.99-server" / "items.json"
OUT = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates" / "items"

configure_utf8_stdio(sys.stdout, sys.stderr)

WEAPON_SLOTS = {"0", "12", "13"}

# ItemFlags (Types/ItemFlags.cs)
EQUIPABLE, TRADEABLE, DROPABLE, BANKABLE, SELLABLE, REPAIRABLE, TWO_HANDED = 1, 4, 8, 16, 32, 64, 1 << 13

MODIFIERS = {
    "방어력": "AcModifer",
    "마법방어": "MrModifer",
    "명중수정": "HitModifer",
    "공격수정": "DmgModifer",
    "체력변화": "HealthModifer",
    "마력변화": "ManaModifer",
    "힘변화": "StrModifer",
    "덱스변화": "DexModifer",
    "인트변화": "IntModifer",
    "위즈변화": "WisModifer",
    "콘변화": "ConModifer",
}


def text(fields, key, default=""):
    """칸 값. 같은 칸이 두 번 적힌 줄은 목록으로 뽑혀 있어 첫 값을 쓴다."""
    value = fields.get(key, default)
    if isinstance(value, list):
        value = value[0] if value else default
    return (value or default).strip()


def number(fields, key, default=0):
    """팩 값은 글자다. `0g` 처럼 끝에 찌꺼기가 붙은 것이 있어 앞의 숫자만 읽는다."""
    text_value = text(fields, key)
    digits = ""
    for ch in text_value:
        if ch.isdigit() or (ch == "-" and not digits):
            digits += ch
        else:
            break
    return int(digits) if digits not in ("", "-") else default


def is_weapon(fields):
    return text(fields, "속성") in WEAPON_SLOTS and "최소공격력1" in fields and number(fields, "착용이미지") > 0


def template(item):
    f = item["fields"]
    flags = EQUIPABLE | TRADEABLE | BANKABLE | SELLABLE
    if text(f, "떨굼여부", "1") != "0":
        flags |= DROPABLE
    if text(f, "수리여부") == "1":
        flags |= REPAIRABLE
    if text(f, "공격모션") == "129":
        flags |= TWO_HANDED

    made = {
        "$type": "Darkages.Types.ItemTemplate, Darkages.Server",
        "Name": text(f, "이름"),
        "Image": number(f, "착용이미지"),
        "DisplayImage": 0x8000 + number(f, "이미지"),
        "EquipmentSlot": 1,
        "Class": number(f, "직업제한"),
        "Gender": {1: 1, 2: 2}.get(number(f, "성별제한"), 255),
        "LevelRequired": min(99, max(1, number(f, "레벨제한", 1))),
        "ScriptName": "Weapon",
        "Flags": flags,
        "CanStack": False,
        "MaxStack": 0,
        "Value": max(0, number(f, "판매가격")),
        "DmgMin": number(f, "최소공격력1"),
        "DmgMax": number(f, "최대공격력1"),
        "MaxDurability": max(0, number(f, "내구력")),
        "CarryWeight": min(255, max(0, number(f, "무게"))),
    }
    if number(f, "승급제한") > 0:
        made["StageRequired"] = 1
    for key, field in MODIFIERS.items():
        value = number(f, key)
        if value:
            made[field] = {
                "$type": "Darkages.Types.StatusOperator, Darkages.Server",
                "Option": 0 if value > 0 else 1,
                "Value": abs(value),
            }
    made["Group"] = f"5.99표/무기/{Path(item['출처']).stem}"
    return made


def main():
    write = "--쓰기" in sys.argv
    items = json.loads(ITEMS.read_text(encoding="utf-8"))
    weapons = [item for item in items if is_weapon(item["fields"])]

    replaced, made, unsendable = [], 0, []
    for item in weapons:
        body = template(item)
        path = OUT / f"{body['Name']}.json"
        if path.exists():
            replaced.append(body["Name"])
        if body["Image"] > 255:
            unsendable.append(f"{body['Name']}({body['Image']})")
        if write:
            path.write_text(json.dumps(body, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        made += 1

    print(f"무기 {made}종 {'썼다' if write else '(미리보기 — --쓰기 로 쓴다)'} → {OUT.relative_to(ROOT)}")
    print("  파일별", dict(Counter(Path(item["출처"]).stem for item in weapons)))
    if replaced:
        print(f"  이미 있던 같은 이름 {len(replaced)}개를 덮는다: {', '.join(replaced)}")
    if unsendable:
        print(f"  착용 번호가 255 를 넘어 서버가 보내지 못한다(ServerFormat33 이 무기를 1바이트로 쓴다): {', '.join(unsendable)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
