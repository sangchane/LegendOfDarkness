#!/usr/bin/env python3
"""장비가 어느 사냥터에서 떨어지는지를 한곳에서 정한다.

  python3 scripts/build-gear-drops.py            # 무엇이 바뀌는지만 본다
  python3 scripts/build-gear-drops.py --쓰기      # 서버 정의에 적는다

**정한 것**(사용자, 2026-09-23): 저레벨 괴물은 잡템만 떨군다. 그 위 괴물이 낮은 확률로 장비를 떨군다.

**왜 확률이 아니라 목록으로 가르나.** 떨어질 확률은 괴물이 아니라 **아이템 템플릿의 `DropRate`** 에
붙어 있다(`ItemTemplate.cs:93`). 그래서 같은 장비를 노비스 괴물과 우드랜드 괴물이 함께 떨구면 확률이
같이 움직여 "노비스만 빼기"를 확률로는 못 한다. 이 생성기는 **목록으로 가른다** — 노비스 괴물의
`Drops` 에서 장비 이름을 빼 버리면 `DropRate` 가 얼마든 안 떨어진다. 그리고 우리가 넣는 장비는
**모두 같은 확률(`GEAR_RATE`)** 을 쓰므로, 한 장비를 여러 괴물이 떨궈도 어긋날 것이 없다.
사냥터 차이는 "어느 등급의 장비가 목록에 올라 있나"로 낸다.

**실제로 떨어질 확률 = `DropRate` ÷ 목록 칸수.** 하데스는 `Drops` 에서 하나를 같은 확률로 고른 뒤 그
물건의 `DropRate` 를 굴린다(`Formulas/monsterexp.cs` DetermineRandomDrop). 목록이 잡템 1 + 장비 1 이면
6% ÷ 2 = **3%**, 잡템 1 + 장비 2 면 6% ÷ 3 = **2%** 다. 둘 다 5.99 팩이 장비에 쓰는 1~5% 안이다.

**LootType 을 Random 쪽으로 맞춘다.** 우드랜드 3~6·14 는 Table(4)을 쓰는데, Table 은 `DropRate` 를
가중치로 써서 하나를 고른 뒤 그 값으로 다시 굴린다(`LootDropper.Drop`). 거기에 장비를 얹으면 잡템
(엘란디스·이슬·아칸더스는 `DropRate` 가 아예 없어 가중치 0)이 굶어 죽고 확률도 셀 수 없다. 그래서
노비스·포테와 같은 Random(2) 으로 맞추고, 잡템에는 5.99 팩이 적은 확률을 넣어 준다.

**용의발톱은 어디에도 넣지 않는다** — 레벨제한 1 에 피해 180~200, 값 0 이다(노바 팩에서는 레벨제한 99).

**근거.** 원작 아카이브·도감에는 드롭표가 없다(`docs/where-the-answers-are.md`). 5.99 팩이 장비를
떨구는 괴물은 99레벨 보스 쪽뿐이고 우드랜드·포테의숲 괴물에는 장비를 한 줄도 안 적었다. 그래서
**어느 장비를 어느 사냥터에 두는지는 우리가 정했다** — 5.99 장비표에서 그 사냥터의 워프 레벨문
(`warp/*.txt` 의 끝 두 칸)에 맞는 등급을 골랐다. 확률 6%(실제 2~3%)도 우리가 정했다.

**던전 보스는 다르다 — 팩에 적힌 것을 그대로 쓴다**(`BOSSES`). 사냥터 한 벌과 달리 보스의 드롭은
5.99 팩이 손수 적어 두었고, 우리가 고를 자리가 없다. 보스는 맵에 서 있지 않고 개인 던전 스크립트가
불러 세우므로(`AreaID 0`) 위의 사냥터 목록으로는 걸리지 않는다.
"""

import argparse
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
ITEMS = SERVER / "templates/items"
MONSTERS = SERVER / "templates/monsters"

# 장비가 나올 확률. 목록 칸수로 나눠진다 — 2칸이면 3%, 3칸이면 2%.
GEAR_RATE = 0.06

