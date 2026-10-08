#!/usr/bin/env python3
"""99레벨 이전 사냥터의 드랍 종류를 늘린다 — 속성·접미사 장비와 포션만, 재료는 늘리지 않는다.

  python3 scripts/gen/items/build-drop-variety.py            # 무엇이 바뀌는지만 본다 (사냥터별 전후 표)
  python3 scripts/gen/items/build-drop-variety.py --쓰기      # 서버 정의에 적는다

**사용자 결정(2026-09-26)**
  1. 드랍 확률 전체 1.5배 — `Formulas/monsterexp.cs` `DropBoost` 가 한다(이 생성기는 건드리지 않는다).
  2. 99레벨 이전 사냥터의 드랍 **종류**를 늘린다. 재료(잡템·괴물 부산물)는 늘리지 않는다 — 나중에 사용자가
     필요한 것만 정한다. 늘리는 것은 속성·접미사 장비와 포션 같은 소모품.
  3. 기본 장비(속성·접미사 없는 것)는 드랍하지 않는다 — 상점에서 판다. (지금 드랍 목록에 기본 장비는 없다 —
     2026-09-25 `build-gear-drops.py` 가 뺐다. 남은 넷 — 실버·골드아쿠아링 · 세줄금반지 · 그림록퀸홀 — 은
     5.99 팩이 이름 있는 괴물에 손수 적은 전리품이라 그대로 둔다.)
  4. 한 마리가 여러 개를 떨구지는 않는다 — 서버 셈(DetermineRandomDrop)이 원래 하나만 고른다.

**셈** — 실제 확률 = `DropRate` × 1.5 ÷ 목록 칸수. 목록에 한 칸을 더하면 **기존 물건이 모두 옅어진다**
(칸수로 나누니까). 그래서 새 칸을 더한 괴물의 기존 물건마다 `DropRate` 를 (새 칸수 ÷ 옛 칸수) 만큼 올려
**옛 실제 확률을 지킨다**. `DropRate` 는 아이템 하나에 하나뿐이라(괴물마다가 아니다) 같은 물건을 여러
괴물이 떨구면 **가장 크게 옅어진 괴물의 배율**을 쓴다 — 다른 괴물에서는 조금 오른다(내려가지는 않는다).
오른 폭은 실행할 때 "영향" 줄로 모두 보인다.

**사냥터마다 무엇을 더하나** (레벨문은 `build-gear-drops.py` TIERS 와 같다. 장비는 **그 사냥터 입장
레벨에서 바로 입을 수 있는 것**, 이미 한글 이름이 있고 **아무도 안 떨구던 것**만 고른다):
  - 우드랜드2-1·3-1·4-1(입장 11·21) — 방어 접미사 **가죽장갑**(11레벨) 7종을 괴물마다 하나씩.
  - 포테의숲1~6존(21) — 공격 속성 **가죽벨트**(11레벨) 4종. 이미 나오는 4원소 룬스톤목걸이와 짝.
  - 우드랜드5-1·6-1(51) — 공격 속성 **크리스탈목걸이**(51레벨) 4종.
  - 우드랜드14-1(81) — 공격 속성 **흑요석목걸이**(81레벨) 4종.
  - 아벨해안(51, 이름 있는 크라켄·킹아크퍼스는 `build-gear-drops.py` FIELD_BOSSES 몫이라 빼고) —
    포션이 하나도 없던 곳이다. **상급체력·상급마력포션**을 모든 일반 괴물에, 잡템 칸이 있는(목록 2칸)
    괴물에는 방어 접미사 **동장갑**(41레벨) 둘을 더 얹는다. 목록이 1칸인 괴물은 +2(3칸), 2칸인 괴물은
    +4(6칸)라 **배율이 모두 3배로 같다** — 같은 은제방패를 1칸·2칸 괴물이 함께 떨궈도 어느 쪽도 오르지
    않는다. 상급 포션은 다른 사냥터가 쓰지 않아(중급은 포테와 같이 쓴다) 확률을 따로 정할 수 있다.
  - 노비스·우드랜드1 은 더하지 않는다 — "저레벨 괴물은 잡템만"(사용자 2026-09-23, `build-gear-drops.py`
    EARLY). 노비스에는 쿠룸·마라디움이 이미 있고, 우드랜드1 괴물은 5.99 에서도 아무것도 안 떨궜다.

**부위별로 레벨에 맞게(사용자 2026-10-04 「나오는 종류가 너무 적다 — 장비 부위별로 레벨에 맞게」)** — 위 목록의
장비 한 벌은 이제 손으로 적지 않고 `fill_gear` 가 고른다: 그 사냥터 입장 레벨 이하에서 **부위(EquipmentSlot)마다
가장 높은 층**의 접미사·속성 장비 전부(표 `docs/items/어둠템#1~5.xlsx` 로 되살린 것, 무기 제외). 입장 레벨보다
`TIER_REACH` 넘게 낮은 층은 그 부위째 뺀다. **장신구(귀걸이·목걸이·반지·벨트)는 거의 다 레벨 1·11 이라 레벨 대신
능력치 점수(`power`)로 등급을 매겨** 사냥터 층에 고르게 나눈다(사용자 2026-10-04, `accessory_tiers`). `build-gear-drops.py` 가 이미
까는 것(`BASE_RATE`)은 겹치지 않게 뺀다. 괴물 이름마다 돌려 가며 붙여 후보 전부가 그 사냥터에서 나온다.
괴물마다 옛 칸수 × (`RATIO`-1) 칸을 붙인다(모든 사냥터 같은 배율 — 잡템 DropRate 가 사냥터를 넘어 하나라서).
새 장비 합은 한 마리당 `NEW_GEAR_TOTAL` — 종류가 늘어도 장비가 더 자주 나오지는 않는다.

**새 장비의 확률(옛 규칙, 2026-09-26)** — 한 종의 실제 확률이 그 사냥터 기존 장비보다 높지 않고 2%(1.5배 전, 1.5배 후 3%)도
넘지 않게 `DropRate` 를 고른다: (그 무리 기존 장비의 가장 낮은 실제 확률, 2% 중 작은 것) × 가장 짧은 목록 칸수.

**다시 돌려도 같다** — 기존 물건의 `DropRate` 는 아래 `BASE_RATE`(이 생성기가 처음 돌기 전, 1.5배 전
값)에서 늘 새로 계산한다. 목록에서도 이 생성기가 더하는 이름을 먼저 빼고 옛 목록을 되살려 센다.
**`build-gear-drops.py` 를 다시 돌리면 장비 칸이 그 생성기의 한 벌로 되돌아간다** — 그 뒤에 이것을 다시
돌려라. 다른 생성기가 기존 물건의 기준값을 바꿨으면(`BASE_RATE` 와도 목표값과도 다르면) 경고를 낸다.
**이것을 적은 뒤에는 늘 `build-drop-cap.py --쓰기` 를 돌려라** — 한 괴물 합 80% 상한(2026-09-26)이 그 생성기에 있어,
여기서 적은 값 중 몇은 거기서 다시 내려간다(그래서 미리 보기에 그 물건들이 "경고" 로 뜬다).
"""

