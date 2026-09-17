#!/usr/bin/env python3
"""게임 클라이언트가 사람에게 겹쳐 입힐 옷장 — 부위마다 서기·걷기·평타·직업 기술 동작 그림을 뽑는다.

서버는 몸 동작을 번호로 보낸다(0x1A) — 1 은 평타(`…02`), 128 부터는 원작 `skill.tbl` 의 NO 줄이고
그 줄이 파일 글자를 고른다: b 성직자 · c 전사 · d 무도가 · e 도적 · f 마법사
(docs/original-sprite-animation.md 3절, `Lod.Mobile.Core.Art.BodyMotion`). 관리페이지용
`build-body-motions.py` 는 몸 한 장의 앞모습만 뽑고, 이것은 클라이언트가 겹쳐 입힐 부위마다 전부 뽑는다.

**어느 부위를 뽑나:** 서버가 입힐 수 있는 것 전부 — 아이템 템플릿의 `ScriptName` 이 갑옷·무기·투구·방패·신발인
것의 `Image` 가 입은 번호다(`scripts/Items/Armor.cs` 가 `Aisling.Armor = Item.Image`). 성별은 템플릿 `Gender`
(1 남 · 2 여 · 255 둘 다). 투구는 100 보다 클 때만 서버가 보낸다(`ServerFormat33`). 여기에 `build-client-assets.ps1`
이 손으로 적어 둔 부위(몸·바지·머리 모양)와 직업 의상을 더한다. 파일 이름은 `mb001.png`(01) · `mb00102.png`(02) ·
`mb001d.png`(기술 동작). 칸·발 기준은 모두 같다(120x96, 표시색 염색).

**부위마다 한 아카이브에서 전부 뽑는다 — 5.99 한국 클라이언트에 그 부위가 있으면 5.99.** 몸·신발·머리는 두
아카이브가 같지만 바지·갑옷은 5.99 쪽 그림이 다르다(`mn00101` 하데스 6,071 · 5.99 4,716 바이트). 하데스
바지는 동작 칸도 모자라(`mn001c` 14칸 · 5.99 30칸) 전사 139~141 에서 바지가 사라졌다. 걷기는 하데스, 기술은
5.99 로 섞으면 옷이 동작마다 바뀌므로 그런 부위는 `01`·`02` 도 5.99 에서 다시 뽑는다.
두 쪽 다 없는 파일(바지의 도적 동작, 방패의 기술 동작)은 만들지 않는다 — 클라이언트는 그 동작 동안 그 부위를
그리지 않는다. **직업 동작은 그 직업 의상에서만 원작이 지원한다**(`skill.tbl` ST) — 기본 옷으로 깨져 보이는
것은 원작도 그렇다. 그래서 동작 확인용으로 전사 옷 2번 · 도적 옷 4번을 함께 뽑는다.

  쓰는 법: python3 scripts/build-client-wardrobe.py [이어서 시작할 부위, 예: mu156]
  산출물:  mobile/client/assets/actor/parts/<부위>.png · <부위>02.png · <부위><글자>.png
"""
import json
import re
import subprocess
import sys
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

#: 모든 부위가 같은 칸이어야 겹친다. 무기가 몸 밖으로 뻗어 가로 114 · 세로 89 까지 그린다 — 80x88 에서는 무기
#: 129 파일이 들어가지 않았다. 도구는 칸의 왼쪽 위에 맞춰 그리므로 칸을 키워도 발 자리는 그대로다.
CELL = "120x96"

#: 직업 의상 — skill.tbl ST 에 전사 동작은 옷 2, 도적 동작은 옷 4 가 있다.
CLASS_CLOTHES = ["mu002", "mu004", "wu002", "wu004"]

ITEMS = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates" / "items"
#: 아이템 스크립트 → 입은 그림의 부위 글자(docs/original-sprite-animation.md 7절).
SLOTS = {"Armor": "u", "Weapon": "w", "Helmet": "h", "Shield": "s", "Boot": "l"}

configure_utf8_stdio(sys.stdout, sys.stderr)


