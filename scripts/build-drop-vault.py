#!/usr/bin/env python3
"""괴물이 무엇을 · 얼마나 · 어디서 떨구는지 Obsidian vault + graphify 그래프로 남긴다.

**드랍 표는 따로 있지 않다.** 괴물 정의의 `Drops`·`LootType`(하나를 고른 뒤 그 아이템의
`DropRate` 를 굴린다)과 `Formulas/monsterexp.cs` 의 식이 합쳐진 것이 드랍 표다. 매번 JSON 을
다시 뒤지지 않도록 한 번 계산해 적어 둔다.

  노트: 사냥터(맵) · 괴물(경험치·골드 범위·드랍 목록과 실제 확률·스폰 맵) · 아이템(누가 어디서
        몇 %로 떨구나 · 어느 상점이 파나) · 식(monsterexp.cs 근거 줄)
  간선: 사냥터 --스폰--> 괴물 --드랍--> 아이템 <--판다-- 상점, 식 --정한다--> 괴물·아이템

**범위**: 몬스터 정의가 있는 맵 전부(사냥터 노트) · 그 괴물 전부. 아이템은 **몬스터가 실제로
떨구거나 상점이 실제로 파는 것만** — 아무도 안 쓰는 "하데스표" 변형 아이템 900여 종은 뺀다
(노트가 잡일로 덮이면 못 쓰게 된다).

  쓰는 법: python3 scripts/build-drop-vault.py           → data/drop-vault/ (Obsidian)
           python3 scripts/build-drop-vault.py --그래프    → 위에 더해 data/drop-vault/graph/graph.json
"""
import json
import re
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FORK = ROOT / "sources/wren11/Dark-Ages-Private-Server"
SERVER = FORK / "database/server"
MONSTERS = SERVER / "templates/monsters"
ITEMS = SERVER / "templates/items"
MUNDANES = SERVER / "templates/mundanes"
FORMULA = SERVER / "scripts/Formulas/monsterexp.cs"
VAULT = ROOT / "data" / "drop-vault"

# `Formulas/monsterexp.cs` 의 값을 그대로 되풀이한다 — 바뀌면 여기도 다시 만든다.
GOLD_PER_EXP = 0.1          # 노비스 밖 (2026-09-25, 0.02 의 다섯 배)
NOVICE_GOLD_PER_EXP = 0.02  # 노비스 맵은 그대로
GOLD_VARIANCE = 0.2


def is_novice(area_id):
    """monsterexp.cs IsNovice 와 같은 맵 번호 — areas/ 에서 이름이 "노비스"로 시작하는 맵."""
    return 20083 <= area_id <= 20086 or 20373 <= area_id <= 20394


def gold_rate(area_id):
    return NOVICE_GOLD_PER_EXP if is_novice(area_id) else GOLD_PER_EXP
LOOT_RANDOM, LOOT_TABLE, LOOT_GOLD = 2, 4, 32


