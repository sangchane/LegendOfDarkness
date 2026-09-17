#!/usr/bin/env python3
"""5.99 서버팩의 장비(무기·갑옷·방패·투구·장신구·장갑·허리띠·각반·신발·장식)를 하데스 아이템 템플릿으로 옮긴다.

2026-09-17 무기·갑옷에 나머지 장비 칸을 더했다 — 상점 판매 목록 중 서버에 없던 103종의 대부분(장식 56 · 목걸이 8 …)이
이 칸들이었다. 칸마다 하데스가 이미 쓰는 스크립트·자리를 따른다(하데스표 템플릿의 ScriptName·EquipmentSlot 짝):
방패 Shield 3 · 투구 Helmet 4 · 귀걸이 Earring 5 · 목걸이 Necklace 6 · 반지 Generic 7(왼손, 차 있으면 오른손) ·
장갑 Generic 9(왼팔, 차 있으면 오른팔) · 허리띠 Belt 11 · 각반 Generic 12 · 신발 Boot 13 · 장식 Generic 14.
**장식은 입은 모습으로 그리지 않는다** — 7.18 겉모습(0x33)의 OverCoat 칸을 채우는 스크립트가 하데스에 없다. 능력치만 붙는다.

사용자 결정(2026-09-17): 5.99 무기를, 이어서 갑옷을 서버에 들인다. 하데스 무기 템플릿은 남겨 둔다(사용자 결정). 상점·드롭은 나중에
5.99 NPC·상점과 함께 옮긴다 — 이번에는 템플릿만.

그림 주의: 우리가 가진 5.99 한국 클라이언트는 무기 27개 번호의 그림이 다른 무기로 바뀌어 있지만, 5.99 팩 아이템의
착용 번호는 하데스(영문) 아카이브 그림과 맞는다(3 커틀라스 = 칼, 6 단검 = 단검, 11 매스케이드 = Masquerade 칼).
그래서 옷장 생성기는 무기만 하데스 아카이브를 먼저 본다(`build-client-wardrobe.py` 의 `archives`).

**어느 것이 무기·갑옷인가:** 5.99 아이템의 `속성` 칸이 장비 자리다 — 0·12·13 무기, 1 갑옷, 2 방패, 3 투구, 4 귀걸이,
5 목걸이, 6 반지, 7 장갑, 8 허리띠, 9 각반, 10 신발, 11 장식. 폴더 이름은 믿을 수 없다(`전사방어구.txt` 에 도끼·곤봉이
있다). 속성 0 에는 재료도 섞여 있어 공격력(`최소공격력1`)과 착용 그림이 있는 것만 무기로 본다. 갑옷은 속성 1 이면서
착용 그림이 있는 것 — 치장옷·드레스·길드옷까지 들어간다. 도복(공격모션 132)을 입으면 무기 없이 주먹이 나가고
(`Assail.BlowMotion`), 신발과 함께 못 입는다(`scripts/Items/Armor.cs`·`Boot.cs`, Novaonline.exe 0x41cc49·0x41d387).

**칸을 잇는 법:**
  이름 → Name · 착용이미지 → Image(입은 그림, `Aisling.Weapon`) · 이미지 → DisplayImage = 0x8000 + 이미지
    (아이콘 칸 번호 체계가 같다 — 5.99 설단검 91 = 하데스 Dirk 91, 에페 87 = Eppe 87)
  직업제한 → Class (1 전사 · 2 도적 · 3 마법사 · 4 성직자 · 5 도가 — 하데스 `Class` 와 번호가 같다)
  성별제한 0 → Gender 255(둘 다) · 1 남 · 2 여(마스터아머1 남 ↔ 마스터아머2 여) · 레벨제한 → LevelRequired(없으면 1) · 승급제한 1 → StageRequired Master
  최소/최대공격력1 → DmgMin/DmgMax(`Pack599.AttackPower` 가 둘의 평균을 쓴다) · 내구력 → MaxDurability
  판매가격 → Value · 무게 → CarryWeight
  방어력·마법방어·명중수정·공격수정·체력변화·마력변화·힘/덱스/인트/위즈/콘변화 → *Modifer
    (부호가 같은 뜻이다 — 5.99 레더튜닉 방어력 −10 = 하데스 Leather Tunic `AcModifer` 빼기 10)
  Flags: 장착·거래·보관·판매 + 떨굼여부 0 이 아니면 버리기 + 수리여부 1 이면 수리 + (무기만) 공격모션 129 면 양손

공격모션·공격속도 → AttackMotion·AttackSpeed(평타 몸 동작, `Assail.BlowMotion`)

**옮기지 못한 칸:** 최소/최대공격력2(마법 공격력 — 하데스 템플릿에 칸이 없다), 공격속도가 뜻하는 평타 잠금(속도×10ms), 사운드1·2, 속성(원소), 어빌제한·전직제한, 장착펄숫·장착해제펄숫(끼고 벗을 때 스크립트).

  쓰는 법: python3 scripts/build-pack-equipment.py [--쓰기]
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

#: 장비 갈래 — 5.99 속성 → 하데스 아이템 스크립트·장비 자리(Types/ItemSlots.cs)·묶음 이름.
KINDS = {
    "무기": {"slots": {"0", "12", "13"}, "script": "Weapon", "equipment": 1},
    "갑옷": {"slots": {"1"}, "script": "Armor", "equipment": 2},
    "방패": {"slots": {"2"}, "script": "Shield", "equipment": 3},
    "투구": {"slots": {"3"}, "script": "Helmet", "equipment": 4},
    "귀걸이": {"slots": {"4"}, "script": "Earring", "equipment": 5},
    "목걸이": {"slots": {"5"}, "script": "Necklace", "equipment": 6},
    "반지": {"slots": {"6"}, "script": "Generic", "equipment": 7},
    "장갑": {"slots": {"7"}, "script": "Generic", "equipment": 9},
    "허리띠": {"slots": {"8"}, "script": "Belt", "equipment": 11},
    "각반": {"slots": {"9"}, "script": "Generic", "equipment": 12},
    "신발": {"slots": {"10"}, "script": "Boot", "equipment": 13},
    "장식": {"slots": {"11"}, "script": "Generic", "equipment": 14},
}

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


def kind_of(fields):
    """장비면 그 갈래 이름, 아니면 None."""
    # 타입 0(또는 칸 없음)만 장비다 — 속성 3 에는 염색약·퀘스트 두루마리(타입 2)·귀환 주문서(타입 1)도 있다.
    if text(fields, "타입", "0") != "0":
        return None
    for kind, spec in KINDS.items():
        if text(fields, "속성") in spec["slots"]:
            if kind == "무기":
                # 속성 0 에 섞인 재료는 공격력 칸이 없다. 무기는 그림이 있거나 일부러 안 보이게(안보이기 1) 한 것만.
                if "최소공격력1" not in fields:
                    return None
                if number(fields, "착용이미지") <= 0 and text(fields, "안보이기") != "1":
                    return None
            elif kind == "갑옷" and number(fields, "착용이미지") <= 0:
                return None
            return kind
    return None


def template(item):
    f = item["fields"]
    kind = kind_of(f)
    spec = KINDS[kind]
    flags = EQUIPABLE | TRADEABLE | BANKABLE | SELLABLE
    if text(f, "떨굼여부", "1") != "0":
        flags |= DROPABLE
    if text(f, "수리여부") == "1":
        flags |= REPAIRABLE
    if kind == "무기" and text(f, "공격모션") == "129":
        flags |= TWO_HANDED

    made = {
        "$type": "Darkages.Types.ItemTemplate, Darkages.Server",
        "Name": text(f, "이름"),
        "Image": number(f, "착용이미지"),
        "DisplayImage": 0x8000 + number(f, "이미지"),
        "EquipmentSlot": spec["equipment"],
        "Class": number(f, "직업제한"),
        "Gender": {1: 1, 2: 2}.get(number(f, "성별제한"), 255),
        "LevelRequired": min(99, max(1, number(f, "레벨제한", 1))),
        "ScriptName": spec["script"],
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
    # 평타 몸 동작 — 5.99 서버는 무기의 공격모션·공격속도를 그대로 보낸다(Assail.BlowMotion).
    if number(f, "공격모션"):
        made["AttackMotion"] = min(255, number(f, "공격모션"))
    if number(f, "공격속도"):
        made["AttackSpeed"] = min(255, number(f, "공격속도"))
    for key, field in MODIFIERS.items():
        value = number(f, key)
        if value:
            made[field] = {
                "$type": "Darkages.Types.StatusOperator, Darkages.Server",
                "Option": 0 if value > 0 else 1,
                "Value": abs(value),
            }
    made["Group"] = f"5.99표/{kind}/{Path(item['출처']).stem}"
    return made


def main():
    write = "--쓰기" in sys.argv
    items = json.loads(ITEMS.read_text(encoding="utf-8"))
    chosen = [item for item in items if kind_of(item["fields"])]

    names = Counter(text(item["fields"], "이름") for item in chosen)
    twice = sorted(name for name, count in names.items() if count > 1)

    replaced, made, unsendable = [], 0, []
    for item in chosen:
        body = template(item)
        path = OUT / f"{body['Name']}.json"
        if path.exists():
            replaced.append(body["Name"])
        if body["ScriptName"] == "Weapon" and body["Image"] > 255:
            unsendable.append(f"{body['Name']}({body['Image']})")
        if write:
            path.write_text(json.dumps(body, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        made += 1

    print(f"장비 {made}종 {'썼다' if write else '(미리보기 — --쓰기 로 쓴다)'} → {OUT.relative_to(ROOT)}")
    if twice:
        print(f"  팩에 같은 이름이 둘 이상 — 나중 것이 남는다: {', '.join(twice)}")
    print("  갈래별", dict(Counter(kind_of(item["fields"]) for item in chosen)))
    print("  파일별", dict(Counter(Path(item["출처"]).stem for item in chosen)))
    if replaced:
        print(f"  이미 있던 같은 이름 {len(replaced)}개를 덮는다: {', '.join(replaced)}")
    if unsendable:
        print(f"  착용 번호가 255 를 넘어 서버가 보내지 못한다(ServerFormat33 이 무기를 1바이트로 쓴다): {', '.join(unsendable)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