# 잡템만 나오는 곳. 노비스 동선과 우드랜드 첫 구역들 — 워프 레벨문이 1~22 이거나 아예 없다.
EARLY = {
    20373: "노비스마을",
    20393: "노비스평원A",
    20394: "노비스평원B",
    **{20380 + n: f"노비스지하던전{'ABC'[n // 3]}{n % 3 + 1}" for n in range(9)},
    20015: "우드랜드1-1",
    20016: "우드랜드1-2",
    20017: "우드랜드1-3",
}

# 장비가 나오는 곳. 칸: (사냥터 이름, 워프 레벨문, 장비 한 벌).
# 한 벌은 괴물 이름 차례대로 하나씩 돌려 가며 붙는다(이름이 같은 괴물은 존이 달라도 같은 것을 떨군다).
#
# **사용자 결정(2026-09-25)**: 기본템(달마티카·단도복 같은 접미사 없는 것)은 상점에서만 산다 — 사냥터
# 드랍에서는 빼고, 그 자리에 **레벨이 맞는 접미사·속성 장비**를 같은 확률(옛 기본템 자리 그대로,
# `GEAR_RATE` ÷ 목록 칸수)로 채운다. 무기까지 포함해 한 벌 전부를 접미사·속성 한 벌로 바꾼다 — 무기
# 쪽에는 아직 살릴 접미사 자료가 없다.
#
# **접미사(사람이름) 장비가 11~12레벨 한 층뿐이던 문제(2026-09-25 첫 삽질)를 사용자가 모은 원작 표로
# 풀었다** — `data/game-data/items-original-sheets.json`(`docs/items/어둠템#1~5.xlsx`, 5,722개) 에는
# 로오·이아·메투스·세토아·세오·셔스·칸(방어) · 화염·바다·바람·대지(공격) 접미사가 붙은 반지·귀걸이·
# 방패·장갑·팔찌·각반이 **11·26·41·56·71·86·99레벨**로 층져 있다(`scripts/build-suffix-gear-from-sheet.py`,
# 449종). 사냥터마다 그 사냥터의 **워프 레벨문**(`Npc_Warp.txt`/`warps.json` — 우드2-1=11 · 3-4=21(반지
# 자체는 11) · 5-6=51(41 층이 가장 가깝다) · 14=81(71 층이 정확히 맞는다) · 아벨해안 일반 몹=51(56 층이
# 가장 가깝다))에 맞춰, 방어 접미사와 공격 속성을 사냥터별로 나눠 썼다 — 사용자 지시대로 "둘 다 사냥터
# 에서 떨어지되" 같은 사냥터에 둘 다 몰지 않고 사냥터마다 하나씩 나눠 겹치지 않게 했다.
TIERS = [
    (
        [20022, 20023, 20024],
        "우드랜드2-1·3-1·4-1",
        "11~99",
        # 11레벨 접미사 반지·목걸이 한 벌 — 로오·칸은 5.99 팩 자기 것(표 값으로 맞춰 1레벨), 나머지
        # 다섯은 표로 되살린 것(11레벨).
        ["로오의반지", "이아의호안석반지", "메투스의호안석반지", "세토아의호안석반지", "세오의호안석반지",
         "셔스의호안석반지", "칸의목걸이"],
    ),
    (
        [20263, 20264, 20265, 20266, 20267, 20268],
        "포테의숲1~6존",
        "21~52",
        # 4원소 룬스톤목걸이(11레벨) — 21레벨에 맞는 층이 없어 바로 아래 층을 쓴다. 우드2-4 와 다른
        # 갈래(공격 속성)를 써서 같은 대 사냥터끼리 겹치지 않는다.
        ["화염의룬스톤목걸이", "바다의룬스톤목걸이", "바람의룬스톤목걸이", "대지의룬스톤목걸이"],
    ),
    (
        [20025, 20026],
        "우드랜드5-1·6-1",
        "51~99",
        # 표로 되살린 방어 접미사 동각반(41레벨) — 41레벨 그대로 맞는다.
        ["로오의동각반", "이아의동각반", "메투스의동각반", "세토아의동각반", "세오의동각반",
         "셔스의동각반", "칸의동각반"],
    ),
    (
        [20020],
        "우드랜드14-1",
        "81~99",
        # 표로 되살린 방어 접미사 은각반(71레벨) — 그대로 들어맞는다.
        ["로오의은각반", "이아의은각반", "메투스의은각반", "세토아의은각반", "세오의은각반",
         "셔스의은각반", "칸의은각반"],
    ),
    (
        [20584, 20585, 20586, 20587, 20588, 20589, 20590, 20591, 20592, 20593, 20594],
        "아벨해안(일반 몹)",
        "51~80",
        # 표로 되살린 방어 접미사 은제방패(56레벨) — 51레벨에 맞는 층이 없어 바로 위 층을 쓴다. 이 맵
        # 들에는 크라켄·킹아크퍼스도 서 있지만 그 둘은 `FIELD_BOSSES` 가 따로 관리한다
        # (`RESERVED_NAMES` — 이 벌이 그 둘의 이름은 절대 건드리지 않는다).
        ["로오의은제방패", "이아의은제방패", "메투스의은제방패", "세토아의은제방패", "세오의은제방패",
         "셔스의은제방패", "칸의은제방패"],
    ),
]

