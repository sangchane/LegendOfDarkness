#!/usr/bin/env python3
"""게임 클라이언트가 그릴 직업 기술 동작 그림을 옷 부위마다 뽑는다.

서버는 몸 동작을 번호로 보낸다(0x1A) — 1 은 평타(`…02`), 128 부터는 원작 `skill.tbl` 의 NO 줄이고
그 줄이 파일 글자를 고른다: b 성직자 · c 전사 · d 무도가 · e 도적 · f 마법사
(docs/original-sprite-animation.md 3절, `Lod.Mobile.Core.Art.BodyMotion`). 관리페이지용
`build-body-motions.py` 는 몸 한 장의 앞모습만 뽑고, 이것은 클라이언트가 겹쳐 입힐 부위마다 전부 뽑는다.

부위 그림(`…01` 서기·걷기, `…02` 평타)은 `build-client-assets.ps1` 이 뽑아 둔 부위 목록을 따르고, 그 옆에
`mb001d.png` 처럼 파일 글자를 붙여 둔다. 칸·발 기준은 같다(80x88, 표시색 염색).

**부위마다 한 아카이브에서 전부 뽑는다 — 5.99 한국 클라이언트에 그 부위가 있으면 5.99.** 몸·신발·머리는 두
아카이브가 같지만 바지·갑옷은 5.99 쪽 그림이 다르다(`mn00101` 하데스 6,071 · 5.99 4,716 바이트). 하데스
바지는 동작 칸도 모자라(`mn001c` 14칸 · 5.99 30칸) 전사 139~141 에서 바지가 사라졌다. 걷기는 하데스, 기술은
5.99 로 섞으면 옷이 동작마다 바뀌므로 그런 부위는 `01`·`02` 도 5.99 에서 다시 뽑는다.
두 쪽 다 없는 파일(바지의 도적 동작, 방패의 기술 동작)은 만들지 않는다 — 클라이언트는 그 동작 동안 그 부위를
그리지 않는다. **직업 동작은 그 직업 의상에서만 원작이 지원한다**(`skill.tbl` ST) — 기본 옷으로 깨져 보이는
것은 원작도 그렇다. 그래서 동작 확인용으로 전사 옷 2번 · 도적 옷 4번을 함께 뽑는다.

  쓰는 법: python3 scripts/build-client-motions.py
  산출물:  mobile/client/assets/actor/parts/<부위><글자>.png
"""
import re
import subprocess
import sys
import tempfile
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
PARTS = ROOT / "mobile" / "client" / "assets" / "actor" / "parts"
HADES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives"
#: 5.99 한국 클라이언트. 저장소 밖에 있다 — 없으면 하데스 것만 쓴다.
KOREAN = Path.home() / "Downloads" / "5.99 클라이언트"
DOTNET = ROOT / ".tools" / "dotnet-9.0.317" / "dotnet"
TOOL = ROOT / "tools" / "dat-extract" / "bin" / "Release" / "net8.0" / "dat-extract.dll"

#: 파일마다 skill.tbl 이 빈틈없이 채우는 칸 수(3.2절). 01 서기·걷기 · 02 평타는 3.3절.
MOTIONS = {"b": 14, "c": 30, "d": 18, "e": 36, "f": 12}
STANDING = {"01": 10, "02": 4}

#: 직업 의상 — skill.tbl ST 에 전사 동작은 옷 2, 도적 동작은 옷 4 가 있다.
CLASS_CLOTHES = ["mu002", "mu004", "wu002", "wu004"]

configure_utf8_stdio(sys.stdout, sys.stderr)


def archives(gender):
    name = "khan.dat" if gender == "m" else "khan2.dat"
    return [KOREAN / name, HADES / name.removesuffix(".dat") / name]


def frames_in(archive, entry, scratch):
    """그 아카이브에 그 파일이 몇 칸 들었나. 없으면 0."""
    if not archive.exists():
        return 0
    proc = subprocess.run([str(DOTNET), str(TOOL), "pose", str(archive), entry, str(Path(scratch) / "probe.png"),
                           "0", "1", "80x88"], capture_output=True, text=True, encoding="utf-8", errors="replace")
    found = re.search(rf"{re.escape(entry)}\.epf: 프레임 (\d+)개", proc.stdout, re.IGNORECASE)
    return int(found.group(1)) if proc.returncode == 0 and found else 0


def cut(archive, entry, count, out):
    proc = subprocess.run([str(DOTNET), str(TOOL), "pose", str(archive), entry, str(out),
                           ",".join(map(str, range(count))), "1", "80x88", "marker"],
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    if proc.returncode != 0:
        raise RuntimeError(f"{entry}: {proc.stderr.strip() or proc.stdout.strip()}")


def main():
    if not TOOL.exists():
        print(f"도구가 없습니다. 먼저: {DOTNET} build tools/dat-extract/DatExtract.csproj -c Release")
        return 1

    pieces = sorted({p.stem for p in PARTS.glob("*.png") if re.fullmatch(r"[mw][a-z]\d{3}", p.stem)} | set(CLASS_CLOTHES))
    made, missing = 0, []
    with tempfile.TemporaryDirectory() as scratch:
        for piece in pieces:
            # 그 부위의 서기·걷기가 있는 첫 아카이브(5.99 → 하데스).
            archive = next((a for a in archives(piece[0]) if frames_in(a, f"{piece}01", scratch)), None)
            if archive is None:
                missing.append(piece)
                continue
            source = "5.99" if archive.is_relative_to(KOREAN) else "하데스"
            files = []
            for suffix, count in {**STANDING, **MOTIONS}.items():
                entry = f"{piece}{suffix}"
                if not frames_in(archive, entry, scratch):
                    missing.append(entry)
                    continue
                # 01 은 부위 이름 그대로(mb001.png), 나머지는 끝을 붙인다(mb00102.png · mb001d.png).
                name = piece if suffix == "01" else entry
                # 하데스 부위의 01·02 는 build-client-assets.ps1 몫이다 — 여기서 덮지 않는다.
                if suffix in STANDING and source == "하데스" and (PARTS / f"{name}.png").exists():
                    continue
                cut(archive, entry, count, PARTS / f"{name}.png")
                files.append(suffix)
                made += 1
            print(f"  {piece}: {source} — {' '.join(files) or '새로 뽑은 것 없음'}")

    print(f"그림 {made}장 → {PARTS.relative_to(ROOT)}")
    if missing:
        print(f"  아카이브에 없음 {len(missing)}개: {', '.join(missing)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
