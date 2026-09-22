#!/usr/bin/env python3
"""맵 한 장을 그림으로 뽑고, 그 위에 워프가 어느 칸에서 어디로 가는지 찍는다.

트리로는 "이어져 있다"까지만 안다. **문이 문 자리에 있는지**는 그림 위에 좌표를 찍어야 보인다.
지형은 마름모로 그려지므로(칸 56x27, 반칸 28x13) 칸 좌표를 그 투영에 맞춰 픽셀로 옮긴다.

  tools/dat-extract/Program.cs 의 RenderMap 과 같은 식이어야 한다:
      x = rows*28 + (칸x - 칸y)*28 - 28
      y = (칸x + 칸y)*13
  가운데는 거기에 (+28, +13).

  쓰는 법: python3 scripts/build-map-images.py [맵 이름...]   (안 주면 아래 DEFAULT_NAMES 전부)

  **이름을 주면 그것만 남는다.** `out` 을 새로 만들어 통째로 덮어쓰므로, 한 장만 더하려고 이름 하나를
  주면 나머지 맵이 자료에서 사라진다. 한 장을 더할 때는 DEFAULT_NAMES 에 넣고 인자 없이 돌린다.

맵·타일과 워프는 Hades 서버 저장소를 우선한다. 맵마다 Hades 출발 좌표가 하나도 없을 때만
5.99·혼든·Novaonline 서버팩의 출발·도착 좌표가 모두 같은 워프를 ``참고표시``로 넣는다.
"""
import json, subprocess, sys, collections, shutil, struct
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
SEO = SERVER.parent / "archives" / "seo" / "seo.dat"
OUTDIR = ROOT / "docs" / "map-images"
DATA = ROOT / "docs" / "map-images-data.js"
PACK_WARPS = {
    "5.99-server": ROOT / "data" / "server-packs" / "extracted" / "5.99-server" / "warps.json",
    "honden-community": ROOT / "data" / "server-packs" / "extracted" / "honden-community" / "warps.json",
    "novaonline": ROOT / "data" / "server-packs" / "extracted" / "novaonline" / "warps.json",
}

HALF_W, HALF_H = 28, 13
SCALE = 4                      # 5600px 는 브라우저에 너무 크다. 1/4 로 줄여 쓴다.
DEFAULT_NAMES = [
    "노비스마을", "노비스마을식당", "노비스무기방어구상점", "노비스민가1", "노비스민가2",
    "노비스잡화상점", "노비스주점", "노비스평원A", "노비스평원B",
    "노비스지하던전A1", "노비스지하던전A2", "노비스지하던전A3", "노비스지하던전B1", "노비스지하던전B2",
    "노비스지하던전B3", "노비스지하던전C1", "노비스지하던전C2", "노비스지하던전C3",
    # 수오미마을은 노비스에서 포테의숲으로 가는 길의 가운데다 — 동쪽 끝 (99,26) 이 포테의숲1존으로 간다.
    "수오미마을",
    "포테의숲1존", "포테의숲2존", "포테의숲3존", "포테의숲4존", "포테의숲5존", "포테의숲6존",
    "포테의숲보스존",
    "우드랜드입구", "우드랜드1-1", "우드랜드1-2", "우드랜드1-3", "우드랜드2-1",
    "우드랜드3-1", "우드랜드4-1", "우드랜드5-1", "우드랜드6-1", "우드랜드14-1",
]


def pixel(col, row, rows):
    """칸 가운데의 픽셀 자리. dat-extract 의 RenderMap 과 같은 식이어야 한다."""
    return (rows * HALF_W + (col - row) * HALF_W, (col + row) * HALF_H + HALF_H)