# `FIELD_BOSSES` 가 이미 관리하는 이름 — 아벨해안 맵을 함께 쓰는 일반 몹 한 벌이 이 이름은 절대
# 건드리지 않는다(겹쳐 서 있어도 `lay_gear` 가 이 이름을 만나면 건너뛴다).
RESERVED_NAMES = {"크라켄1", "크라켄2", "킹아크퍼스1", "킹아크퍼스2"}

# 개인 던전 보스. 칸: (괴물, 떨구는 것, 확률, 레벨제한, 어디 보스인가).
#
# **자이언트맨티스 — 5.99 팩이 `드롭아이템 80 세줄금반지` 라고 적었다**
# (`data/server-packs/extracted/5.99-server/mobs.json`). 이 반지를 떨구는 괴물은 팩에도 서버에도 이
# 한 마리뿐이다. Novaonline 은 같은 괴물에 100% 로 적었고 혼든에는 이 반지가 없다. **80% 를 쓴다** —
# 사용자 기억과 같고, 팩 둘 중 낮은 쪽이며, 하나뿐인 목록이라 실제 확률이 그대로 80% 다.
#
# 서 있는 자리: `scripts/Pack599/Npcs/포테의숲오솔길입장.cs` 가 포테의숲5존에서 열어 주는 **개인 던전**
# 「포테의숲오솔길보스존」이다(`mob_spawn3 자이언트맨티스 … 1마리`). 입장은 **52레벨 미만**이고 안쪽
# 워프 레벨문이 21~52 다. 그래서 **초반 동선(노비스 1~20)에는 걸리지 않는다** — 80% 여도 1레벨이
# 만날 수 없다. 체력 19,500 · 피해 300~350 인 한 판에 한 마리짜리 보스다.
#
# 레벨제한 11: **원작 도감이 정본**이다(`판매가격 500000 · 레벨제한 11`). Novaonline 도 11 이고 사용자
# 기억도 11 인데, 서버에 들어온 5.99 값만 30 이다. 도감으로 맞춘다. 다른 칸(능력치)은 건드리지 않는다.
# **판매가격 50만은 `scripts/build-gear-from-original.py` 가 적는다**(그 파일 `VALUE_ONLY`) — 도감 값을
# 맞추는 일은 거기 한곳에 둔다. 장신구를 묶음째 되돌리지 않는 까닭도 거기 적혀 있다.
BOSSES = [
    ("자이언트맨티스", "세줄금반지", 0.80, 11, "포테의숲오솔길보스존(개인 던전 · 입장 52레벨 미만)"),
]

