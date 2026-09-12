#!/usr/bin/env python3
"""맵 한 장을 그림으로 뽑고, 그 위에 워프가 어느 칸에서 어디로 가는지 찍는다.

트리로는 "이어져 있다"까지만 안다. **문이 문 자리에 있는지**는 그림 위에 좌표를 찍어야 보인다.
지형은 마름모로 그려지므로(칸 56x27, 반칸 28x13) 칸 좌표를 그 투영에 맞춰 픽셀로 옮긴다.

  tools/dat-extract/Program.cs 의 RenderMap 과 같은 식이어야 한다:
      x = rows*28 + (칸x - 칸y)*28 - 28
      y = (칸x + 칸y)*13
  가운데는 거기에 (+28, +13).

  쓰는 법: python3 scripts/build-map-images.py 마인마을 [더 그릴 맵 이름...]
"""
import json, subprocess, sys, collections
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PACK = "5.99-server"
EXTRACTED = ROOT / "data" / "server-packs" / "extracted" / PACK
MAPSRC = ROOT / "data" / "map-source" / PACK
SEO = Path.home() / "Downloads" / "5.99 클라이언트" / "seo.dat"
OUTDIR = ROOT / "docs" / "map-images"
DATA = ROOT / "docs" / "map-images-data.js"

HALF_W, HALF_H = 28, 13
SCALE = 4                      # 5600px 는 브라우저에 너무 크다. 1/4 로 줄여 쓴다.


def pixel(col, row, rows):
    """칸 가운데의 픽셀 자리. dat-extract 의 RenderMap 과 같은 식이어야 한다."""
    return (rows * HALF_W + (col - row) * HALF_W, (col + row) * HALF_H + HALF_H)


def main(names):
    from PIL import Image

    maps = json.loads((EXTRACTED / "maps.json").read_text(encoding="utf-8"))
    warps = json.loads((EXTRACTED / "warps.json").read_text(encoding="utf-8"))
    OUTDIR.mkdir(parents=True, exist_ok=True)

    out = {}
    for name in names:
        hit = [m for m in maps if m["이름"] == name]
        if not hit:
            print(f"  {name}: maps.json 에 없다"); continue
        f = hit[0]["fields"]
        cols, rows = int(f["너비"]), int(f["높이"])
        src = MAPSRC / f["맵파일"]
        if not src.exists():
            print(f"  {name}: 맵 파일이 없다 {f['맵파일']}"); continue

        big = OUTDIR / f"{name}.full.png"
        subprocess.run(
            ["dotnet", "run", "--project", str(ROOT / "tools/dat-extract"), "-c", "Release", "--",
             "map", str(SEO), str(src), str(cols), str(rows), str(big)],
            check=True, capture_output=True, cwd=ROOT)

        im = Image.open(big)
        small = im.resize((im.width // SCALE, im.height // SCALE), Image.LANCZOS)
        small.save(OUTDIR / f"{name}.png", optimize=True)
        big.unlink()

        # 나가는 워프를 출발 칸마다 모은다. 한 칸에서 여러 곳으로 가는 일은 없지만
        # 같은 곳으로 가는 칸이 여럿인 것은 흔하다(문이 두 칸 넓이다).
        marks = collections.defaultdict(list)
        for w in warps:
            if w["출발맵"] != name:
                continue
            cx, cy = int(w["출발"][0]), int(w["출발"][1])
            px, py = pixel(cx, cy, rows)
            marks[(cx, cy)].append(w["도착맵"])

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
        print(f"  {name}: {small.width}x{small.height} · 워프 칸 {len(marks)}개 → {n_to}곳 · "
              f"{(OUTDIR / (name + '.png')).stat().st_size // 1024} KB")

    DATA.write_text("window.MAP_IMAGES = " + json.dumps(out, ensure_ascii=False) + ";\n", encoding="utf-8")
    print(f"→ {DATA.relative_to(ROOT)}")


if __name__ == "__main__":
    main(sys.argv[1:] or ["마인마을"])
