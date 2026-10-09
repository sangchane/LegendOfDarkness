#!/usr/bin/env python3
"""현황판 지도 — 맵을 원작 타일로 다시 그리고 출구 방향대로 띄워 놓아 잇는다(화살표).

  python3 scripts/gen/world/build-atlas.py        → docs/atlas-maps/<맵>-s.webp(한눈) · <맵>-l.webp(확대) · docs/atlas-layout-data.js

**사용자 2026-10-09** 「맵 복원해서 방향으로 연결하는 그런 방식이면 좋겠는데」 → 「여백을 좀 주고 화살표 같은걸 넣고 해당 맵 클릭하면
확대시켜서 나오는 몬스터, 드랍정보, npc 같은 정보 보여주게해」(`autopilot/dashboard-atlas/SPEC.md`). 괴물·드랍·NPC 는
`docs/atlas-data.js`(게임 볼트 생성기 `scripts/gen/vault/build-game-vault.py`)가 같은 맵 번호로 준다.

**잇는 법** — 지역 = 월드맵 카드(앱 `guide.txt` area 줄, 마을·사냥터 입구 13). 카드 맵에서 출발해 서버 워프(`templates/warps`, Map)를
따라간다. 출구 칸이 가장자리에서 `EDGE` 칸 안이면 그 방향(북·남·서·동)으로 이웃 맵을 놓는다 — 가장자리를 따라서는 출구 칸과 도착 칸을
맞추고, 가로질러서는 `GAP` 칸 띄운다. 이미 놓인 맵과 겹치면 같은 방향에서 가장자리를 따라 비켜 놓는다(우드랜드입구 북쪽처럼 큰 맵
여럿이 한 가장자리에 붙는 곳). 가장자리가 아닌 출구는 문 — 건물은 놓지 않고 문 표시로 둔다. 다만 문 너머가 괴물이 사는 곳이면(던전
계단 — 구광산 35층·노비스지하던전) 동쪽에 띄워 놓고 화살표로 잇는다. 다른 카드 맵(다른 지역 입구)에서는 멈춘다.

**그림** — `build-map-images.py` 의 렌더러(원작 seo.dat 바닥 타일, 칸 56x27 마름모)로 그려 1/8(한눈)·1/4(확대) WebP 로 줄인다.
.map 이 그림보다 새로울 때만 다시 그린다.
"""
import importlib.util
import json
import statistics
import sys
from collections import defaultdict, deque

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from graphify_runtime import configure_utf8_stdio

from lib._paths import ROOT

configure_utf8_stdio(sys.stdout, sys.stderr)

SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
GUIDE = ROOT / "mobile" / "client" / "assets" / "world" / "guide.txt"
IMAGES = ROOT / "docs" / "atlas-maps"
DATA = ROOT / "docs" / "atlas-layout-data.js"

EDGE = 3        # 가장자리에서 이만큼 안의 출구는 그 방향으로 잇는다
GAP = 14        # 이웃 맵 사이 띄움(칸) — 화살표가 지나갈 자리
SMALL, LARGE = 1 / 8, 1 / 4
HALF_W, HALF_H = 28, 13
MOST = 70       # 한 지역에 놓는 맵 수 위한도


def read(path):
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (json.JSONDecodeError, UnicodeDecodeError):
        return None


