#!/usr/bin/env python3
"""접미사 방패·장갑·팔찌·각반·신발·반지·귀걸이를 사용자가 모은 원작 표로 되살리거나 바로잡는다.

  python3 scripts/build-suffix-gear-from-sheet.py            # 무엇이 바뀌는지만 본다
  python3 scripts/build-suffix-gear-from-sheet.py --쓰기      # 아이템 템플릿에 적는다

**사용자 결정(2026-09-25)**: 어제 되살린 접미사 반지·귀걸이는 11~12레벨 한 층뿐이었다. 사용자가 원작에서
모은 표(`docs/items/어둠템#1~5.xlsx` → `data/game-data/items-original-sheets.json`, 5,722개, 생성기
`scripts/build-original-item-sheets.py`) 에는 방패·장갑·팔찌·각반·신발·반지·귀걸이가 **레벨별로** 있다.
이 표의 값(무게·판매가격·내구력·능력치·레벨제한)을 그대로 써서 살린다.

**접두사** — 방어 접미사: 로오·이아·메투스·세토아·세오·셔스·칸·풍요·축복·마력·체력. 공격 속성: 화염·바다·
바람·대지. **Sgrios(뮤레칸) 는 여전히 쓰지 않는다**(이름이 혼수 보스와 겹친다).

**장비만, 무기·소모품은 뺀다.** 표에는 뮤레칸의무기·축복의스크롤처럼 접미사가 붙었지만 장비가 아닌 것도
섞여 있다. 이름에서 접두사를 뗀 나머지가 아래 세 갈래 중 하나로 자리를 찾을 때만 옮긴다:

  1. **이미 있는 한글 이름** — 오늘·어제 만든 것 포함. 지우지 않고 표 값으로 **능력치만 고친다**(그림·
     자리·스크립트는 그대로 둔다) — 사용자 지시("겹치면 표 값으로 맞추되 지우지 말고 고치기만").
  2. **하데스표 영문 템플릿** — `data/pack-compare/한글이름-검토.tsv` 로 접두사+이름을 찾는다(어제 쓴
     7종 대신 15종 전부, 반지·귀걸이·목걸이·벨트·팔찌·장갑·각반·신발·방패 전부). 표에서 후보가 갈려
     `제안` 이 비어 있던 보석 반지 넷(사파이어·현철·녹옥·자수정)은 팩터가 그 갈림의 원인(어느 접두사가
     맞는 한글인지)일 뿐 영문 파일 자체는 접두사마다 있어 수동으로 더한다(`GEM_OVERRIDES`).
  3. **5.99 자체 기본템** — 철방패·은장갑·동각반처럼 접미사 없는 같은 이름이 이미 서버에 있으면 그
     자리·그림·스크립트를 그대로 쓴다(사용자 지시: "그림은 기본템의 서버 템플릿에서 가져와라").

  이 셋 어디에도 없으면(예: 나무방패 — Wooden Shield/Wooden Shield 2 둘 다 후보라 하나를 못 고른다,
  고무장갑·녹색신발·블루세피아반지 — 팩에도 표에도 그림 정본이 없다) **만들지 않고 건너뛴다** — 표에
  없는 그림 번호를 지어내지 않는다.

**옮기는 칸**: 무게→CarryWeight · 판매가격→Value · 내구력→MaxDurability · 레벨제한→LevelRequired ·
직업제한→Class · 방어력→AcModifer(부호를 뒤집는다 — 표는 음수, 하데스는 절대값에 Option 1) ·
명중수정→HitModifer · 공격수정→DmgModifer · 체력변화→HealthModifer · 마력변화→ManaModifer ·
힘/덱스/인트/위즈/콘변화→Str/Dex/Int/Wis/ConModifer. **표 값이 0인 칸은 뺀다**(다른 생성기와 같은 관례).
**옮기지 않는 칸**: 공격력·마법공격력(무기만 쓴다, 장비는 늘 0) · 수리여부·수리가격(장비 자리·그림과
같이 그대로 둔다).
"""

