"""밟으면 스크립트가 도는 워프(서버 `WarpTemplate.ScriptNpc`)가 실제로 가는 곳.

이런 워프는 템플릿의 `To` 가 제자리다 — 어디로 보낼지는 NPC 스크립트가 정한다. 포테의숲5존 26·27,0 은
`포테의숲오솔길입장` 이 `map_create` 로 그 사람 전용 사본(개인 던전)을 짓고 보낸다(docs/pote-forest.md 3-2).
그대로 뽑으면 출구 이름이 "포테의숲5존"(선 곳 자신)이 되어 지도에 이동 정보가 없는 셈이다.

스크립트의 첫 `map_create` 에서 5.99 가 보여 주는 이름과 팩 맵 파일을 읽고, 맵 파일은
`static/pack599-mapfiles.tsv` 로 서버 맵 번호로 바꾼다. 못 찾으면 None — 부르는 쪽은 원래대로 둔다.

  build-client-guide.py · build-map-images.py · build-world-map-data.py 가 쓴다.
"""
import re
from pathlib import Path

MAP_CREATE = re.compile(r'p\.Call\("map_create",[^;]*?\(V\)"([^"]+)",[^;]*\(V\)"([^"]+\.map)"\)')


def script_warp_destination(server: Path, warp: dict):
    """(서버 맵 번호, 보일 이름) 또는 None. 보일 이름은 "포테의숲오솔길(개인 던전)" 처럼 사본임을 붙인다."""
    npc = warp.get("ScriptNpc")
    if not npc:
        return None

    table = server / "static" / "pack599-mapfiles.tsv"
    files = {}
    if table.exists():
        for line in table.read_text(encoding="utf-8-sig").splitlines():
            parts = line.split("\t")
            if len(parts) == 2 and parts[1].strip().isdigit():
                files[parts[0].strip()] = int(parts[1])

    for path in (server / "scripts" / "Pack599" / "Npcs").glob("*.cs"):
        text = path.read_text(encoding="utf-8-sig")
        if f'[Script("{npc}"' not in text:
            continue
        hit = MAP_CREATE.search(text)
        if hit and hit.group(2) in files:
            return files[hit.group(2)], f"{hit.group(1)}(개인 던전)"
    return None