import argparse
import json
import math
import re
import sys
from collections import defaultdict

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT
from lib._drops import drops_of, woodland_west_north_layers
from lib._io import read_lenient_json as read
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MONSTERS = SERVER / "templates/monsters"

# `Formulas/monsterexp.cs` DropBoost — 표를 읽을 때만 곱한다(자료에는 곱하지 않는다).
DROP_BOOST = 1.5

# 새 장비 한 종의 실제 확률 윗선(1.5배 전). 사용자: "지금 수준(1.2~2%)을 넘지 않게".
GEAR_CAP = 0.02

# 새 장비 전부를 합한 한 마리당 실제 확률(1.5배 전 4% → 뒤 6%), 한 마리에 붙은 새 장비 칸 전부에 고르게 (2026-10-04).
NEW_GEAR_TOTAL = 0.04

# 입장 레벨보다 이만큼 넘게 낮은 층의 부위는 그 사냥터에 안 넣는다.
TIER_REACH = 30

# 접미사(방어)·속성(공격) 장비 — 이름 앞머리로 가린다.
SUFFIXED = tuple(f"{p}의" for p in ["로오", "이아", "메투스", "세토아", "세오", "셔스", "칸", "화염", "바다", "바람", "대지",
                                    # 축복·체력·풍요 장비도 드랍에(사용자 2026-10-04).
                                    "축복", "체력", "풍요"])


# 서버 레벨이 원작 표와 달랐던 5.99 팩 장비 84종(2026-10-04 조사) — 사용자 「표대로 하고 드랍도 시켜」. 레벨을 표대로
# 고친 뒤에도 드랍 후보로 남게 이름을 박아 둔다(고치고 나면 「다른 것」으로는 다시 못 찾는다). 접두·접미가 없어도
# 레벨이 맞는 사냥터에 들어간다(무기·기본템·흑요석 포함, 99레벨은 아직 맞는 사냥터가 없다).
TABLE_LEVELLED = {
    "강화된리젠트다이아귀걸이", "검정두건", "구리방패", "금각반", "금벨트", "금장갑", "기사단방패", "대왕관", "동각반", "동장갑", "레인헌트각반", "레인헌트투구", "로톤캐프린",
    "루돌프빨간코", "루딘의귀걸이", "리젠트다이아귀걸이", "매직루나", "매직마르시아", "매직솔라", "매직쥬피티아", "무당벌레장식", "문어군", "물안경", "브레스럭각반", "브릴윙각반",
    "브릴윙투구", "산소통", "산타모자", "산호귀걸이", "산호반지", "세일라링", "세피라링(Lev1)", "세피라링(Lev10)", "세피라링(Lev2)", "세피라링(Lev3)",
    "세피라링(Lev4)", "세피라링(Lev5)", "세피라링(Lev6)", "세피라링(Lev7)", "세피라링(Lev8)", "세피라링(Lev9)", "신발", "쌍금귀걸이", "쌍은귀걸이",
    "약과헤어핀", "은각반", "은장갑", "자수정반지", "철방패", "캐프린1", "캐프린2", "코뿔소악세", "크리스탈목걸이", "타고르캐프린", "파란두건", "파파야방패", "페이로브각반",
    "홀리루나", "홀리머큐리아", "홀리솔라", "홀리쥬피티아", "홍시모자", "홍옥반지", "화려한귀걸이", "횃불", "흑요석로그각반", "흑요석로그귀걸이", "흑요석로그반지", "흑요석로그장갑",
    "흑요석몽크각반", "흑요석몽크귀걸이", "흑요석몽크반지", "흑요석소서러각반", "흑요석소서러귀걸이", "흑요석소서러반지", "흑요석소서러장갑", "흑요석워리어각반", "흑요석워리어귀걸이",
    "흑요석워리어반지", "흑요석워리어장갑", "흑요석프리스트각반", "흑요석프리스트귀걸이", "흑요석프리스트반지", "흑요석프리스트장갑",
}

