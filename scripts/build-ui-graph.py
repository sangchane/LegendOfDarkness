#!/usr/bin/env python3
"""원작 4.51 UI 자료와 테마 규칙(data/original-ui/451.json)을 지식 그래프로 바꾼다.

사실은 이미 정리된 표라 LLM 으로 뜻을 뽑을 것이 없다 — 노드와 간선은 JSON 이 그대로 정하고,
군집 찾기·중심 노드·보고서·HTML 은 graphify 가 한다(`build-disassembly-graph.py` 와 같은 방식).

  노드: 판 · 아카이브 · 화면(원작 그림) · 글꼴 · 재질 · 색 · 치수 · 규칙 · 시안 · 화면시안 · 구현대상
  간선: 들어있다 · 뽑았다 · 정한다 · 쓴다 · 닿는다 · 그린다 · 채택 · 버렸다

  답하게 하려는 질문: "이 색을 왜 여기 쓰나" 를 규칙 → 근거 → 원작 그림까지 한 번에 따라가는 것.

  쓰는 법: python scripts/build-ui-graph.py
  산출물:  data/original-ui/graph/graph.json · graph.html · GRAPH_REPORT.md  (무시 목록 — 언제든 다시 만든다)
"""
import json
import sys
from pathlib import Path

from graphify_runtime import (
    configure_utf8_stdio,
    execute_graphify_script,
    find_graphify_python,
)

ROOT = Path(__file__).resolve().parent.parent
FACTS = ROOT / "data" / "original-ui" / "451.json"
OUT = ROOT / "data" / "original-ui" / "graph"

configure_utf8_stdio(sys.stdout, sys.stderr)


def ensure_graphify_python():
    """graphify 는 uv tool 로 따로 깔려 있다. 그쪽 파이썬으로 옮겨 탄다."""
    try:
        import graphify  # noqa: F401
        return
    except ImportError:
        pass
    try:
        py = find_graphify_python()
    except RuntimeError as exc:
        sys.exit(str(exc))
    status = execute_graphify_script(py, Path(__file__).resolve(), sys.argv[1:])
    raise SystemExit(status)


ensure_graphify_python()

from graphify.build import build_from_json                    # noqa: E402
from graphify.cluster import cluster, score_all               # noqa: E402
from graphify.analyze import god_nodes, surprising_connections, suggest_questions  # noqa: E402
from graphify.export import to_json, to_html                  # noqa: E402
from graphify.report import generate                          # noqa: E402


