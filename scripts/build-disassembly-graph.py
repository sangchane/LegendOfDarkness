#!/usr/bin/env python3
"""역어셈블로 확인한 사실(data/disassembly/findings.json)을 지식 그래프로 바꾼다.

사실은 이미 주소까지 정리된 표라 LLM 으로 뜻을 뽑을 것이 없다 — 노드와 간선은 JSON 이 그대로 정하고,
군집 찾기·중심 노드·보고서·HTML 은 graphify 가 한다(`build-server-pack-graph.py` 와 같은 방식).

  노드: 실행파일 · 함수 · 칸(오프셋) · 표 · 패킷 · 규칙 · 비교 · 구현(우리 코드) · 주소 · 명령 · 발췌
  간선: 들어있다 · 근거 · 읽는다 · 쓴다 · 만든다 · 보낸다 · 부른다 · 옮겼다

  쓰는 법: python3 scripts/build-disassembly-graph.py
  산출물:  data/disassembly/graph/graph.json · graph.html · GRAPH_REPORT.md  (무시 목록 — 언제든 다시 만든다)
"""
import json
import re
import sys
from pathlib import Path

from graphify_runtime import (
    configure_utf8_stdio,
    execute_graphify_script,
    find_graphify_python,
)

ROOT = Path(__file__).resolve().parent.parent
FINDINGS = ROOT / "data" / "disassembly" / "findings.json"
OUT = ROOT / "data" / "disassembly" / "graph"

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

ADDRESS = re.compile(r"0x[0-9a-fA-F]{5,}")
NOT_PORTED = ("옮기지 않음", "해당 없음", "아직")


def extraction(facts):
    nodes, edges, seen = [], [], set()
    src = str(FINDINGS.relative_to(ROOT))

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

    binaries = {b["id"]: node("실행파일", b["name"]) for b in facts["binaries"]}
    known = {}  # 주소(소문자) → 노드

    for f in facts["functions"]:
        nid = node("함수", f["address"], f"{f['address']} {f['name']}")
        known[f["address"].lower()] = nid
        edge(binaries[f["binary"]], nid, "들어있다")
        if f.get("excerpt"):
            edge(nid, node("발췌", Path(f["excerpt"]).name), "근거")

    for t in facts["tables"]:
        nid = node("표", t["address"], f"{t['address']} {t['name']}")
        known[t["address"].lower()] = nid
        edge(binaries[t["binary"]], nid, "들어있다")
        if t.get("excerpt"):
            edge(nid, node("발췌", Path(t["excerpt"]).name), "근거")

    def address(binary, text):
        """글 속의 주소를 아는 함수·표에 잇고, 모르면 주소 노드를 만든다."""
        found = []
        for raw in ADDRESS.findall(text):
            a = "0x" + raw[2:].lower().lstrip("0")
            nid = known.get(a) or node("주소", f"{binary}:{a}", a)
            if nid.startswith("주소:"):
                edge(binaries[binary], nid, "들어있다")
            found.append(nid)
        return found

    for o in facts["offsets"]:
        nid = node("칸", f"{o['binary']}:{o['struct']}+{o['offset']}", f"{o['struct']}+{o['offset']} {o['meaning'].split(' —')[0].split('(')[0]}")
        edge(binaries[o["binary"]], nid, "들어있다")
        for who in o.get("read_by", []):
            for a in address(o["binary"], who):
                edge(a, nid, "읽는다")
        for who in o.get("written_by", []):
            for a in address(o["binary"], who):
                edge(a, nid, "쓴다")

    for p in facts["packets"]:
        nid = node("패킷", p["opcode"], f"{p['opcode']} {p['name']}")
        for a in address(p["binary"], p["builder"]):
            edge(a, nid, "만든다")
        for sender in p.get("senders", []):
            for a in address(p["binary"], sender):
                edge(a, nid, "보낸다")

    def implemented(from_id, text):
        if not text or text.startswith(NOT_PORTED):
            return
        for part in text.split("·"):
            symbol = re.split(r"[ (]", part.strip(), maxsplit=1)[0].strip()
            if symbol:
                edge(from_id, node("구현", symbol), "옮겼다")

    for r in facts["rules"]:
        nid = node("규칙", r["id"], r["statement"].split("—")[0].split(".")[0][:40])
        edge(binaries[r["binary"]], nid, "들어있다")
        for evidence in r["evidence"]:
            for a in address(r["binary"], evidence):
                edge(nid, a, "근거")
        implemented(nid, r.get("implemented", ""))

    for c in facts["comparisons"]:
        implemented(node("비교", c["id"], c["statement"][:40]), c.get("implemented", ""))

    for cmd in facts["script_command_evidence"]["examples"]:
        nid = node("명령", cmd["name"])
        edge(nid, known.get(cmd["address"].lower()) or node("주소", f"server:{cmd['address']}", cmd["address"]), "들어있다")
        for key, relation in (("reads", "읽는다"), ("writes", "쓴다")):
            if key in cmd:
                for o in facts["offsets"]:
                    if o["binary"] == "server" and o["struct"] == "character" and o["offset"].lower() == cmd[key].lower():
                        edge(nid, node("칸", f"server:character+{o['offset']}"), relation)
        if "calls" in cmd:
            for a in address("server", cmd["calls"]):
                edge(nid, a, "부른다")
        if "sends" in cmd:
            edge(nid, node("패킷", cmd["sends"]), "보낸다")

    uniq, keep = set(), []
    for e in edges:
        k = (e["source"], e["target"], e["relation"])
        if k not in uniq and e["source"] != e["target"]:
            uniq.add(k)
            keep.append(e)
    return {"nodes": nodes, "edges": keep, "hyperedges": [], "input_tokens": 0, "output_tokens": 0}


def main():
    facts = json.loads(FINDINGS.read_text(encoding="utf-8"))
    OUT.mkdir(parents=True, exist_ok=True)
    ext = extraction(facts)
    G = build_from_json(ext, root=str(FINDINGS.parent))
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
            k = attr(m, "type") or "?"
            kinds[k] = kinds.get(k, 0) + 1
        hub = max(members, key=lambda m: G.degree(m))
        labels[cid] = f"{attr(hub, 'label') or hub} 둘레 {max(kinds, key=kinds.get)}"

    questions = suggest_questions(G, communities, labels)
    to_json(G, communities, str(OUT / "graph.json"), community_labels=labels, force=True)
    try:
        to_html(G, communities, str(OUT / "graph.html"), community_labels=labels, node_limit=5000)
    except ValueError as exc:
        print(f"   (그림 건너뜀: {exc})")
    detection = {"total_files": 1, "total_words": 0, "files": {"document": [str(FINDINGS.relative_to(ROOT))]}}
    (OUT / "GRAPH_REPORT.md").write_text(
        generate(G, communities, cohesion, labels, gods, surprises, detection,
                 {"input": 0, "output": 0}, str(FINDINGS.parent), suggested_questions=questions),
        encoding="utf-8")

    print(f"역어셈블 그래프 — 노드 {G.number_of_nodes():,} · 간선 {G.number_of_edges():,} · 군집 {len(communities)}")
    for g in gods[:6]:
        print(f"   중심 {g.get('label', g.get('node', g.get('id', '?')))} — 이어진 것 {g.get('degree', g.get('edges', '?'))}")
    print(f"→ {OUT.relative_to(ROOT)}/graph.html")


if __name__ == "__main__":
    main()