def _cut_module():
    """깎기용 괴물 레벨 — `scripts/build-monster-cut-level.py` 와 같은 기준점·식을 쓴다(같은 코드를 불러온다)."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("cut_level", Path(__file__).resolve().parent / "build-monster-cut-level.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


CUT = _cut_module()
CUT_ROWS = list(CUT.monsters())
CUT_POINTS = CUT.fit(CUT_ROWS)
CUT_WOODS = CUT.woodland(CUT_ROWS)
FORGIVEN, HALVING, LEAST = 5, 5, 0.02  # monsterexp.cs ForLevel 의 상수 — 바뀌면 여기도 고친다


def cut_level(exp, area=None):
    return CUT.level_for(CUT_POINTS, CUT_WOODS, area, exp)


def cut_basis(exp, area=None):
    """어느 기준점 사이에서 보간했나 — 사람이 읽는 한 줄."""
    import math
    for ids, maps, e0, e1, low, high, why in CUT_WOODS:
        if area in ids or area in maps:
            return (f"우드랜드 구간 {low}~{high}({' · '.join(maps)}) 안 — 구간의 가장 낮은 경험치 {e0:,}→{low}, "
                    f"가장 높은 {e1:,}→{high} 사이 보간. 근거: {why}")
    x = math.log(max(exp, 1))
    logs = [math.log(e) for e, _, _ in CUT_POINTS]
    if x <= logs[0] or x >= logs[-1]:
        (e0, l0, a0), (e1, l1, a1) = CUT_POINTS[0], CUT_POINTS[-1]
        side = "아래" if x <= logs[0] else "위"
        return f"기준점 {side} 끝 밖 — 첫 점 {a0}({e0:,}→{l0})과 끝 점 {a1}({e1:,}→{l1})을 잇는 기울기로 뻗음"
    i = next(i for i in range(1, len(CUT_POINTS)) if x <= logs[i])
    (e0, l0, a0), (e1, l1, a1) = CUT_POINTS[i - 1], CUT_POINTS[i]
    return f"{a0}({e0:,}→{l0}) 와 {a1}({e1:,}→{l1}) 사이 보간"


def cut_share(gap):
    """monsterexp.cs ForLevel — 레벨 차이 gap 에서 실제로 받는 몫."""
    if gap <= FORGIVEN:
        return 1.0
    return max(LEAST, 0.5 ** ((gap - FORGIVEN) / HALVING))


def entry_levels():
    """맵 이름 → 그 맵으로 들어오는 워프의 레벨문 [(최소, 최대, 출처)] — 5.99 warps.json 줄 끝 두 칸 + 하데스 워프 템플릿."""
    gates = {}
    warps = ROOT / "data/server-packs/extracted/5.99-server/warps.json"
    if warps.exists():
        for w in json.loads(warps.read_text(encoding="utf-8")):
            lo, hi = int(w["raw"][-2]), int(w["raw"][-1])
            if (lo, hi) != (0, 99):
                gates.setdefault(w["도착맵"], set()).add((lo, hi, f"5.99 {w['출처']}"))
    names = {}
    for path in (SERVER / "areas").glob("*.json"):
        d = lenient(path)
        if d and "ID" in d:
            names[d["ID"]] = d.get("Name")
    for path in (SERVER / "templates/warps").glob("*.json"):
        d = lenient(path)
        if not d:
            continue
        lo, hi = d.get("LevelRequired") or 0, d.get("LevelMaximum") or 99
        if lo > 1 or hi < 99:
            name = names.get((d.get("To") or {}).get("AreaID"))
            if name:
                gates.setdefault(name, set()).add((lo, hi, "하데스 워프 템플릿"))
    return gates

BANNED = re.compile(r'[\\/:*?"<>|#\[\]^]')


def slug(name):
    return BANNED.sub("_", name or "").strip() or "_"


def lenient(path):
    """하데스가 손으로 쓴 정의엔 꼬리 쉼표가 남는다. 못 읽는 것은 건너뛴다."""
    text = path.read_text(encoding="utf-8-sig", errors="ignore")
    text = re.sub(r",\s*([}\]])", r"\1", text)
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        return None


def dropped(monster):
    drops = monster.get("Drops")
    values = drops.get("$values") if isinstance(drops, dict) else drops
    return [n for n in (values or []) if isinstance(n, str) and n and n != "random"]


def monster_exp(m):
    if m.get("Exp") is not None:
        return int(m["Exp"])
    level = m.get("Level") or 1
    return int(level * (level * 0.1 + 1.5) * 300)


def gold_range(exp, area_id):
    rate = gold_rate(area_id)
    lo = int(exp * rate * (1 - GOLD_VARIANCE))
    hi = int(exp * rate * (1 + GOLD_VARIANCE)) + 1
    return lo, hi


def zone_name(area_id, files_here):
    """파일 이름 `<괴물>@<구역>.json` 의 구역 쪽. 없으면 AreaID 뿐."""
    for f in files_here:
        if "@" in f.stem:
            return f.stem.split("@", 1)[1]
    return f"맵{area_id}"


def load_all():
    monsters, items, mundanes = [], {}, []

    for path in sorted(MONSTERS.rglob("*.json")):
        d = lenient(path)
        if d and isinstance(d, dict) and "AreaID" in d:
            d["_file"] = path
            monsters.append(d)

    for path in sorted(ITEMS.rglob("*.json")):
        d = lenient(path)
        if d and isinstance(d, dict) and d.get("Name"):
            items[d["Name"]] = (path, d)

    for path in sorted(MUNDANES.rglob("*.json")):
        d = lenient(path)
        if d and isinstance(d, dict) and d.get("DefaultMerchantStock"):
            d["_file"] = path
            mundanes.append(d)

    return monsters, items, mundanes


def slot_kind(item):
    if (item.get("EquipmentSlot") or 0) > 0:
        return "장비"
    if item.get("ScriptName") == "Consumable":
        return "소모품"
    return "잡템"


def bundle_text(item):
    """겹쳐지는 소모품(Consumable 256 | Stackable 128)은 1~3개 묶음으로 떨어진다(`monsterexp.cs` BundleSize,
    2026-09-26). 나머지는 하나."""
    both = 256 | 128
    return "1~3개 묶음" if ((item.get("Flags") or 0) & both) == both else "1개"


def real_rate(item, listed_len, loot_type):
    """`DetermineRandomDrop` 그대로 — 목록의 DropRate 를 이어 붙인 줄(길이 = 칸수)에서 한 점을 뽑는다.
    그래서 한 물건의 확률은 DropRate ÷ 칸수 이고, DropRate 가 1 을 넘어도 그대로다(2026-09-26 부터).
    Table 갈래는 가중치 추첨이라 이 나눗셈이 안 맞으므로 `None`."""
    if loot_type & LOOT_TABLE:
        return None
    if not listed_len:
        return 0.0
    return (item.get("DropRate") or 0) / listed_len


def build_notes(monsters, items, mundanes):
    if VAULT.exists():
        shutil.rmtree(VAULT)
    for sub in ("사냥터", "괴물", "아이템", "식"):
        (VAULT / sub).mkdir(parents=True)

    by_area = {}
    for m in monsters:
        by_area.setdefault(m["AreaID"], []).append(m)

    stocked_by = {}  # 아이템 이름 -> [상점 이름]
    for shop in mundanes:
        for name in shop["DefaultMerchantStock"]:
            stocked_by.setdefault(name, []).append(shop["Name"])

    dropped_by = {}  # 아이템 이름 -> [(괴물note, 사냥터note, 실제확률 or None)]
    monster_notes = {}  # (이름, area) -> note 이름

    # 1) 괴물 노트 + 사냥터별 목록
    zone_rows = {}
    for area, here in sorted(by_area.items()):
        zname = zone_name(area, [m["_file"] for m in here])
        zone_rows[area] = (zname, [])

        for m in here:
            note = f"{m['Name']}@{zname}"
            monster_notes[(m["Name"], area)] = note
            exp = monster_exp(m)
            lo, hi = gold_range(exp, area)
            lvl = cut_level(exp, area)
            loot_type = m.get("LootType") or 0
            names = dropped(m)

            rows = []
            for name in names:
                item = items.get(name)
                if item is None:
                    rows.append((name, "**정의 없음 — 영영 안 나옴**", "?", "?"))
                    continue
                rate = real_rate(item[1], len(names), loot_type)
                pct = "표(가중치) 추첨" if rate is None else f"{rate:.2%}"
                rows.append((name, pct, slot_kind(item[1]), bundle_text(item[1])))
                dropped_by.setdefault(name, []).append((note, f"{area}-{zname}", rate))

            drop_table = "\n".join(
                f"| [[아이템/{slug(n)}\\|{n}]] | {p} | {c} | {k} |" for n, p, k, c in rows
            ) or "| (없음) | | | |"

            (VAULT / "괴물" / f"{slug(note)}.md").write_text(
                "---\n"
                f'이름: "{m["Name"]}"\nAreaID: {area}\n사냥터: "{zname}"\n'
                f'경험치: {exp}\n골드범위: "{lo}~{hi}"\nLootType: {loot_type}\n'
                f'체력: {m.get("MaximumHP", 0)}\n'
                f"추정레벨: {lvl}\n깎이기시작: {lvl + FORGIVEN + 1}\n"
                "---\n\n"
                f"# {m['Name']} @ {zname}\n\n"
                f"사냥터: [[사냥터/{slug(f'{area}-{zname}')}|{zname}]]\n\n"
                f"경험치 {exp} · 골드 {lo}~{hi}전(경험치×{gold_rate(area)}, ±{int(GOLD_VARIANCE*100)}%, "
                f"항상 지급 — [[식/골드-경험치식]]) · LootType {loot_type}\n\n"
                "## 경험치 깎기\n\n"
                f"추정 레벨 **{lvl}** (깎기에만 쓴다 — [[식/경험치-레벨-대응]]): {cut_basis(exp, area)}.\n\n"
                f"**{lvl + FORGIVEN + 1}레벨부터** 경험치가 깎인다([[식/경험치-깎기]]) — "
                + " · ".join(f"{lvl + g}레벨 {round(exp * cut_share(g)):,}" for g in (6, 10, 15, 20, 30)) + "\n\n"
                "## 드랍 목록 (실제 확률 = DropRate ÷ 목록 칸수)\n\n"
                "| 아이템 | 실제 확률 | 한 번에 | 갈래 |\n|---|---|---|---|\n" + drop_table + "\n",
                encoding="utf-8")
            zone_rows[area][1].append((m["Name"], note, exp, lo, hi, lvl))

    # 2) 사냥터 노트
    gates = entry_levels()
    for area, (zname, rows) in sorted(zone_rows.items()):
        table = "\n".join(
            f"| [[괴물/{slug(note)}\\|{name}]] | {exp} | {lvl} | {lo}~{hi} |"
            for name, note, exp, lo, hi, lvl in rows
        ) or "| (없음) | | | |"
        exps = [r[2] for r in rows] or [0]
        lvls = [r[5] for r in rows] or [0]
        entry = "\n".join(f"- {a}~{b} ({src})" for a, b, src in sorted(gates.get(zname, []))) \
            or "- 워프 레벨문 없음"
        (VAULT / "사냥터" / f"{slug(f'{area}-{zname}')}.md").write_text(
            "---\n"
            f'AreaID: {area}\n사냥터: "{zname}"\n괴물수: {len(rows)}\n'
            f'경험치범위: "{min(exps)}~{max(exps)}"\n추정레벨범위: "{min(lvls)}~{max(lvls)}"\n'
            "---\n\n"
            f"# {zname} (AreaID {area})\n\n"
            f"경험치 {min(exps):,}~{max(exps):,} → 추정 레벨 {min(lvls)}~{max(lvls)} ([[식/경험치-레벨-대응]])\n\n"
            "## 입장 레벨 (이 맵으로 들어오는 워프의 레벨문)\n\n" + entry + "\n\n"
            "## 스폰\n\n"
            "| 괴물 | 경험치 | 추정레벨 | 골드범위 |\n|---|---|---|---|\n" + table + "\n",
            encoding="utf-8")

    # 3) 아이템 노트 — 몬스터가 떨구거나 상점이 파는 것만
    relevant = sorted(set(dropped_by) | set(stocked_by))
    for name in relevant:
        found = items.get(name)
        drops_rows = "\n".join(
            f"| [[괴물/{slug(note)}\\|{note}]] | {zone} | {'표 추첨' if rate is None else f'{rate:.2%}'} |"
            for note, zone, rate in dropped_by.get(name, [])
        ) or "| (없음) | | |"
        shops_rows = "\n".join(f"- {s}" for s in stocked_by.get(name, [])) or "- (없음)"

        if found is None:
            kind, value, level = "?", "?", "?"
        else:
            item = found[1]
            kind, value, level = slot_kind(item), item.get("Value", 0), item.get("LevelRequired", 0)

        (VAULT / "아이템" / f"{slug(name)}.md").write_text(
            "---\n"
            f'이름: "{name}"\n갈래: "{kind}"\n값: {value}\n레벨: {level}\n'
            "---\n\n"
            f"# {name}\n\n갈래 {kind} · 값 {value}전 · 레벨제한 {level}\n\n"
            "## 어느 괴물이 떨구나\n\n"
            "| 괴물 | 사냥터 | 실제 확률 |\n|---|---|---|\n" + drops_rows + "\n\n"
            "## 어느 상점이 파나\n\n" + shops_rows + "\n",
            encoding="utf-8")

    # 4) 식 노트 — monsterexp.cs 근거 줄을 그대로 인용한다(다시 만들 때마다 최신 줄로 갱신됨)
    write_formula_note()
    write_cut_notes()

    # README
    zone_index = "\n".join(
        f"| [[사냥터/{slug(f'{a}-{z}')}\\|{z}]] | {a} | {len(rows)} |"
        for a, (z, rows) in sorted(zone_rows.items())
    )
    (VAULT / "README.md").write_text(
        "# 드랍 볼트 — 무엇이 어디서 얼마나 떨어지나\n\n"
        "드랍 표는 따로 없다. 괴물 정의의 `Drops`·`LootType` 과 `Formulas/monsterexp.cs` 식이 합쳐진 "
        "것이 드랍 표다 — 이 볼트는 그것을 한 번 계산해 둔 것이다. 낡으면 "
        "`python3 scripts/build-drop-vault.py` 로 다시 만든다.\n\n"
        f"사냥터 {len(zone_rows)}개 · 괴물 {len(monster_notes)}종 · "
        f"아이템 {len(relevant)}종(몬스터가 떨구거나 상점이 파는 것만 — 아무도 안 쓰는 "
        "\"하데스표\" 변형 900여 종은 뺐다).\n\n"
        "식: [[식/골드-경험치식]] · [[식/경험치-깎기]] · [[식/경험치-레벨-대응]]\n\n"
        "## 사냥터\n\n| 사냥터 | AreaID | 괴물수 |\n|---|---|---|\n" + zone_index + "\n",
        encoding="utf-8")

    return len(zone_rows), len(monster_notes), len(relevant)


def write_formula_note():
    text = FORMULA.read_text(encoding="utf-8")
    lines = text.splitlines()

    def excerpt(pattern, context=1):
        for i, line in enumerate(lines):
            if re.search(pattern, line):
                lo, hi = max(0, i - context), min(len(lines), i + context + 25)
                return i + 1, "\n".join(lines[lo:hi])
        return None, ""

    gold_line, gold_src = excerpt(r"private void GenerateGold")
    exp_line, exp_src = excerpt(r"private int MonsterExp")

    (VAULT / "식" / "골드-경험치식.md").write_text(
        "---\n제목: 골드-경험치식\n파일: \"database/server/scripts/Formulas/monsterexp.cs\"\n---\n\n"
        "# 골드는 경험치에 비례한다\n\n"
        "**2026-09-24 사용자 결정.** 원작에도 하데스에도 \"몬스터 레벨\" 이라는 값이 없어(서버팩 3개·"
        "원작 아카이브·참고저장소 16개를 다 뒤져 확인) 레벨 대신 경험치를 쓴다.\n\n"
        f"금화 = 경험치 × {GOLD_PER_EXP} × (0.8~1.2 무작위), 항상 지급 — **노비스 맵은 × {NOVICE_GOLD_PER_EXP}**. "
        f"{NOVICE_GOLD_PER_EXP} 는 노비스 괴물 11마리의 경험치(1,068~1,849)와 그때 금화(20~30)에서 역산한 값이고, "
        "**2026-09-25 사용자 결정**(\"포테 3존인데 47원씩 — 금전이 너무 적다\")으로 노비스 밖은 그 다섯 배로 올렸다. "
        "노비스 맵 번호: 20083~20086 · 20373~20394 (areas/ 에서 이름이 \"노비스\"로 시작하는 맵).\n\n"
        "금화는 쓰러진 자리 바닥에 놓이고, 주워야 들어온다(2026-09-25 — 서버가 대신 줍던 AUTO LOOT GOLD 를 껐다).\n\n"
        f"## 근거 — `monsterexp.cs:{gold_line}`\n\n```csharp\n{gold_src}\n```\n\n"
        f"## 경험치를 읽는 곳(같은 값을 되풀이) — `monsterexp.cs:{exp_line}`\n\n```csharp\n{exp_src}\n```\n\n"
        "같이 볼 식: [[경험치-깎기]] · [[경험치-레벨-대응]]\n\n"
        "시험: [[../README|드랍 볼트]] 의 괴물 노트마다 있는 골드범위 칸이 이 식의 결과다. "
        "`tests/hades-characterization/MonsterGoldTests.cs` 가 실제 서버로 확인한다.\n",
        encoding="utf-8")


def write_cut_notes():
    lines = FORMULA.read_text(encoding="utf-8-sig").splitlines()

    def excerpt(pattern, after):
        for i, line in enumerate(lines):
            if re.search(pattern, line):
                return i + 1, "\n".join(lines[i:i + after])
        return None, ""

    cut_line, cut_src = excerpt(r"private double ForLevel", 11)
    use_line, use_src = excerpt(r"public uint DistributeExperience", 1)
    ratio = "\n".join(f"| {g} | {cut_share(g):.1%} |" for g in (0, 5, 6, 8, 10, 15, 20, 25, 30, 40, 50, 60))
    (VAULT / "식" / "경험치-깎기.md").write_text(
        "---\n제목: 경험치-깎기\n파일: \"database/server/scripts/Formulas/monsterexp.cs\"\n---\n\n"
        "# 저보다 한참 낮은 괴물은 경험치를 덜 준다\n\n"
        f"레벨 차이(내 레벨 − 괴물 추정 레벨)가 {FORGIVEN} 까지는 그대로, 그 뒤로 {HALVING} 레벨마다 반, "
        f"{LEAST:.0%} 에서 멈춘다. 괴물 레벨은 정의의 `Level`(모두 1) 이 아니라 경험치에서 추정한 값 — "
        "[[경험치-레벨-대응]]. 알림 \"경험치가 N 올랐습니다\" 의 N 은 깎인 뒤의 값이다(2026-09-25).\n\n"
        "| 레벨 차이 | 받는 몫 |\n|---|---|\n" + ratio + "\n\n"
        f"## 근거 — `monsterexp.cs:{cut_line}`\n\n```csharp\n{cut_src}\n```\n\n"
        f"## 쓰는 곳 — `monsterexp.cs:{use_line}`\n\n```csharp\n{use_src}\n```\n\n"
        "같이 볼 식: [[골드-경험치식]] · [[경험치-레벨-대응]]\n\n"
        "시험: `tests/hades-characterization/ExperienceNoticeTests.cs` — 알림 = '다음까지' 줄어든 양 = 저장값, "
        "깎이는지·안 깎이는지.\n",
        encoding="utf-8")

    points = "\n".join(f"| {a} | {e:,} | {l} |" for e, l, a in CUT_POINTS)
    grounds = {}
    for area, name, exp, _ in CUT_ROWS:
        key = area if area.startswith("우드랜드") else re.sub(r"[\d\-A-Za-z]+$", "", area.replace("존", ""))
        grounds.setdefault(key, set()).add(exp)
    summary = "\n".join(
        f"| {g} | {min(v):,}~{max(v):,} | {cut_level(min(v), g)}~{cut_level(max(v), g)} |"
        for g, v in sorted(grounds.items(), key=lambda kv: sorted(kv[1])[len(kv[1]) // 2]))
    (VAULT / "식" / "경험치-레벨-대응.md").write_text(
        "---\n제목: 경험치-레벨-대응\n생성기: \"scripts/build-monster-cut-level.py\"\n---\n\n"
        "# 괴물의 경험치로 레벨을 추정한다 (깎기에만)\n\n"
        "괴물 정의가 모두 `Level` 1 이라, 입장 레벨이 워프로 알려진 사냥터의 경험치를 기준으로 레벨을 매긴다"
        "(사용자 2026-09-25: \"입장 레벨 생각해서 경험치량으로 비교해 봐\" · \"존마다 몬스터 레벨 차이가 좀 날 거야\"). "
        "체력·능력치(`Template.Level`)·경험치·금화 식은 그대로다.\n\n"
        "## 방법\n\n"
        "1. 기준 사냥터: 노비스 1~22 (5.99 `Novice_Warp`) · 포테의숲 21~51 (하데스 워프 템플릿) · 아벨해안 51~80 (5.99 `Abel_Warp`).\n"
        "2. 존(맵)마다 대표 경험치 = 괴물 경험치의 기하평균(SpawnMax 무게).\n"
        "3. 사냥터 안에서 존들을 ln(경험치) 순으로 범위에 펼친다 — 가장 낮은 존 = 아래 끝, 가장 높은 존 = 위 끝.\n"
        "4. 레벨이 줄지 않게 앞 값보다 작으면 앞 값으로 올린다. 사이는 ln(경험치) 위 꺾은선, 양 끝 밖은 첫 점·끝 점을 "
        "잇는 기울기로 뻗고 1~99 로 자른다.\n\n"
        "## 우드랜드는 구간마다 따로\n\n"
        "5.99 팩이 새로 만든 판이라 같은 입장 레벨에서 경험치가 3~4배 적어 위 대응에 못 넣는다(사용자 2026-09-26: "
        "\"우드랜드도 존별로 차이가 많이 나\"). 구간 안에서 가장 낮은 경험치 괴물 = 아래 끝, 가장 높은 = 위 끝, 사이는 "
        "ln 경험치에 비례.\n\n| 구간 | 레벨 | 경험치 | 근거 |\n|---|---|---|---|\n"
        + "\n".join(f"| {' · '.join(maps)} | {lo}~{hi} | {e0:,}~{e1:,} | {why} |" for _, maps, e0, e1, lo, hi, why in CUT_WOODS)
        + "\n\n"
        "## 기준점\n\n| 존 | 대표 경험치 | 레벨 |\n|---|---|---|\n" + points + "\n\n"
        "## 사냥터별 추정 레벨\n\n| 사냥터 | 경험치 | 추정 레벨 |\n|---|---|---|\n" + summary + "\n\n"
        "같이 볼 식: [[경험치-깎기]] · [[골드-경험치식]]\n",
        encoding="utf-8")


def build_graph(zones, monsters, items_count):
    from graphify_runtime import configure_utf8_stdio, execute_graphify_script, find_graphify_python

    configure_utf8_stdio(sys.stdout, sys.stderr)

    try:
        import graphify  # noqa: F401
    except ImportError:
        try:
            py = find_graphify_python()
        except RuntimeError as exc:
            sys.exit(str(exc))
        raise SystemExit(execute_graphify_script(py, Path(__file__).resolve(), sys.argv[1:]))

    from graphify.build import build_from_json
    from graphify.cluster import cluster, score_all
    from graphify.analyze import god_nodes, surprising_connections, suggest_questions
    from graphify.export import to_json, to_html
    from graphify.report import generate

    nodes, edges, seen = [], [], set()

    def node(kind, key, label=None):
        nid = f"{kind}:{key}"
        if nid not in seen:
            seen.add(nid)
            nodes.append({"id": nid, "label": label or key, "type": kind,
                          "source_file": "data/drop-vault", "confidence": "EXTRACTED"})
        return nid

    def edge(a, b, relation):
        edges.append({"source": a, "target": b, "relation": relation,
                      "confidence": "EXTRACTED", "source_file": "data/drop-vault"})

    formula = node("식", "골드-경험치식", f"골드=경험치×{GOLD_PER_EXP}(노비스 ×{NOVICE_GOLD_PER_EXP})×0.8~1.2")
    cutting = node("식", "경험치-깎기", f"경험치 깎기 — 레벨 차이 {FORGIVEN} 넘으면 {HALVING}레벨마다 반, {LEAST:.0%}까지")
    mapping = node("식", "경험치-레벨-대응", "괴물 경험치 → 추정 레벨(노비스·포테의숲·아벨해안 존별 기준점 · 우드랜드 구간별)")
    edge(mapping, cutting, "정한다")

    all_mons, all_items, all_mund = load_all()
    by_area = {}
    for m in all_mons:
        by_area.setdefault(m["AreaID"], []).append(m)

    stocked_by = {}
    for shop in all_mund:
        shop_id = node("상점", shop["Name"])
        for name in shop["DefaultMerchantStock"]:
            stocked_by.setdefault(name, []).append(shop_id)

    item_nodes = {}
    for area, here in by_area.items():
        zname = zone_name(area, [m["_file"] for m in here])
        lvls = [cut_level(monster_exp(m), area) for m in here]
        zone_id = node("사냥터", f"{area}-{zname}", f"{zname} 괴물 레벨 {min(lvls)}~{max(lvls)}")
        for m in here:
            exp = monster_exp(m)
            mon_id = node("괴물", f"{m['Name']}@{area}", f"{m['Name']} 경험치 {exp} 레벨 {cut_level(exp, area)}")
            edge(zone_id, mon_id, "스폰")
            edge(formula, mon_id, "정한다")
            edge(mapping, mon_id, f"추정레벨 {cut_level(exp, area)}")
            for name in dropped(m):
                item = all_items.get(name)
                if item is None:
                    continue
                if name not in item_nodes:
                    item_nodes[name] = node("아이템", name)
                edge(mon_id, item_nodes[name], "드랍")

    for name, shops in stocked_by.items():
        if name not in item_nodes:
            item_nodes[name] = node("아이템", name)
        for shop_id in shops:
            edge(shop_id, item_nodes[name], "판다")

    data = {"nodes": nodes, "edges": edges}
    out = VAULT / "graph"
    out.mkdir(parents=True, exist_ok=True)

    G = build_from_json(data, root=str(VAULT))
    communities = cluster(G)
    cohesion = score_all(G, communities)
    gods = god_nodes(G)
    surprises = surprising_connections(G, communities)

    def attr(n, key, default=""):
        return G.nodes.get(n, {}).get(key, default)

    labels = {}
    for cid, members in communities.items():
        members = [m for m in members if m in G]
        if not members:
            continue
        kinds = {}
        for m in members:
            k = attr(m, "type") or "?"
            kinds[k] = kinds.get(k, 0) + 1
        hub = max(members, key=lambda m: G.degree(m))
        labels[cid] = f"{attr(hub, 'label') or hub} 둘레 {max(kinds, key=kinds.get)}"

    questions = suggest_questions(G, communities, labels)
    to_json(G, communities, str(out / "graph.json"), community_labels=labels, force=True)
    try:
        to_html(G, communities, str(out / "graph.html"), community_labels=labels, node_limit=5000)
    except ValueError as exc:
        print(f"   (그림 건너뜀: {exc})")

    detection = {"total_files": 1, "total_words": 0, "files": {"document": ["data/drop-vault"]}}
    (out / "GRAPH_REPORT.md").write_text(
        generate(G, communities, cohesion, labels, gods, surprises, detection,
                 {"input": 0, "output": 0}, str(VAULT), suggested_questions=questions),
        encoding="utf-8")

    print(f"드랍 그래프 — 노드 {G.number_of_nodes():,} · 간선 {G.number_of_edges():,} · 군집 {len(communities)}")
    for g in gods[:6]:
        print(f"   중심 {g.get('label', g.get('node', g.get('id', '?')))} — "
              f"이어진 것 {g.get('degree', g.get('edges', '?'))}")
    print(f"-> {out.relative_to(ROOT)}/graph.json")


def main():
    monsters, items, mundanes = load_all()
    n_zones, n_monsters, n_items = build_notes(monsters, items, mundanes)
    print(f"드랍 볼트 — 사냥터 {n_zones} · 괴물 {n_monsters} · 아이템 {n_items} -> {VAULT.relative_to(ROOT)}/")

    if "--그래프" in sys.argv:
        build_graph(n_zones, monsters, n_items)


if __name__ == "__main__":
    main()
