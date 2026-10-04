#!/usr/bin/env python3
"""맵 배경음 채우기 — 음악이 없거나(0), 효과음 번호로 적혔거나(128 미만), 앱에 없는 곡인 맵에 배경음을 넣는다(사용자 2026-10-04 「빈 맵 전부」).

서버는 맵의 `Music` 을 0x19 로 그대로 보내고 앱은 128 이상을 곡(번호 − 128, `mobile/client/assets/music/<n>.ogg`)으로 튼다.
이미 앱이 틀 수 있는 값은 건드리지 않는다(하데스 자기 자료가 먼저). 고칠 맵은 차례로:
  1. 같은 이름 맵의 팩 배경음 — 5.99 팩 → 노바 → 혼든. 혼든은 곡 번호 체계가 달라(아벨마을 혼든 58 · 5.99 141) 두 팩에 같은 이름으로
     있는 맵들에서 가장 많이 짝지어진 5.99 번호로 바꿔 쓴다.
  2. 같은 묶음(이름에서 층·구역 숫자를 뗀 것 — 구광산5-1 → 구광산, 레드오피온의굴 10층 → 레드오피온의굴)의 다른 맵에서 가장 많이 쓰는 곡.
  3. 그래도 없으면 괴물이 사는 맵은 사냥터들이, 아니면 마을·건물들이 가장 많이 쓰는 곡.

  쓰는 법: python3 scripts/gen/world/build-map-music.py          # 무엇이 바뀌는지만
           python3 scripts/gen/world/build-map-music.py --쓰기   # 서버 맵 정의에 쓴다(Music 칸만)
"""
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))   # scripts/ — lib/
from lib._paths import ROOT
from lib._world import AREAS, SERVER

FIRST = 128
SONGS = {int(p.stem) for p in (ROOT / "mobile" / "client" / "assets" / "music").glob("*.ogg")}
PACKS = [
    ("5.99", Path.home() / "Downloads" / "5.99 서버팩" / "db" / "maps", "cp949"),
    ("노바", ROOT / "data" / "server-packs" / "novaonline" / "db" / "maps", "utf-8"),
    ("혼든", ROOT / "data" / "server-packs" / "honden-community" / "db" / "maps", "utf-8"),
]


def playable(music):
    return music is not None and music >= FIRST and music - FIRST in SONGS


def pack_music(folder, encoding):
    """이름 → 배경음. 맵 정의 블록 `{ … 이름 … 배경음 … }`."""
    out = {}
    for path in folder.rglob("*.txt"):
        text = path.read_bytes().decode(encoding, errors="replace")
        for block in re.findall(r"\{([^{}]*)\}", text):
            name = re.search(r"^이름\t(.+?)\s*$", block, re.M)
            music = re.search(r"^배경음?\t(\d+)", block, re.M)          # 5.99 일부 파일은 칸 이름이 「배경」
            if name and music:
                out.setdefault(name[1], int(music[1]))
    return out


def family(name):
    """층·구역 숫자를 뗀 묶음 이름 — 구광산5-1 → 구광산, 레드오피온의굴 10층 → 레드오피온의굴, 흉가1층-방5 → 흉가."""
    return re.sub(r"[\s\d\-]*(층|존)?([\s\-]*(방|B|A|C|D|E)?\d*)*$", "", re.sub(r"\(.*?\)", "", name)).strip() or name


def main():
    write = "--쓰기" in sys.argv
    packs = {label: pack_music(folder, enc) if folder.exists() else {} for label, folder, enc in PACKS}
    pairs = defaultdict(Counter)                                  # 혼든 번호 → 5.99 번호
    for name, honden in packs["혼든"].items():
        if name in packs["5.99"]:
            pairs[honden][packs["5.99"][name]] += 1
    honden_to_599 = {h: c.most_common(1)[0][0] for h, c in pairs.items()}

    areas = []
    for path in sorted(AREAS.glob("*.json")):
        raw = path.read_bytes().decode("utf-8-sig")
        areas.append((path, raw, json.loads(raw)))

    chosen, why = {}, {}
    for path, _, area in areas:
        if playable(area.get("Music")):
            continue
        name = area["Name"]
        for label in ("5.99", "노바", "혼든"):
            value = packs[label].get(name)
            value = honden_to_599.get(value) if label == "혼든" else value
            if playable(value):
                chosen[path], why[path] = value, f"{label} 같은 이름"
                break

    kin = defaultdict(Counter)                                    # 묶음 → 틀 수 있는 곡(지금 값 + 1단계에서 고른 값)
    for path, _, area in areas:
        value = chosen.get(path, area.get("Music"))
        if playable(value):
            kin[family(area["Name"])][value] += 1
    for path, _, area in areas:
        if path in chosen or playable(area.get("Music")):
            continue
        if kin[family(area["Name"])]:
            chosen[path] = kin[family(area["Name"])].most_common(1)[0][0]
            why[path] = f"묶음 「{family(area['Name'])}」"

    # 3. 괴물이 사는 맵인가로 사냥터 곡·마을 곡.
    hunted = set()
    for monster in (SERVER / "templates" / "monsters").rglob("*.json"):
        found = re.search(r'"AreaID"\s*:\s*(\d+)', monster.read_text(encoding="utf-8-sig", errors="replace"))
        if found:
            hunted.add(int(found[1]))
    common = {True: Counter(), False: Counter()}
    for path, _, area in areas:
        value = chosen.get(path, area.get("Music"))
        if playable(value):
            common[area["Id"] in hunted][value] += 1
    for path, _, area in areas:
        if path in chosen or playable(area.get("Music")):
            continue
        field = area["Id"] in hunted
        chosen[path] = common[field].most_common(1)[0][0]
        why[path] = "사냥터 곡" if field else "마을 곡"

    left = [area["Name"] for path, _, area in areas if not playable(area.get("Music")) and path not in chosen]
    for path, raw, area in areas:
        if path not in chosen:
            continue
        print(f"  {area['Name']:24} {area.get('Music') or 0:>4} → {chosen[path]} (곡 {chosen[path] - FIRST}) · {why[path]}")
        if write:
            new = re.sub(r'"Music":\s*-?\d+', f'"Music": {chosen[path]}', raw, count=1)
            path.write_bytes(new.encode("utf-8-sig" if path.read_bytes().startswith(b"\xef\xbb\xbf") else "utf-8"))
    print("\n" + " · ".join(f"{k} {v}" for k, v in Counter(w.split(" 「")[0] for w in why.values()).most_common()))
    print(f"채움 {len(chosen)} · 못 채움 {len(left)}: {' · '.join(left[:40])}{' …' if len(left) > 40 else ''}")
    print(f"혼든 → 5.99 곡 짝 {len(honden_to_599)}개" + ("" if write else "  — 미리보기, --쓰기 로 쓴다"))


if __name__ == "__main__":
    main()