def agreed_pack_warps():
    """세 서버팩의 출발/도착 맵과 좌표가 전부 같은 워프만 fallback 후보로 돌려준다."""
    per_pack = {}
    for pack, path in PACK_WARPS.items():
        if not path.exists():
            return []
        packed = {}
        for row in json.loads(path.read_text(encoding="utf-8-sig")):
            try:
                key = (
                    str(row["출발맵"]).strip(), int(row["출발"][0]), int(row["출발"][1]),
                    str(row["도착맵"]).strip(), int(row["도착"][0]), int(row["도착"][1]),
                )
            except (KeyError, IndexError, TypeError, ValueError):
                continue
            packed[key] = str(row.get("출처") or path.name)
        per_pack[pack] = packed

    shared = set.intersection(*(set(rows) for rows in per_pack.values()))
    return [
        {"from": key[0], "x": key[1], "y": key[2], "to": key[3],
         "to_x": key[4], "to_y": key[5],
         "sources": {pack: rows[key] for pack, rows in per_pack.items()}}
        for key in sorted(shared)
    ]


def archive_entries(path):
    """Hades Archive.UnpackArchive와 같은 17바이트 목차를 읽는다."""
    raw = path.read_bytes()
    count = struct.unpack_from("<I", raw, 0)[0]
    entries = {}
    for index in range(count - 1):
        offset = 4 + index * 17
        start = struct.unpack_from("<I", raw, offset)[0]
        name = raw[offset + 4:offset + 17].split(b"\0", 1)[0].decode("ascii")
        end = struct.unpack_from("<I", raw, offset + 17)[0]
        entries[name] = raw[start:end]
    return entries


def render_map_without_dotnet(archive, map_path, columns, rows, output):
    """dotnet이 없는 환경에서 tools/dat-extract RenderMap과 같은 결과를 만든다."""
    from PIL import Image

    entries = archive_entries(archive)
    tile_data = entries["TILEA.BMP"]
    tiles = [tile_data[i:i + 1512] for i in range(0, len(tile_data), 1512)]
    palette_names = sorted(name for name in entries if name.lower().startswith("mpt") and name.lower().endswith(".pal"))
    palettes = [entries[name] for name in palette_names]
    tables = []
    for name, data in entries.items():
        stem = Path(name).stem
        if not (name.lower().startswith("mpt") and name.lower().endswith(".tbl")):
            continue
        if stem[3:].isdigit() or "ani" in stem.lower():
            continue
        for line in data.decode("ascii").splitlines():
            parts = line.split()
            if len(parts) == 3:
                tables.append(tuple(map(int, parts)))
            elif len(parts) == 2:
                minimum, palette = map(int, parts)
                tables.append((minimum - 1, minimum, palette))

    def palette_for(index):
        chosen = 0
        for minimum, maximum, palette in tables:
            if minimum <= index <= maximum:
                chosen = palette
        return palettes[max(0, min(chosen, len(palettes) - 1))]

    width = (columns + rows) * HALF_W
    height = ((columns + rows) * HALF_H) + 27
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    cells = map_path.read_bytes()
    tile_cache = {}
    for row in range(rows):
        for column in range(columns):
            offset = (row * columns + column) * 6
            if offset + 2 > len(cells):
                continue
            floor = struct.unpack_from("<H", cells, offset)[0]
            if floor <= 0 or floor > len(tiles):
                continue
            index = floor - 1
            if index not in tile_cache:
                palette = palette_for(index)
                rgba = bytearray()
                for code in tiles[index]:
                    if code == 0:
                        rgba.extend((0, 0, 0, 0))
                    else:
                        rgba.extend((palette[code * 3], palette[code * 3 + 1], palette[code * 3 + 2], 255))
                tile_cache[index] = Image.frombytes("RGBA", (56, 27), bytes(rgba))
            x = rows * HALF_W + (column - row) * HALF_W - HALF_W
            y = (column + row) * HALF_H
            canvas.alpha_composite(tile_cache[index], (x, y))
    canvas.save(output)