# 강화해서 얻는 장비 — 레벨은 표대로 고치지만 드랍에는 안 넣는다(사용자 2026-10-04 「세피라링·리젠트다이아귀걸이는
# 드랍템이 아니라 강화시키는 것」).
ENHANCED = ("세피라링", "리젠트다이아")

LOOT_RANDOM, LOOT_GOLD = 1 << 1, 1 << 5

# `build-gear-drops.py` FIELD_BOSSES 가 한 칸짜리 목록으로 관리한다 — 건드리지 않는다.
RESERVED_NAMES = {"크라켄1", "크라켄2", "킹아크퍼스1", "킹아크퍼스2"}

DEFENSE = ["로오", "이아", "메투스", "세토아", "세오", "셔스", "칸"]
ELEMENT = ["화염", "바다", "바람", "대지"]

ABEL = [20584, 20585, 20586, 20587, 20588, 20589, 20590, 20591, 20592, 20593, 20594]

# 칸: 이름, 맵들, 입장 레벨, 장비 한 벌(`fill_gear` 가 채운다), 모든 괴물에 더할 소모품 {이름: DropRate},
#     장비를 얹을 괴물의 옛 목록 최소 칸수.
GROUPS = [
    dict(name="우드랜드2-1·3-1·4-1", areas=[20022, 20023, 20024], entry=11,
         gear=[], potions={}, gear_min_slots=1),
    dict(name="포테의숲1~6존", areas=[20263, 20264, 20265, 20266, 20267, 20268], entry=21,
         gear=[], potions={}, gear_min_slots=1),
    dict(name="우드랜드5-1·6-1", areas=[20025, 20026], entry=51,
         gear=[], potions={}, gear_min_slots=1),
    dict(name="우드랜드14-1", areas=[20020], entry=81,
         gear=[], potions={}, gear_min_slots=1),
    # 상급 포션 확률: 3칸 괴물에서 체력 10%·마력 20%, 6칸 괴물에서 5%·10% (1.5배 전). 다른 사냥터의
    # 체력:마력 = 1:2 (`build-hunting-ground-rules.py` 마력 두 배)를 따른다.
    dict(name="아벨해안(일반 괴물)", areas=ABEL, entry=51,
         gear=[], potions={"상급체력포션": 0.3, "상급마력포션": 0.6},
         gear_min_slots=1),
]

# 서·북의우드랜드(2026-10-04) — 노바엔 레벨문이 없어 구역 깊이로 층을 밟는다(`lib/_drops.py` woodland_west_north_layers,
# build-gear-drops.py TIERS 와 같은 무리). 노바 괴물은 잡템·포션이 없어(옛 목록 = 한 벌 장비 한 칸) 칸수가 적다 — `thin`:
# 이 무리 때문에 RATIO 가 오르거나 장신구 등급 층이 늘면 모든 사냥터 목록이 바뀌므로, 둘 다 다른 무리가 정하고 여기엔
# 칸수 × (RATIO-1) 만큼만(돌림 차례대로) 싣는다. 장신구는 그 층 이하에서 가장 가까운 기존 사냥터 층의 것.
GROUPS += [dict(name=f"서·북의우드랜드 {layer}층", areas=areas, entry=layer, gear=[], potions={}, gear_min_slots=1, thin=True)
           for layer, areas in woodland_west_north_layers(SERVER / "areas").items()]

# 99레벨 사냥터(2026-10-05 검수 `plans/drop-audit-2026-10-05.md` — 사용자 「드랍 종류가 너무 적다」). 구광산은 포션 두 가지뿐,
# 드라큐라백작의성·지하수로D 와 신죽·카스마늄의 맨손 괴물은 아무것도 안 떨궜다(LootType Gold 만). 원작 표 99층 장비(기사단방패·
# 금장갑·금각반·매직부츠 …, 아무도 안 떨구던 것)와 81층 장신구를 맵마다 대략 `FRESH_MAP_GEAR` 종이 되게 괴물 이름마다 돌려 붙인다 — `fresh`:
# 옛 목록이 비었거나 포션뿐이라 위 배율(RATIO) 셈을 타지 않고 그냥 뒤에 붙인다. 포션은 `build-potion-by-level.py` 가 채운다.
# 5.99 증거 물건이 있거나 괴물에 자기 DropRate 를 적은 괴물(열쇠·가위·엑스쿠라눔·헬옷·그림록퀸홀)은 건드리지 않는다.
FRESH_MAP_GEAR = 14
FRESH_REGIONS = {"구광산": r"구광산\d+-\d+", "드라큐라백작의성": r"드라큐라백작의성.+", "지하수로D": r"지하수로D-\d+",
                 "신죽": r"신죽(마집안|음의마을)[\d-]+", "카스마늄": r"카스마늄제\d-\d갱도"}

