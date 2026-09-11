#!/usr/bin/env python3
"""추출한 JSON 을 지식 그래프로 바꾼다 — 팩마다 따로.

graphify 는 .txt 를 산문으로 보고 LLM 으로 뜻을 뽑는다. 그런데 db/ 는 산문이 아니라
정확한 표다. 표에 LLM 을 돌리면 값도 비싸고 결과는 파싱보다 부정확하다.
그래서 뽑는 일은 여기서 정확히 하고, graphify 에는 그 결과를 그대로 넘긴다 —
군집 찾기·중심 노드·HTML 은 graphify 가 하고, 간선은 자료가 정한다. LLM 은 쓰지 않는다.

  쓰는 법: python3 scripts/build-server-pack-graph.py
  (graphify 가 깔린 파이썬으로 자동으로 다시 실행한다)
"""
import json, os, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
EXTRACTED = ROOT / "data" / "server-packs" / "extracted"
GRAPH_ROOT = ROOT / "data" / "server-packs" / "graph"


def ensure_graphify_python():
    """graphify 는 uv tool 로 따로 깔려 있다. 그쪽 파이썬으로 옮겨 탄다."""
    try:
        import graphify  # noqa: F401
        return
    except ImportError:
        pass
    import shutil
    exe = shutil.which("graphify")
    if not exe:
        sys.exit("graphify 가 없다: uv tool install graphifyy")
    py = Path(exe).read_text(encoding="utf-8", errors="replace").splitlines()[0].lstrip("#!").strip()
    if not Path(py).exists():
        sys.exit(f"graphify 의 파이썬을 못 찾았다: {py}")
    os.execv(py, [py, __file__, *sys.argv[1:]])


ensure_graphify_python()

from graphify.build import build_from_json                    # noqa: E402
from graphify.cluster import cluster, score_all               # noqa: E402
from graphify.analyze import god_nodes, surprising_connections, suggest_questions  # noqa: E402
from graphify.export import to_json, to_html                  # noqa: E402
from graphify.report import generate                          # noqa: E402

CATS = [("맵", "maps"), ("괴물", "mobs"), ("아이템", "items"), ("NPC", "npcs"),
        ("마법", "spells"), ("기술", "skills"), ("상점", "shops"), ("스크립트", "scripts")]


def load(pack, key):
    f = EXTRACTED / pack / f"{key}.json"
    return json.loads(f.read_text(encoding="utf-8")) if f.exists() else []


def extraction_for(pack):
    """노드와 간선을 만든다. 전부 자료에서 그대로 나온 것이라 EXTRACTED 다."""
    nodes, edges, seen = [], [], set()

    def node(cat, name, src):
        nid = f"{cat}:{name}"
        if nid in seen:
            return nid
        seen.add(nid)
        nodes.append({"id": nid, "label": name, "type": cat,
                      "source_file": src, "confidence": "EXTRACTED"})
        return nid

    def edge(a, b, rel, src):
        edges.append({"source": a, "target": b, "relation": rel,
                      "confidence": "EXTRACTED", "source_file": src})

    for cat, key in CATS:
        for e in load(pack, key):
            node(cat, e["이름"], e["출처"])

    for w in load(pack, "warps"):
        if w["출발맵"] != w["도착맵"]:
            edge(node("맵", w["출발맵"], w["출처"]), node("맵", w["도착맵"], w["출처"]),
                 "워프", w["출처"])
    for s in load(pack, "mob_spawns"):
        edge(node("맵", s["맵"], s["출처"]), node("괴물", s["괴물"], s["출처"]),
             "산다", s["출처"])
    for s in load(pack, "npc_spawns"):
        edge(node("맵", s["맵"], s["출처"]), node("NPC", s["NPC"], s["출처"]),
             "선다", s["출처"])
    for sh in load(pack, "shops"):
        a = node("상점", sh["이름"], sh["출처"])
        for it in sh["아이템"]:
            edge(a, node("아이템", it, sh["출처"]), "판다", sh["출처"])
    for sc in load(pack, "scripts"):
        a = node("스크립트", sc["이름"], sc["출처"])
        for cat, names in sc["부름"].items():
            for n in names:
                edge(a, node(cat, n, sc["출처"]), "부른다", sc["출처"])
    for t in load(pack, "traps"):
        edge(node("맵", t["맵"], t["출처"]), node("스크립트", t["스크립트"], t["출처"]),
             "함정", t["출처"])

    # 같은 간선이 여러 번 선언된 것은 한 번만 센다 (워프는 칸마다 한 줄이라 수백 번 겹친다)
    uniq, keep = set(), []
    for e in edges:
        k = (e["source"], e["target"], e["relation"])
        if k not in uniq:
            uniq.add(k)
            keep.append(e)
    return {"nodes": nodes, "edges": keep, "hyperedges": [],
            "input_tokens": 0, "output_tokens": 0}


