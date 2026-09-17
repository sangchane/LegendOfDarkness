#!/usr/bin/env python3
"""게임 클라이언트가 그릴 괴물·NPC 그림 — 서버 템플릿이 부르는 `mns###.MPF` 를 전부 뽑는다.

`build-client-assets.ps1` 의 "Drawing creatures" 단계와 같은 일을 맥에서 하고, **NPC 도 함께** 뽑는다. 서버는 괴물과
NPC 를 같은 번호 체계로 보낸다(ServerFormat07, 0x4000 + 그림 번호 — NPC 템플릿도 `tools/pack-import/import.py` 가
그렇게 쓴다). 클라이언트는 그림이 없는 것을 화면에 올리지 않아(`WorldView.Herd`) **누를 수도 없으므로**, 서버에
세운 것은 모두 뽑아야 한다.

`strip`: 정사각 칸 한 줄과 구간 파일(`frames/stand/walk/attack`)을 함께 낸다 — docs/original-sprite-animation.md 4절.
아카이브는 하데스 `hades.dat` 가 먼저, 없으면 5.99 한국 클라이언트의 `hades.dat`.

  쓰는 법: python3 scripts/build-client-creatures.py [--새것만]
  산출물:  mobile/client/assets/actor/creature/mns###.png · mns###.txt
"""
import importlib.util
import re
import subprocess
import sys
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database"
TEMPLATES = SERVER / "server" / "templates"
ARCHIVES = [SERVER / "archives" / "hades" / "hades.dat", Path.home() / "Downloads" / "5.99 클라이언트" / "hades.dat"]
OUT = ROOT / "mobile" / "client" / "assets" / "actor" / "creature"
DOTNET = ROOT / ".tools" / "dotnet-9.0.317" / "dotnet"
TOOL = ROOT / "tools" / "dat-extract" / "bin" / "Release" / "net8.0" / "dat-extract.dll"
# 0x4000 은 서버 템플릿을 쓰는 쪽(import.py)이 정한다 — 두 곳에 따로 적으면 한쪽만 바뀐다.
_spec = importlib.util.spec_from_file_location("pack_import", ROOT / "tools" / "pack-import" / "import.py")
_pack_import = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_pack_import)
CREATURE_BASE = _pack_import.MONSTER_IMAGE_BASE

# 템플릿은 서버의 너그러운 파서에 맞춰 쓰여 있다(끝에 남은 쉼표, 따옴표 없는 16진수) — JSON 으로 읽지 않고 이 줄만 집는다.
IMAGE = re.compile(r'"Image"\s*:\s*"?(0x[0-9A-Fa-f]+|\d+)"?')

configure_utf8_stdio(sys.stdout, sys.stderr)


def wanted():
    """서버 템플릿이 부르는 그림 번호 → 어느 쪽(괴물/NPC)이 불렀나."""
    found = {}
    for kind, folder in (("괴물", TEMPLATES / "monsters"), ("NPC", TEMPLATES / "mundanes")):
        for path in folder.rglob("*.json"):
            match = IMAGE.search(path.read_text(encoding="utf-8-sig", errors="replace"))
            if not match:
                continue
            written = match.group(1)
            number = (int(written, 16) if written.startswith("0x") else int(written)) - CREATURE_BASE
            if number > 0:
                found.setdefault(number, set()).add(kind)
    return found


def main():
    if not TOOL.exists():
        print(f"도구가 없습니다. 먼저: {DOTNET} build tools/dat-extract/DatExtract.csproj -c Release")
        return 1

    OUT.mkdir(parents=True, exist_ok=True)
    numbers = wanted()
    made, missing = [], []
    for number in sorted(numbers):
        name = f"mns{number:03d}"
        if "--새것만" in sys.argv and (OUT / f"{name}.png").exists():
            continue
        for archive in ARCHIVES:
            if not archive.exists():
                continue
            proc = subprocess.run([str(DOTNET), str(TOOL), "mpf", str(archive), f"{name.upper()}.MPF",
                                   str(OUT / f"{name}.png"), "1", "transparent", "strip"],
                                  capture_output=True, text=True, encoding="utf-8", errors="replace")
            if proc.returncode == 0:
                made.append(number)
                break
        else:
            missing.append(f"{number}({'·'.join(sorted(numbers[number]))})")

    npc = sum(1 for n in numbers if "NPC" in numbers[n])
    print(f"그림 번호 {len(numbers)}종(NPC 가 부르는 것 {npc}) · 뽑음 {len(made)} → {OUT.relative_to(ROOT)}")
    if missing:
        print(f"  두 아카이브 모두 없는 번호 {len(missing)}: {', '.join(missing)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