# 이 생성기가 처음 돌기 전(2026-09-26, 1.5배 전)의 DropRate — 기존 물건은 늘 여기서 다시 계산한다.
# 장비 0.06 은 `build-gear-drops.py` GEAR_RATE, 포션은 `build-hunting-ground-rules.py`
# POTION_DROP_RATE·MANA_POTION_DROP_RATE, 잡템은 그 두 생성기가 적은 값이다.
BASE_RATE = {
    **{f"{p}의{kind}": 0.06 for p in DEFENSE for kind in ("동각반", "은각반", "은제방패")},
    **{f"{p}의호안석반지": 0.06 for p in ["이아", "메투스", "세토아", "세오", "셔스"]},
    "로오의반지": 0.06, "칸의목걸이": 0.06,
    **{f"{e}의룬스톤목걸이": 0.06 for e in ELEMENT},
    "하급체력포션": 0.6, "하급마력포션": 1.2, "중급체력포션": 0.6, "중급마력포션": 1.2,
    "엘란디스": 0.4, "이슬": 0.4, "아칸더스": 0.4,
    "그린팜팻의알": 0.2, "레드팜팻의알": 0.2, "옐로우팜팻의알": 0.2, "퍼플팜팻의알": 0.2,
    "놀의단검": 0.2, "엔트자이언트의날개": 0.2, "사슴의정수": 0.35, "실버팜팻의인장": 0.1,
    "엔트라이온의몸통": 0.15, "트랜트의뿌리": 0.15, "은빛늑대의갈기털": 0.15,
    "거북이등껍질": 0, "그래브의집게": 0, "바크의척추뼈": 0, "퐁퐁이의점액질": 0,
}

DROPS_TYPE = "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib"


def write(path, data, writing, newline):
    if writing:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + newline, encoding="utf-8")


def load_items():
    items = {}
    bad = False
    for path in ITEMS.rglob("*.json"):
        try:
            item = read(path)
        except json.JSONDecodeError:
            print(f"깨진 JSON 건너뜀: {path}", file=sys.stderr)
            bad = True
            continue
        if item.get("Name"):
            items[item["Name"]] = (path, item)
    if bad:
        sys.exit(1)
    return items


def load_area_names():
    names = {}
    bad = False
    for path in (SERVER / "areas").glob("*.json"):
        try:
            area = read(path)
        except json.JSONDecodeError:
            print(f"깨진 JSON 건너뜀: {path}", file=sys.stderr)
            bad = True
            continue
        names[area.get("ID") or area.get("Id")] = area.get("Name")
    if bad:
        sys.exit(1)
    return names


AREA_NAMES = load_area_names()

GROUPS += [dict(name=f"{region}(99)", areas=sorted(a for a, n in AREA_NAMES.items() if re.fullmatch(pattern, n or "")),
                entry=99, gear=[], potions={}, gear_min_slots=0, fresh=True)
           for region, pattern in FRESH_REGIONS.items()]


def load_monsters():
    monsters = []
    for path in sorted(MONSTERS.rglob("*.json")):
        try:
            monsters.append((path, read(path)))
        except json.JSONDecodeError:
            continue
    return monsters


# 2026-10-04 전에 손으로 적어 두었던 장비 — 다시 돌릴 때 옛 목록에서 걷어 내려고 남긴다.
LEGACY_GEAR = {*(f"{p}의가죽장갑" for p in DEFENSE), *(f"{e}의가죽벨트" for e in ELEMENT),
               *(f"{e}의크리스탈목걸이" for e in ELEMENT), *(f"{e}의흑요석목걸이" for e in ELEMENT),
               "로오의동장갑", "칸의동장갑"}


def sheet_rows():
    """원작 표(`docs/items/어둠템#1~5.xlsx`) 한 줄씩 — 레벨은 서버 값보다 이것을 믿는다(세일라링: 서버 11·99, 표 71)."""
    return {row["이름"]: row for row in read(ROOT / "data/game-data/items-original-sheets.json")["수치표"]}


def number(row, key):
    try:
        return int(row.get(key) or 0)
    except ValueError:
        return 0


# 장신구 갈래 — 귀걸이 5 · 목걸이 6 · 반지 7·8 · 벨트 11. 거의 다 레벨 1·11 이라 레벨로는 못 가른다.
ACCESSORY = {5: "귀걸이", 6: "목걸이", 7: "반지", 8: "반지", 11: "벨트"}


def power(row):
    """장신구의 능력치 점수(사용자 2026-10-04 「체력상승이나 포인트 상승 능력치로 등급을」) — 체력·마력 100 당 1,
    힘·덱스·인트·위즈·콘 1 당 1, 방어력(음수가 좋다) 1 당 1, 공격수정 1 당 1, 명중수정 10 당 1."""
    stats = sum(number(row, k) for k in ("힘변화", "덱스변화", "인트변화", "위즈변화", "콘변화"))
    return ((number(row, "체력변화") + number(row, "마력변화")) / 100 + stats + max(0, -number(row, "방어력"))
            + number(row, "공격수정") + number(row, "명중수정") / 10)


