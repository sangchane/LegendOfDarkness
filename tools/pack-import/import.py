#!/usr/bin/env python3
"""서버팩 자료를 Hades 가 읽는 모양으로 옮긴다.

**갈래마다 손으로 하지 않기 위한 도구다.** 넣을 것이 아홉 갈래 3,400건이 넘는데,
갈래마다 따로 스크립트를 쓰면 아홉 번 같은 실수를 한다. 읽기·거르기·세기·보고는 공용이고,
갈래마다 다른 것은 **무엇을 거르나(RULES)** 와 **칸을 어떻게 옮기나(MAPPERS)** 둘뿐이다.

세 가지를 지킨다.

  **파일이 정답이다.** 팩의 `너비`·`높이` 가 맵 파일 크기와 어긋나는 선언이 12개 있다.
  선언을 믿고 넣으면 서버가 거르거나(길이 검사) 맵이 뒤엉킨다. 파일을 재서 가린다.

  **뜻이 확인된 칸만 옮긴다.** 모르는 칸은 옮기지 않고 보고서에 남긴다.
  근거 없이 옮기면 다음 사람이 그것을 근거로 삼는다.

  **쓰기 전에 센다.** `--dry-run` 이 기본이다. 넣을 수·못 넣는 수와 그 이유·이름 겹침을
  먼저 보고, 그 수가 계획의 실측 표와 같을 때만 `--write` 한다.

  쓰는 법:
    python3 tools/pack-import/import.py --kind maps                # 세어만 본다
    python3 tools/pack-import/import.py --kind maps --write        # 실제로 넣는다
    python3 tools/pack-import/import.py --kind all                 # 아홉 갈래를 다 센다
"""
import argparse, collections, json, re, shutil, sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
PACK = "5.99-server"
EXTRACTED = ROOT / "data" / "server-packs" / "extracted" / PACK
MAPSRC = ROOT / "data" / "map-source" / PACK
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
IDTABLE = ROOT / "plans" / "5.99-맵번호표.tsv"

BYTES_PER_TILE = 6          # 바닥 + 왼벽 + 오른벽, 각 ushort
FIRST_MAP_ID = 20_000        # 1~65535 만 쓸 수 있다 — 맵 번호는 전선에서 16비트다(0x15).
                             # 10만번대는 100287 이 34751 로 잘려 클라이언트가 다른 맵이라고 믿는다.

# 맵 파일 크기로 확인된 정정. 팩의 선언이 틀렸고 파일이 맞다.
#   흉가2층 은 두 파일 사이에 값이 뒤바뀌어 있었다 — lod10293 이 29x50 이다.
#   노바방어구점 은 144칸이라 12x12 말고 다른 답이 없다.
SIZE_FIXES = {
    ("흉가2층", "db/maps/서쪽대륙/maps/lod10293.map"): (29, 50),
    ("노바방어구점", "db/maps/default/maps/lod2650.map"): (12, 12),
}

# Hades 기존 맵 넷이 모두 이 값이다. 팩에 대응하는 칸을 아직 못 찾았으므로 같게 둔다.
# 눈 비트(128) 를 비롯해 뜻을 모르는 채 바꾸지 않는다.
DEFAULT_MAP_FLAGS = 106240

BANNED = re.compile(r'[:\\/*?"<>|]')


def load(kind):
    p = EXTRACTED / f"{kind}.json"
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else []


def safe_name(name):
    """파일 이름으로 쓸 수 있는 **서버 이름**을 만든다.

    파일 이름만 정화하면 소용이 없다 — 서버는 `Name.ToLower()` 로 파일을 쓰므로
    (AreaStorage.Save), Name 에 금지문자가 남아 있으면 기동할 때마다 **파일을 하나 더 만든다.**
    실제로 `뮤레칸의역습::밀레스` 가 그랬다. 이름 자체를 정화한다.
    """
    return BANNED.sub("_", name).strip()


# ──────────────────────────────────────────────────────────────────────────
# 갈래마다 다른 것 ①: 무엇을 거르나
# ──────────────────────────────────────────────────────────────────────────

def eligible_maps(_):
    """선언한 크기가 맵 파일과 맞는 것만. 어긋나면 서버가 거르거나 맵이 뒤엉킨다."""
    keep, drop = [], []
    for m in load("maps"):
        f = m["fields"]
        name, rel = m["이름"], f["맵파일"]
        w, h = SIZE_FIXES.get((name, rel), (int(f["너비"]), int(f["높이"])))
        path = MAPSRC / rel
        if not path.exists():
            drop.append((m, f"맵 파일이 없다: {rel}")); continue
        actual = path.stat().st_size
        if w * h * BYTES_PER_TILE != actual:
            drop.append((m, f"선언 {w}x{h}={w*h*BYTES_PER_TILE} ≠ 파일 {actual}")); continue
        m = dict(m); m["_크기"] = (w, h)
        keep.append(m)
    return keep, drop


def eligible_warps(_):
    """양 끝이 다 실재하는 맵이어야 번호로 바꿀 수 있다. 완전히 같은 줄은 하나만 남긴다."""
    names = {m["이름"] for m in load("maps")}
    keep, drop, seen = [], [], set()
    for x in load("warps"):
        miss = [k for k in ("출발맵", "도착맵") if x[k] not in names]
        if miss:
            drop.append((x, f"{'·'.join(miss)} 이 맵 목록에 없다: {x[miss[0]]}")); continue
        key = (x["출발맵"], tuple(x["출발"]), x["도착맵"], tuple(x["도착"]))
        if key in seen:
            drop.append((x, "같은 줄이 이미 있다")); continue
        seen.add(key); keep.append(x)
    return keep, drop


def eligible_monsters(_):
    """Hades 의 괴물 템플릿은 '종류'가 아니라 '배치'다 — (맵, 괴물) 쌍 하나가 한 장."""
    defined = {m["이름"] for m in load("mobs")}
    keep, drop, seen = [], [], set()
    for s in load("mob_spawns"):
        pair = (s["맵"], s["괴물"])
        if pair in seen:
            continue                                    # 같은 쌍이 여러 줄 — 한 장이면 된다
        seen.add(pair)
        if s["괴물"] not in defined:
            drop.append((s, f"괴물 정의가 없다: {s['괴물']}")); continue
        keep.append(s)
    return keep, drop


def eligible_mundanes(_):
    """정의가 있는 NPC 만. 나머지는 팩 스크립트가 만드는 것이라 여기서 놓을 수 없다."""
    defined = {n["이름"] for n in load("npcs")}
    keep, drop = [], []
    for s in load("npc_spawns"):
        (keep if s["NPC"] in defined else drop).append(
            s if s["NPC"] in defined else (s, f"NPC 정의가 없다(스크립트가 만든다): {s['NPC']}"))
    return keep, drop


