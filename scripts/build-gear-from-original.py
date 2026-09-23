#!/usr/bin/env python3
"""5.99 팩에서 들여온 무기·갑옷의 값을 **원작 도감** 값으로 되돌린다.

  python3 scripts/build-gear-from-original.py            # 무엇이 바뀌는지만 본다
  python3 scripts/build-gear-from-original.py --쓰기      # 아이템 템플릿에 적는다

사용자 결정(2026-09-23): **아이템 값의 정본은 원작 도감이다.** 5.99 팩은 같은 이름의 물건에 값을 다시
매긴 갈래다 — 갑옷 값을 계단으로 뭉갰고(전부 300 / 3,000 / 7,000 …), 방어를 깎았고, 레벨 칸을 한 칸씩
올렸다(도감 1·11·41·71·99 → 팩 1·21·51·81·99). 도감에 값이 적힌 칸은 도감대로 되돌린다.

**왜 `build-pack-equipment.py` 안에 넣지 않았나.** 그것은 아이템 json 을 통째로 다시 쓴다
(`path.write_text(...)`). 그래서 다른 생성기가 나중에 얹은 칸을 말없이 지운다 — 지금 설단검·레더튜닉에
붙어 있는 `DropRate` 는 `scripts/build-novice-drops.py` 가 얹은 것이라 거기서 같이 돌리면 노비스 드롭
29마리분이 사라진다. 이 생성기는 `build-novice-drops.py` 와 같은 꼴이다: **json 을 읽고, 정한 칸만
고치고, 나머지는 읽은 그대로 다시 쓴다.**

**무엇을 고르나.** `Group` 이 `5.99표/무기/` 나 `5.99표/갑옷/` 로 시작하고 이름이 도감 수치표에 있는
169장(무기 66 · 갑옷 103). 팩이 새로 만든 것(도감에 이름이 없는 것)과 하데스표·장신구는 손대지 않는다
(장신구는 아래 `VALUE_ONLY` 에 이름을 적은 것만 판매가격을 따라간다).

**고치는 칸** (괄호는 도감 열 번호):
  판매가격(4) → Value · 무게(3) → CarryWeight · 내구력(9) → MaxDurability
  직업제한(20) → Class · 레벨제한(22) → LevelRequired · 공격력(25) → DmgMin/DmgMax
  방어력(10)·명중수정(11)·공격수정(12)·체력변화(13)·마력변화(14)·힘/덱스/인트/위즈/콘변화(15~19) → *Modifer

**안 고치는 칸** — 도감에 없으니 팩 값이 유일한 근거다:
  Image·DisplayImage(그림) · EquipmentSlot·ScriptName(장비 자리) · Gender·StageRequired
  AttackMotion·AttackSpeed(평타 몸 동작 — 시험이 값을 박아 쓴다) · Flags · MrModifer(마법방어)
  DropRate(`build-novice-drops.py` 것) · Group · CanStack·MaxStack
  도감 열26 **마법공격력**은 `ItemTemplate` 에 둘 칸이 없어 자료로만 남는다.

**도감 값이 0 인 칸은 칸을 뺀다.** 이 템플릿 꼴에서 없는 칸이 곧 0 이다. 그래야 팩이 자릿수를 늘려 둔
것이 풀린다 — 다크크로어 체력·마력 +100,000(도감 0·0) · 페이로브아머 마력 +50,000(도감 −200).

**되돌리지 않는 것: 지팡이 10종**(`KEEP` 참조). 도감의 해당 줄이 **글자 하나까지 똑같아서**
(공격 4m20 · 마법공격 6m30 · 레벨 11) 되돌리면 마법사·성직자 무기 사다리 다섯 칸이 레벨 11 한 칸으로
뭉개진다. 도감이 지팡이 등급을 나누기 전 시절의 표다. 아래에서 그 「똑같음」을 실제로 확인하고,
확인이 깨지면 멈춘다 — 이유가 주석이 아니라 코드에 있어야 한다.

**무기·갑옷 밖에서 판매가격만 따라가는 것**(`VALUE_ONLY` 참조). 도감에 이름이 있는 장신구도 값은
도감이 정본이다. 다만 **묶음째 되돌리지 않고 이름을 적은 것만 되돌린다** — 반지·귀걸이·목걸이·장갑·
각반·허리띠·신발·방패·투구·장식 416장 중 도감에 이름이 있는 것이 219장이고, 묶음째 되돌리면 그
219장의 값이 한꺼번에 움직인다(2026-09-23 세어 봄). 방금 문을 연 우드랜드 보석상 물목 22개 중
도감에 이름이 있는 21개가 **하나도 빠짐없이** 거기 들어 있어 상점 값이 통째로 흔들린다(로오의반지
500→200 · 가죽방패 3,000→750 …). 값 말고 다른 칸까지 되돌리면 더 크게 움직인다. 그래서
**필요한 한 장씩** 이름과 까닭을 적어 넣는다.
"""
import argparse
import json
import sys
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
SHEET = ROOT / "data" / "game-data" / "items-original-sheets.json"
ITEMS = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates" / "items"

configure_utf8_stdio(sys.stdout, sys.stderr)

#: 이 묶음만 건드린다.
GROUPS = ("5.99표/무기/", "5.99표/갑옷/")

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

#: 되돌리지 않는 물건 — 이름: 왜.
KEEP = {name: "도감의 지팡이 다섯 줄이 완전히 같아(4m20·레벨11) 되돌리면 무기 사다리가 한 칸으로 뭉개진다"
        for name in ("매직마르시아", "매직쥬피티아", "매직솔라", "매직루나", "매직파나",
                     "홀리머큐리아", "홀리쥬피티아", "홀리솔라", "홀리루나", "홀리파나")}

