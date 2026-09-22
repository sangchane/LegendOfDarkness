#!/usr/bin/env python3
"""Build a Graphify graph from the checked 2023 workbook JSON (no LLM edges)."""
import json
import sys
from collections import defaultdict
from pathlib import Path

from graphify_runtime import configure_utf8_stdio, execute_graphify_script, find_graphify_python

ROOT = Path(__file__).resolve().parent.parent
FACTS = ROOT / "data" / "skill-spell-2023" / "skills.json"
OUT = ROOT / "data" / "skill-spell-2023" / "graph"
configure_utf8_stdio(sys.stdout, sys.stderr)


def ensure_graphify_python():
    try:
        import graphify  # noqa: F401
        return
    except ImportError:
        pass
    try:
        python = find_graphify_python()
    except RuntimeError as exc:
        sys.exit(str(exc))
    raise SystemExit(execute_graphify_script(python, Path(__file__).resolve(), sys.argv[1:]))


ensure_graphify_python()
from graphify.analyze import god_nodes, surprising_connections, suggest_questions  # noqa: E402
from graphify.build import build_from_json  # noqa: E402
from graphify.cluster import cluster, score_all  # noqa: E402
from graphify.export import to_html, to_json  # noqa: E402
from graphify.report import generate  # noqa: E402


def extraction(facts):
    nodes, edges, seen = [], [], set()
    source = str(FACTS.relative_to(ROOT))
    def node(kind, key, label=None):
        nid = f"{kind}:{key}"
        if nid not in seen:
            seen.add(nid); nodes.append({"id": nid, "label": label or str(key), "type": kind,
                                         "source_file": source, "confidence": "EXTRACTED"})
        return nid
    def edge(a, b, relation):
        edges.append({"source": a, "target": b, "relation": relation,
                      "source_file": source, "confidence": "EXTRACTED"})
    groups = defaultdict(list)
    for record in facts["records"]:
        ability = node("표행", record["id"], record["name"])
        edge(node("직업", record["class"]), ability, "표에 있다")
        edge(node("구분", record["section"]), ability, "표에 있다")
        base = node("기술묶음", f"{record['class']}:{record['base_name']}", record["base_name"])
        edge(base, ability, "등급")
        if record["materials"]:
            edge(ability, node("준비물", record["materials"]), "준비물")
        if record["book"]:
            edge(ability, node("책", record["book"]), "책")
        groups[(record["class"], record["base_name"])].append(record)
    # These are rank sequences explicitly named LevN/master in the spreadsheet,
    # never guessed prerequisites.
    for values in groups.values():
        ranked = [r for r in values if r["rank"]]
        ranked.sort(key=lambda r: (r["rank"] == "master", int(r["rank"][3:]) if r["rank"] != "master" else 999))
        for before, after in zip(ranked, ranked[1:]):
            edge(node("표행", before["id"], before["name"]), node("표행", after["id"], after["name"]), "다음 등급")
    unique = {(e["source"], e["target"], e["relation"]): e for e in edges}
    return {"nodes": nodes, "edges": list(unique.values()), "hyperedges": [], "input_tokens": 0, "output_tokens": 0}


def main():
    facts = json.loads(FACTS.read_text(encoding="utf-8")); extracted = extraction(facts); OUT.mkdir(parents=True, exist_ok=True)
    graph = build_from_json(extracted, root=str(FACTS.parent))
    if graph.number_of_nodes() == 0: sys.exit("그래프 노드가 없다")
    communities = cluster(graph); cohesion = score_all(graph, communities); gods = god_nodes(graph)
    labels = {}
    for cid, members in communities.items():
        members = [m for m in members if m in graph]
        if members:
            hub = max(members, key=lambda item: graph.degree(item))
            labels[cid] = f"{graph.nodes[hub].get('label', hub)} 둘레"
    to_json(graph, communities, str(OUT / "graph.json"), community_labels=labels, force=True)
    try: to_html(graph, communities, str(OUT / "graph.html"), community_labels=labels, node_limit=5000)
    except ValueError as exc: print(f"(그림 건너뜀: {exc})")
    report = generate(graph, communities, cohesion, labels, gods, surprising_connections(graph, communities),
                      {"total_files": 1, "total_words": 0, "files": {"document": [str(FACTS.relative_to(ROOT))]}},
                      {"input": 0, "output": 0}, str(FACTS.parent), suggested_questions=suggest_questions(graph, communities, labels))
    (OUT / "GRAPH_REPORT.md").write_text(report, encoding="utf-8")
    print(f"2023 기술·마법 그래프 — 노드 {graph.number_of_nodes()} · 간선 {graph.number_of_edges()} · 군집 {len(communities)}")


if __name__ == "__main__": main()