def eligible_plain(kind):
    """거를 것이 없는 갈래 — 이름이 있으면 넣는다."""
    return [x for x in load(kind)], []


RULES = {
    "skills":    lambda _: [json.loads(ABILITIES.read_text(encoding="utf-8-sig"))] and (
                     [r for r in json.loads(ABILITIES.read_text(encoding="utf-8-sig"))
                      if r["kind"] == "skill"], []),
    "spells":    lambda _: ([r for r in json.loads(ABILITIES.read_text(encoding="utf-8-sig"))
                             if r["kind"] == "spell"], []),
    "worldmaps": lambda _: eligible_plain("worldmaps"),
    "maps":      eligible_maps,
    "warps":     eligible_warps,
    "monsters":  eligible_monsters,
    "mundanes":  eligible_mundanes,
    "items":     lambda _: eligible_plain("items"),
    "doors":     lambda _: eligible_plain("doors"),
    "shops":     lambda _: eligible_plain("shops"),
    "quests":    lambda _: (json.loads(QUESTS.read_text(encoding="utf-8-sig")), []),
}

# 계획의 실측 표. 여기서 벗어나면 표가 틀렸거나 적재기가 틀렸다 — 진행 전에 가린다.
# 기술·마법은 원작(abilities.json) 기준이다 — 팩의 82·71 이 아니다.
EXPECTED = {"quests": 38, "shops": 47, "maps": 797, "warps": 886, "items": 989, "monsters": 565,
            "mundanes": 84, "skills": 275, "spells": 338, "worldmaps": 1, "doors": 1}


# ──────────────────────────────────────────────────────────────────────────
# 맵 번호 — 신원은 번호가 아니라 파일 경로다
# ──────────────────────────────────────────────────────────────────────────

def map_ids(keep):
    """(출처, 이름, 맵파일) 마다 전역 번호를 하나씩. 한 번 적어 두면 그대로 다시 쓴다.

    팩 번호는 폴더 안에서만 세는 지역 번호라 807개가 정수 445개를 나눠 쓴다.
    Hades 는 번호를 전역 열쇠로 쓰므로(GlobalMapCache) 그대로 넣으면 맵이 사라진다.
    """
    table = {}
    if IDTABLE.exists():
        for line in IDTABLE.read_text(encoding="utf-8").splitlines():
            if not line or line.startswith("#"):
                continue
            c = line.split("\t")
            table[(c[0], c[1], c[3])] = int(c[2])

    nxt = max(table.values(), default=FIRST_MAP_ID - 1) + 1
    if nxt + len(keep) > 65535:
        raise SystemExit("맵 번호가 65535 를 넘는다 — 전선에서 잘린다")
    rows = []
    for m in sorted(keep, key=lambda x: (x["출처"], x["이름"], x["fields"]["맵파일"])):
        key = (m["출처"], m["이름"], m["fields"]["맵파일"])
        if key not in table:
            table[key] = nxt; nxt += 1
        rows.append((key, table[key], m))

    # 서버는 기동할 때마다 영역을 Name.ToLower() 로 **다시 쓴다**(AreaStorage.Save).
    # 그래서 파일 이름만 갈라 두면 소용이 없다 — 첫 기동에 둘이 한 파일로 덮인다.
    # **이름 자체**를 갈라야 한다. 이 이름들을 부르는 워프·젠은 0건이라 참조가 안 깨진다.
    dupes = {n for n, c in Counter(safe_name(m["이름"]).lower() for _, _, m in rows).items() if c > 1}
    for key, mid, m in rows:
        base = safe_name(m["이름"])
        m["_번호"] = mid
        m["_이름"] = f"{base}-{mid}" if base.lower() in dupes else base
        m["_파일이름"] = m["_이름"].lower()          # 서버가 쓰는 이름과 반드시 같아야 한다
    return rows


def write_id_table(rows):
    out = ["# 팩 맵 → Hades 전역 번호. 신원은 (출처, 이름, 맵파일) 이고 팩 번호는 쓰지 않는다.",
           "# 1~99999 는 Hades 것이라 100000 부터 매긴다.",
           "# 이름이 겹치는 맵은 서버 이름 자체를 번호로 갈랐다 — 서버가 Name 으로 파일을 쓰기 때문이다.",
           "# 칸: 출처 / 팩이름 / 새번호 / 맵파일 / 서버이름", ""]
    for (src, name, rel), mid, m in rows:
        out.append(f"{src}\t{name}\t{mid}\t{rel}\t{m['_이름']}")
    IDTABLE.write_text("\n".join(out) + "\n", encoding="utf-8")


# ──────────────────────────────────────────────────────────────────────────
# 갈래마다 다른 것 ②: 칸을 어떻게 옮기나
# ──────────────────────────────────────────────────────────────────────────

def area_json(m):
    """Hades 의 영역 파일. 뜻이 확인된 칸만 옮긴다 — 나머지는 기존 맵과 같게 둔다."""
    w, h = m["_크기"]
    return {
        "FilePath": f"../../database/server/maps/lod{m['_번호']}.map",
        "Cols": w,
        "Rows": h,
        "Id": m["_번호"],
        "ID": m["_번호"],
        "Name": m["_이름"],
        "Music": int(m["fields"].get("배경음") or 0),
        "Flags": DEFAULT_MAP_FLAGS,     # 팩에 대응하는 칸을 못 찾았다. 기존 맵과 같게 둔다
        "Blocks": [],
        "ScriptKey": None,
    }


# ── 아이템 ────────────────────────────────────────────────────────────────
# 뜻이 확인된 칸만 적는다. 확인 못 한 칸(타입.속성.떨굼여부.사운드.공격모션.
# 어빌제한.수리가격 …)은 옮기지 않고 보고서에만 남긴다 — 근거 없이 옮기면
# 다음 사람이 그것을 근거로 삼는다.
ITEM_STATS = {                      # 팩 칸 → Hades 의 StatusOperator 칸
    "방어력": "AcModifer", "체력변화": "HealthModifer", "마력변화": "ManaModifer",
    "힘변화": "StrModifer", "덱스변화": "DexModifer", "인트변화": "IntModifer",
    "위즈변화": "WisModifer", "콘변화": "ConModifer", "재생력": "RegenModifer",
    "명중수정": "HitModifer", "공격수정": "DmgModifer", "마법방어": "MrModifer",
}
ITEM_PLAIN = {                      # 팩 칸 → Hades 의 숫자 칸, 최대값
    "내구력": ("MaxDurability", 2**31 - 1), "판매가격": ("Value", 2**31 - 1),
    "무게": ("CarryWeight", 255), "레벨제한": ("LevelRequired", 255),
    "최소공격력1": ("DmgMin", 2**31 - 1), "최대공격력1": ("DmgMax", 2**31 - 1),
}
# 성별제한 0 은 "제한 없음" 이다. Hades 는 그것을 Both(255) 로 쓴다.
ITEM_GENDER = {"0": 255, "1": 1, "2": 2}

