#!/usr/bin/env python3
"""기술이 부르는 소리를 원작 아카이브에서 꺼낸다.

스크립트의 `game_sound N` 이 그대로 `Legend.dat` 의 `N.mp3` 다 — `사운드: [80]` 이면 `80.mp3`.
번호가 곧 파일 이름이라 짝을 맞출 표가 따로 필요 없다.

아카이브에는 165개가 들어 있지만 기술이 실제로 부르는 것은 36개뿐이라 그것만 남긴다. 나머지는
걸음·문·장터 같은 세계의 소리다.

  쓰는 법: python3 scripts/build-ability-sounds.py
  산출물:  docs/ui/assets/ability-sounds/<번호>.mp3
"""
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
EFFECTS = ROOT / "data" / "game-data" / "ability-effects.json"
ARCHIVE = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives" / "legend" / "Legend.dat"
DEST = ROOT / "docs" / "ui" / "assets" / "ability-sounds"

configure_utf8_stdio(sys.stdout, sys.stderr)


def wanted():
    """기술이 부르는 소리 번호."""
    data = json.loads(EFFECTS.read_text(encoding="utf-8"))["밑말"]
    found = set()
    for entry in data.values():
        for level in entry["레벨"]:
            for number in level.get("사운드") or []:
                found.add(int(number))
    return sorted(found)


def main():
    if not ARCHIVE.exists():
        print(f"원작 아카이브가 없습니다: {ARCHIVE.relative_to(ROOT)}")
        return 1

    numbers = wanted()
    DEST.mkdir(parents=True, exist_ok=True)

    # 한 번에 다 꺼낸 뒤 골라 옮긴다. dump 는 이름 조각으로 거르는데 `8` 로 거르면 8·18·80·81 이
    # 다 걸리므로, 한 번 꺼내 놓고 번호로 고르는 쪽이 짧고 틀릴 여지가 없다.
    with tempfile.TemporaryDirectory() as scratch:
        proc = subprocess.run(
            ["dotnet", "run", "--project", str(ROOT / "tools" / "dat-extract"), "-c", "Release", "--",
             "dump", str(ARCHIVE), scratch, ".mp3"],
            capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)

        if proc.returncode != 0:
            print(proc.stderr.strip() or proc.stdout.strip())
            return 1

        # dump 는 아카이브 이름의 하위 폴더에 넣는다(`Legend/80.mp3`). 바로 밑을 보면 하나도 없다.
        holding = next((path for path in Path(scratch).rglob("0.mp3")), None)
        inside = holding.parent if holding else Path(scratch)

        taken, missing = [], []
        for number in numbers:
            source = inside / f"{number}.mp3"
            if not source.exists():
                missing.append(number)
                continue
            shutil.copyfile(source, DEST / f"{number}.mp3")
            taken.append(number)

    print(f"소리 {len(taken)}개 → {DEST.relative_to(ROOT)}")
    if missing:
        print(f"  아카이브에 없는 번호 {len(missing)}개: {', '.join(str(n) for n in missing)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