def accessory_tiers(items, rows):
    """장신구마다 나올 사냥터 입장 레벨 — 갈래마다 점수 차례로 사냥터 층(11·21·51·81) 수만큼 고르게 나눈다.
    원작 표 레벨이 더 높으면 그 레벨을 받는 층 아래로는 안 내린다. 표에 없는 것은 넣지 않는다(레벨 규칙을 따른다)."""
    entries = sorted({g["entry"] for g in GROUPS if not g.get("thin") and not g.get("fresh")})
    kinds = defaultdict(list)
    for name, (path, item) in items.items():
        slot = item.get("EquipmentSlot") or 0
        if (slot in ACCESSORY and (name.startswith(SUFFIXED) or name in TABLE_LEVELLED) and name in rows
                and not any(word in name for word in ENHANCED)):
            kinds[ACCESSORY[slot]].append((power(rows[name]), name))
    tier = {}
    for listed in kinds.values():
        listed.sort()
        for rank, (score, name) in enumerate(listed):
            by_power = rank * len(entries) // len(listed)
            level = number(rows[name], "레벨제한")
            by_level = next((i for i, e in enumerate(entries) if e >= level), len(entries) - 1)
            tier[name] = entries[max(by_power, by_level)]
    return tier


def align_levels(items, rows):
    """드랍에 쓰는 장비의 서버 레벨(LevelRequired)을 원작 표 레벨제한으로 맞춘다(사용자 2026-10-04 「레벨 제한 표대로」).
    고친 아이템 이름을 돌려준다 — 적는 것은 apply."""
    fixed = []
    for name, (path, item) in items.items():
        if (((item.get("EquipmentSlot") or 0) > 1 and name.startswith(SUFFIXED) or name in TABLE_LEVELLED)
                and name in rows and str(rows[name].get("레벨제한", "")).isdigit()
                and int(rows[name]["레벨제한"]) != (item.get("LevelRequired") or 0)):
            item["LevelRequired"] = int(rows[name]["레벨제한"])
            fixed.append(name)
    return fixed


LEVELLED = []


def part(slot, name):
    """한 벌을 고르는 칸 — 팔찌와 장갑은 같은 자리(9·10)에 끼지만 다른 물건이라 따로 고른다. 같은 칸으로 묶었더니
    접두 팔찌 40종이 모두 장갑에 밀려 빠졌다(사용자 2026-10-08 「동팔찌가 드랍이 안되나본데?」 → 「드랍에 넣고」)."""
    return slot, "팔찌" in name


def fill_gear(items, monsters):
    """사냥터마다 장비 한 벌(부위마다 입장 레벨 이하 가장 높은 층, 장신구는 점수 등급)과 같은 배율 RATIO 를 정한다."""
    rows = sheet_rows()
    LEVELLED[:] = align_levels(items, rows)
    tiers = accessory_tiers(items, rows)
    for group in GROUPS:
        best = {}
        for name, (path, item) in items.items():
            slot = item.get("EquipmentSlot") or 0
            if name in tiers:  # 장신구는 점수 등급으로 — 아래 레벨 규칙을 타지 않는다.
                continue
            level = number(rows[name], "레벨제한") if name in rows else item.get("LevelRequired") or 0
            # 서버가 막는 레벨(LevelRequired)도 입장 레벨 이하여야 주운 사람이 입는다.
            if (slot in (0, 1) or not name.startswith(SUFFIXED) or name in BASE_RATE or level > group["entry"]
                    or (item.get("LevelRequired") or 0) > group["entry"]):
                continue
            if level > best.get(part(slot, name), (-1, []))[0]:
                best[part(slot, name)] = (level, [])
            if level == best[part(slot, name)][0]:
                best[part(slot, name)][1].append(name)
        # 부위마다 앞머리(로오·화염 …) 하나에 한 종 — 11레벨 반지의 보석 갈래(루비·사파이어 …)까지 다 넣으면
        # 160종이 넘어 목록이 너무 길어진다.
        picked = {}
        for (slot, _), (level, names) in best.items():
            if level >= group["entry"] - TIER_REACH:
                for name in sorted(names):
                    picked.setdefault((*part(slot, name), name.split("의")[0]), name)
        own = max(e for e in tiers.values() if e <= group["entry"]) if group.get("thin") or group.get("fresh") else group["entry"]
        for name, entry in sorted(tiers.items()):
            item = items[name][1]
            if entry == own and name not in BASE_RATE and (item.get("LevelRequired") or 0) <= entry:
                picked.setdefault((*part(item["EquipmentSlot"], name), name.split("의")[0]), name)
        # 표대로 고친 84종(접두·접미 없는 것 포함)은 그 레벨이 이 사냥터 층 안이면 하나하나 넣는다.
        for name in sorted(TABLE_LEVELLED - set(tiers)):
            if name in items and name not in BASE_RATE and not any(word in name for word in ENHANCED):
                level = items[name][1].get("LevelRequired") or 0
                if group["entry"] - TIER_REACH <= level <= group["entry"]:
                    picked.setdefault((*part(items[name][1]["EquipmentSlot"], name), name), name)
        group["gear"] = sorted(picked.values())
        if group.get("thin"):  # 다 못 싣는 무리 — 그 층(가장 높은) 장비부터, 같은 층에서는 부위를 번갈아 돌린다
            # (이름순 그대로면 한 부위 열 종 — 팔찌를 넣으며 은팔찌 10종이 앞자리를 다 차지했다, 2026-10-08).
            turn, seen = {}, defaultdict(int)
            for name in group["gear"]:
                key = part(items[name][1]["EquipmentSlot"], name)
                turn[name] = seen[key]
                seen[key] += 1
            group["gear"].sort(key=lambda n: (-(items[n][1].get("LevelRequired") or 0), turn[n]))
        group["slots"] = sum(len([n for n in drops_of(m) if n in BASE_RATE]) for p, m in {
            m["Name"]: (p, m) for p, m in monsters
            if m.get("AreaID") in group["areas"] and m.get("Name") not in RESERVED_NAMES}.values())

    # 모든 사냥터에 같은 배율(새 칸수 ÷ 옛 칸수) — 잡템·포션의 DropRate 는 사냥터를 넘어 하나라서, 배율이
    # 다르면 한쪽 괴물의 합이 100% 를 넘는다. 가장 많이 필요한 사냥터에 맞춘다.
    global RATIO
    # 정수 배율 — 칸수를 반올림하면 괴물마다 배율이 조금씩 달라져 합이 넘는다.
    # `thin` 무리(옛 목록이 한 칸뿐인 괴물만 있는 곳)는 배율을 정하지 않는다 — 실을 수 있는 만큼만(돌림 차례대로) 싣는다.
    RATIO = 1 + math.ceil(max(len(g["gear"]) / max(1, g["slots"]) for g in GROUPS if not g.get("thin") and not g.get("fresh")))