# `속성` 은 원소가 아니라 **착용 부위**다. 다만 장비에서만 그렇다 — 타입 1(음식)·2(소모품)의
# 속성은 0~3 에 몰려 있고 부위와 맞지 않으므로 건드리지 않는다.
#   확인: 타입 0 과 타입 없음(=장비) 754개의 속성이 이름과 정확히 갈린다.
#   0 길드칼 · 1 아머 · 2 방패 · 3 투구/홀 · 4 귀걸이 · 5 목걸이 · 6 반지 · 7 장갑
#   8 벨트 · 9 각반 · 10 신발 · 11 장식(펫·썬글라스) · 12 양손무기 · 13 지팡이
# Hades 는 반지와 장갑을 좌우로 나누므로 왼쪽을 기본으로 준다.
ITEM_SLOT = {0: 1, 1: 2, 2: 3, 3: 4, 4: 5, 5: 6, 6: 7, 7: 9,
             8: 11, 9: 12, 10: 13, 11: 14, 12: 1, 13: 1}

EQUIPABLE, PERISHABLE, REPAIRABLE, STACKABLE, CONSUMABLE, TWO_HANDED = (
    1, 1 << 1, 1 << 6, 1 << 7, 1 << 8, 1 << 13)
TYPED = "Darkages.Types.{0}, Darkages.Server"


def whole(v, cap=None):
    """팩 값에는 '04' 나 '0.' 이나 목록이 섞여 있다. 숫자로 못 읽으면 없는 칸이다."""
    if isinstance(v, list):
        v = v[0] if v else ""
    try:
        n = int(float(str(v).strip()))
    except (TypeError, ValueError):
        return None
    return max(0, min(n, cap)) if cap is not None else n


def operator(n):
    """Add=0 / Remove=1. 팩은 부호로 적는다 — -65 는 65를 빼라는 뜻이다."""
    return {"$type": TYPED.format("StatusOperator"),
            "Option": 0 if n >= 0 else 1, "Value": abs(n)}


def item_json(rec, skipped):
    f = rec["fields"]
    out = {"$type": TYPED.format("ItemTemplate"), "Name": rec["이름"]}

    for pack, (field, cap) in ITEM_PLAIN.items():
        n = whole(f.get(pack), cap)
        if n is not None:
            out[field] = n

    for pack, field in ITEM_STATS.items():
        n = whole(f.get(pack))
        if n:                                   # 0 은 굳이 적지 않는다
            out[field] = operator(n)

    for pack, field, cap in (("이미지", "Image", 65535), ("착용이미지", "DisplayImage", 65535)):
        n = whole(f.get(pack), cap)
        if n is not None:
            out[field] = n

    g = ITEM_GENDER.get(str(f.get("성별제한", "")).strip())
    if g is not None:
        out["Gender"] = g

    cls = whole(f.get("직업제한"))               # 0~5 가 Hades 의 Class 와 곧바로 맞는다
    if cls is not None and 0 <= cls <= 5:
        out["Class"] = cls

    stage = whole(f.get("승급제한"))             # 0~2 가 ClassStage 의 Class/Master/Dedicated
    if stage is not None and 0 <= stage <= 4:
        out["StageRequired"] = stage

    # 타입 0 이거나 타입 칸이 없으면 장비다(타입 없는 98개는 전부 방패·갑옷·투구였다).
    kind = whole(f.get("타입"))
    slot = whole(f.get("속성"))
    flags = 0

    if kind in (0, None):
        flags |= EQUIPABLE
        if slot in ITEM_SLOT:
            out["EquipSlot"] = ITEM_SLOT[slot]
            out["EquipmentSlot"] = ITEM_SLOT[slot]
        if slot == 12:                           # 투핸드크레이모어 따위가 여기 있다
            flags |= TWO_HANDED
        if whole(f.get("수리여부")) == 1:
            flags |= REPAIRABLE
        if whole(f.get("떨굼여부")) == 1:        # 죽으면 잃는 것. 989개 중 47개뿐이다
            flags |= PERISHABLE
    else:
        flags |= CONSUMABLE | STACKABLE
        out["CanStack"] = True

    out["Flags"] = flags

    handled = set(ITEM_STATS) | set(ITEM_PLAIN) | {
        "이름", "이미지", "착용이미지", "성별제한", "직업제한", "승급제한",
        "타입", "속성", "수리여부", "떨굼여부"}
    for k in f:
        if k not in handled:
            skipped[k] += 1
    return out


def write_items(keep):
    out = SERVER / "templates" / "items"
    out.mkdir(parents=True, exist_ok=True)
    skipped = Counter()
    for rec in keep:
        j = item_json(rec, skipped)
        (out / f"{safe_name(rec['이름']).lower()}.json").write_text(
            json.dumps(j, ensure_ascii=False, indent=2), encoding="utf-8")
    return len(keep), skipped


# ── 괴물 ────────────────────────────────────────────────────────────────
# 젠 표는 `맵, 괴물, 마리수` 세 칸뿐이다 — **좌표가 없다.** 일반 사냥터 괴물은 자리를
# 고정하지 않고 맵 안에 흩어 놓는 것이 원작 방식이고, 자료도 그렇게 말한다.
# 그래서 SpawnType 은 Random 이고 DefinedX/Y 는 쓰지 않는다. (좌표를 주는 젠은
# 스크립트의 mob_spawn2 "결계남도가", 4, 7, … 쪽이고 그건 결계 같은 특수 이벤트다.)
SPAWN_RANDOM = 1 << 1            # SpawnQualifer.Random
LOOT_RANDOM, LOOT_TABLE, LOOT_GOLD, LOOT_NONE = 1 << 1, 1 << 2, 1 << 5, 256
MONSTER_IMAGE_BASE = 0x4000      # bees 의 16385 = 0x4000 + 1
MONSTER_SCRIPT = "Common Monster"


def first(v):
    return (v[0] if v else None) if isinstance(v, list) else v