def extraction(facts):
    nodes, edges, seen = [], [], set()
    src = str(FACTS.relative_to(ROOT))

    def node(kind, key, label=None):
        nid = f"{kind}:{key}"
        if nid not in seen:
            seen.add(nid)
            nodes.append({"id": nid, "label": label or key, "type": kind,
                          "source_file": src, "confidence": "EXTRACTED"})
        return nid

    def edge(a, b, relation):
        edges.append({"source": a, "target": b, "relation": relation,
                      "confidence": "EXTRACTED", "source_file": src})

    versions = {v["id"]: node("판", v["이름"]) for v in facts["판"]}

    # 아카이브는 판 안에 들어 있다.
    archives = {}
    for a in facts["아카이브"]:
        nid = node("아카이브", a["이름"], f"{a['이름']} ({a['판']})")
        archives[a["이름"]] = nid
        edge(versions[a["판"]], nid, "들어있다")

    for f in facts["글꼴"]:
        edge(archives["Legend.dat"], node("글꼴", f["파일"]), "들어있다")

    # 화면(원작 그림)은 4.51 의 Legend.dat 에서 나왔다. 쓰임을 확인한 것만 이름을 붙인다.
    screens = {}
    for s in facts["화면"]:
        confirmed = s["확인"] == "그림으로 확인"
        label = f"{s['파일']} {s['무엇']}" if confirmed else s["파일"]
        nid = node("화면", s["파일"], label)
        screens[s["파일"]] = nid
        edge(archives["Legend.dat"], nid, "들어있다")

    # 재질은 화면 한 장에서 뽑은 것이다.
    materials = {}
    for m in facts["재질"]:
        nid = node("재질", m["id"], f"{m['id']} {m['평균색']}")
        materials[m["id"]] = nid
        if m["원본"] in screens:
            edge(nid, screens[m["원본"]], "뽑았다")
        for place in m["쓰는곳"]:
            edge(nid, node("쓰는곳", place), "쓴다")

    colours = {c["id"]: node("색", c["id"], f"{c['id']} {c['값']}") for c in facts["색"]}
    sizes = {s["id"]: node("치수", s["id"], f"{s['id']} {s['값']}") for s in facts["치수"]}

    # 구현대상 — 우리 코드. 규칙이 여기로 닿는다.
    targets = {}
    for t in facts["구현대상"]:
        nid = node("구현대상", Path(t["파일"]).name, f"{Path(t['파일']).name} ({t['상태']})")
        targets[t["파일"]] = nid

    # 규칙이 그래프의 가운데다 — 재질·색·치수를 정하고 코드로 닿는다.
    for r in facts["규칙"]:
        nid = node("규칙", r["id"])
        for one in r.get("재질", []):
            edge(nid, materials[one], "정한다")
        for one in r.get("색", []):
            edge(nid, colours[one], "정한다")
        for one in r.get("치수", []):
            edge(nid, sizes[one], "정한다")
        for one in r.get("구현대상", []):
            if one in targets:
                edge(nid, targets[one], "닿는다")
        edge(nid, node("근거", r["근거"][:60]), "근거")

    # 시안 — 채택된 것 하나와 버린 둘. 채택안이 규칙을 낳았다.
    for d in facts["시안"]:
        nid = node("시안", d["id"], f"{d['id']} {d['이름']}")
        if d["채택"]:
            for r in facts["규칙"]:
                edge(nid, f"규칙:{r['id']}", "채택")
        else:
            edge(nid, node("판정", d["판정"][:60]), "버렸다")

    for s in facts["화면시안"]:
        nid = node("화면시안", s["id"])
        if s["구현대상"] in targets:
            edge(targets[s["구현대상"]], nid, "그린다")

    return {"nodes": nodes, "edges": edges}


def main():
    if not FACTS.exists():
        sys.exit(f"{FACTS.relative_to(ROOT)} 가 없다")
    facts = json.loads(FACTS.read_text(encoding="utf-8"))
    OUT.mkdir(parents=True, exist_ok=True)

    G = build_from_json(extraction(facts), root=str(FACTS.parent))
    if G.number_of_nodes() == 0:
        sys.exit("노드가 없다")

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
            kind = attr(m, "type") or "?"
            kinds[kind] = kinds.get(kind, 0) + 1
        hub = max(members, key=lambda m: G.degree(m))
        labels[cid] = f"{attr(hub, 'label') or hub} 둘레 {max(kinds, key=kinds.get)}"

    questions = suggest_questions(G, communities, labels)
    to_json(G, communities, str(OUT / "graph.json"), community_labels=labels, force=True)
    try:
        to_html(G, communities, str(OUT / "graph.html"), community_labels=labels, node_limit=5000)
    except ValueError as exc:
        print(f"   (그림 건너뜀: {exc})")

    detection = {"total_files": 1, "total_words": 0,
                 "files": {"document": [str(FACTS.relative_to(ROOT))]}}
    (OUT / "GRAPH_REPORT.md").write_text(
        generate(G, communities, cohesion, labels, gods, surprises, detection,
                 {"input": 0, "output": 0}, str(FACTS.parent), suggested_questions=questions),
        encoding="utf-8")

    print(f"UI 테마 그래프 — 노드 {G.number_of_nodes():,} · 간선 {G.number_of_edges():,} · 군집 {len(communities)}")
    for g in gods[:6]:
        print(f"   중심 {g.get('label', g.get('node', g.get('id', '?')))} — "
              f"이어진 것 {g.get('degree', g.get('edges', '?'))}")
    print(f"-> {OUT.relative_to(ROOT)}/graph.html")


if __name__ == "__main__":
    main()
