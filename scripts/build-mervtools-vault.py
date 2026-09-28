#!/usr/bin/env python3
"""MervTools 분석 자료를 Obsidian vault와 지식 그래프로 만든다.

MervTools의 다이얼로그 ID, 설정(Settings), 경보(Alarm) 맵 정보 등을 파싱하여 노드와 간선으로 엮는다.

  쓰는 법: python scripts/build-mervtools-vault.py
  산출물: data/mervtools-vault/ (마크다운 볼트) 및 data/mervtools-vault/graph/ (그래프 데이터)
"""
import sys, os, re
import xml.etree.ElementTree as ET
from pathlib import Path

# graphify 경로를 찾기 위해
from graphify_runtime import (
    configure_utf8_stdio,
    execute_graphify_script,
    find_graphify_python,
)

ROOT = Path(__file__).resolve().parent.parent
MERV = ROOT / "sources" / "MervTools" / "MervTools"
VAULT = ROOT / "data" / "mervtools-vault"
OUT = VAULT / "graph"

configure_utf8_stdio(sys.stdout, sys.stderr)

def ensure_graphify_python():
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

from graphify.build import build_from_json
from graphify.cluster import cluster, score_all
from graphify.analyze import god_nodes, surprising_connections, suggest_questions
from graphify.export import to_json, to_html
from graphify.report import generate

def slug(name):
    return re.sub(r'[\\/:*?"<>|#\[\]^]', "_", str(name)).strip()

def extract_merv_data():
    data = {"dialogs": [], "alarm_maps": []}
    
    # Dialogs
    dialog_file = MERV / "Settings" / "dialogs.txt"
    if dialog_file.exists():
        text = dialog_file.read_text(encoding="utf-8", errors="replace")
        for line in text.splitlines():
            line = line.strip()
            if not line: continue
            parts = line.split(" - ", 1)
            if len(parts) == 2:
                data["dialogs"].append({"id": parts[0].strip(), "name": parts[1].strip()})
                
    # Main Settings
    settings_file = MERV / "Settings" / "mainsettings.xml"
    if settings_file.exists():
        try:
            tree = ET.parse(settings_file)
            root = tree.getroot()
            for child in root:
                if child.tag.startswith("alarmmap_"):
                    data["alarm_maps"].append(child.text.strip())
        except Exception as e:
            print(f"Error parsing xml: {e}")
            
    return data

def build_markdown_vault(data):
    if VAULT.exists():
        import shutil
        shutil.rmtree(VAULT)
    (VAULT / "다이얼로그").mkdir(parents=True)
    (VAULT / "경보맵").mkdir(parents=True)

    for d in data["dialogs"]:
        (VAULT / "다이얼로그" / f"{slug(d['id'])}.md").write_text(
            f"---\nid: {d['id']}\n---\n\n# 다이얼로그 {d['id']}\n\n이름: {d['name']}\n",
            encoding="utf-8"
        )
        
    for m in data["alarm_maps"]:
        if not m: continue
        (VAULT / "경보맵" / f"{slug(m)}.md").write_text(
            f"---\nmap: {m}\n---\n\n# 경보맵: {m}\n\nMervTools 경보 설정에 등록된 맵입니다.\n",
            encoding="utf-8"
        )
        
    (VAULT / "README.md").write_text(
        "# MervTools Vault\n\n"
        "MervTools 설정과 다이얼로그 ID를 파싱하여 생성된 볼트입니다.\n\n"
        f"- 다이얼로그: {len(data['dialogs'])}개\n"
        f"- 경보맵: {len(data['alarm_maps'])}개\n",
        encoding="utf-8"
    )

def build_graph_data(data):
    nodes, edges = [], []
    src = "MervTools/Settings"
    
    def add_node(kind, key, label=None):
        nid = f"{kind}:{key}"
        nodes.append({"id": nid, "label": label or key, "type": kind, "source_file": src, "confidence": "EXTRACTED"})
        return nid
        
    def add_edge(a, b, relation):
        edges.append({"source": a, "target": b, "relation": relation, "confidence": "EXTRACTED", "source_file": src})
        
    merv_root = add_node("Tool", "MervTools", "MervTools")
    dialog_root = add_node("Category", "Dialogs", "다이얼로그")
    map_root = add_node("Category", "AlarmMaps", "경보맵")
    
    add_edge(merv_root, dialog_root, "has_category")
    add_edge(merv_root, map_root, "has_category")
    
    for d in data["dialogs"]:
        dn = add_node("Dialog", d["id"], f"[{d['id']}] {d['name']}")
        add_edge(dialog_root, dn, "contains")
        
    for m in data["alarm_maps"]:
        if not m: continue
        mn = add_node("Map", m, m)
        add_edge(map_root, mn, "contains")
        
    return {"nodes": nodes, "edges": edges}

def main():
    if not MERV.exists():
        sys.exit(f"{MERV} 디렉토리가 없습니다. 먼저 압축을 풀어주세요.")
        
    data = extract_merv_data()
    build_markdown_vault(data)
    
    OUT.mkdir(parents=True, exist_ok=True)
    graph_json = build_graph_data(data)
    
    G = build_from_json(graph_json, root=str(VAULT))
    
    communities = cluster(G)
    cohesion = score_all(G, communities)
    gods = god_nodes(G)
    surprises = surprising_connections(G, communities)
    
    labels = {}
    for cid, members in communities.items():
        members = [m for m in members if m in G]
        if not members: continue
        hub = max(members, key=lambda m: G.degree(m))
        labels[cid] = f"{G.nodes.get(hub, {}).get('label', hub)} 군집"
        
    questions = suggest_questions(G, communities, labels)
    to_json(G, communities, str(OUT / "graph.json"), community_labels=labels, force=True)
    try:
        to_html(G, communities, str(OUT / "graph.html"), community_labels=labels, node_limit=5000)
    except ValueError as exc:
        print(f"   (그림 건너뜀: {exc})")
        
    detection = {"total_files": 1, "total_words": 0, "files": {"document": ["MervTools"]}}
    (OUT / "GRAPH_REPORT.md").write_text(
        generate(G, communities, cohesion, labels, gods, surprises, detection,
                 {"input": 0, "output": 0}, str(VAULT), suggested_questions=questions),
        encoding="utf-8"
    )

    print(f"MervTools 그래프 — 노드 {G.number_of_nodes():,} · 간선 {G.number_of_edges():,} · 군집 {len(communities)}")
    print(f"볼트 경로: {VAULT.relative_to(ROOT)}")
    print(f"그래프 출력: {OUT.relative_to(ROOT)}/graph.html")

if __name__ == "__main__":
    main()