def monster_json(spawn, mob, area_id, items):
    f = mob["fields"]
    speed = whole(f.get("속도"), 2**31 - 1) or 1000

    # 드롭 목록은 ["확률", "아이템이름"] 꼴이다. 없는 아이템은 넣지 않는다.
    drops = []
    d = f.get("드롭아이템")
    for chunk in (d if d and isinstance(d[0], list) else [d] if d else []):
        name = chunk[1] if isinstance(chunk, list) and len(chunk) > 1 else None
        if name in items:
            drops.append(name)

    # LootType 에 Random 을 켜면서 Drops 를 비우면 잡을 때마다 터진다 —
    # Formulas/monsterexp.cs 가 빈 목록에 Drops[0] 을 한다. 있는 것만 켠다.
    loot = 0
    if drops:
        loot |= LOOT_TABLE
    if f.get("골드"):
        loot |= LOOT_GOLD
    if not loot:
        loot = LOOT_NONE

    return {
        "Name": mob["이름"], "BaseName": mob["이름"],
        "AreaID": area_id,
        "SpawnMax": whole(spawn.get("마리수")) or 1,
        "SpawnType": SPAWN_RANDOM,
        "SpawnRate": whole(f.get("젠타임"), 2**31 - 1) or 30,
        "SpawnSize": 0,
        "SpawnOnlyOnActiveMaps": False,
        "Image": (whole(f.get("이미지")) or 0) + MONSTER_IMAGE_BASE,
        "ImageVarience": whole(f.get("이미지염색")) or 0,
        # Int32 를 넘는 체력이 실제로 있다(42억). 한 장이 넘치면 Newtonsoft 가 던지고
        # 괴물 적재가 통째로 멎는다 — "Monster Templates Loaded" 줄 자체가 안 찍힌다.
        "MaximumHP": 0,     # 0 = "레벨에서 만들어라" (하데스 bees·minion 과 같은 관례)
        "MaximumMP": 0,
        # **수치는 넣지 않는다.** 체력·최소·최대공격력·방어력·경험치가 팩에 다 있지만 5.99 **단독**
        # 이다. 규칙은 하데스가 베이스이고 팩은 두 쪽이 일치할 때만 후보인데, 괴물 수치는 두 팩에서
        # 이름이 겹치는 35마리조차 **전부 어긋난다**(체력 35/35 불일치 · 공격력 31 · 경험치 30).
        # 교차 검증이 불가능하므로 후보가 아니다.
        #
        # 원작 아카이브에도 없다 — 아카이브는 클라이언트 자료이고 괴물 수치는 서버가 갖는 값이다.
        # 그래서 근거 있는 값이 없고, 체력 0 을 두어 **하데스 식이 레벨에서 만들게** 한다
        # (Creations/monsters.cs 가 0 이면 계산한다). 서버는 적혀 있으면 읽으므로, 근거 있는
        # 자료가 생기면 여기만 채우면 된다.
        "Level": 1,
        "MovementSpeed": speed, "EngagedWalkingSpeed": speed,
        "AttackSpeed": 1000, "CastSpeed": 8000,
        "MoodType": 4, "PathQualifer": 1,
        "LootType": loot,
        "Drops": {"$values": drops},
        "ScriptName": MONSTER_SCRIPT,
        "UpdateMapWide": True, "UpdateRate": 1000.0,
        "Grow": False, "IgnoreCollision": False,
    }


def write_monsters(keep):
    out = SERVER / "templates" / "monsters" / "5.99"
    out.mkdir(parents=True, exist_ok=True)
    ids = name_to_id()
    mobs = {m["이름"]: m for m in load("mobs")}
    items = {i["이름"] for i in load("items")}
    skipped = Counter()
    n = 0
    for sp in keep:
        area = ids.get(sp["맵"])
        if area is None:
            continue
        j = monster_json(sp, mobs[sp["괴물"]], area, items)
        (out / f'{safe_name(sp["괴물"] + "@" + sp["맵"]).lower()}.json').write_text(
            json.dumps(j, ensure_ascii=False, indent=2), encoding="utf-8")
        n += 1
        for k in mobs[sp["괴물"]]["fields"]:
            if k not in ("이름", "속도", "이미지", "이미지염색", "젠타임", "드롭아이템", "골드"):
                skipped[k] += 1
    return n, skipped


# ── NPC ─────────────────────────────────────────────────────────────────
# 캐시가 `Name` 열쇠라(GlobalMundaneTemplateCache) 31종을 그대로 쓰면 179배치가
# 31개로 뭉갠다. 배치마다 이름을 새로 짓는다 — 화면에 뜨는 이름이 아니라 열쇠다.
NPC_SCRIPT = "pack_speaker"          # scripts/Mundanes/PackSpeaker.cs


def flatten(v):
    """말하기는 같은 키가 되풀이돼 ["0","말", ["0","말2"]] 처럼 겹쳐 있다. 글만 꺼낸다."""
    if isinstance(v, list):
        return [x for e in v for x in flatten(e)]
    t = str(v).strip() if v is not None else ""
    return [t] if t and not t.isdigit() else []


def mundane_json(sp, npc, area_id):
    f = npc["fields"]
    x, y = int(sp["좌표"][0]), int(sp["좌표"][1])
    return {
        "Name": f'{npc["이름"]}@{sp["맵"]}#{x},{y}',
        "AreaID": area_id, "X": x, "Y": y,
        "Direction": whole(sp["raw"][3], 3) if len(sp.get("raw", [])) > 3 else 0,
        "Image": whole(f.get("이미지"), 32767) or 0,
        "Level": 1, "MaximumHp": 1000, "MaximumMp": 1000,
        "Speech": flatten(f.get("말하기")),
        "ScriptKey": NPC_SCRIPT,
        "DefaultMerchantStock": [],
        "EnableWalking": False, "EnableTurning": False,
        "EnableAttacking": False, "EnableCasting": False,
        "WalkRate": 0, "TurnRate": 0, "CastRate": 0, "ChatRate": 0,
        "PathQualifer": 1, "ViewingQualifer": 1,
    }


def write_mundanes(keep):
    out = SERVER / "templates" / "mundanes"
    out.mkdir(parents=True, exist_ok=True)
    ids = name_to_id()
    npcs = {n["이름"]: n for n in load("npcs")}
    n = mute = 0
    for sp in keep:
        area = ids.get(sp["맵"])
        if area is None:
            continue
        j = mundane_json(sp, npcs[sp["NPC"]], area)
        mute += not j["Speech"]
        (out / f'{safe_name(j["Name"]).lower()}.json').write_text(
            json.dumps(j, ensure_ascii=False, indent=2), encoding="utf-8")
        n += 1
    return n, mute


