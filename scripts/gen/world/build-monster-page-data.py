#!/usr/bin/env python3
"""지금 구현된 지역의 괴물을 화면에서 볼 수 있게 한 덩어리로 뽑는다.

표에 흩어진 값(체력·경험치·피해·젠·선공·드랍)을 **실제로 굴러가는 규칙과 함께** 모은다.
숫자만 옮기면 "쿠룸 0.8" 이 80% 처럼 보이지만, 하데스는 괴물별(없으면 물건별) `DropRate×1.5` 를
목록 길이 위에 순서대로 놓는다(`scripts/Formulas/monsterexp.cs` DetermineRandomDrop).
남은 구간에 들어오는 실제 확률을 여기서 계산해 둔다.

  쓰는 법: python3 scripts/gen/world/build-monster-page-data.py   → docs/monsters-data.js
"""
import json
import shutil
import sys

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT
from lib._git import git_pointer
from lib._io import read_json as read
from lib import _cut_level as cut
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
AREAS = SERVER / "areas"
MONSTERS = SERVER / "templates/monsters"
ITEMS = SERVER / "templates/items"
SPRITES = ROOT / "mobile/client/assets/actor/creature"
OUT = ROOT / "docs" / "monsters-data.js"
PUBLIC_SPRITES = OUT.parent / "ui/assets/creature"

# 지금 모바일로 실제 돌아다닐 수 있는 지역. 이름 앞머리로 가른다.
REGIONS = ["노비스", "수오미", "우드랜드", "포테의숲", "아벨해안", "구광산", "뤼케시온해안"]  # 사냥터 넷은 사용자 2026-10-02

# 목록 드랍 전체에 곱하는 배율 — `Formulas/monsterexp.cs` DropBoost 와 같아야 한다(사용자 2026-09-26, 1.5배).
DROP_BOOST = 1.5

# 괴물 그림 번호 → 원작 스프라이트 번호. 16437(거미) → MNS053 으로 확인(docs/monster-behaviour.md).
SPRITE_BASE = 16384

# 레벨 차이로 경험치를 깎는 규칙. monsterexp.cs 의 Forgiven·Halving·Least 와 같아야 한다.
PENALTY = {"용서": 5, "반감": 5, "최소": 0.02}


EXPERIENCE = ROOT / "data" / "server-packs" / "5.99-server" / "db" / "server" / "experience.txt"


def to_reach(level):
    """ExperienceCurve.ToReach 와 같은 값 — 5.99 experience.txt 직업 1 줄의 넷째 칸 그대로. 누적이 아니라 그 한 레벨의 값이다."""
    if level <= 1:
        return 0
    for line in EXPERIENCE.read_text(encoding="cp949").splitlines():
        parts = line.split()
        if len(parts) >= 4 and parts[0] == "1" and parts[1] == str(level):
            return int(parts[3])
    raise KeyError(level)


def areas():
    out = {}
    for path in AREAS.glob("*.json"):
        try:
            data = read(path)
        except (OSError, ValueError):
            print(f"건너뜀(읽기 실패): {path}", file=sys.stderr)
            continue
        out[data["Id"]] = data["Name"]
    return out


def region_of(name):
    for region in REGIONS:
        if name.startswith(region):
            return region
    return None


def item_facts():
    out = {}
    for path in ITEMS.glob("*.json"):
        try:
            data = read(path)
        except (OSError, ValueError):
            print(f"건너뜀(읽기 실패): {path}", file=sys.stderr)
            continue
        out[data.get("Name")] = data
    return out


def sprite_for(image):
    """그림 한 칸만 보여 주려면 칸 수와 판 크기가 있어야 한다.

    칸 수는 곁의 `.txt` 가 `frames N` 으로 적어 두고, 판 크기는 PNG 머리(IHDR)에 있다.
    둘 다 없으면 통째로 줄여 그리는 수밖에 없으므로 그때는 `None` 으로 둔다.
    """
    number = image - SPRITE_BASE
    if number <= 0:
        return None
    name = f"mns{number:03d}"
    png = SPRITES / f"{name}.png"
    if not png.exists():
        return None

    head = png.read_bytes()[:24]
    if head[1:4] != b"PNG":
        return None
    width = int.from_bytes(head[16:20], "big")
    height = int.from_bytes(head[20:24], "big")

    frames = 1
    actions = {}
    meta = SPRITES / f"{name}.txt"
    if meta.exists():
        for line in meta.read_text(encoding="utf-8").splitlines():
            parts = line.split()
            if not parts: continue
            if parts[0] == "frames":
                frames = max(1, int(parts[1]))
            elif len(parts) >= 3:
                actions[parts[0]] = [int(parts[1]), int(parts[2])]

    return {"이름": name, "칸": frames, "너비": width, "높이": height, "동작": actions}


def kind_of(item):
    """떨구는 물건이 무엇인가 — 화면에서 갈래별로 묶어 보려고."""
    if item.get("HealthRestore") or item.get("ManaRestore"):
        return "시약"
    if item.get("EquipmentSlot"):
        return "장비"
    return "재료"


