#!/usr/bin/env python3
"""홈페이지 「퀘스트」 탭 그림 — 에리얼 퀘스트 공략(사용자 2026-10-11 「홈페이지에 퀘스트탭 만들어서 자세한 공략정보」).

  - 지도 넷: 3-D(입구) · 게길(시작 · 게 15자리 · 끝) · 대기실(도착·문) · 에리얼의방(도착·에리얼·마레) — 둘 다 3-D 사본.
    바닥은 `build-map-images.py` 렌더러(원작 seo.dat), 막힌 칸은 앱 배치 글(`mobile/client/assets/world/map<번호>.txt`).
    자리는 서버 대본에서 읽는다(`Pack599/Quests/에리얼입구.cs` 의 게 자리) — 대본을 바꾸면 다시 돌린다.
  - 그림: 앱 괴물 그림 첫 장(게 · 에리얼 · 마레 · 에리얼입구) · 아이템 아이콘(재료 둘 · 팬던트 · 파파야방패).

  쓰는 법: python3 scripts/gen/vault/build-quest-images.py   → docs/quests/*.webp · *.png
"""
import importlib.util
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))   # scripts/ — lib/
from lib._paths import ROOT
from PIL import Image, ImageDraw, ImageFont

SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
WORLD = ROOT / "mobile" / "client" / "assets" / "world"
CREATURES = ROOT / "mobile" / "client" / "assets" / "actor" / "creature"
ICONS = ROOT / "mobile" / "client" / "assets" / "item"
OUT = ROOT / "docs" / "quests"
FONT = "/System/Library/Fonts/AppleSDGothicNeo.ttc"
SHORE, TRAIL = 20881, 20882

spec = importlib.util.spec_from_file_location("mapimages", ROOT / "scripts" / "gen" / "world" / "build-map-images.py")
mapimages = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mapimages)


def crabs():
    script = (SERVER / "scripts" / "Pack599" / "Quests" / "에리얼입구.cs").read_text(encoding="utf-8-sig")
    line = next(l for l in script.splitlines() if "Crabs =" in l)
    return [(int(x), int(y)) for x, y in re.findall(r"\((\d+), (\d+)\)", line)]


def coma():
    """킹아크퍼스 외딴길 코마 칸 — 대본 `Coma` 배열(여러 줄)."""
    script = (SERVER / "scripts" / "Pack599" / "Quests" / "킹아크퍼스암초.cs").read_text(encoding="utf-8-sig")
    block = script[script.index("Coma =") : script.index("};", script.index("Coma ="))]
    return [(int(x), int(y)) for x, y in re.findall(r"\((\d+), (\d+)\)", block)]


def king_arcfus(gold, blue, green, pink, purple, red):
    """킹아크퍼스 퀘스트(2026-10-11) — 3-C 숨은 칸 · 외딴곳 · 4-C 바위 틈 · 외딴길 코마밭 · 숨겨진길 A·B · 두 본거지."""
    render(20591, 50, 50, [((20, 37), "숨은 칸", gold)], OUT / "arcfus-hole.webp", crop=((8, 26), (34, 46)), width=1100)
    render(20883, 10, 10, [((5, 8), "도착", blue), ((6, 5), "브라토", gold), ((9, 8), "<3-C 로", green)], OUT / "arcfus-nook.webp", width=900)
    render(20594, 50, 50, [((10, 39), "바위 틈", gold)], OUT / "arcfus-reef.webp", crop=((0, 28), (24, 48)), width=1100)
    render(20884, 40, 60, [((21, 57), "시작", blue), ((12, 0), "<숨겨진길로", green)] + [(c, "", red) for c in coma()],
           OUT / "arcfus-trail.webp")
    render(20885, 14, 14, [((1, 1), "도착", blue), ((12, 13), "<다음 방", green)], OUT / "arcfus-road-a.webp", width=900)
    render(20886, 13, 13, [((1, 1), "도착", blue), ((12, 11), "<본거지로", green)], OUT / "arcfus-road-b.webp", width=900)
    render(20887, 20, 20, [((5, 5), "도착", blue), ((14, 14), "거대문어", purple), ((19, 19), "<대기실로", green)], OUT / "arcfus-fake.webp", width=1000)
    render(20888, 12, 12, [((1, 9), "도착", blue), ((8, 4), "킹아크퍼스", purple), ((10, 10), "<미레", pink)], OUT / "arcfus-lair.webp", width=900)
    for number, name in ((254, "arcfus"), (237, "octopus")):
        sprite(number, OUT / f"{name}.png")
    for file, name in (("터틀껍질", "turtle-shell"), ("게껍질", "crab-shell"), ("퐁퐁이조각", "pongpong"), ("대지의인장", "seal"),
                       ("해저의팬던트", "sea-pendant"), ("문어다리", "octopus-leg"), ("문어의원한", "grudge"), ("원한의에센스", "essence"),
                       ("세줄금반지", "ring")):
        image = json.loads((SERVER / "templates" / "items" / f"{file}.json").read_text(encoding="utf-8-sig"))["DisplayImage"]
        Image.open(ICONS / f"{image}.png").convert("RGBA").resize((64, 64), Image.NEAREST).save(OUT / f"{name}.png")


