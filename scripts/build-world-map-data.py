#!/usr/bin/env python3
"""서버에 들어간 맵과 워프를 눈으로 볼 수 있게 한 덩어리로 뽑는다.

개수만으로는 세계가 제대로 이어졌는지 알 수 없다. 워프 890장이 다 실렸다는 말은
"파일 890개를 읽었다"는 뜻이지 "갈 수 있다"는 뜻이 아니다. 그래서 **맵이 어떻게
이어져 있는지를 사람이 직접 본다** — 이 파일이 그 재료다.

읽는 곳은 **서버에 실제로 들어간 것**이다(팩 원본이 아니라). 넣은 결과를 보는 것이 목적이다.

  쓰는 법: python3 scripts/build-world-map-data.py   → docs/world-map-data.js
"""
import json, glob, collections
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
OUT = ROOT / "docs" / "world-map-data.js"

# 팩의 warp_db.txt 가 싣지 않는 워프 파일. 주석 처리도 아니고 아예 목록에 없다 —
# 팩 서버가 이 지역들을 쓰지 않는다는 뜻이다. 무엇이 빠졌는지는 보여 줘야 한다.
UNLOADED = ["Baeqnatop", "Casmanum", "Christmas", "DraculaCastle", "Mechanic", "Roues", "Suomi"]


def main():
    areas = {}
    for f in glob.glob(str(SERVER / "areas" / "*.json")):
        d = json.loads(Path(f).read_text(encoding="utf-8-sig"))
        areas[d["Id"]] = {"이름": d["Name"], "가로": d["Cols"], "세로": d["Rows"]}

    edges = collections.Counter()
    dangling = 0
    for f in glob.glob(str(SERVER / "templates" / "warps" / "*.json")):
        w = json.loads(Path(f).read_text(encoding="utf-8-sig"))
        a, b = w["ActivationMapId"], w["To"]["AreaID"]
        if a in areas and b in areas:
            edges[(a, b)] += 1
        else:
            dangling += 1

    # 덩어리 나누기 — 방향을 무시하고 이어진 것끼리 묶는다
    par = {i: i for i in areas}
    def find(x):
        while par[x] != x:
            par[x] = par[par[x]]; x = par[x]
        return x
    for a, b in edges:
        ra, rb = find(a), find(b)
        if ra != rb:
            par[ra] = rb
    groups = collections.defaultdict(list)
    for i in areas:
        groups[find(i)].append(i)

    deg = collections.Counter()
    for (a, b), n in edges.items():
        deg[a] += n; deg[b] += n

    # 큰 덩어리부터. 혼자 있는 맵은 하나로 묶어 뒤에 붙인다.
    clusters = sorted((v for v in groups.values() if len(v) > 1), key=len, reverse=True)
    lonely = sorted((v[0] for v in groups.values() if len(v) == 1), key=lambda i: areas[i]["이름"])

    data = {
        "생성": "서버에 들어간 areas/ 와 templates/warps/ 를 그대로 읽었다",
        "요약": {
            "맵": len(areas),
            "이어진맵": sum(len(c) for c in clusters),
            "혼자인맵": len(lonely),
            "워프": sum(edges.values()) + dangling,
            "덩어리": len(clusters),
        },
        "맵": {str(i): dict(a, 이음=deg[i]) for i, a in areas.items()},
        "간선": [[a, b, n] for (a, b), n in edges.items()],
        "덩어리": [sorted(c, key=lambda i: -deg[i]) for c in clusters],
        "혼자": lonely,
        "안싣는워프파일": UNLOADED,
    }

    OUT.write_text("window.WORLD_MAP_DATA = " + json.dumps(data, ensure_ascii=False) + ";\n",
                   encoding="utf-8")
    s = data["요약"]
    print(f"맵 {s['맵']} · 이어진 맵 {s['이어진맵']} · 혼자인 맵 {s['혼자인맵']} · "
          f"덩어리 {s['덩어리']} · 워프 {s['워프']}")
    print(f"덩어리 크기: {[len(c) for c in clusters][:12]}")
    print(f"→ {OUT.relative_to(ROOT)}  ({OUT.stat().st_size/1024:.0f} KB)")


if __name__ == "__main__":
    main()
