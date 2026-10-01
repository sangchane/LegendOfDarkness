"""원작 도감 줄과 하데스 템플릿 칸을 잇는 법 — build-gear-from-original.py 와 build-item-page-data.py 가 같이 쓴다."""


#: 도감 칸 → 템플릿 칸. 부호가 그대로 옮겨진다(`Option 1` 이 빼기다).
MODIFIERS = {
    "방어력": "AcModifer",
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


STATUS_OPERATOR = "Darkages.Types.StatusOperator, Darkages.Server"


def number(value, default=0):
    """도감 값은 글자다. 앞의 숫자만 읽는다(`build-pack-equipment.py` 와 같은 규칙)."""
    digits = ""
    for ch in str(value or "").strip():
        if ch.isdigit() or (ch == "-" and not digits):
            digits += ch
        else:
            break
    return int(digits) if digits not in ("", "-") else default


def modifier_of(item, field):
    """템플릿에 적힌 수정치를 부호 있는 정수로."""
    made = item.get(field)
    if not made:
        return 0
    value = int(made.get("Value", 0))
    return -value if int(made.get("Option", 0)) == 1 else value


def wanted(row):
    """도감 한 줄 → 템플릿 칸 이름과 값. 값 `None` 은 「칸을 뺀다」."""
    want = {
        "Value": max(0, number(row.get("판매가격"))),
        "CarryWeight": min(255, max(0, number(row.get("무게")))),
        "MaxDurability": max(0, number(row.get("내구력"))),
        "Class": number(row.get("직업제한")),
        "LevelRequired": min(99, max(1, number(row.get("레벨제한"), 1))),
    }
    attack = row.get("공격력", "")
    if "m" in attack:  # `10m20` 꼴. 갑옷 줄에는 없다
        least, _, most = attack.partition("m")
        want["DmgMin"], want["DmgMax"] = number(least), number(most)
    for column, field in MODIFIERS.items():
        value = number(row.get(column))
        want[field] = None if value == 0 else {
            "$type": STATUS_OPERATOR,
            "Option": 0 if value > 0 else 1,
            "Value": abs(value),
        }
    return want


def differs(item, field, value):
    if field in MODIFIERS.values():
        have = modifier_of(item, field)
        return have != (0 if value is None else (value["Value"] if value["Option"] == 0 else -value["Value"]))
    return item.get(field) != value