import argparse
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
ITEMS = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server/templates/items"
TSV = ROOT / "data/pack-compare/한글이름-검토.tsv"
SHEET = ROOT / "data/game-data/items-original-sheets.json"

PREFIX_EN = {
    "로오": "Luathas", "이아": "Glioca", "메투스": "Cail", "세토아": "Ceannlaidir",
    "세오": "Deoch", "셔스": "Fiosachd", "칸": "Gramail",
    "풍요": "Abundance", "축복": "Blessed", "마력": "Magic", "체력": "Might",
    "화염": "Fire", "바다": "Sea", "바람": "Wind", "대지": "Earth",
}

# TSV 가 후보를 못 고른(CONFLICT) 보석 반지 넷 — 영문 파일은 접두사마다 있어 우리가 직접 잇는다.
GEM_OVERRIDES = {"사파이어반지": "Lapis Ring", "현철반지": "Talos Ring",
                 "녹옥반지": "Jade Ring", "자수정반지": "Ruby Ring"}

SKIP_GRADES = {"CONFLICT", "NAME_CLASH"}

# 몸에 걸치는 자리만 — 무기(1)·갑옷(2)은 표에 섞여 있어도 뺀다("장비 접미사가 아닌 것은 빼라").
ALLOWED_SLOTS = {3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13}

# 표 칸 → ItemTemplate 칸 · Operator(0=Add·1=Remove) · 부호. 체력·마력·힘 같은 칸은 Add 로 더한다.
# 방어력만 다르다 — 하데스는 낮을수록 좋은 값이라 Remove(1) 로 "깎아서" 좋아지게 하고, 표의 음수를
# 절대값으로 뒤집는다(`buff_armachd.cs` — Remove 25 는 `BonusAc -= 25`, 방어력이 오른다는 뜻).
STAT_FIELDS = [
    ("체력변화", "HealthModifer", 0, 1), ("마력변화", "ManaModifer", 0, 1),
    ("힘변화", "StrModifer", 0, 1), ("덱스변화", "DexModifer", 0, 1), ("인트변화", "IntModifer", 0, 1),
    ("위즈변화", "WisModifer", 0, 1), ("콘변화", "ConModifer", 0, 1),
    ("명중수정", "HitModifer", 0, 1), ("공격수정", "DmgModifer", 0, 1),
    ("방어력", "AcModifer", 1, -1),
]

LENIENT = re.compile(r",(\s*[\]}])")


def read(path):
    return json.loads(LENIENT.sub(r"\1", path.read_text(encoding="utf-8-sig")))


def load_items():
    by_name, by_lower = {}, {}
    for path in ITEMS.rglob("*.json"):
        try:
            item = read(path)
        except json.JSONDecodeError:
            continue
        name = item.get("Name")
        if name:
            by_name[name] = (path, item)
            by_lower[name.lower()] = (path, item)
    return by_name, by_lower


def load_korean_to_english():
    mapping = {}
    lines = TSV.read_text(encoding="utf-8").splitlines()
    for line in lines[1:]:
        cols = line.split("\t")
        if len(cols) < 6:
            continue
        english, _part, _level, korean, _candidates, grade = cols[:6]
        prefix_en = english.split(" ", 1)[0]
        if prefix_en not in PREFIX_EN.values() or not korean or grade in SKIP_GRADES:
            continue
        mapping[korean] = english
    for prefix_ko, prefix_en in PREFIX_EN.items():
        for suffix_ko, suffix_en in GEM_OVERRIDES.items():
            mapping.setdefault(f"{prefix_ko}의{suffix_ko}", f"{prefix_en} {suffix_en}")
    return mapping


def category_of(base):
    for word, cat in (("방패", "방패"), ("장갑", "장갑"), ("팔찌", "팔찌"), ("각반", "각반"),
                      ("반지", "반지"), ("귀걸이", "귀걸이"), ("목걸이", "목걸이"), ("벨트", "벨트"),
                      ("신발", "신발"), ("부츠", "신발"), ("티", "신발"), ("투구", "투구")):
        if base.endswith(word):
            return cat
    return "장신구"


