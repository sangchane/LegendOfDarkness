"""깎기용 괴물 레벨 — 기준점·식. build-monster-cut-level.py 와 build-game-vault.py 가 같이 쓴다."""
import collections
import json
import math
import re

from lib._paths import ROOT

SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"


# 기준 사냥터: 맵 이름 앞머리 → 입장 레벨 범위(위 docstring 의 근거).
ANCHORS = [("노비스", 1, 22), ("포테의숲", 21, 51), ("아벨해안", 51, 80)]

# 우드랜드 구간: (맵 이름들, 아래, 위, 근거). 괴물이 없는 맵도 적어 둔다(생기면 그 구간으로).
WOODLAND = [
    (["우드랜드1-1", "우드랜드1-2", "우드랜드1-3"], 1, 10, "입장 레벨문 없음 — 2-1 입장 11 의 바로 아래까지"),
    (["우드랜드2-1"], 11, 20, "5.99 입구→2-1 11~99, 위는 3-1 입장 21 −1"),
    (["우드랜드3-1", "우드랜드4-1"], 21, 50, "5.99 입구→3-1·4-1 21~99, 위는 5-1 입장 51 −1"),
    (["우드랜드5-1", "우드랜드6-1", "우드랜드6-1(진)", "우드랜드10-1", "우드랜드11-1"], 51, 80,
     "5.99 입구→5-1·6-1 51~99, 위는 14-1 입장 81 −1. 10-1 은 6-1 에서 문 없이(0~99) 들어가고 11-1 은 10-1 에서 — "
     "그래서 6-1 구간. 6-1(진) 은 워프가 없어 이름대로 6-1 구간(지금 괴물 없음)"),
    (["우드랜드14-1", "우드랜드14-1(진)"], 81, 99, "5.99 입구→14-1 81~99. 14-1(진) 은 워프가 없어 이름대로(지금 괴물 없음)"),
]


def load(path):
    return json.loads(re.sub(r",\s*([\]}])", r"\1", path.read_text(encoding="utf-8-sig")))


def area_names():
    names = {}
    for path in (SERVER / "areas").glob("*.json"):
        try:
            area = load(path)
            names[area["ID"]] = area["Name"]
        except (ValueError, KeyError):
            pass
    return names


def monsters():
    names = {}
    for path in (SERVER / "areas").glob("*.json"):
        try:
            area = load(path)
            names[area["ID"]] = area["Name"]
        except (ValueError, KeyError):
            pass

    for path in sorted((SERVER / "templates/monsters").rglob("*.json")):
        try:
            template = load(path)
        except ValueError:
            continue
        exp = template.get("Exp") or 0
        if exp > 0:
            area = names.get(template.get("AreaID")) or f"맵{template.get('AreaID')}"
            yield area, template.get("Name"), exp, max(template.get("SpawnMax") or 1, 1)


def zones(rows, prefix):
    """존(맵)마다 SpawnMax 무게 기하평균 경험치."""
    groups = collections.defaultdict(list)
    for area, name, exp, weight in rows:
        if area.startswith(prefix):
            groups[area].append((exp, weight))
    return {area: math.exp(sum(math.log(e) * w for e, w in v) / sum(w for _, w in v)) for area, v in groups.items()}


def woodland(rows):
    """구간마다 (맵 번호들, 맵 이름들, 가장 낮은 경험치, 가장 높은 경험치, 아래, 위, 근거)."""
    ids = {name: i for i, name in area_names().items()}
    brackets = []
    for maps, low, high, why in WOODLAND:
        exps = [exp for area, _, exp, _ in rows if area in maps]
        brackets.append(([ids[m] for m in maps if m in ids], maps, min(exps), max(exps), low, high, why))
    return brackets


def woodland_level(bracket, exp):
    """monsterexp.cs CutLevel 의 우드랜드 갈래와 같은 식."""
    _, _, e0, e1, low, high, _ = bracket
    value = low if e1 == e0 else low + (math.log(max(exp, 1)) - math.log(e0)) * (high - low) / (math.log(e1) - math.log(e0))
    return max(low, min(high, int(round(value))))


def level_for(points, brackets, area, exp):
    """area 는 맵 번호나 맵 이름. 우드랜드면 제 구간, 아니면 한 줄 대응."""
    for bracket in brackets:
        if area in bracket[0] or area in bracket[1]:
            return woodland_level(bracket, exp)
    return level(points, exp)


def fit(rows):
    points = []
    for prefix, low, high in ANCHORS:
        found = zones(rows, prefix)
        lo, hi = math.log(min(found.values())), math.log(max(found.values()))
        for area, exp in found.items():
            points.append((exp, low + (math.log(exp) - lo) / (hi - lo) * (high - low), area))
    points.sort()
    fixed, top = [], 0.0
    for exp, lvl, area in points:
        top = max(top, lvl)
        fixed.append((round(exp), round(top, 2), area))
    return fixed


def level(points, exp):
    """monsterexp.cs CutLevel 과 같은 식."""
    x = math.log(max(exp, 1))
    logs = [math.log(e) for e, _, _ in points]
    if x <= logs[0] or x >= logs[-1]:
        (e0, l0, _), (e1, l1, _) = points[0], points[-1]
    else:
        i = next(i for i in range(1, len(points)) if x <= logs[i])
        (e0, l0, _), (e1, l1, _) = points[i - 1], points[i]
    value = l0 + (x - math.log(e0)) * (l1 - l0) / (math.log(e1) - math.log(e0)) if e1 != e0 else l1
    return max(1, min(99, int(round(value))))
