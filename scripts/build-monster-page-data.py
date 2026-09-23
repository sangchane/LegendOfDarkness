#!/usr/bin/env python3
"""지금 구현된 지역의 괴물을 화면에서 볼 수 있게 한 덩어리로 뽑는다.

표에 흩어진 값(체력·경험치·피해·젠·선공·드랍)을 **실제로 굴러가는 규칙과 함께** 모은다.
숫자만 옮기면 "쿠룸 0.8" 이 80% 처럼 보이지만, 하데스는 **목록에서 하나를 고른 뒤** 그 물건의
`DropRate` 를 굴리므로(`scripts/Formulas/monsterexp.cs` DetermineRandomDrop) 목록이 넷이면 0.2 다.
그 곱을 여기서 계산해 둔다.

  쓰는 법: python3 scripts/build-monster-page-data.py   → docs/monsters-data.js
"""
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
AREAS = SERVER / "areas"
MONSTERS = SERVER / "templates/monsters"
ITEMS = SERVER / "templates/items"
SPRITES = ROOT / "mobile/client/assets/actor/creature"
OUT = ROOT / "docs" / "monsters-data.js"

# 지금 모바일로 실제 돌아다닐 수 있는 지역. 이름 앞머리로 가른다.
REGIONS = ["노비스", "수오미"]

# 괴물 그림 번호 → 원작 스프라이트 번호. 16437(거미) → MNS053 으로 확인(docs/monster-behaviour.md).
SPRITE_BASE = 16384

# 레벨 차이로 경험치를 깎는 규칙. monsterexp.cs 의 Forgiven·Halving·Least 와 같아야 한다.
PENALTY = {"용서": 5, "반감": 5, "최소": 0.02}


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def to_reach(level):
    """ExperienceCurve.ToReach 와 같은 식. 누적이 아니라 그 한 레벨의 값이다."""
    early = [0, 0, 600, 1800, 3000, 4200, 5850, 6798]
    if level <= 1:
        return 0
    if level < len(early):
        return early[level]
    if level <= 49:
        return int(1236 * level - 1854)
    if level <= 68:
        return int(61000 * 1.02747 ** (level - 50))
    return int(130872 * 1.02482 ** (level - 69))


def areas():
    out = {}
    for path in AREAS.glob("*.json"):
        try:
            data = read(path)
        except Exception:
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
        except Exception:
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


def main():
    names = areas()
    facts = item_facts()
    wanted = {i: n for i, n in names.items() if region_of(n)}

    rows = []
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
            drops = []
            for name in listed:
                item = facts.get(name, {})
                rate = item.get("DropRate") or 0
                drops.append({
                    "이름": name,
                    "갈래": kind_of(item),
                    "표확률": rate,
                    "실제확률": round(share * rate, 4),
                    "값": item.get("Value") or 0,
                    "체력회복": item.get("HealthRestore") or 0,
                    "마력회복": item.get("ManaRestore") or 0,
                    "직업": item.get("Class") or 0,
                    "요구레벨": item.get("LevelRequired") or 0,
                    "템플릿있음": name in facts,
                })

            loot = data.get("LootType") or 0
            level = data.get("Level") or 1
            mood = data.get("MoodType") or 0
            rows.append({
                "이름": data.get("Name"),
                "맵번호": area,
                "맵": wanted[area],
                "지역": region_of(wanted[area]),
                "체력": data.get("MaximumHP"),
                "마력": data.get("MaximumMP") or 0,
                "경험치": data.get("Exp") or 0,
                "피해": [data.get("DmgMin") or 0, data.get("DmgMax") or 0],
                "방어": data.get("Ac") or 0,
                "레벨": level,
                "젠최대": data.get("SpawnMax") or 0,
                "젠주기": data.get("SpawnRate") or 0,
                "이동속도": data.get("MovementSpeed") or 0,
                "공격속도": data.get("AttackSpeed") or 0,
                # MoodType 은 깃발이고 스폰할 때 한 번 접힌다 — docs/monster-behaviour.md 2절.
                # Aggressive(2) 면 선공, 아니고 Unpredicable(4) 면 동전 던지기, 나머지는 비선공.
                "선공": "선공" if mood & 2 else ("반반" if mood & 4 else "비선공"),
                "금화": [level * 500, level * 1000] if loot & 32 else [0, 0],
                "드랍켜짐": bool(loot & 2),
                "그림": data.get("Image"),
                "스프라이트": sprite_for(data.get("Image") or 0),
                "드랍": drops,
                "근거": f"templates/monsters/{folder.name}/{path.name}",
            })

    rows.sort(key=lambda r: (r["지역"], r["맵번호"], r["경험치"]))

    # 사람이 갈 수 있는 맵인데 괴물이 하나도 없는 곳 — 마을이라 없는 것일 수도, 안 채운 것일 수도.
    filled = {r["맵번호"] for r in rows}
    empty = [{"맵번호": i, "맵": n, "지역": region_of(n)}
             for i, n in sorted(wanted.items()) if i not in filled]

    try:
        pointer = subprocess.run(
            ["git", "-C", str(SERVER.parents[1]), "rev-parse", "--short", "HEAD"],
            capture_output=True, text=True, check=True).stdout.strip()
    except Exception:
        pointer = ""

    payload = {
        "생성": "scripts/build-monster-page-data.py",
        "서버포인터": pointer,
        "지역": REGIONS,
        "규칙": {
            "드랍": "목록에서 하나를 고르고(같은 확률) 그 물건의 DropRate 를 굴린다 — monsterexp.cs DetermineRandomDrop",
            "금화": "레벨 × 500 ~ 레벨 × 1000",
            "감산": PENALTY,
            "감산근거": "레벨 차이로 경험치를 깎는 값은 우리가 정한 것이다 — 원작에도 5.99 팩에도 그 규칙이 없다",
            "선공": "MoodType 은 스폰할 때 한 번 접힌다. 반반 = Unpredicable(4), 그 마리는 죽을 때까지 그대로",
        },
        "레벨표": {str(n): to_reach(n) for n in range(2, 51)},
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


if __name__ == "__main__":
    main()