def archives(gender):
    name = "khan.dat" if gender == "m" else "khan2.dat"
    return [KOREAN / name, HADES / name.removesuffix(".dat") / name]


def entries(archive):
    """그 아카이브에 든 그림 파일 이름들(소문자, 확장자 없이). 없는 아카이브는 빈 집합."""
    if not archive.exists():
        return set()
    proc = subprocess.run([str(DOTNET), str(TOOL), "list", str(archive), ".epf"],
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    return {name.lower() for name in re.findall(r"^\s+(\S+)\.epf\s", proc.stdout, re.MULTILINE)}


def worn_by_items():
    """서버 아이템이 입히는 부위 이름들."""
    found = set()
    for path in ITEMS.rglob("*.json"):
        try:
            template = json.loads(path.read_text(encoding="utf-8-sig"))
        except ValueError:
            continue
        letter = SLOTS.get(template.get("ScriptName"))
        image = template.get("Image")
        if not letter or not isinstance(image, int) or image <= 0 or image > 999:
            continue
        if letter == "h" and image <= 100:
            continue
        genders = {1: "m", 2: "w"}.get(template.get("Gender"), "mw")
        found.update(f"{gender}{letter}{image:03d}" for gender in genders)
    return found


def cut(archive, entry, count, out):
    proc = subprocess.run([str(DOTNET), str(TOOL), "pose", str(archive), entry, str(out),
                           ",".join(map(str, range(count))), "1", CELL, "marker"],
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    if proc.returncode == 0:
        return None
    # 칸보다 큰 그림("잘립니다")은 자르지 않고, 그림이 빈 파일("겹칠 것이 없습니다")은 건너뛴다 — 그 파일만 빼고
    # 알린다. 발 기준이 모든 부위에서 같아야 해서 칸을 못 키운다.
    said = (proc.stderr.strip() or proc.stdout.strip()).splitlines()
    return said[-1] if said else "알 수 없는 실패"


def main():
    if not TOOL.exists():
        print(f"도구가 없습니다. 먼저: {DOTNET} build tools/dat-extract/DatExtract.csproj -c Release")
        return 1

    pieces = sorted({p.stem for p in PARTS.glob("*.png") if re.fullmatch(r"[mw][a-z]\d{3}", p.stem)}
                    | set(CLASS_CLOTHES) | worn_by_items())
    # 이어서 돌릴 때: 그 부위부터(이름 순).
    if len(sys.argv) > 1:
        pieces = [piece for piece in pieces if piece >= sys.argv[1]]
    made, missing, skipped = 0, [], []
    listed = {archive: entries(archive) for gender in "mw" for archive in archives(gender)}
    for piece in pieces:
        # 그 부위의 서기·걷기가 있는 첫 아카이브(5.99 → 하데스).
        archive = next((a for a in archives(piece[0]) if f"{piece}01" in listed[a]), None)
        if archive is None:
            missing.append(piece)
            continue
        source = "5.99" if archive.is_relative_to(KOREAN) else "하데스"
        files = []
        for suffix, count in {**STANDING, **MOTIONS}.items():
            entry = f"{piece}{suffix}"
            # 동작 파일이 없는 것은 흔하다 — 직업 갑옷·무기는 자기 직업 동작만 갖는다.
            if entry not in listed[archive]:
                continue
            # 01 은 부위 이름 그대로(mb001.png), 나머지는 끝을 붙인다(mb00102.png · mb001d.png).
            name = piece if suffix == "01" else entry
            if (why := cut(archive, entry, count, PARTS / f"{name}.png")) is not None:
                skipped.append(f"{entry}({why})")
                continue
            files.append(suffix)
            made += 1
        print(f"  {piece}: {source} — {' '.join(files) or '새로 뽑은 것 없음'}")

    print(f"그림 {made}장 → {PARTS.relative_to(ROOT)}")
    if skipped:
        print(f"  뺀 파일 {len(skipped)}개: {', '.join(skipped)}")
    if missing:
        print(f"  두 아카이브 모두 없는 부위 {len(missing)}개: {', '.join(missing)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