def renderer():
    spec = importlib.util.spec_from_file_location("map_images", ROOT / "scripts" / "gen" / "world" / "build-map-images.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def cards():
    """월드맵 카드 맵 → (이름, 입장 레벨, 마을/사냥터)."""
    found = {}
    for line in GUIDE.read_text(encoding="utf-8").splitlines():
        part = line.split(" ", 4)
        if part[0] == "area" and len(part) == 5:
            found[int(part[1])] = (part[4], int(part[2]), part[3])
    return found


def side(tile, cols, rows):
    """출구 칸이 붙은 가장자리 — N·S·W·E, 가장자리가 아니면 None(문)."""
    x, y = tile
    gaps = {"N": y, "S": rows - 1 - y, "W": x, "E": cols - 1 - x}
    near = min(gaps, key=gaps.get)
    return near if gaps[near] < EDGE else None


def lay(root, areas, exits, stops, hunting):
    """한 지역 — 맵 → 칸 원점(전역), 화살표 [(맵A, 맵B, A 출구 칸, B 도착 칸)], 문 [(맵, 칸, 간 곳)]."""
    origin = {root: (0, 0)}
    arrows, doors = [], []
    queue = deque([root])

    def clashes(map_id, at):
        cols, rows = areas[map_id]["Cols"], areas[map_id]["Rows"]
        for other, (ox, oy) in origin.items():
            if other == map_id:
                continue
            oc, orr = areas[other]["Cols"], areas[other]["Rows"]
            if at[0] < ox + oc + GAP // 2 and ox < at[0] + cols + GAP // 2 and at[1] < oy + orr + GAP // 2 and oy < at[1] + rows + GAP // 2:
                return True
        return False

    while queue and len(origin) < MOST:
        here = queue.popleft()
        cols, rows = areas[here]["Cols"], areas[here]["Rows"]
        for there, pairs in sorted(exits[here].items()):
            if there == here or there not in areas:
                continue
            ex = statistics.median(a[0] for a, _ in pairs)
            ey = statistics.median(a[1] for a, _ in pairs)
            ax = statistics.median(b[0] for _, b in pairs)
            ay = statistics.median(b[1] for _, b in pairs)
            sides = [side(a, cols, rows) for a, _ in pairs]
            way = max(set(sides), key=sides.count)
            if way is None and there in hunting and there not in stops:
                way = "E"  # 던전 계단 — 괴물이 사는 곳은 문이어도 옆에 놓는다
            if way is None:
                doors.append((here, (ex, ey), there))
                continue
            if there in stops and there != root:
                doors.append((here, (ex, ey), there))  # 다른 지역 입구 — 그 지역으로 가는 길
                continue
            back = side((ax, ay), areas[there]["Cols"], areas[there]["Rows"]) or {"N": "S", "S": "N", "W": "E", "E": "W"}[way]
            arrows.append((here, there, (ex, ey), (ax, ay), way, back))
            if there in origin:
                continue
            tc, tr = areas[there]["Cols"], areas[there]["Rows"]
            ox, oy = origin[here]
            at = {"N": (ox + ex - ax, oy - tr - GAP), "S": (ox + ex - ax, oy + rows + GAP),
                  "W": (ox - tc - GAP, oy + ey - ay), "E": (ox + cols + GAP, oy + ey - ay)}[way]
            # 겹치면 가장자리를 따라 양쪽으로 번갈아 찾고, 한 바퀴(40번)마다 바깥으로 GAP 더 민다.
            first, outward = at, {"N": (0, -1), "S": (0, 1), "W": (-1, 0), "E": (1, 0)}[way]
            for tries in range(1, 400):
                if not clashes(there, at):
                    break
                ring, turn = divmod(tries, 40)
                step = 8 * ((turn + 1) // 2) * (1 if turn % 2 else -1)
                base = (first[0] + outward[0] * GAP * ring, first[1] + outward[1] * GAP * ring)
                at = (base[0] + step, base[1]) if way in "NS" else (base[0], base[1] + step)
            origin[there] = (int(at[0]), int(at[1]))
            queue.append(there)
    return origin, arrows, doors


def crosses(p, q, polygon):
    """선분 p–q 가 마름모(다각형)의 변과 만나거나 안으로 들어가는지."""
    def turn(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    for i in range(len(polygon)):
        a, b = polygon[i], polygon[(i + 1) % len(polygon)]
        if turn(p, q, a) * turn(p, q, b) < 0 and turn(a, b, p) * turn(a, b, q) < 0:
            return True
    mid = ((p[0] + q[0]) / 2, (p[1] + q[1]) / 2)
    return all(turn(polygon[i], polygon[(i + 1) % len(polygon)], mid) > 0 for i in range(len(polygon)))


def screen(gx, gy, scale):
    """전역 칸 → 화면 픽셀(마름모 투영)."""
    return (gx - gy) * HALF_W * scale, (gx + gy) * HALF_H * scale


def draw(module, map_id, area):
    """맵 그림 두 장(한눈·확대) — .map 이 더 새로울 때만 다시 그린다."""
    from PIL import Image

    small, large = IMAGES / f"{map_id}-s.webp", IMAGES / f"{map_id}-l.webp"
    source = SERVER / "maps" / f"lod{map_id}.map"
    if not source.exists():
        return False
    if small.exists() and large.exists() and small.stat().st_mtime >= source.stat().st_mtime:
        return True
    full = IMAGES / f".{map_id}-full.png"
    module.render_map_without_dotnet(module.SEO, source, area["Cols"], area["Rows"], full)
    image = Image.open(full)
    for path, scale in ((small, SMALL), (large, LARGE)):
        image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.LANCZOS).save(path, "WEBP", quality=80)
    full.unlink()
    return True


def main():
    areas = {}
    for path in (SERVER / "areas").glob("*.json"):
        area = read(path)
        if area and "ID" in area:
            areas[area["ID"]] = area
    exits = defaultdict(lambda: defaultdict(list))
    for path in (SERVER / "templates" / "warps").glob("*.json"):
        warp = read(path)
        if not warp or warp.get("WarpType") != "Map":
            continue
        to = warp.get("To") or {}
        landing = to.get("Location") or {}
        for spot in warp.get("Activations") or []:
            at = spot.get("Location") or {}
            if spot.get("AreaID") in areas and to.get("AreaID"):
                exits[spot["AreaID"]][to["AreaID"]].append(((at.get("X"), at.get("Y")), (landing.get("X"), landing.get("Y"))))

    hunting = set()
    for path in (SERVER / "templates" / "monsters").rglob("*.json"):
        monster = read(path)
        if monster and monster.get("AreaID"):
            hunting.add(monster["AreaID"])

    IMAGES.mkdir(parents=True, exist_ok=True)
    module = renderer()
    regions, drawn = [], 0
    starts = cards()
    for card, (name, level, kind) in sorted(starts.items(), key=lambda kv: (kv[1][2] != "town", kv[1][1], kv[1][0])):
        if card not in areas:
            continue
        origin, arrows, doors = lay(card, areas, exits, set(starts), hunting)
        placed = {}
        for map_id, (gx, gy) in origin.items():
            if not draw(module, map_id, areas[map_id]):
                continue
            drawn += 1
            rows, cols = areas[map_id]["Rows"], areas[map_id]["Cols"]
            x, y = screen(gx, gy, SMALL)
            placed[map_id] = {"x": round(x - rows * HALF_W * SMALL + HALF_W * SMALL, 1), "y": round(y, 1),
                              "w": round((cols + rows) * HALF_W * SMALL, 1), "h": round((cols + rows) * HALF_H * SMALL, 1),
                              "cols": cols, "rows": rows, "gx": gx, "gy": gy}
        if not placed:
            continue
        left = min(m["x"] for m in placed.values()) - 40
        top = min(m["y"] for m in placed.values()) - 40
        for m in placed.values():
            m["x"], m["y"] = round(m["x"] - left, 1), round(m["y"] - top, 1)

        def point(map_id, tile):
            gx, gy = placed[map_id]["gx"] + tile[0], placed[map_id]["gy"] + tile[1]
            x, y = screen(gx + 0.5, gy + 0.5, SMALL)
            return round(x - left, 1), round(y - top, 1)

        def diamond(m):
            k, r, c = SMALL, m["rows"], m["cols"]
            return [(m["x"] + r * 28 * k, m["y"]), (m["x"] + (r + c) * 28 * k, m["y"] + c * 13 * k),
                    (m["x"] + c * 28 * k, m["y"] + (c + r) * 13 * k), (m["x"], m["y"] + r * 13 * k)]

        def outward(map_id, way):
            """그 가장자리에서 바깥으로 나가는 화면 방향(단위 벡터) — 짧은 화살표용."""
            dx, dy = {"N": (0, -1), "S": (0, 1), "W": (-1, 0), "E": (1, 0)}[way]
            x, y = (dx - dy) * HALF_W, (dx + dy) * HALF_H
            size = (x * x + y * y) ** 0.5
            return [round(x / size, 3), round(y / size, 3)]

        def outside(map_id, tile, way):
            """출구 칸을 그 가장자리 바로 바깥으로 — 화살표가 맵을 가로지르지 않고 맵 사이 빈 칸에만 그려진다(사용자 2026-10-09
            「맵을 가르지르는 방식으로 화살표 그리지마」)."""
            cols, rows = placed[map_id]["cols"], placed[map_id]["rows"]
            x, y = tile
            spot = {"N": (x, -2.5), "S": (x, rows + 1.5), "W": (-2.5, y), "E": (cols + 1.5, y)}[way]
            return point(map_id, spot)

        lines, seen = [], {}
        for a, b, out, landing, way, back in arrows:
            if a not in placed or b not in placed:
                continue
            key = tuple(sorted((a, b)))
            if key in seen:
                lines[seen[key]]["both"] = True
                continue
            seen[key] = len(lines)
            start, end = outside(a, out, way), outside(b, landing, back)
            # 곧게 그으면 다른 맵을 지나가는 연결(멀리 떨어진 던전 층) — 선 대신 양쪽 가장자리에 짧은 화살표와 갈 곳 이름만.
            blocked = any(crosses(start, end, diamond(placed[o])) for o in placed if o not in (a, b))
            lines.append({"from": a, "to": b, "a": start, "b": end, "both": False, "stub": blocked,
                          "aOut": outward(a, way), "bOut": outward(b, back)})
        gates = [{"map": m, "at": point(m, tile), "to": to, "toName": (areas.get(to) or {}).get("Name") or str(to)}
                 for m, tile, to in doors if m in placed]
        regions.append({"card": card, "name": name, "lv": level, "kind": kind,
                        "w": round(max(m["x"] + m["w"] for m in placed.values()) + 40, 1),
                        "h": round(max(m["y"] + m["h"] for m in placed.values()) + 40, 1),
                        "maps": {str(k): {kk: vv for kk, vv in v.items() if kk not in ("gx", "gy")} for k, v in placed.items()},
                        "arrows": lines, "doors": gates})
        overlap = sum(1 for i, a in enumerate(origin) for b in list(origin)[i + 1:]
                      if origin[a][0] < origin[b][0] + areas[b]["Cols"] and origin[b][0] < origin[a][0] + areas[a]["Cols"]
                      and origin[a][1] < origin[b][1] + areas[b]["Rows"] and origin[b][1] < origin[a][1] + areas[a]["Rows"])
        print(f"  {name}: 맵 {len(placed)} · 화살표 {len(lines)} · 문 {len(gates)} · 겹친 쌍 {overlap}")

    DATA.write_text("window.LOD_ATLAS_LAYOUT = " + json.dumps(
        {"생성": "scripts/gen/world/build-atlas.py", "한눈": SMALL, "확대": LARGE, "지역": regions},
        ensure_ascii=False, separators=(",", ":")) + ";\n", encoding="utf-8")
    print(f"지역 {len(regions)} · 맵 그림 {drawn} -> docs/atlas-maps/ · docs/atlas-layout-data.js")


if __name__ == "__main__":
    raise SystemExit(main())