# ── 기술·마법 ────────────────────────────────────────────────────────────
# **원작이 먼저다.** 팩은 기술 82·마법 71 이지만, Hades 의 metafile/SClass1~5 에서 뽑아 둔
# data/game-data/abilities.json 이 613개(기술 275 · 마법 338)이고 **무엇을 배워야 무엇을
# 배우는지**까지 들어 있다. 팩에는 그 관계가 없다.
#
# 능력치 요구 다섯 칸의 순서는 **힘 / 지력 / 지혜 / 체력 / 민첩** 이다. 2026-09-13 에 세 직업이
# 서로 독립적으로 증명했다 — 성직자 126개의 자리2(지혜) 중간값이 5, 마법사 155개의 자리1(지력)이 4,
# 전사의 자리0(힘) 최대가 215. 나머지 자리는 전부 중간값 3(기본값)이다.
#
# `raw[0]` 은 `요구레벨 / 2차여부 / 요구 어빌리티레벨` 이다. 자리1 이 1 인 356개는 **전부** 자리0 이
# 99 이고 자리2 가 0~99 다 — 99레벨 2차 직업(어빌리티) 능력이라는 뜻이다. 1차에서 배울 수 있는 것은
# 257개뿐이다.
#
# `atLevel` 은 캐릭터 레벨이 아니다. `raw[3]` 의 `Assail/10` 에서 온 **선행 기술의 레벨**이라
# `Skill_Level_Required` 쪽이다. 캐릭터 레벨은 `raw[0]` 첫 자리다.
ABILITIES = ROOT / "data" / "game-data" / "abilities.json"
NAMETABLE = ROOT / "data" / "기술마법-한글이름.tsv"
ABILITY_MARK = "원작표"      # 우리가 쓴 것이라는 표. 없으면 Hades 가 손으로 넣은 것이다
SCRIPTS = SERVER / "scripts"

# 첫 플레이 가능한 세로 조각. 대상 방식은 실제 스크립트(beagiocfein.cs)가 자신에게 쓰는 것으로
# 확인했고, 21은 원작 SClass 표 raw[1]의 첫 값이다. 나머지 328개는 뜻을 확정하기 전까지 꾸며 넣지 않는다.
ABILITY_RUNTIME_OVERRIDES = {
    ("spell", "beag ioc fein"): {
        "Icon": 21,
        "TargetType": 5,       # SpellUseType.NoTarget
        "Pane": 1,             # Pane.Spells
        "Text": "자신의 체력을 회복합니다.",
        "ManaCost": 1,
        "BaseLines": 0,
    },
}


def korean_names():
    """사람이 채운 한글 이름. 비어 있으면 영문 이름을 그대로 쓴다.

    팩의 82·71 로 바꾸지 않는다 - 운영자가 손댄 사본이라 수치를 믿을 수 없고, 무엇보다
    무엇을 배워야 무엇이 열리는지가 팩에는 없다. 구조는 원작, 이름만 사람이 준다.
    """
    if not NAMETABLE.exists():
        return {}
    out = {}
    for line in NAMETABLE.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#") or line.startswith("갈래\t"):
            continue
        c = line.split("\t")
        if len(c) >= 6 and c[5].strip():
            out[c[2].lstrip("· ").strip()] = c[5].strip()
    return out


def script_names():
    """[Script("이름")] 로 등록된 것. 없는 이름을 런타임 열쇠에 넣으면 붙지 않는다."""
    out = set()
    for f in SCRIPTS.rglob("*.cs"):
        out |= set(re.findall(r'\[Script\("([^"]+)"', f.read_text(encoding="utf-8", errors="replace")))
    return out


def requirement(raw):
    """`raw[0]` 의 `요구레벨 / 2차여부 / 요구 어빌리티레벨` 을 숫자 셋으로 읽는다."""
    parts = (str((raw or [""])[0]) + "/0/0").split("/")
    out = []
    for one in parts[:3]:
        out.append(int(one) if one.isdigit() else 0)
    return out


def ability_json(r, kind_of, scripts, korean):
    raw = r.get("raw") or []
    level, advanced, ability_level = requirement(raw)
    stats = r.get("statCosts") or []
    need = r.get("requires")

    # `0/0/0` 에 `5/5/5/5/5` 는 **원작 표가 비워 둔 행**이다. 정확히 12개이고 전부 수도사다.
    # 조건을 적으면 없는 근거를 만드는 것이고, 실제로 그중 `Kelberoth Strike` 는 사람이 아는 바로
    # 99레벨 기술인데 표에는 레벨 0 으로 보인다. 그래서 조건 없이 두고 Group 에 표시만 남긴다.
    empty = level == 0 and not advanced and list(stats) == [5, 5, 5, 5, 5]

    pre = {"Class_Required": r.get("class") or 0}

    if not empty:
        pre["ExpLevel_Required"] = level

        # 기본값 3 은 "요구 없음" 이다(613개 중 대부분이 3 이다). 3 을 적어 두면 새 캐릭터가
        # 힘 10·나머지 5 라서 지혜·체력·민첩에서 걸린다 — 원작에서 걸리지 않는 것이 걸린다.
        for field, value in zip(("Str_Required", "Int_Required", "Wis_Required",
                                 "Con_Required", "Dex_Required"), stats):
            if value and value > 3:
                pre[field] = value

    if advanced:
        # 2차 직업 능력이다. Hades 에는 어빌리티 레벨을 보는 칸이 없어 여기서는 기록만 하고
        # (ExpLevel_Required 99 로 걸린다) 분류는 Group 표에 남긴다.
        pre["Stage_Required"] = 1

    if need:
        # 선행도 캐시에 들어간 이름으로 불러야 한다 - 이름을 바꾸면 여기도 같이 바뀐다.
        pre["Skill_Required" if kind_of.get(need) == "skill" else "Spell_Required"] = \
            korean.get(need, need)
        # `Assail/10` 의 10 은 그 선행 기술의 레벨이다. 캐릭터 레벨과 섞지 않는다.
        if r.get("atLevel"):
            pre["Skill_Level_Required" if kind_of.get(need) == "skill"
                else "Spell_Level_Required"] = r["atLevel"]

    kind = kind_of[r["name"]]
    script = r["name"] if r["name"] in scripts else None

    result = {"Name": korean.get(r["name"], r["name"])}
    # 둘은 같은 Template 기반이지만 런타임 열쇠가 다르다. 기술은 ScriptName, 마법은
    # Spell.AttachScript 가 ScriptKey 를 읽는다. 마법에 ScriptName 을 썼던 이식본은 목록만
    # 보이고 눌러도 실행되지 않았다(격리 Hades 실동작 시험으로 확인).
    if kind == "skill" or script is None:
        # Null ScriptName is the old generated shape; keep it for unscripted spells to avoid rewriting
        # hundreds of otherwise unchanged templates.
        result["ScriptName"] = script
    else:
        result["ScriptKey"] = script

    result.update({
        "Prerequisites": pre,
        "MaxLevel": 100,
        "ID": 0, "Description": None,
        # 이 표가 없으면 Hades 가 손으로 넣은 것이다 — Assail 에는 Buff 와 Icon 이 들어 있는데
        # 원작 표에는 그 값이 없으므로 덮으면 잃는다. 이름을 바꾸면 옛 파일이 남으니,
        # 다시 쓸 때 표가 붙은 것만 먼저 지운다.
        # 2차 직업(어빌리티) 능력은 따로 가려야 한다 — 613개 중 356개가 그렇다.
        "Group": (f"{ABILITY_MARK}/2차{ability_level}" if advanced
                  else f"{ABILITY_MARK}/조건없음" if empty else ABILITY_MARK),
    })
    result.update(ABILITY_RUNTIME_OVERRIDES.get((kind, r["name"]), {}))
    return result