#: 위 「같음」을 실제로 확인할 칸과 값. 도감이 바뀌어 사다리가 생기면 여기서 멈춘다.
KEEP_PROOF = {"공격력": "4m20", "마법공격력": "6m30", "레벨제한": "11"}

#: 무기·갑옷 밖에서 **판매가격 한 칸만** 도감을 따르는 것 — 이름: 왜. 묶음(`GROUPS`)을 넓히지 않고
#: 여기 이름을 적는다(까닭은 위 설명 참조 — 장신구를 묶음째 되돌리면 219장이 함께 움직인다).
VALUE_ONLY = {
    "세줄금반지": "자이언트맨티스가 80% 로 떨구는 상인데 서버 값이 0 이라 팔아도 한 푼이 아니다",
    "바다의목걸이": "1~10레벨 장신구로 보석상에 들였다 — 값은 도감(10,000)을 따른다(팩 1,000)",
    "바람의목걸이": "1~10레벨 장신구로 보석상에 들였다 — 값은 도감(10,000)을 따른다(팩 1,000)",
    "화염의목걸이": "1~10레벨 장신구로 보석상에 들였다 — 값은 도감(10,000)을 따른다(팩 1,000)",
    "대지의목걸이": "1~10레벨 장신구로 보석상에 들였다 — 값은 도감(10,000)을 따른다(팩 1,000)",
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


def check_keep(rows):
    """지팡이를 두는 이유가 아직 참인지 확인한다. 아니면 멈춘다."""
    wrong = []
    for name in KEEP:
        row = rows.get(name)
        if row is None:
            wrong.append(f"{name}: 도감에 없다")
            continue
        for column, expected in KEEP_PROOF.items():
            if row.get(column) != expected:
                wrong.append(f"{name}: 도감 {column} 가 {row.get(column)} 다(같음을 보던 값 {expected})")
    if wrong:
        raise SystemExit(
            "지팡이를 두는 이유가 더는 맞지 않습니다 — 도감이 등급을 나누기 시작했다면 되돌려야 합니다:\n  "
            + "\n  ".join(wrong)
        )


def check_value_only(rows):
    """판매가격만 따라가는 것이 도감에 실제로 있는지 확인한다. 없으면 따라갈 근거가 없으니 멈춘다."""
    missing = [name for name in VALUE_ONLY if name not in rows]
    if missing:
        raise SystemExit("도감에 없는 이름이 `VALUE_ONLY` 에 적혀 있습니다: " + ", ".join(missing))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    sheet = json.loads(SHEET.read_text(encoding="utf-8"))
    rows = {}
    for entry in sheet["수치표"]:
        rows.setdefault(entry["이름"], entry)
    check_keep(rows)
    check_value_only(rows)

    mine, only, kept, untouched = [], [], [], 0
    for path in sorted(ITEMS.glob("*.json")):
        item = json.loads(path.read_text(encoding="utf-8-sig"))
        name = item.get("Name")
        if name in VALUE_ONLY:  # 묶음 밖 — 판매가격 한 칸만 따라간다
            only.append((path, item, rows[name]))
            continue
        if not str(item.get("Group") or "").startswith(GROUPS):
            continue
        if name not in rows:
            untouched += 1  # 팩이 새로 만든 것 — 도감에 견줄 줄이 없다
            continue
        if name in KEEP:
            kept.append(name)
            continue
        mine.append((path, item, rows[name]))

    jobs = [(path, item, wanted(row)) for path, item, row in mine]
    jobs += [(path, item, {"Value": max(0, number(row.get("판매가격")))}) for path, item, row in only]

    changed, fields, removed, lines = 0, 0, 0, []
    for path, item, want in jobs:
        moved = {field: value for field, value in want.items() if differs(item, field, value)}
        if not moved:
            continue
        changed += 1
        fields += len(moved)
        was = []
        for field, value in moved.items():
            if field in MODIFIERS.values():
                was.append(f"{field} {modifier_of(item, field)}→{0 if value is None else (value['Value'] if value['Option'] == 0 else -value['Value'])}")
            else:
                was.append(f"{field} {item.get(field)}→{value}")
            if value is None:
                item.pop(field, None)
                removed += 1
            else:
                item[field] = value
        lines.append(f"  {item['Name']}: " + " · ".join(was))
        if writing:
            path.write_text(json.dumps(item, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(f"도감에 이름이 있는 5.99 무기·갑옷 {len(mine) + len(kept)}장 + 판매가격만 따르는 {len(only)}장 중"
          f" {changed}장 · {fields}칸을 도감 값으로 되돌렸다"
          f" ({'적었다' if writing else '미리보기 — --쓰기 로 적는다'})")
    print(f"  칸을 뺀 것 {removed}개 — 도감이 0 인 수정치다(없는 칸이 0 이다)")
    print(f"  안 되돌린 것 {len(kept)}장(지팡이): {', '.join(sorted(kept))}")
    print(f"    까닭: {next(iter(KEEP.values()))}")
    print(f"  판매가격만 따라간 것 {len(only)}장(무기·갑옷 밖):")
    for _, item, _ in only:
        print(f"    {item['Name']} — {VALUE_ONLY[item['Name']]}")
    print(f"  손대지 않은 5.99 무기·갑옷 {untouched}장 — 도감에 이름이 없다(팩이 새로 만든 것)")
    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