def monster_rows(wanted, facts):
    """괴물 자리마다 한 줄 — 맵, 수치, 금화, 드랍, 그림."""
    rows = []
    calibration = list(cut.monsters())
    points, brackets = cut.fit(calibration), cut.woodland(calibration)
    for folder in sorted(p for p in MONSTERS.iterdir() if p.is_dir()):
        for path in sorted(folder.glob("*.json")):
            try:
                data = read(path)
            except Exception:
                continue
            area = data.get("AreaID")
            if area not in wanted:
                continue

            listed = (data.get("Drops") or {}).get("$values") or []
            share = 1 / len(listed) if listed else 0
            left = len(listed)
            drops = []
            for name in listed:
                item = facts.get(name, {})
                override = data.get("DropRate")
                rate = override if override is not None else (item.get("DropRate") or 0)
                weight = max(0, rate * DROP_BOOST) if name in facts else 0
                drops.append({
                    "이름": name,
                    "갈래": kind_of(item),
                    "표확률": rate,
                    "실제확률": round(min(left, weight) * share, 4),
                    "값": item.get("Value") or 0,
                    "체력회복": item.get("HealthRestore") or 0,
                    "마력회복": item.get("ManaRestore") or 0,
                    "직업": item.get("Class") or 0,
                    "요구레벨": item.get("LevelRequired") or 0,
                    "템플릿있음": name in facts,
                    # 팩이 나눈 파일(5.99표/투구/치장아이템 …) — 치장·장식은 나중에 드랍에서 뺄지 정한다(사용자 2026-10-04).
                    "분류": item.get("Group") or "",
                })
                left = max(0, left - weight)

            loot = data.get("LootType") or 0
            level = data.get("Level") or 1
            exp = data.get("Exp")
            if exp is None:
                exp = int(level * (level * 0.1 + 1.5) * 300)
            per_exp = 0.02 if 20083 <= area <= 20086 or 20373 <= area <= 20394 else 0.1
            minimum = data.get("GoldMinimum") or 0
            mood = data.get("MoodType") or 0
            rows.append({
                "이름": data.get("Name"),
                "맵번호": area,
                "맵": wanted[area],
                "지역": region_of(wanted[area]),
                "체력": data.get("MaximumHP"),
                "마력": data.get("MaximumMP") or 0,
                "경험치": exp,
                "피해": [data.get("DmgMin") or 0, data.get("DmgMax") or 0],
                "방어": data.get("Ac") or 0,
                "레벨": level,
                "감산레벨": cut.level_for(points, brackets, area, exp),
                "젠최대": data.get("SpawnMax") or 0,
                "젠주기": data.get("SpawnRate") or 0,
                "이동속도": data.get("MovementSpeed") or 0,
                "공격속도": data.get("AttackSpeed") or 0,
                # MoodType 은 깃발이고 스폰할 때 한 번 접힌다 — docs/monster-behaviour.md 2절.
                # Aggressive(2) 면 선공, 아니고 Unpredicable(4) 면 동전 던지기, 나머지는 비선공.
                "선공": "선공" if mood & 2 else ("반반" if mood & 4 else "비선공"),
                "금화": [max(0, minimum, round(exp * per_exp * factor)) for factor in (0.8, 1.2)],
                "드랍켜짐": bool(loot & 2),
                "그림": data.get("Image"),
                "스프라이트": sprite_for(data.get("Image") or 0),
                "드랍": drops,
                "근거": f"templates/monsters/{folder.name}/{path.name}",
            })

    rows.sort(key=lambda r: (r["지역"], r["맵번호"], r["경험치"]))
    return rows


def write_page(rows, wanted):
    """monsters-data.js 를 쓰고 수를 알린다."""
    PUBLIC_SPRITES.mkdir(parents=True, exist_ok=True)
    for name in sorted({r["스프라이트"]["이름"] for r in rows if r["스프라이트"]}):
        shutil.copyfile(SPRITES / f"{name}.png", PUBLIC_SPRITES / f"{name}.png")
    # 사람이 갈 수 있는 맵인데 괴물이 하나도 없는 곳 — 마을이라 없는 것일 수도, 안 채운 것일 수도.
    filled = {r["맵번호"] for r in rows}
    empty = [{"맵번호": i, "맵": n, "지역": region_of(n)}
             for i, n in sorted(wanted.items()) if i not in filled]

    pointer = git_pointer(SERVER.parents[1])

    payload = {
        "생성": "scripts/gen/world/build-monster-page-data.py",
        "서버포인터": pointer,
        "지역": REGIONS,
        "규칙": {
            "드랍": "괴물 DropRate(없으면 물건 DropRate)×1.5 를 목록 길이 위에 순서대로 놓는다 — monsterexp.cs DetermineRandomDrop·DropBoost",
            "금화": "경험치 × 0.1(노비스 0.02) × 0.8~1.2, GoldMinimum 이상",
            "감산": PENALTY,
            "감산근거": "레벨 차이로 경험치를 깎는 값은 우리가 정한 것이다 — 원작에도 5.99 팩에도 그 규칙이 없다",
            "선공": "MoodType 은 스폰할 때 한 번 접힌다. 반반 = Unpredicable(4), 그 마리는 죽을 때까지 그대로",
        },
        "레벨표": {str(n): to_reach(n) for n in range(2, 100)},
        "괴물": rows,
        "빈맵": empty,
        "셈": {
            "괴물자리": len(rows),
            "이름": len({r["이름"] for r in rows}),
            "그림있음": sum(1 for r in rows if r["스프라이트"]),
            "빈맵": len(empty),
        },
    }

    OUT.write_text(
        "window.LOD_MONSTERS = " + json.dumps(payload, ensure_ascii=False) + ";\n",
        encoding="utf-8")
    print(f"괴물 자리 {len(rows)} · 이름 {payload['셈']['이름']} · 그림 {payload['셈']['그림있음']} · 빈 맵 {len(empty)}")
    print(f"→ {OUT.relative_to(ROOT)}  ({OUT.stat().st_size // 1024} KB)")


def main():
    names = areas()
    facts = item_facts()
    wanted = {i: n for i, n in names.items() if region_of(n)}

    rows = monster_rows(wanted, facts)

    write_page(rows, wanted)


if __name__ == "__main__":
    main()