def write_abilities(kind):
    rows = json.loads(ABILITIES.read_text(encoding="utf-8-sig"))
    kind_of = {r["name"]: r["kind"] for r in rows}
    scripts = script_names()
    korean = korean_names()
    want = "skill" if kind == "skills" else "spell"
    out = SERVER / "templates" / kind
    out.mkdir(parents=True, exist_ok=True)

    shipped = set()
    for f in out.glob("*.json"):
        try:
            # 2차 능력은 `원작표/2차NN` 이라 같음 비교로는 안 걸린다 — 안 걸리면 지우지 못해
            # 옛 파일이 남고, 그러면 다음 실행이 그것을 "Hades 것" 으로 보고 비켜 간다.
            mine = str(json.loads(f.read_text(encoding="utf-8-sig")).get("Group") or "") \
                .startswith(ABILITY_MARK)
        except Exception:
            mine = False
        if mine:
            f.unlink()
        else:
            shipped.add(f.stem.lower())
    seen, wrote, kept, scripted = set(), 0, 0, 0
    for r in rows:
        if r["kind"] != want or r["name"] in seen:
            continue
        seen.add(r["name"])
        if safe_name(r["name"]).lower() in shipped:      # Hades 가 이미 들고 있는 것은 그대로 둔다
            kept += 1
            continue
        j = ability_json(r, kind_of, scripts, korean)
        scripted += (j.get("ScriptName") if want == "skill" else j.get("ScriptKey")) is not None
        (out / f'{safe_name(j["Name"]).lower()}.json').write_text(
            json.dumps(j, ensure_ascii=False, indent=2), encoding="utf-8")
        wrote += 1
    return wrote, kept, len(seen), scripted, sum(1 for n in seen if n in korean)


# ── 퀘스트 ──────────────────────────────────────────────────────────────
# **원작이 먼저다.** 팩의 quests.json 60건은 퀘스트 정의가 아니라 추출기가 스크립트에서
# 긁은 **변수 목록**이다 — 팩의 퀘스트는 스크립트 안에 코드로 있다. 원작은 metafile
# SEvent1~7 에서 38건이 표로 나와 있고(data/game-data/quests.json), Hades 가 그중 한 건
# (Mother's Love)을 static/meta/quests/ 에 실어 형식까지 보여 준다.
#
# 그 한 건을 그대로 다시 만들어 원본과 같은지 보고, 같으면 나머지를 같은 꼴로 만든다.
# 형식을 짐작하지 않고 **있는 것으로 확인한다.**
QUESTS = ROOT / "data" / "game-data" / "quests.json"
QUESTDIR = SERVER / "static" / "meta" / "quests"
QUEST_FIELDS = ["start", "title", "id", "qual", "sum", "result", "sub", "reward", "end"]


def quest_atoms(q, field):
    """조각을 끊는 것은 `|` 가 아니라 **` | `**(공백-막대-공백)다.

    그냥 `|` 로 끊으면 `1 | 12345` 가 `"1 "`, `" 12345"` 가 되어 Hades 가 들고 있는 것과
    다르다. 안쪽 공백은 뜻이 있으므로(설명이 칸 맞춰 적혀 있다) 다듬지 않는다.
    start 와 end 는 비어 있으면 조각이 없다.
    """
    v = q.get(field) or ""
    if field in ("start", "end"):
        return [] if not v.strip() else [v]
    return v.split(" | ") if v else [""]


def quest_json(q):
    n = q["key"].split("-")[-1]
    return [{"Atoms": quest_atoms(q, f), "Name": f"{n}_{f}"} for f in QUEST_FIELDS]


def write_quests(_keep):
    QUESTDIR.mkdir(parents=True, exist_ok=True)
    rows = json.loads(QUESTS.read_text(encoding="utf-8-sig"))

    # 먼저 Hades 가 들고 있는 것으로 형식을 확인한다. 다르면 만들지 않는다.
    shipped = QUESTDIR / "Mother's Love.txt"
    same = None
    if shipped.exists():
        want = [q for q in rows if q["title"] == "Mother's Love"]
        if want:
            mine = json.dumps(quest_json(want[0]), ensure_ascii=False, indent=2)
            same = json.loads(mine) == json.loads(shipped.read_text(encoding="utf-8-sig"))

    if same is False:
        raise SystemExit("Hades 가 들고 있는 퀘스트와 모양이 다르다 — 형식을 다시 본다")

    n = 0
    for q in rows:
        (QUESTDIR / f"{safe_name(q['title'])}.txt").write_text(
            json.dumps(quest_json(q), ensure_ascii=False, indent=2), encoding="utf-8")
        n += 1
    return n, same


# ── 상점 ────────────────────────────────────────────────────────────────
# 상점은 만드는 게 아니다. shop1.cs 가 사기·팔기·수리를 다 하고 DefaultMerchantStock 만
# 읽는다. 문제는 **어느 NPC 가 어느 목록을 여느냐** 였고, 그건 팩 스크립트의 shop 호출에
# 있었다(scripts/build-shop-binding.py 가 표로 뽑아 둔다).
#
# 이 NPC 들은 팩 npc/Npc.txt 에 정의가 없다 — 스크립트가 만드는 NPC 라 8단계에서 놓지
# 못한 95건 쪽이다. 상점을 여는 13명은 여기서 놓는다.
SHOPBIND = ROOT / "plans" / "5.99-상점결합.tsv"
SHOP_SCRIPT = "shop1"           # scripts/Mundanes/shop1.cs
SHOP_IMAGE = 1                  # 팩에 그림 번호가 없다. 보이긴 해야 하므로 1 로 둔다


