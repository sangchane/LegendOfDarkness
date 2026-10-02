"""5.99 장비 표를 하데스 템플릿으로 바꾸는 규칙 — build-pack-equipment.py 와 build-nova-knuckles.py 가 같이 쓴다."""
from pathlib import Path

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


# 원작처럼 바지까지 그려지는 옷. 혼든 팩이 더 많은 옷을 입히지만 팩 셋이 일치하지 않아 사용자가 기억한 것만.
WITH_PANTS = {"천지도복"}

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


def classify(fields):
    """(갈래 이름, None) 또는 장비가 아니면 (None, 뺀 이유)."""
    # 타입 0(또는 칸 없음)만 장비다 — 속성 3 에는 염색약·퀘스트 두루마리(타입 2)·귀환 주문서(타입 1)도 있다.
    if text(fields, "타입", "0") != "0":
        return None, "타입이 0 이 아니다"
    for kind, spec in KINDS.items():
        if text(fields, "속성") in spec["slots"]:
            if kind == "무기":
                # 속성 0 에 섞인 재료는 공격력 칸이 없다. 무기는 그림이 있거나 일부러 안 보이게(안보이기 1) 한 것만.
                if "최소공격력1" not in fields:
                    return None, "무기 자리인데 공격력 칸이 없다(재료)"
                if number(fields, "착용이미지") <= 0 and text(fields, "안보이기") != "1":
                    return None, "무기인데 착용 그림이 없다"
            elif kind == "갑옷" and number(fields, "착용이미지") <= 0:
                return None, "갑옷인데 착용 그림이 없다"
            return kind, None
    return None, "속성이 장비 자리가 아니다"


def kind_of(fields):
    """장비면 그 갈래 이름, 아니면 None."""
    return classify(fields)[0]


#: 원작 그림이 없는 착용 번호를 빌려 입힌다 — 글러브1 의 79 는 5.99·하데스 아카이브 모두 mw079 가 없어(무기 그림 001~151 중
#: 79 만 빔) 맨손으로 보였다. 견습자의글러브(86) 그림을 쓴다(사용자 2026-10-03).
WORN_IMAGE = {"글러브1": 86}


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
        "Image": WORN_IMAGE.get(text(f, "이름"), number(f, "착용이미지")),
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
    # 바지를 입히는 옷(서버가 몸 번호 아래 반쪽에 바지 색을 실어 보낸다 — Armor.cs · 원작 Legend.exe 0x54ffaa).
    # 5.99 팩에는 이 칸이 없다. 사용자(2026-09-26): "천지도복 입으면 원래 하의까지 표시됐다".
    if made["Name"] in WITH_PANTS:
        made["HasPants"] = True
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
