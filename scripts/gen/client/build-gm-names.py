#!/usr/bin/env python3
"""운영자 명령 자동완성 이름표 — 앱 대화창이 `/give`·`/spell`·`/skill`·`/tp` 뒤에 고를 이름(사용자 2026-10-04).

서버 명령(`Systems/Commander.cs`)은 이름으로 찾는다: 아이템·마법·기술은 템플릿 Name, 순간이동은 맵 Name 과 칸.
맵 칸은 그 맵으로 드는 워프의 도착 칸 중 이름 순 첫 것(벽이 아닌 칸) — 워프가 없는 맵은 뺀다.

  쓰는 법: python3 scripts/gen/client/build-gm-names.py
  산출물:  mobile/client/assets/gm-names.txt — 한 줄에 `item 이름` · `spell 이름` · `skill 이름` · `map 이름 x y`
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))   # scripts/ — lib/
from lib._paths import ROOT
from lib._world import AREAS, SERVER, WARPS

OUT = ROOT / "mobile" / "client" / "assets" / "gm-names.txt"


def names(folder):
    found = set()
    for path in (SERVER / "templates" / folder).rglob("*.json"):
        name = json.loads(path.read_text(encoding="utf-8-sig")).get("Name")
        if name and "\n" not in name:
            found.add(name)
    return sorted(found)


def main():
    maps = {}
    for path in AREAS.glob("*.json"):
        area = json.loads(path.read_text(encoding="utf-8-sig"))
        maps[area["Id"]] = area["Name"]
    landing = {}
    for path in sorted(WARPS.glob("*.json")):
        to = json.loads(path.read_text(encoding="utf-8-sig"))["To"]
        if to.get("Location") and to["AreaID"] in maps:
            landing.setdefault(maps[to["AreaID"]], (to["Location"]["X"], to["Location"]["Y"]))

    lines = ["# tools: scripts/gen/client/build-gm-names.py 가 서버 템플릿에서 만든다. 손으로 고치지 말 것."]
    lines += [f"{kind} {n}" for kind, folder in (("item", "items"), ("spell", "spells"), ("skill", "skills")) for n in names(folder)]
    lines += [f"map {n} {x} {y}" for n, (x, y) in sorted(landing.items())]
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{OUT.name}: " + " · ".join(f"{k} {sum(l.startswith(k + ' ') for l in lines)}" for k in ("item", "spell", "skill", "map")))


if __name__ == "__main__":
    main()