def write_shops(_keep):
    if not SHOPBIND.exists():
        raise SystemExit("상점 결합표가 없다 — python3 scripts/build-shop-binding.py 를 먼저 돌려라")
    ids = name_to_id()
    stock = {s["이름"]: s["아이템"] for s in load("shops")}
    items = {i["이름"] for i in load("items")}

    # 한 NPC 가 여러 목록을 열기도 한다. 파는 목록만 모은다 —
    # 사 주는 목록(물건팔기)은 DefaultMerchantStock 이 아니다. shop1.cs 의 사는 쪽은
    # 목록을 안 쓰고 인벤토리를 받는다. 넣으면 뜻이 뒤집힌다.
    goods, spots, sells = collections.defaultdict(list), {}, collections.defaultdict(list)
    for line in SHOPBIND.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        c = line.split("\t")
        if len(c) < 6:
            continue
        npc, mp, x, y, kind, shop = c[0], c[1], int(c[2]), int(c[3]), c[4], c[5]
        if mp not in ids:
            continue
        key = (npc, mp, x, y)
        spots[key] = (npc, mp, x, y)
        (goods if kind == "물건사기" else sells)[key] += [
            g for g in stock.get(shop, []) if g in items]

    out = SERVER / "templates" / "mundanes"
    out.mkdir(parents=True, exist_ok=True)
    n = 0
    for key, (npc, mp, x, y) in sorted(spots.items()):
        seen, keep = set(), []
        for g in goods.get(key, []):
            if g not in seen:
                seen.add(g); keep.append(g)
        j = {
            "Name": f"{npc}@{mp}#{x},{y}",
            "AreaID": ids[mp], "X": x, "Y": y, "Direction": 0,
            "Image": SHOP_IMAGE, "Level": 1, "MaximumHp": 1000, "MaximumMp": 1000,
            "Speech": [], "ScriptKey": SHOP_SCRIPT,
            "DefaultMerchantStock": keep,
            "EnableWalking": False, "EnableTurning": False,
            "EnableAttacking": False, "EnableCasting": False,
            "WalkRate": 0, "TurnRate": 0, "CastRate": 0, "ChatRate": 0,
            "PathQualifer": 1, "ViewingQualifer": 1,
        }
        (out / f'{safe_name(j["Name"]).lower()}.json').write_text(
            json.dumps(j, ensure_ascii=False, indent=2), encoding="utf-8")
        n += 1
    return n, sum(len(v) for v in goods.values()), len(sells)


# ── 월드맵 ───────────────────────────────────────────────────────────────
# **원작이 먼저다.** 노드 이름·화면 위치·그림은 Legend.dat 의 field001.txt 에서 온다
# (25개. 열 장이 같은 목록의 다른 판이다). 팩의 마이소시아는 그 위에 4개를 더하고 3개를
# 뺀 운영자 판이라, 목록은 원작을 쓰고 **어디로 가는지만** 팩에서 가져온다 — 원작 표에
# 목적지가 없기 때문이다. 어느 쪽에서 왔는지는 노트에 남긴다.
ORIGINAL_FIELD = ROOT / "data" / "archives-vault" / "표" / "Legend — field001.txt.md"
WORLD_FIELD_NUMBER = 1               # Hades 의 Temuair. fieldmaps/field001.png 가 그 그림이다


def original_nodes():
    """볼트 노트에 옮겨 둔 field001.txt 를 읽는다. `이름 그림키 x y [EX …]`."""
    if not ORIGINAL_FIELD.exists():
        raise SystemExit("원작 월드맵 노트가 없다 — python3 scripts/build-archive-vault.py 를 먼저 돌려라")
    body = ORIGINAL_FIELD.read_text(encoding="utf-8").split("```")[1]
    out = []
    for line in body.splitlines()[2:]:
        col = re.split(r"\s+", line.strip())
        if len(col) >= 4 and re.fullmatch(r"f\d+", col[1]) and col[2].lstrip("-").isdigit():
            out.append((col[0], int(col[2]), int(col[3])))
    return out


def write_worldmaps(_keep):
    ids = name_to_id()
    pack = {}
    for wm in load("worldmaps"):
        for row in wm["fields"].get("추가", []):
            c = [x.strip() for x in row.split(",")]
            if len(c) >= 7:
                pack[c[1]] = (c[4], int(c[5]), int(c[6]))     # 노드이름 → (도착맵, x, y)

    # Hades 가 원래 들고 있던 노드 둘. 같은 그림(field001.png) 위의 자리이고 가리키는 맵도
    # 살아 있으므로 지우지 않는다 — 원작 목록을 얹는 것이지 있던 것을 없애는 일이 아니다.
    portals = [
        {"Destination": {"AreaID": 2, "Location": {"X": 66, "Y": 35}, "PortalKey": 0},
         "DisplayName": "Refugee Camp", "PointX": 442, "PointY": 225},
        {"Destination": {"AreaID": 3, "Location": {"X": 95, "Y": 50}, "PortalKey": 0},
         "DisplayName": "Lost Woods", "PointX": 417, "PointY": 50},
    ]
    missing = []
    for name, px, py in original_nodes():
        where = pack.get(name)
        if where is None or where[0] not in ids:
            missing.append(name)
            continue
        portals.append({
            "Destination": {"AreaID": ids[where[0]],
                            "Location": {"X": where[1], "Y": where[2]}, "PortalKey": 0},
            "DisplayName": name, "PointX": px, "PointY": py,
        })

    out = SERVER / "templates" / "worldmaps"
    out.mkdir(parents=True, exist_ok=True)
    (out / "temuair.json").write_text(json.dumps({
        "Portals": portals,
        "FieldNumber": WORLD_FIELD_NUMBER,
        "Description": "원작 월드맵 — 노드는 Legend.dat field001.txt, 목적지는 5.99 팩",
        "Group": "WorldMaps", "Name": "Temuair",
    }, ensure_ascii=False, indent=2), encoding="utf-8")
    return len(portals), missing


def world_warp_json(rows, ids):
    """종류 2 는 월드맵으로 나가는 문이다. 같은 맵의 칸들을 한 장에 모은다."""
    src = ids[rows[0]["출발맵"]]
    return {
        "ActivationMapId": src,
        "Activations": [{"AreaID": src,
                         "Location": {"X": int(r["출발"][0]), "Y": int(r["출발"][1])},
                         "PortalKey": 0} for r in rows],
        "LevelRequired": 1,
        "To": {"AreaID": 0, "Location": None, "PortalKey": 1},
        "WarpRadius": 0, "WarpType": "World",
        "WorldResetWarpId": 0, "WorldTransionWarpId": 0,
        "Description": None, "Group": None,
        "Name": f'warp {rows[0]["출발맵"]} to world map',
    }


def write_world_warps():
    ids = name_to_id()
    by_map = collections.defaultdict(list)
    for x in load("warps"):
        if x["raw"][0] == "2" and x["출발맵"] in ids:
            by_map[x["출발맵"]].append(x)
    out = SERVER / "templates" / "warps"
    out.mkdir(parents=True, exist_ok=True)
    for rows in by_map.values():
        j = world_warp_json(rows, ids)
        (out / f'{safe_name(j["Name"]).lower()}.json').write_text(
            json.dumps(j, ensure_ascii=False, indent=2), encoding="utf-8")
    return len(by_map), sum(len(v) for v in by_map.values())


