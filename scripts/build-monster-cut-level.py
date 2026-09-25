#!/usr/bin/env python3
"""경험치를 깎을 때 쓰는 괴물 레벨을 괴물의 경험치에서 추정한다(사용자 결정 2026-09-25).

  python3 scripts/build-monster-cut-level.py           # 기준점·대조표·사냥터별 추정 레벨을 보여 준다
  python3 scripts/build-monster-cut-level.py --쓰기     # monsterexp.cs 의 <cut-level> 칸을 새 값으로 고친다

## 왜

괴물 정의가 모두 Level 1 이라, 레벨 차이로 경험치를 깎는 규칙(`Formulas/monsterexp.cs` `ForLevel`)이 7레벨부터
어디서나 깎았다. 사용자: "입장 레벨 생각해서 경험치량으로 비교해 봐" — 입장 레벨이 워프로 알려진 사냥터의 괴물
경험치를 기준으로 "경험치 → 레벨" 을 만들어 모든 괴물에 쓴다. **깎기에만** 쓴다(Template.Level·경험치·금화 식은 그대로).

## 방법

1. 기준 사냥터 셋 — 입장 레벨이 워프 레벨문으로 적혀 있는 곳:
   - 노비스 1~22 (5.99 `warp/Novice_Warp.txt` 줄 끝 두 칸: 마을→평원 1~22, 평원→지하던전 5~22, 던전끼리 10~22)
   - 포테의숲 21~51 (하데스 워프 템플릿 수오미마을→1존·존끼리 21~51 — 5.99 map_create 사본)
   - 아벨해안 51~80 (5.99 `warp/Abel_Warp.txt` 가 해안 안 모든 문에 51~80)
2. **존(맵)마다** 대표 경험치 = 그 존 괴물 경험치의 기하평균(SpawnMax 로 무게 — 한 마리뿐인 보스·사슴이 존을
   끌어올리지 않게). 사용자: "존마다 몬스터 레벨 차이가 좀 날 거야, 경험치량이랑."
3. 사냥터 안에서 존들을 ln(대표 경험치) 순으로 범위에 펼친다 — 가장 낮은 존 = 범위 아래 끝, 가장 높은 존 = 위 끝,
   그 사이는 ln 경험치에 비례. 이 존 점들이 기준점이다.
4. 기준점을 경험치 순으로 늘어놓고 레벨이 줄지 않게 앞 값보다 작으면 앞 값으로 올린다(노비스 맨 위 22 뒤에
   포테의숲 맨 아래 21 이 오는 자리). 그 사이는 ln 경험치 위의 꺾은선, 양 끝 밖은 첫 점과 끝 점을 잇는 전체 기울기로
   뻗는다(끝 구간 기울기로 뻗으면 아벨해안 안의 가파른 기울기 탓에 6만만 넘어도 99 가 된다). 1~99 로 자른다.

## 우드랜드는 따로 (사용자 2026-09-26: "우드랜드도 존별로 차이가 많이 나")

우드랜드는 5.99 팩이 새로 만든 판이라 같은 입장 레벨에서 경험치가 3~4배 적다(2-1 은 11레벨 이상인데 450~590 으로
노비스 1,068~1,849 보다 낮다). 위의 한 줄 대응에 넣으면 단조 증가가 깨지고, 빼면 1 로 떨어진다. 그래서 **우드랜드 자체를
기준 사냥터로** 삼아 구간마다 따로 매긴다 — 구간 안에서 가장 낮은 경험치 괴물 = 구간 아래 끝, 가장 높은 = 위 끝, 사이는
ln 경험치에 비례(괴물마다 제 경험치로). 구간은 5.99 `WoodLand_Warp` 의 입장 레벨(2-1 11 · 3·4-1 21 · 5·6-1 51 · 14-1 81)
이고 위쪽은 다음 구간 입장 −1. 우드랜드 괴물에는 보스·한 마리짜리가 없어(스폰 10~63) 튀는 값이 끌어올리지 않는다.

"""

import argparse
import collections
import json
import math
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
SCRIPT = SERVER / "scripts/Formulas/monsterexp.cs"

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


