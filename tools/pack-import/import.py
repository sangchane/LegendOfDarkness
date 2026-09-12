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
import argparse, json, re, shutil, sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
PACK = "5.99-server"
EXTRACTED = ROOT / "data" / "server-packs" / "extracted" / PACK
MAPSRC = ROOT / "data" / "map-source" / PACK
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
IDTABLE = ROOT / "plans" / "5.99-맵번호표.tsv"

BYTES_PER_TILE = 6          # 바닥 + 왼벽 + 오른벽, 각 ushort
FIRST_MAP_ID = 100_000      # 1~99999 는 Hades 것 (safe house=1, refugee camp=2, lost woods=3, hades=99999)

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
    "maps":      eligible_maps,
    "warps":     eligible_warps,
    "monsters":  eligible_monsters,
    "mundanes":  eligible_mundanes,
    "items":     lambda _: eligible_plain("items"),
    "skills":    lambda _: eligible_plain("skills"),
    "spells":    lambda _: eligible_plain("spells"),
    "worldmaps": lambda _: eligible_plain("worldmaps"),
    "doors":     lambda _: eligible_plain("doors"),
}

# 계획의 실측 표. 여기서 벗어나면 표가 틀렸거나 적재기가 틀렸다 — 진행 전에 가린다.
EXPECTED = {"maps": 797, "warps": 886, "items": 989, "monsters": 565,
            "mundanes": 84, "skills": 82, "spells": 71, "worldmaps": 1, "doors": 1}


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
        elif a.write:
            print("     (이 갈래는 칸 대응이 아직 없다 — 자기 단계에서 붙인다)")

    print()
    print("계획의 실측 표와 맞다." if allok else "계획의 실측 표와 다르다 — 진행 전에 어느 쪽이 틀렸는지 가려라.")
    return 0 if allok else 1


if __name__ == "__main__":
    sys.exit(main())