def warp_json(x, ids):
    """Hades 의 워프. 맵을 **이름이 아니라 번호**로 가리킨다 — 그래서 번호표가 먼저다.

    팩 워프는 한 줄이 한 방향이다(출발 칸을 밟으면 도착 칸으로). Hades 의 `Activations` 는
    같은 목적지로 보내는 **밟는 칸 목록**이라, 한 줄이 칸 하나짜리 워프 한 장이 된다.
    """
    fx, fy = int(x["출발"][0]), int(x["출발"][1])
    tx, ty = int(x["도착"][0]), int(x["도착"][1])
    src, dst = ids[x["출발맵"]], ids[x["도착맵"]]
    return {
        "ActivationMapId": src,
        "Activations": [{"AreaID": src, "Location": {"X": fx, "Y": fy}, "PortalKey": 0}],
        "LevelRequired": 1,
        "To": {"AreaID": dst, "Location": {"X": tx, "Y": ty}, "PortalKey": 0},
        "WarpRadius": 0,
        "WarpType": "Map",
        "WorldResetWarpId": 0,
        "WorldTransionWarpId": 0,
        "Description": None,
        "Group": None,
        # 파일은 Name.ToLower() 로 쓰인다(WarpStorage.Save). 이름이 겹치면 덮이므로
        # 출발 칸까지 넣어 유일하게 만든다.
        "Name": f'warp {x["출발맵"]}({fx},{fy}) to {x["도착맵"]}({tx},{ty})',
    }


def name_to_id():
    """팩 맵 이름 → 전역 번호. 이름이 겹친 맵은 번호표에 두 줄이지만 워프가 안 부르므로 상관없다."""
    ids = {}
    for line in IDTABLE.read_text(encoding="utf-8").splitlines():
        if line and not line.startswith("#"):
            c = line.split("\t")
            ids.setdefault(c[1], int(c[2]))
    return ids


def write_warps(keep):
    out = SERVER / "templates" / "warps"
    out.mkdir(parents=True, exist_ok=True)
    ids = name_to_id()
    n = 0
    for x in keep:
        j = warp_json(x, ids)
        (out / f'{safe_name(j["Name"]).lower()}.json').write_text(
            json.dumps(j, ensure_ascii=False, indent=2), encoding="utf-8")
        n += 1
    return n


def write_maps(rows):
    areas, maps = SERVER / "areas", SERVER / "maps"
    areas.mkdir(parents=True, exist_ok=True); maps.mkdir(parents=True, exist_ok=True)
    for (_, _, rel), mid, m in rows:
        (areas / f"{m['_파일이름']}.json").write_text(
            json.dumps(area_json(m), ensure_ascii=False, indent=2), encoding="utf-8")
        shutil.copy2(MAPSRC / rel, maps / f"lod{mid}.map")
    return len(rows)


# ──────────────────────────────────────────────────────────────────────────

def report(kind, keep, drop):
    exp = EXPECTED.get(kind)
    mark = "" if exp is None else ("  ✓" if len(keep) == exp else f"  ✗ 계획은 {exp}")
    print(f"══ {kind}  넣을 수 {len(keep)}{mark}  ·  못 넣음 {len(drop)}")
    if drop:
        why = Counter(re.sub(r"[:：].*", "", d[1]) for d in drop)
        for reason, n in why.most_common():
            print(f"     {n:>5}  {reason}")
    return len(keep) == exp if exp is not None else True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--kind", default="all", choices=list(RULES) + ["all"])
    ap.add_argument("--write", action="store_true", help="실제로 쓴다 (기본은 세어만 본다)")
    a = ap.parse_args()

    kinds = list(RULES) if a.kind == "all" else [a.kind]
    allok = True
    for kind in kinds:
        keep, drop = RULES[kind](kind)
        allok &= report(kind, keep, drop)

        if kind == "maps":
            rows = map_ids(keep)
            write_id_table(rows)
            print(f"     번호표 {len(rows)}줄 → {IDTABLE.relative_to(ROOT)}")
            if a.write:
                print(f"     넣음 {write_maps(rows)}장 → areas/ · maps/")
        elif kind in ("skills", "spells") and a.write:
            wrote, kept, distinct, scripted, named = write_abilities(kind)
            print(f"     넣음 {wrote}장 → templates/{kind}/  "
                  f"(서로 다른 이름 {distinct} · Hades 것 그대로 둠 {kept} · "
                  f"스크립트 붙은 것 {scripted} · 한글 이름 {named})")
        elif kind == "quests" and a.write:
            n, same = write_quests(keep)
            print(f"     넣음 {n}장 → static/meta/quests/  "
                  f"(Hades 가 든 한 건과 모양 {'같다' if same else '대조 못 함'})")
        elif kind == "shops" and a.write:
            n, goods, sells = write_shops(keep)
            print(f"     상점 NPC {n}명 → templates/mundanes/  (파는 물건 {goods}개 · 사 주는 목록 {sells}자리)")
        elif kind == "worldmaps" and a.write:
            got, missing = write_worldmaps(keep)
            files, cells = write_world_warps()
            print(f"     월드맵 노드 {got}개 → templates/worldmaps/temuair.json")
            print(f"     목적지를 못 찾은 원작 노드 {len(missing)}: {', '.join(missing)}")
            print(f"     월드맵으로 나가는 문 {files}장 ({cells}칸) → templates/warps/")
        elif kind == "mundanes" and a.write:
            n, mute = write_mundanes(keep)
            print(f"     넣음 {n}장 → templates/mundanes/  (할 말이 없는 NPC {mute}명)")
        elif kind == "monsters" and a.write:
            n, skipped = write_monsters(keep)
            print(f"     넣음 {n}장 → templates/monsters/5.99/")
            print("     옮기지 않은 칸: " + ", ".join(f"{k}×{v}" for k, v in skipped.most_common(8)))
        elif kind == "items" and a.write:
            n, skipped = write_items(keep)
            print(f"     넣음 {n}장 → templates/items/")
            print(f"     옮기지 않은 칸(뜻 미확인): " +
                  ", ".join(f"{k}×{v}" for k, v in skipped.most_common(10)))
        elif kind == "warps" and a.write:
            if not IDTABLE.exists():
                print("     번호표가 없다 — 먼저 --kind maps 를 돌려라"); return 1
            print(f"     넣음 {write_warps(keep)}장 → templates/warps/")
        elif a.write:
            print("     (이 갈래는 칸 대응이 아직 없다 — 자기 단계에서 붙인다)")

    print()
    print("계획의 실측 표와 맞다." if allok else "계획의 실측 표와 다르다 — 진행 전에 어느 쪽이 틀렸는지 가려라.")
    return 0 if allok else 1


if __name__ == "__main__":
    sys.exit(main())