def build(pack):
    out = GRAPH_ROOT / pack
    out.mkdir(parents=True, exist_ok=True)
    ext = extraction_for(pack)
    G = build_from_json(ext, root=str(EXTRACTED / pack))
    if G.number_of_nodes() == 0:
        sys.exit(f"{pack}: 노드가 없다")
    communities = cluster(G)
    cohesion = score_all(G, communities)
    gods = god_nodes(G)
    surprises = surprising_connections(G, communities)

    # 군집 이름은 그 안에서 가장 많은 갈래와 가장 이어진 노드로 짓는다 — 사람이 짐작하지 않는다
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
        top_kind = max(kinds, key=kinds.get)
        hub = max(members, key=lambda m: G.degree(m))
        labels[cid] = f"{attr(hub, 'label') or hub} 둘레 {top_kind}"

    questions = suggest_questions(G, communities, labels)
    detection = {"total_files": len(set(n["source_file"] for n in ext["nodes"])),
                 "total_words": 0, "files": {"document": []}}
    to_json(G, communities, str(out / "graph.json"), community_labels=labels, force=True)

    # 그림에는 이어진 것만 그린다. 아이템 대부분은 어느 상점에도 없고 어느 스크립트도
    # 부르지 않아서 홀로 떠 있다 — 그건 자료의 사실이고 graph.json 에 그대로 남지만,
    # 점 수천 개를 흩뿌리면 그림에서 아무것도 안 보인다.
    linked = [n for n in G if G.degree(n) > 0]
    H = G.subgraph(linked).copy()
    hcomms = {cid: [m for m in ms if m in H] for cid, ms in communities.items()}
    hcomms = {cid: ms for cid, ms in hcomms.items() if ms}
    try:
        to_html(H, hcomms, str(out / "graph.html"),
                community_labels={k: v for k, v in labels.items() if k in hcomms},
                node_limit=5000)
    except ValueError as e:
        print(f"   (그림 건너뜀: {e})")
    isolated = G.number_of_nodes() - H.number_of_nodes()
    (out / "GRAPH_REPORT.md").write_text(
        generate(G, communities, cohesion, labels, gods, surprises, detection,
                 {"input": 0, "output": 0}, str(EXTRACTED / pack),
                 suggested_questions=questions), encoding="utf-8")
    return G, communities, gods, surprises, labels, isolated


def main():
    for pack in sorted(d.name for d in EXTRACTED.iterdir() if d.is_dir()):
        G, comms, gods, surprises, labels, isolated = build(pack)
        print(f"══ {pack} — 노드 {G.number_of_nodes():,} · 간선 {G.number_of_edges():,}"
              f" · 이어진 노드 {G.number_of_nodes() - isolated:,} · 홀로 떠 있는 것 {isolated:,}")
        for g in gods[:5]:
            print(f"   중심 {g.get('node', g.get('id', '?'))} — 이어진 것 {g.get('degree', '?')}")
    print(f"\n→ {GRAPH_ROOT.relative_to(ROOT)}/<팩>/graph.html")


if __name__ == "__main__":
    main()
