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
GOLD_PER_EXP = 0.02
GOLD_VARIANCE = 0.2
LOOT_RANDOM, LOOT_TABLE, LOOT_GOLD = 2, 4, 32

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


def gold_range(exp):
    lo = int(exp * GOLD_PER_EXP * (1 - GOLD_VARIANCE))
    hi = int(exp * GOLD_PER_EXP * (1 + GOLD_VARIANCE)) + 1
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


def real_rate(item, listed_len, loot_type):
    """`DetermineRandomDrop` 그대로 — 목록에서 하나를 고른 뒤 그 아이템의 DropRate 를 굴린다.
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
            lo, hi = gold_range(exp)
            loot_type = m.get("LootType") or 0
            names = dropped(m)

            rows = []
            for name in names:
                item = items.get(name)
                if item is None:
                    rows.append((name, "**정의 없음 — 영영 안 나옴**", "?"))
                    continue
                rate = real_rate(item[1], len(names), loot_type)
                pct = "표(가중치) 추첨" if rate is None else f"{rate:.2%}"
                rows.append((name, pct, slot_kind(item[1])))
                dropped_by.setdefault(name, []).append((note, f"{area}-{zname}", rate))

            drop_table = "\n".join(
                f"| [[아이템/{slug(n)}\\|{n}]] | {p} | {k} |" for n, p, k in rows
            ) or "| (없음) | | |"

            (VAULT / "괴물" / f"{slug(note)}.md").write_text(
                "---\n"
                f'이름: "{m["Name"]}"\nAreaID: {area}\n사냥터: "{zname}"\n'
                f'경험치: {exp}\n골드범위: "{lo}~{hi}"\nLootType: {loot_type}\n'
                f'체력: {m.get("MaximumHP", 0)}\n'
                "---\n\n"
                f"# {m['Name']} @ {zname}\n\n"
                f"사냥터: [[사냥터/{slug(f'{area}-{zname}')}|{zname}]]\n\n"
                f"경험치 {exp} · 골드 {lo}~{hi}전(경험치×{GOLD_PER_EXP}, ±{int(GOLD_VARIANCE*100)}%, "
                f"항상 지급 — [[식/골드-경험치식]]) · LootType {loot_type}\n\n"
                "## 드랍 목록 (실제 확률 = DropRate ÷ 목록 칸수)\n\n"
                "| 아이템 | 실제 확률 | 갈래 |\n|---|---|---|\n" + drop_table + "\n",
                encoding="utf-8")
            zone_rows[area][1].append((m["Name"], note, exp, lo, hi))

    # 2) 사냥터 노트
    for area, (zname, rows) in sorted(zone_rows.items()):
        table = "\n".join(
            f"| [[괴물/{slug(note)}\\|{name}]] | {exp} | {lo}~{hi} |"
            for name, note, exp, lo, hi in rows
        ) or "| (없음) | | |"
        (VAULT / "사냥터" / f"{slug(f'{area}-{zname}')}.md").write_text(
            "---\n"
            f'AreaID: {area}\n사냥터: "{zname}"\n괴물수: {len(rows)}\n'
            "---\n\n"
            f"# {zname} (AreaID {area})\n\n"
            "## 스폰\n\n"
            "| 괴물 | 경험치 | 골드범위 |\n|---|---|---|\n" + table + "\n",
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
        "식: [[식/골드-경험치식]]\n\n"
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
        f"금화 = 경험치 × {GOLD_PER_EXP} × (0.8~1.2 무작위), 항상 지급. 비율 {GOLD_PER_EXP} 는 노비스 "
        "괴물 11마리의 경험치(1,068~1,849)와 지금 금화(20~30)에서 역산한 값이다.\n\n"
        f"## 근거 — `monsterexp.cs:{gold_line}`\n\n```csharp\n{gold_src}\n```\n\n"
        f"## 경험치를 읽는 곳(같은 값을 되풀이) — `monsterexp.cs:{exp_line}`\n\n```csharp\n{exp_src}\n```\n\n"
        "시험: [[../README|드랍 볼트]] 의 괴물 노트마다 있는 골드범위 칸이 이 식의 결과다. "
        "`tests/hades-characterization/MonsterGoldTests.cs` 가 실제 서버로 확인한다.\n",
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

    formula = node("식", "골드-경험치식", "골드=경험치×0.02×0.8~1.2")

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
        zone_id = node("사냥터", f"{area}-{zname}", zname)
        for m in here:
            mon_id = node("괴물", f"{m['Name']}@{area}", m["Name"])
            edge(zone_id, mon_id, "스폰")
            edge(formula, mon_id, "정한다")
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