# 맵에 그냥 서 있는 이름 있는 몬스터(개인 던전 보스가 아니다). 칸: (괴물, 떨구는 것, 확률, 어디인가).
#
# **크라켄1·2 → 실버아쿠아링, 킹아크퍼스2 → 골드아쿠아링** — 5.99 팩 `db/mob/Abel/Abel_Monster.txt`:
# 크라켄1 `드롭아이템 3 실버아쿠아링`(49줄) · 크라켄2 `드롭아이템 5 실버아쿠아링`(97줄) · 킹아크퍼스2
# `드롭아이템 40 골드아쿠아링`(259줄). Novaonline `db/mob/아벨해안/아벨해안.txt` 도 크라켄(1) 3% ·
# 킹아크퍼스2 40% 로 같다 — 크라켄2 의 5% 만 5.99 에만 있다. 아이템의 `DropRate` 는 하나뿐이라 크라켄
# 1·2 를 가르지 못해 두 팩이 겹치는 3% 를 쓴다.
#
# **그림록퀸 → 그림록퀸홀** — 5.99 `db/mob/Casmanum/Casmanum_Monster.txt` `드롭아이템 1 그림록퀸홀`
# (1%). Novaonline `mine.txt` 는 15%, 혼든은 그림록퀸1-3 5% · 그림록퀸2-3 3% 로 셋이 갈린다 — 이 서버의
# 괴물 정의가 온 5.99 값을 쓴다(카스마늄 갱도는 아직 손대지 않은 지역이라 다른 근거가 없다).
#
# 왜 죽어 있었나: 넷 다 `LootType` 이 Table(4) 이었다. Table 은 `DropRate` 를 가중치로 써서 고르는데
# 목록이 한 칸뿐이고 `DropRate` 가 0(기본값)이면 사실상 절대 안 뽑힌다. `BOSSES` 와 같은 자리(Random)로
# 옮기면 `DropRate` 가 그대로 실제 확률이 된다.
FIELD_BOSSES = [
    ("크라켄1", "실버아쿠아링", 0.03, "아벨해안1(51레벨)"),
    ("크라켄2", "실버아쿠아링", 0.03, "아벨해안2(51레벨)"),
    ("킹아크퍼스2", "골드아쿠아링", 0.40, "아벨해안4(81레벨)"),
    ("그림록퀸", "그림록퀸홀", 0.01, "카스마늄제1-3갱도(99레벨)"),
]

# Random 쪽으로 옮기면 잡템도 제 확률이 있어야 한다. 5.99 팩 `드롭아이템` 의 확률 그대로다.
JUNK_RATE = {"엘란디스": 0.40, "이슬": 0.40, "아칸더스": 0.40}

LOOT_RANDOM, LOOT_TABLE, LOOT_GOLD = 1 << 1, 1 << 2, 1 << 5

DROPS_TYPE = "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib"

LENIENT = re.compile(r",(\s*[\]}])")


def read(path):
    """꼬리 쉼표가 남은 정의가 있다(하데스가 제 손으로 쓴 것). 너그럽게 읽는다."""
    return json.loads(LENIENT.sub(r"\1", path.read_text(encoding="utf-8-sig")))


def write(path, data, writing, newline):
    if writing:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + newline, encoding="utf-8")


def drops_of(monster):
    listed = monster.get("Drops")
    values = listed.get("$values") if isinstance(listed, dict) else listed
    return [name for name in (values or []) if isinstance(name, str)]


def set_drops(monster, names):
    monster["Drops"] = {"$type": DROPS_TYPE, "$values": names}


def load_items():
    items = {}

    for path in ITEMS.rglob("*.json"):
        try:
            item = read(path)
        except json.JSONDecodeError:
            continue

        if item.get("Name"):
            items[item["Name"]] = path

    return items


def is_gear(items, name):
    path = items.get(name)

    return path is not None and (read(path).get("EquipmentSlot") or 0) > 0


def rate_item(name, rate, items, writing, said):
    path = items.get(name)

    if path is None:
        said.append(f"  없는 아이템: {name}")
        return False

    item = read(path)

    if item.get("DropRate") != rate:
        item["DropRate"] = rate
        write(path, item, writing, "\n")

    return True


def load_monsters():
    monsters = []

    for path in sorted(MONSTERS.rglob("*.json")):
        try:
            monsters.append((path, read(path)))
        except json.JSONDecodeError:
            continue

    return monsters