RATIO = 1.0


ADDED = set()
TAIL = {}   # path -> `build-potion-by-level.py` 가 붙인 뒤쪽 포션 칸
FRESH = {}  # path -> (괴물, 새 목록, 무리) — 99레벨 사냥터(`fresh`)
FRESH_AT = 0


def plan_fresh(group, here, items, gear):
    """99레벨 사냥터 — 옛 목록이 비었거나 포션뿐인 괴물에 그 무리 장비를 이름마다 돌려 붙인다(맵마다 `FRESH_MAP_GEAR` 종 안팎)."""
    def potion(n):  # 엑스쿠라눔은 5.99 증거값(한 칸 목록) — 포션으로 치지 않아 그 괴물은 건드리지 않는다
        return "포션" in n or n in ("파프리카", "블루피치")
    open_ = [(p, m) for p, m in here
             if m.get("DropRate") is None and all(potion(n) or gear(n) for n in drops_of(m, items_only=True))]
    # 한 마리가 질 몫 = 맵 하나의 장비 종류 `FRESH_MAP_GEAR` ÷ 그 맵 괴물 이름 수(그 이름이 서는 맵 중 가장 적은 곳에 맞춘다).
    names_in = defaultdict(set)
    for p, m in open_:
        names_in[m["AreaID"]].add(m["Name"])
    each = defaultdict(int)
    for names in names_in.values():
        for name in names:
            each[name] = max(each[name], min(len(group["gear"]), math.ceil(FRESH_MAP_GEAR / len(names))))
    # 돌림 자리는 무리를 넘어 이어 간다 — 무리마다 0 에서 시작하면 차례 끝쪽(칸·풍요 …) 장비는 어디서도 안 나온다.
    global FRESH_AT
    carried = {}
    for name in sorted(each):
        carried[name] = [group["gear"][(FRESH_AT + k) % len(group["gear"])] for k in range(each[name])]
        FRESH_AT += each[name]
    for p, m in open_:
        listed = drops_of(m, items_only=True)
        # 장비는 제자리(마지막 장비 칸)에 바꿔 끼우고, 그 뒤에 `build-potion-by-level.py` 가 붙인 포션은 그대로 둔다 — 다시 돌려도 같게.
        cut = max((i + 1 for i, n in enumerate(listed) if gear(n)), default=len(listed))
        FRESH[p] = (m, [n for n in listed[:cut] if not gear(n)] + carried[m["Name"]] + listed[cut:], group)