def main(names):
    from PIL import Image

    maps = {}
    for path in (SERVER / "areas").glob("*.json"):
        area = json.loads(path.read_text(encoding="utf-8-sig"))
        maps[area["Name"]] = area
    warps = []
    for path in (SERVER / "templates" / "warps").glob("*.json"):
        warps.append(json.loads(path.read_text(encoding="utf-8-sig")))
    names_by_id = {area["Id"]: area["Name"] for area in maps.values()}
    reference_rows = agreed_pack_warps()
    OUTDIR.mkdir(parents=True, exist_ok=True)

    out = {}
    for name in names:
        area = maps.get(name)
        if not area:
            print(f"  {name}: Hades areas/ 에 없다"); continue
        cols, rows = int(area["Cols"]), int(area["Rows"])
        src = SERVER / "maps" / Path(area["FilePath"]).name
        if not src.exists():
            print(f"  {name}: Hades 맵 파일이 없다 {src.name}"); continue

        big = OUTDIR / f"{name}.full.png"
        if shutil.which("dotnet"):
            subprocess.run(
                ["dotnet", "run", "--project", str(ROOT / "tools/dat-extract"), "-c", "Release", "--",
                 "map", str(SEO), str(src), str(cols), str(rows), str(big)],
                check=True, capture_output=True, cwd=ROOT)
        else:
            render_map_without_dotnet(SEO, src, cols, rows, big)

        im = Image.open(big)
        small = im.resize((im.width // SCALE, im.height // SCALE), Image.LANCZOS)
        small.save(OUTDIR / f"{name}.png", optimize=True)
        big.unlink()

        # 나가는 워프를 출발 칸마다 모은다. 한 칸에서 여러 곳으로 가는 일은 없지만
        # 같은 곳으로 가는 칸이 여럿인 것은 흔하다(문이 두 칸 넓이다).
        marks = collections.defaultdict(list)
        for w in warps:
            if w["ActivationMapId"] != area["Id"]:
                continue
            destination_id = w["To"]["AreaID"]
            destination = "월드맵" if destination_id == 0 else names_by_id.get(destination_id, "맵 #" + str(destination_id))
            for activation in w.get("Activations", []):
                if activation.get("AreaID") != area["Id"]:
                    continue
                cx, cy = int(activation["Location"]["X"]), int(activation["Location"]["Y"])
                marks[(cx, cy)].append(destination)

        out[name] = {
            "그림": f"map-images/{name}.png",
            "폭": small.width, "높이": small.height,
            "칸": [cols, rows],
            "배율": SCALE,
            "표시": [
                {"칸": [cx, cy],
                 "x": round(pixel(cx, cy, rows)[0] / SCALE, 1),
                 "y": round(pixel(cx, cy, rows)[1] / SCALE, 1),
                 "도착": sorted(set(dests))}
                for (cx, cy), dests in sorted(marks.items())
            ],
        }
        n_to = len({d for v in marks.values() for d in v})
        reference_marks = collections.defaultdict(list)
        if not marks:
            for row in reference_rows:
                if row["from"] != name:
                    continue
                reference_marks[(row["x"], row["y"])].append(row)

        out[name]["참고표시"] = [
            {"칸": [cx, cy],
             "x": round(pixel(cx, cy, rows)[0] / SCALE, 1),
             "y": round(pixel(cx, cy, rows)[1] / SCALE, 1),
             "도착": sorted({entry["to"] for entry in entries}),
             "도착칸": sorted({f'{entry["to_x"]},{entry["to_y"]}' for entry in entries}),
             "근거": [entry["sources"] for entry in entries]}
            for (cx, cy), entries in sorted(reference_marks.items())
        ]
        out[name]["참고출처"] = "5.99-server + honden-community + novaonline 완전 일치"
        out[name]["워프출처"] = (
            "Hades templates/warps" if marks else
            "서버팩 3개 일치" if reference_marks else
            "없음"
        )
        print(f"  {name}: Hades · {small.width}x{small.height} · 워프 칸 {len(marks)}개 → {n_to}곳 · "
              f"서버팩 합의 칸 {len(reference_marks)}개 · "
              f"{(OUTDIR / (name + '.png')).stat().st_size // 1024} KB")

    DATA.write_text("window.MAP_IMAGES = " + json.dumps(out, ensure_ascii=False) + ";\n", encoding="utf-8")
    print(f"→ {DATA.relative_to(ROOT)}")


if __name__ == "__main__":
    main(sys.argv[1:] or DEFAULT_NAMES)