def cut_dead_names(monsters, items, writing, said):
    """이름만 있고 아이템이 없는 드롭 줄은 영영 안 떨어진다. 그 줄만 지운다(파일 모양은 그대로 둔다)."""
    cut = 0

    for path, monster in monsters:
        # "random" 은 아이템 이름이 아니라 하데스가 알아듣는 낱말이다.
        dead = [name for name in drops_of(monster) if name != "random" and name not in items]

        if not dead:
            continue

        text = path.read_text(encoding="utf-8-sig")

        for name in dead:
            text = re.sub(rf'^\s*"{re.escape(name)}",?\s*\n', "", text, flags=re.MULTILINE)
            said.append(f"  {monster.get('Name')}({path.name}) 의 드롭에서 «{name}» 을 뺐다 — 그 이름의 아이템이 없다")
            cut += 1

        if writing:
            path.write_text(text, encoding="utf-8")

    said.append(f"끊긴 드롭 이름 {cut} 줄을 뺐다")


def strip_early(monsters, items, writing, said):
    taken = 0
    lines = []

    for path, monster in monsters:
        area = monster.get("AreaID")

        if area not in EARLY:
            continue

        listed = drops_of(monster)
        kept = [name for name in listed if not is_gear(items, name)]

        if kept == listed:
            continue

        gone = [name for name in listed if name not in kept]
        set_drops(monster, kept)
        write(path, monster, writing, "")
        taken += len(gone)
        lines.append(f"  {EARLY[area]} {monster['Name']}: {' · '.join(gone)} 을 뺐다 → 남은 것 {' · '.join(kept) or '없음'}")

    said.append(f"초반 사냥터 괴물의 장비 드롭 {taken} 줄을 뺐다 (잡템·시약은 그대로)")
    said.extend(lines)


def lay_gear(monsters, items, writing, said):
    for areas, ground, band, suit in TIERS:
        here = [(path, monster) for path, monster in monsters
                if monster.get("AreaID") in areas and monster.get("Name") not in RESERVED_NAMES]
        names = sorted({monster["Name"] for _, monster in here})

        if not names:
            said.append(f"{ground}: 괴물을 못 찾았다")
            continue

        # 괴물 이름 차례대로 한 벌을 돌려 가며 하나씩. 한 벌이 괴물보다 많으면 앞 괴물이 남은 것을 더 진다.
        carried = {name: [suit[index % len(suit)]] for index, name in enumerate(names)}

        for index in range(len(names), len(suit)):
            carried[names[index % len(names)]].append(suit[index])

        for name in suit:
            rate_item(name, GEAR_RATE, items, writing, said)

        lines = []

        for path, monster in here:
            listed = drops_of(monster)
            junk = [item for item in listed if not is_gear(items, item)]
            wanted = junk + carried.get(monster["Name"], [])

            for name in junk:
                if name in JUNK_RATE:
                    rate_item(name, JUNK_RATE[name], items, writing, said)

            loot = monster.get("LootType") or 0
            loot = (loot & LOOT_GOLD) | LOOT_RANDOM if loot & (LOOT_TABLE | LOOT_RANDOM) else loot | LOOT_RANDOM

            if drops_of(monster) != wanted or monster.get("LootType") != loot:
                set_drops(monster, wanted)
                monster["LootType"] = loot
                write(path, monster, writing, "")

            worn = carried.get(monster["Name"], [])
            lines.append(
                f"  {monster['Name']}: {' · '.join(wanted)}"
                f"  ({' · '.join(f'{piece} {100 * GEAR_RATE / len(wanted):.0f}%' for piece in worn)})"
            )

        said.append(f"{ground} (워프 {band}레벨) — 괴물 {len(names)}종에 {' · '.join(suit)}")
        said.extend(sorted(set(lines)))