def plan(monsters, items, said):
    """괴물 파일마다 (옛 목록, 새 목록) 과 아이템마다 새 DropRate 를 정한다."""
    fill_gear(items, monsters)
    ADDED.update(LEGACY_GEAR, (n for g in GROUPS for n in g["gear"]), (n for g in GROUPS for n in g["potions"]))
    # 지난번에 이 생성기가 붙였다가 이번 한 벌에서 빠진 것까지 걷어 낸다 — 기준값(BASE_RATE)이 아닌 접두·접미 장비.
    ADDED.update(n for n, (path, item) in items.items()
                 if (n.startswith(SUFFIXED) or n in TABLE_LEVELLED) and (item.get("EquipmentSlot") or 0) > 0
                 and n not in BASE_RATE)
    lists = {}  # path -> (monster, old, new, group)
    new_rates = {}

    def gear(n):
        return n in ADDED and (items[n][1].get("EquipmentSlot") or 0) > 0

    for group in GROUPS:
        for name in group["gear"] + list(group["potions"]):
            if name not in items:
                raise SystemExit(f"없는 아이템: {name}")
            item = items[name][1]
            if name in group["gear"]:
                level = item.get("LevelRequired") or 0
                if level > group["entry"] or not (item.get("EquipmentSlot") or 0):
                    raise SystemExit(f"{name}: 레벨 {level} — {group['name']} 입장 {group['entry']} 에 못 입는다")

        here = [(p, m) for p, m in monsters
                if m.get("AreaID") in group["areas"] and m.get("Name") not in RESERVED_NAMES]
        if group.get("fresh"):
            plan_fresh(group, here, items, gear)
            continue
        # `build-potion-by-level.py` 가 이 생성기 뒤에 붙인 포션 칸(마지막 장비 칸 뒤) — 이 생성기 몫이 아니라
        # 셈에서 빼고 그대로 뒤에 둔다(2026-10-05, 그것까지 옛 목록으로 세면 「BASE_RATE 에 없다」 며 멈췄다).
        old_of = {}
        for p, m in here:
            listed = drops_of(m)
            cut = max((i + 1 for i, n in enumerate(listed) if (items[n][1].get("EquipmentSlot") or 0) > 0),
                      default=len(listed))
            TAIL[p] = listed[cut:]
            old_of[p] = [n for n in listed[:cut] if n not in ADDED]

        # 장비를 얹을 괴물 이름 — 이름 차례대로 한 벌을 돌려 가며(`build-gear-drops.py` lay_gear 와 같은 꼴).
        # 옛 칸수에 비례해 붙인다 — 그래야 괴물마다 (새 칸수 ÷ 옛 칸수) 가 같아 같은 잡템을 함께 쓰는 괴물의
        # 합이 어긋나지 않는다(2026-10-04, 똑같이 붙였더니 합 110% 인 괴물이 생겼다).
        slots = {m["Name"]: len(old_of[p]) for p, m in here if len(old_of[p]) >= group["gear_min_slots"]}
        carried, at = {}, 0
        for name in sorted(slots):
            count = round(slots[name] * (RATIO - 1))
            carried[name] = [group["gear"][(at + k) % len(group["gear"])] for k in range(count)]
            at += count

        for p, m in here:
            if not (m.get("LootType") or 0) & LOOT_RANDOM:
                raise SystemExit(f"{m['Name']}({p.name}) 가 목록 갈래(Random)가 아니다 — LootType {m.get('LootType')}")
            old = old_of[p]
            new = old + list(group["potions"]) + carried.get(m["Name"], [])
            lists[p] = (m, old, new, group)

    # 기존 물건: 가장 크게 옅어진 괴물의 배율.
    factor = defaultdict(lambda: 1.0)
    for m, old, new, group in lists.values():
        for name in old:
            factor[name] = max(factor[name], len(new) / len(old))

    for name, f in factor.items():
        if name not in BASE_RATE:
            raise SystemExit(f"{name}: BASE_RATE 에 없다 — 기준값을 먼저 적어라")
        if BASE_RATE[name]:  # 0 인 잡템(아벨해안)은 몇 배를 해도 0 이다 — 적지 않는다.
            new_rates[name] = round(BASE_RATE[name] * f, 6)

    # 새 소모품: 정한 값 그대로.
    for group in GROUPS:
        new_rates.update(group["potions"])

    # 새 장비: 한 마리당 합이 NEW_GEAR_TOTAL 이 되게.
    for group in GROUPS:
        rows = [(m, old, new) for m, old, new, g in lists.values() if g is group]
        # 한 마리의 새 장비 합 = DropRate × (RATIO-1)/RATIO (1.5배 전) — 그것이 NEW_GEAR_TOTAL 이 되게.
        for name in group["gear"]:
            new_rates[name] = round(NEW_GEAR_TOTAL * RATIO / (RATIO - 1), 6)

    return lists, new_rates


def real(rate, slots):
    return rate * DROP_BOOST / slots if slots else 0.0


