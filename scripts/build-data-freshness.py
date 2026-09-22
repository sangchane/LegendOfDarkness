#!/usr/bin/env python3
"""화면이 읽는 자료가 근거보다 낡았는지 잰다.

**이 화면이 뜻을 잃는 가장 흔한 길이 이것이다.** 2026-09-19 에 기술 구현 수가 95 인데 화면은
69 로 보여 주고 있었다 — `docs/abilities-data.js` 가 사흘 전 것이었고, **화면은 그 사실을 알
방법이 없었다.** 사람이 눈치채기 전에는 틀린 숫자가 맞는 숫자처럼 보인다.

그래서 자료마다 「만든 때」와 「그 자료의 근거가 마지막으로 바뀐 때」를 나란히 적어 둔다.
근거가 더 새것이면 낡은 것이다. 내용은 안 읽는다 — 수정 시각만 본다.

**화면에는 안 띄운다.** 한동안 대시보드 맨 위에 빨간 띠로 걸었는데, 보러 온 사람에게는 「왜 낡았나」가
군더더기라 자리만 차지했다(사용자, 2026-09-19). 이 셈이 필요한 것은 **자료를 다시 만드는 사람**이므로
아래 터미널 출력과 `docs/data-freshness.js` 파일로만 남긴다.

  쓰는 법: python3 scripts/build-data-freshness.py   → docs/data-freshness.js
"""
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server"
OUT = ROOT / "docs" / "data-freshness.js"
HADES = "sources/wren11/Dark-Ages-Private-Server/database/server"

# 같은 판에서 찍혀 나온 것끼리 서로 낡았다고 하지 않도록 두는 여유(초).
SLACK = 5

# 자료 파일 → 그것을 만드는 생성기 → 그 생성기가 읽는 곳.
# 근거는 폴더여도 되고 파일이어도 된다. 없는 경로는 조용히 건너뛴다.
SHEETS = [
    ("abilities-data.js", "scripts/build-ability-page-data.py", [
        "data/game-data/abilities.json", "data/기술마법-한글이름.tsv", f"{HADES}/scripts"]),
    ("items-data.js", "scripts/build-item-page-data.py", [
        "data/game-data/items-hades.json", "data/pack-compare/item-korean-names.json",
        "docs/ui/assets/item-icons.json"]),
    ("monsters-data.js", "scripts/build-monster-page-data.py", [
        f"{HADES}/areas", f"{HADES}/templates/monsters", f"{HADES}/templates/items"]),
    ("region-warps-data.js", "scripts/build-region-warp-data.py", [
        f"{HADES}/areas", f"{HADES}/templates/warps",
        f"{HADES}/templates/monsters", f"{HADES}/templates/mundanes"]),
    ("feature-map-data.js", "scripts/build-feature-map-data.py", ["docs/feature-map.md"]),
    ("world-map-data.js", "scripts/build-world-map-data.py", [
        f"{HADES}/areas", f"{HADES}/templates/warps"]),
    ("map-images-data.js", "scripts/build-map-images.py", [f"{HADES}/templates/warps"]),
    # 그림을 자르는 쪽이 이 파일도 함께 쓴다 — 바탕·기준점이 그림에서 나오기 때문이다.
    # 어느 번호를 자를지는 `ability-presentation.json`(게임이 실제로 쏘는 번호)이 정한다.
    ("ability-effects-data.js", "scripts/build-ability-sprites.py", [
        "data/game-data/ability-presentation.json"]),
    ("body-motions-data.js", "scripts/build-body-motions.py", [
        "data/game-data/ability-presentation.json", "data/legend-tables/skill.tbl"]),
    # 손으로 적는 자료는 생성기가 없다. 낡았는지는 사람만 안다 — 그래서 근거도 비운다.
    ("changes-data.js", None, []),
    ("dashboard-data.js", None, []),
]


def when(path):
    """가장 마지막으로 바뀐 때. 폴더면 그 안에서 가장 새것을 찾는다."""
    if not path.exists():
        return None
    if path.is_file():
        return path.stat().st_mtime
    newest = 0.0
    for child in path.rglob("*"):
        if child.is_file():
            newest = max(newest, child.stat().st_mtime)
    return newest or None


def stamp(seconds):
    if not seconds:
        return None
    return datetime.fromtimestamp(seconds, timezone.utc).isoformat(timespec="seconds")


def main():
    rows = []
    for name, builder, sources in SHEETS:
        sheet = ROOT / "docs" / name
        made = when(sheet)
        newest, where = None, None
        for source in sources:
            found = when(ROOT / source)
            if found and (newest is None or found > newest):
                newest, where = found, source

        # 근거가 자료보다 새것이면 낡았다. 근거를 모르면(손으로 적는 것) 판정하지 않는다.
        # 한 번에 여러 파일을 찍어내는 생성기는 자기 입력을 자기보다 몇 밀리초 늦게 건드릴 수
        # 있어 몇 초의 여유를 둔다 — 안 그러면 방금 만든 것이 낡았다고 나온다.
        rows.append({
            "파일": name,
            "있음": bool(made),
            "생성기": builder,
            "만든때": stamp(made),
            "근거최신": stamp(newest),
            "근거": where,
            "낡음": bool(made and newest and newest > made + SLACK),
        })

    try:
        pointer = subprocess.run(
            ["git", "-C", str(SERVER.parents[1]), "rev-parse", "--short", "HEAD"],
            capture_output=True, text=True, check=True).stdout.strip()
    except Exception:
        pointer = ""

    payload = {
        "생성": "scripts/build-data-freshness.py",
        "잰때": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "서버포인터": pointer,
        "자료": rows,
        "셈": {
            "전체": len(rows),
            "낡음": sum(1 for row in rows if row["낡음"]),
            "없음": sum(1 for row in rows if not row["있음"]),
        },
    }
    OUT.write_text(
        "window.LOD_FRESHNESS = " + json.dumps(payload, ensure_ascii=False) + ";\n",
        encoding="utf-8")

    for row in rows:
        mark = "낡음" if row["낡음"] else ("없음" if not row["있음"] else "최신")
        print(f"{mark}  {row['파일']:<24} {row['만든때'] or '—'}  근거 {row['근거최신'] or '—'}")
    print(f"→ {OUT.relative_to(ROOT)}  (낡음 {payload['셈']['낡음']} / {len(rows)})")


if __name__ == "__main__":
    main()