def image_fields(source_item):
    return {k: source_item[k] for k in ("Image", "DisplayImage", "EquipmentSlot", "ScriptName", "Flags")
            if k in source_item}


def to_int(text):
    try:
        return int(float(text))
    except (TypeError, ValueError):
        return 0


def apply_stats(item, row):
    for sheet_col, field, option, sign in STAT_FIELDS:
        value = to_int(row.get(sheet_col, "0")) * sign
        if value == 0:
            item.pop(field, None)
            continue
        item[field] = {"$type": "Darkages.Types.StatusOperator, Darkages.Server", "Option": option, "Value": value}

    item["CarryWeight"] = to_int(row.get("무게", "0"))
    item["Value"] = to_int(row.get("판매가격", "0"))
    item["MaxDurability"] = to_int(row.get("내구력", "0"))
    item["LevelRequired"] = to_int(row.get("레벨제한", "0"))
    item["Class"] = to_int(row.get("직업제한", "0"))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    by_name, by_lower = load_items()
    korean_to_english = load_korean_to_english()
    rows = json.loads(SHEET.read_text(encoding="utf-8"))["수치표"]

    merged, created, skipped, protected = [], [], [], []
    seen = set()

    for row in rows:
        name = row.get("이름", "")
        if not name or name.endswith("R") or name in seen:
            continue

        prefix_ko = next((p for p in PREFIX_EN if name.startswith(p + "의")), None)
        if prefix_ko is None:
            continue
        seen.add(name)
        base = name[len(prefix_ko) + 1:]

        if name in by_name:
            path, item = by_name[name]

            # 5.99 팩 자기 물건은 이름이 접두사 모양이어도 건드리지 않는다 — 로오의반지·칸의목걸이
            # (`db/item/Armor/공통반지.txt`·`공통목걸이.txt`)처럼 팩이 손수 값을 매긴 것이다. 표는
            # 접미사로 우리가 되살린 하데스표(`Group: "하데스표/..."`)만 고친다.
            if str(item.get("Group", "")).startswith("5.99표/"):
                protected.append(name)
                continue

            apply_stats(item, row)
            if writing:
                path.write_text(json.dumps(item, ensure_ascii=False, indent=2), encoding="utf-8")
            merged.append(name)
            continue

        english = korean_to_english.get(name)
        source = by_lower.get(english.lower()) if english else None

        if source is None:
            source = by_name.get(base)

        if source is None or (source[1].get("EquipmentSlot") not in ALLOWED_SLOTS):
            skipped.append(name)
            continue

        item = {
            "$type": "Darkages.Types.ItemTemplate, Darkages.Server",
            "Name": name,
            **image_fields(source[1]),
            "Class": 0,
            "Gender": 255,
            "LevelRequired": 0,
            "CanStack": False,
            "MaxStack": 0,
            "Value": 0,
            "DmgMin": 0,
            "DmgMax": 0,
            "MaxDurability": 0,
            "CarryWeight": 0,
        }
        apply_stats(item, row)
        item["Group"] = f"하데스표/{category_of(base)}/원작표"

        target = ITEMS / f"{name}.json"
        if writing:
            target.write_text(json.dumps(item, ensure_ascii=False, indent=2), encoding="utf-8")
        created.append(name)

    print(f"고침 {len(merged)}개 {'씀' if writing else '(미리 봄)'}")
    print(f"새로 살림 {len(created)}개 {'씀' if writing else '(미리 봄)'}")
    print(f"5.99 팩 자기 물건이라 안 건드림 {len(protected)}개: {', '.join(sorted(protected))}")
    print(f"그림 정본을 못 찾아 건너뜀 {len(skipped)}개: {', '.join(sorted(skipped))}")
    if not writing:
        print("\n미리 본 것입니다 — 적으려면 --쓰기")
    return 0


if __name__ == "__main__":
    sys.exit(main())