def report(monsters, items, lists, new_rates, said):
    # 괴물마다 확률 확인: 옛 물건이 떨어지지 않았나, 합이 100% 를 넘지 않나.
    for p, (m, old, new, group) in lists.items():
        for name in old:
            before = real(BASE_RATE[name], len(old))
            after = real(new_rates.get(name, 0), len(new))
            if after + 1e-9 < before:
                raise SystemExit(f"{m['Name']} {name}: {before:.2%} → {after:.2%} 로 떨어진다")
        total = sum(real(new_rates.get(n, items[n][1].get("DropRate") or 0), len(new)) for n in new)
        if total > 1 + 1e-9:
            raise SystemExit(f"{m['Name']}({p.name}): 합 {total:.0%} — 100% 를 넘으면 뒤 칸이 잘린다")

    # 사냥터별 전후 표 (1.5배 뒤 실제 확률).
    said.append("사냥터별 전후 (1.5배 뒤 실제 확률, 종류 = 그 맵 괴물들이 떨구는 서로 다른 물건 수)")
    said.append(f"{'맵':<14}{'종류 전':>6}{'종류 후':>6}   {'뭐라도(평균) 전':>14} → 후     더한 것")
    by_area = defaultdict(list)
    for p, (m, old, new, group) in lists.items():
        by_area[m["AreaID"]].append((m, old, new))
    for group in GROUPS:
        for area in group["areas"]:
            rows = by_area.get(area)
            if not rows:
                continue
            kinds_before = {n for m, old, new in rows for n in old}
            kinds_after = {n for m, old, new in rows for n in new}
            any_before = sum(sum(real(BASE_RATE[n], len(old)) for n in old) for m, old, new in rows) / len(rows)
            any_after = sum(sum(real(new_rates.get(n, 0), len(new)) for n in new) for m, old, new in rows) / len(rows)
            area_name = AREA_NAMES.get(area, str(area))
            said.append(f"{area_name:<14}{len(kinds_before):>6}{len(kinds_after):>6}   {any_before:>13.1%} → {any_after:.1%}"
                        f"   {' · '.join(sorted(kinds_after - kinds_before))}")

    said.append("\n99레벨 사냥터(fresh) — 맵마다 종류 전 → 후 (포션은 뒤에 build-potion-by-level 이 채운다)")
    fresh_area = defaultdict(lambda: [set(), set()])
    for p, (m, new, group) in FRESH.items():
        fresh_area[m["AreaID"]][0].update(drops_of(m, items_only=True))
        fresh_area[m["AreaID"]][1].update(new)
    for area, (before, after) in sorted(fresh_area.items()):
        said.append(f"  {AREA_NAMES.get(area, area):<16}{len(before):>4} → {len(after):<4}")
    for p, (m, new, group) in FRESH.items():
        total = sum(real(new_rates.get(n, items[n][1].get("DropRate") or 0), len(new)) for n in new)
        if total > 1 + 1e-9:
            raise SystemExit(f"{m['Name']}({p.name}): 합 {total:.0%} — 100% 를 넘으면 뒤 칸이 잘린다")

    said.append("\n괴물마다 새 목록 (1.5배 뒤 실제 확률)")
    seen = set()
    for p, (m, old, new, group) in sorted(lists.items(), key=lambda kv: (kv[1][0]["AreaID"], kv[1][0]["Name"])):
        key = (group["name"], m["Name"], tuple(new))
        if key in seen:
            continue
        seen.add(key)
        parts = [f"{n} {real(new_rates.get(n, 0), len(new)):.1%}" for n in new]
        said.append(f"  [{group['name']}] {m['Name']} ({len(old)}→{len(new)}칸): {' · '.join(parts)}")

    # 영향: 바꾼 DropRate 가 이 생성기 밖의 괴물(또는 배율이 더 작은 괴물)에서 오른 폭.
    said.append("\nDropRate 를 바꾼 물건과 영향 (다른 괴물에서 오른 것)")
    touched = {p for p in lists}
    for name in sorted(new_rates):
        was = items[name][1].get("DropRate") or 0
        base = BASE_RATE.get(name)
        if base is not None and abs(was - base) > 1e-9 and abs(was - new_rates[name]) > 1e-9:
            said.append(f"  경고: {name} 의 지금 DropRate {was} 가 기준 {base} 와도 목표 {new_rates[name]} 와도 다르다 —"
                        " 다른 생성기가 바꿨나? BASE_RATE 를 확인하라")
        rises = []
        for p, m in monsters:
            listed = drops_of(m)
            if name not in listed:
                continue
            if p in touched:
                mm, old, new, g = lists[p]
                if name not in old:
                    continue
                before, after = real(BASE_RATE[name], len(old)), real(new_rates[name], len(new))
            else:
                before, after = real(base if base is not None else was, len(listed)), real(new_rates[name], len(listed))
            if after > before + 1e-9:
                rises.append(f"{m['Name']}({m.get('AreaID')}) {before:.2%}→{after:.2%}")
        line = f"  {name}: {was} → {new_rates[name]}"
        if rises:
            uniq = sorted(set(rises))
            line += f"  · 오름 {len(uniq)}: {', '.join(uniq[:6])}{' …' if len(uniq) > 6 else ''}"
        said.append(line)


def apply(items, lists, new_rates, writing):
    changed_monsters = changed_items = 0
    for p, (m, old, new, group) in lists.items():
        if drops_of(m) != new + TAIL.get(p, []):
            m["Drops"] = {"$type": DROPS_TYPE, "$values": new + TAIL.get(p, [])}
            write(p, m, writing, "")
            changed_monsters += 1
    for p, (m, new, group) in FRESH.items():
        loot = ((m.get("LootType") or 0) & LOOT_GOLD) | LOOT_RANDOM
        if drops_of(m) != new or m.get("LootType") != loot:
            m["Drops"] = {"$type": DROPS_TYPE, "$values": new}
            m["LootType"] = loot
            write(p, m, writing, "\n" if p.read_text(encoding="utf-8-sig").endswith("\n") else "")
            changed_monsters += 1
    for name, rate in new_rates.items():
        path, item = items[name]
        if item.get("DropRate") != rate or name in LEVELLED:
            item["DropRate"] = rate
            write(path, item, writing, "\n")
            changed_items += 1

    for name in LEVELLED:
        if name not in new_rates:
            path, item = items[name]
            write(path, item, writing, "\n")
            changed_items += 1

    # 이 생성기가 예전에 붙였다가 이번 한 벌에서 빠진 장비 — 아무도 안 떨구면 DropRate 를 걷는다(GearDropTests).
    listed = {n for p, m in load_monsters() for n in drops_of(m)} if writing else set()
    listed |= {n for m, old, new, g in lists.values() for n in new} | {n for m, new, g in FRESH.values() for n in new}
    for name in sorted(ADDED - listed):
        path, item = items[name]
        if item.get("DropRate") is not None and (item.get("EquipmentSlot") or 0) > 0:
            del item["DropRate"]
            write(path, item, writing, "\n")
            changed_items += 1
    return changed_monsters, changed_items


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    said = []
    items = load_items()
    monsters = load_monsters()
    lists, new_rates = plan(monsters, items, said)
    report(monsters, items, lists, new_rates, said)
    changed_monsters, changed_items = apply(items, lists, new_rates, writing)

    said.append(f"\n레벨을 원작 표대로 고친 장비 {len(LEVELLED)}종: {' · '.join(sorted(LEVELLED))}")
    print("\n".join(said))
    print(f"\n괴물 정의 {changed_monsters}장 · 아이템 {changed_items}장이 바뀐다.")
    print("적었습니다." if writing else "미리 본 것입니다 — 적으려면 --쓰기")


if __name__ == "__main__":
    sys.exit(main())