def render(number, cols, rows, marks, out, crop=None, width=1400):
    full = OUT / f".{number}-full.png"
    mapimages.render_map_without_dotnet(mapimages.SEO, SERVER / "maps" / f"lod{number}.map", cols, rows, full)
    image = Image.open(full).convert("RGB")
    full.unlink()
    draw = ImageDraw.Draw(image, "RGBA")
    blocked = (WORLD / f"map{number}.txt").read_text(encoding="utf-8").split("\n")
    top = blocked.index("blocked") + 1
    for y in range(rows):
        for x, cell in enumerate(blocked[top + y][:cols]):
            if cell == "#":
                px, py = mapimages.pixel(x, y, rows)
                draw.polygon([(px + 28, py), (px + 56, py + 13), (px + 28, py + 26), (px, py + 13)], fill=(10, 14, 30, 70))
    # 줄여도 읽히게 — 맵을 줄이는 비율만큼 점·글자를 키운다(80칸 3-D 는 약 3배).
    shown = image.width if not crop else (crop[1][0] - crop[0][0] + crop[1][1] - crop[0][1]) * 28
    grow = max(1.0, shown / width)
    font = ImageFont.truetype(FONT, round(30 * grow))
    r = round(12 * grow)
    for (x, y), label, colour in marks:
        px, py = mapimages.pixel(x, y, rows)
        cx, cy = px + 28, py + 13
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=colour, outline=(0, 0, 0), width=max(3, r // 4))
        if label:
            left = label.startswith("<")             # 오른쪽 가장자리 표시는 이름표를 점 왼쪽에
            label = label.lstrip("<")
            at = cx - r - 6 - draw.textlength(label, font=font) if left else cx + r + 6
            draw.text((at, cy - round(20 * grow)), label, fill=colour, font=font, stroke_width=max(4, r // 3), stroke_fill=(0, 0, 0))
    if crop:
        (x0, y0), (x1, y1) = crop
        corners = [mapimages.pixel(x, y, rows) for x, y in ((x0, y0), (x0, y1), (x1, y0), (x1, y1))]
        image = image.crop((min(c[0] for c in corners), min(c[1] for c in corners),
                            max(c[0] for c in corners) + 56, max(c[1] for c in corners) + 27))
    image.thumbnail((width, width))
    image.save(out, "WEBP", quality=82)


def sprite(number, out, scale=2):
    strip = Image.open(CREATURES / f"mns{number:03d}.png").convert("RGBA")
    frames = int(re.search(r"frames (\d+)", (CREATURES / f"mns{number:03d}.txt").read_text()).group(1))
    one = strip.crop((0, 0, strip.width // frames, strip.height))
    one = one.crop(one.getbbox())
    one.resize((one.width * scale, one.height * scale), Image.NEAREST).save(out)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    gold, blue, green, pink, purple, red = (255, 214, 64), (110, 200, 255), (120, 240, 150), (255, 150, 200), (200, 140, 255), (255, 110, 90)
    render(SHORE, 80, 50, [((1, 21), "3-C 에서", blue), ((57, 25), "에리얼입구", gold)], OUT / "aerial-shore.webp")
    render(TRAIL, 70, 30, [((1, 6), "시작", blue), ((69, 6), "<대기실로", green)] + [(c, "", red) for c in crabs()],
           OUT / "aerial-trail.webp")
    room = ((14, 20), (54, 38))
    render(SHORE, 80, 50, [((20, 30), "도착", blue), ((45, 29), "에리얼의방 문", green)], OUT / "aerial-lobby.webp", crop=room, width=1100)
    render(SHORE, 80, 50, [((20, 30), "도착", blue), ((45, 30), "에리얼", purple), ((48, 27), "마레", pink)],
           OUT / "aerial-room.webp", crop=room, width=1100)
    for number, name in ((99, "crab"), (267, "aerial"), (61, "mare"), (249, "gatekeeper")):
        sprite(number, OUT / f"{name}.png")
    for file, name in (("블루하콘무기", "blue-hakon"), ("레드하콘다리", "red-hakon"), ("킹아크퍼스의팬던트", "pendant"), ("파파야방패", "papaya")):
        image = json.loads((SERVER / "templates" / "items" / f"{file}.json").read_text(encoding="utf-8-sig"))["DisplayImage"]
        Image.open(ICONS / f"{image}.png").convert("RGBA").resize((64, 64), Image.NEAREST).save(OUT / f"{name}.png")
    king_arcfus(gold, blue, green, pink, purple, red)
    print(f"→ {OUT.relative_to(ROOT)}: " + " ".join(sorted(p.name for p in OUT.iterdir() if not p.name.startswith("."))))


if __name__ == "__main__":
    main()