def block(points, brackets):
    exps = ", ".join(str(e) for e, _, _ in points)
    levels = ", ".join(f"{l}" for _, l, _ in points)
    return "\n".join([
        "        // <cut-level> scripts/build-monster-cut-level.py 가 쓴다 — 손으로 고치지 말고 생성기를 다시 돌린다.",
        "        // 노비스(1~22)·포테의숲(21~51)·아벨해안(51~80) 존마다의 대표 경험치 → 레벨, 경험치 순.",
        f"        private static readonly double[] CutExp = {{ {exps} }};",
        f"        private static readonly double[] CutLevels = {{ {levels} }};",
        "        // 우드랜드 구간(맵 번호들, 가장 낮은·높은 경험치, 아래·위 레벨) — 5.99 WoodLand_Warp 입장 레벨.",
        "        private static readonly (int[] Maps, double LowExp, double HighExp, int Low, int High)[] CutWoodland =",
        "        {",
        *[f"            (new[] {{ {', '.join(map(str, b[0]))} }}, {b[2]}, {b[3]}, {b[4]}, {b[5]}), // {' · '.join(b[1])}" for b in brackets],
        "        };",
        "        // </cut-level>",
    ])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", dest="write", action="store_true")
    args = parser.parse_args()

    rows = list(monsters())
    points = fit(rows)
    brackets = woodland(rows)

    print("## 기준점 (존별 대표 경험치 → 사냥터 범위 안에 경험치 순으로 펼친 레벨)")
    for exp, lvl, area in points:
        print(f"  {area:12s} 경험치 {exp:>7,} → {lvl}")

    print("\n## 기준 사냥터 대조 (존별 괴물 경험치 범위 → 매긴 레벨, 입장 범위 안인가)")
    for prefix, low, high in ANCHORS:
        found = collections.defaultdict(list)
        for area, name, exp, _ in rows:
            if area.startswith(prefix):
                found[area].append(exp)
        for area in sorted(found, key=lambda a: zones(rows, prefix)[a]):
            lo_e, hi_e = min(found[area]), max(found[area])
            lv = sorted({level(points, e) for e in found[area]})
            out = [x for x in lv if not low <= x <= high]
            print(f"  {area:12s} {low}~{high}: 경험치 {lo_e:,}~{hi_e:,} → {lv[0]}~{lv[-1]}" + (f"  (벗어남 {out})" if out else ""))

    print("\n## 우드랜드 (구간마다 따로 — 괴물별 경험치 → 레벨)")
    for bracket in brackets:
        print(f"  {' · '.join(bracket[1])} {bracket[4]}~{bracket[5]} — {bracket[6]}")
        for area in bracket[1]:
            kinds = sorted({(n, e) for a, n, e, _ in rows if a == area}, key=lambda k: k[1])
            if kinds:
                print(f"    {area:10s} " + " · ".join(f"{n} {e:,}→{woodland_level(bracket, e)}" for n, e in kinds))

    print("\n## 사냥터별 추정 레벨 (최소·가운데·최대 경험치)")
    grounds = collections.defaultdict(set)
    for area, name, exp, _ in rows:
        grounds[re.sub(r"[\d\-A-Za-z]+$", "", area.replace("존", "")) if not area.startswith("우드랜드") else area].add((name, exp))
    for ground, kinds in sorted(grounds.items(), key=lambda g: sorted(e for _, e in g[1])[len(g[1]) // 2]):
        exps = sorted(e for _, e in kinds)
        picks = [exps[0], exps[len(exps) // 2], exps[-1]]
        print(f"  {ground:12s} " + " · ".join(f"{e:,}→{level_for(points, brackets, ground, e)}" for e in picks))

    text = SCRIPT.read_text(encoding="utf-8-sig")
    new = re.sub(r"        // <cut-level>.*?// </cut-level>", block(points, brackets), text, flags=re.S)
    if new == text:
        print("\nmonsterexp.cs 는 이미 이 값이다.")
    elif args.write:
        SCRIPT.write_text(new, encoding="utf-8-sig")
        print("\nmonsterexp.cs 의 <cut-level> 칸을 고쳤다.")
    else:
        print("\n--쓰기 를 붙이면 monsterexp.cs 의 <cut-level> 칸을 고친다.")


if __name__ == "__main__":
    main()