def lay_boss(monsters, items, writing, said):
    """개인 던전 보스에 팩이 적은 드롭을 건다. 목록이 한 칸이라 확률이 그대로 나온다."""
    for beast, prize, rate, level, where in BOSSES:
        here = [(path, monster) for path, monster in monsters if monster.get("Name") == beast]

        if not here:
            said.append(f"{beast}: 괴물 정의를 못 찾았다")
            continue

        if not rate_item(prize, rate, items, writing, said):
            continue

        # 레벨제한만 도감으로 맞춘다 — 위 `BOSSES` 주석의 근거다.
        item = read(items[prize])

        if item.get("LevelRequired") != level:
            said.append(f"  {prize} 레벨제한 {item.get('LevelRequired')} → {level} (원작 도감)")
            item["LevelRequired"] = level
            write(items[prize], item, writing, "\n")

        for path, monster in here:
            loot = monster.get("LootType") or 0
            # 목록에서 하나를 고르는 갈래여야 `DropRate` 가 그대로 확률이 된다(Table 은 가중치로 쓴다).
            loot = (loot & LOOT_GOLD) | LOOT_RANDOM

            if drops_of(monster) != [prize] or monster.get("LootType") != loot:
                set_drops(monster, [prize])
                monster["LootType"] = loot
                write(path, monster, writing, "")

        said.append(f"{beast} — {where}: {prize} {100 * rate:.0f}% (레벨제한 {level})")


def lay_field_boss(monsters, items, writing, said):
    """맵에 서 있는 이름 있는 몬스터에 팩이 적은 드롭을 건다. 목록이 한 칸이라 확률이 그대로 나온다.

    `lay_boss` 와 같은 모양이지만 레벨제한은 건드리지 않는다 — 넷 다 이미 원작 도감과 같다
    (실버아쿠아링 51 · 골드아쿠아링 81 · 그림록퀸홀 99, `FIELD_BOSSES` 주석 참고).
    """
    for beast, prize, rate, where in FIELD_BOSSES:
        here = [(path, monster) for path, monster in monsters if monster.get("Name") == beast]

        if not here:
            said.append(f"{beast}: 괴물 정의를 못 찾았다")
            continue

        if not rate_item(prize, rate, items, writing, said):
            continue

        for path, monster in here:
            loot = monster.get("LootType") or 0
            # 목록에서 하나를 고르는 갈래여야 `DropRate` 가 그대로 확률이 된다(Table 은 가중치로 쓴다).
            loot = (loot & LOOT_GOLD) | LOOT_RANDOM

            if drops_of(monster) != [prize] or monster.get("LootType") != loot:
                set_drops(monster, [prize])
                monster["LootType"] = loot
                write(path, monster, writing, "")

        said.append(f"{beast} — {where}: {prize} {100 * rate:.0f}%")


def cut_stale_rates(monsters, items, writing, said):
    """아무도 안 떨구는데 `DropRate` 가 남은 **우리 장비**에서 그 칸을 뺀다.

    노비스 괴물에 장비를 달았다가 뗀 자국이다(`build-novice-drops.py` 옛 판). 목록에 이름이 없으면
    확률이 얼마든 안 떨어지니 하는 일은 없지만, 「이건 왜 0.1 이지」로 읽힌다.
    **우리가 들여온 5.99 장비만 본다** — 하데스가 제 손으로 쓴 영문 아이템 930장에도 쓰지 않는
    `DropRate` 가 남아 있는데 그건 우리 것이 아니라 건드리지 않는다.
    """
    listed = {name for _, monster in monsters for name in drops_of(monster)}
    cut = []

    for name, path in sorted(items.items()):
        if name in listed:
            continue

        item = read(path)

        if not str(item.get("Group") or "").startswith("5.99표/") or (item.get("EquipmentSlot") or 0) <= 0:
            continue

        if "DropRate" not in item:
            continue

        cut.append(f"{name}({item['DropRate']})")
        item.pop("DropRate")
        write(path, item, writing, "\n")

    said.append(f"아무도 안 떨구는 5.99 장비 {len(cut)} 종에서 남은 DropRate 를 뺐다: {' · '.join(cut) or '없음'}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    said = []
    items = load_items()
    monsters = load_monsters()

    cut_dead_names(monsters, items, writing, said)
    strip_early(monsters, items, writing, said)
    lay_gear(monsters, items, writing, said)
    lay_boss(monsters, items, writing, said)
    lay_field_boss(monsters, items, writing, said)
    cut_stale_rates(monsters, items, writing, said)

    print("\n".join(said))
    print("\n" + ("적었습니다." if writing else "미리 본 것입니다 — 적으려면 --쓰기"))


if __name__ == "__main__":
    sys.exit(main())
