#!/usr/bin/env python3
"""기술이 시키는 **몸동작**을 원작 그림으로 뽑는다.

`motion` 은 연출 그림이 아니라 몸동작 번호다. 같은 번호의 `efct` 파일이 따로 있어서 한동안
그것을 시전자 위에 그렸는데, 크래셔 시전자에게 엉뚱한 불꽃이 얹혔다.

  `skill.tbl`(Legend.dat) 이 정한다 — 줄은 `NO FN SI FC ST…` 이고 **`motion - 128` 이 NO** 다.
     FN : 그림 파일 글자 (1=b 성직자 · 2=c 전사 · 3=d 무도 · 4=e 도적 · 5=f 마법사)
     SI : 시작 칸
     FC : 칸 수
  한 동작은 **등 구간 다음에 앞 구간**이 이어진다. 화면에 쓰는 것은 앞 구간, 곧 `SI+FC` 부터 FC 칸.

번호가 직업별로 정확히 갈리는 것이 이 읽기의 증거다 — 128 은 치유마법 전부, 129·130 은 검 기술,
131~133 은 무도가, 135 는 도적 찌르기, 142 는 활. 하데스도 같은 번호를 보낸다:
`Skills/DoublePunch.cs` 가 무도가일 때 `ServerFormat1A.Number = 0x84`(132) 다.

**동작마다 그 직업 의상을 입혀 뽑는다.** `skill.tbl` 의 다섯째 칸 `ST` 가 "이 동작을 할 수 있는 옷 번호"를
줄줄이 적어 둔 것이고, 원작은 그 옷일 때만 동작을 제대로 그린다. 옷은 `mu<번호><글자>.epf` 다
(`build-client-wardrobe.py` 도 같은 자리를 쓴다 — 전사 옷 2 · 도적 옷 4).
한동안 몸과 머리만 겹쳐서 다섯 직업이 **전부 같은 모습**으로 나왔다(사용자, 2026-09-19).

ST 목록은 직업 다섯을 돌아가며 채운다 — 2 전사 · 3 무도가 · 4 도적 · 5 사제 · 6 마법사, 그다음 7~11 이
같은 차례다. 그래서 줄마다 ST 의 **첫 번호**를 그 동작의 옷으로 쓴다. 같은 파일 안에서도 줄이 다르면 옷이
다르다(성직자 NO 0 은 5, NO 9 는 266) — 그래서 **파일 한 장이 아니라 동작마다 한 장**을 뽑는다.

  쓰는 법: python3 scripts/build-body-motions.py
  산출물:  docs/ui/assets/motion/motion-<번호>.png(앞) · motion-<번호>-back.png(등) · motions.json
"""
import json
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
EFFECTS = ROOT / "data" / "game-data" / "ability-effects.json"
KHAN = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives" / "khan" / "khan.dat"
LEGEND = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives" / "legend" / "Legend.dat"
DEST = ROOT / "docs" / "ui" / "assets" / "motion"
INDEX = DEST / "motions.json"
PAGE = ROOT / "docs" / "body-motions-data.js"
#: 게임이 실제로 시키는 몸동작 번호. `build-ability-page-data.py` 가 적어 둔다.
USED = ROOT / "data" / "game-data" / "ability-presentation.json"
# 시스템 PATH 에 dotnet 이 없는 맥에서도 돌게 — 다른 생성기와 같은 자리를 쓴다.
DOTNET = ROOT / ".tools" / "dotnet-9.0.317" / "dotnet"
TOOL = ROOT / "tools" / "dat-extract" / "bin" / "Release" / "net8.0" / "dat-extract.dll"

configure_utf8_stdio(sys.stdout, sys.stderr)

#: `skill.tbl` 머리말이 적어 둔 것: "FN : 스킬 그림이 들어 있는 파일의 번호 (b = 1, c = 2, ....)"
FILES = {1: "b", 2: "c", 3: "d", 4: "e", 5: "f"}

#: 몸동작 번호는 NO 에 이만큼 더한 값이다. 하데스가 보내는 `0x80`(성직자 시전)이 NO 0 이다.
FIRST = 128

#: 한 칸의 크기. `build-client-assets.ps1` 이 걷기·평타를 뽑을 때 쓰는 것과 같다.
CELL = "80x88"
CELL_WIDE, CELL_TALL = 80, 88


def run(args):
    return subprocess.run([str(DOTNET), str(TOOL), *args],
                          capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)


def foot_line(png):
    """그림에서 **발이 닿는 줄** — 맨 아래 칠해진 가로줄이다.

    칸 아래가 곧 발밑일 것 같지만 아니다: 80x88 칸에서 발은 77 줄에 있고 그 아래는 비어 있다.
    칸 바닥을 발밑으로 치면 사람이 땅에 박혀 서고, 옆에 세운 것과 바닥이 어긋난다.
    """
    blob = png.read_bytes()
    at, width, height, data = 8, 0, 0, b""
    while at < len(blob):
        length = int.from_bytes(blob[at:at + 4], "big")
        kind = blob[at + 4:at + 8]
        if kind == b"IHDR":
            width = int.from_bytes(blob[at + 8:at + 12], "big")
            height = int.from_bytes(blob[at + 12:at + 16], "big")
        elif kind == b"IDAT":
            data += blob[at + 8:at + 8 + length]
        at += 12 + length

    raw, stride, previous, at, bottom = zlib.decompress(data), width * 4, bytearray(width * 4), 0, 0
    for y in range(height):
        filtered, line, at = raw[at], bytearray(raw[at + 1:at + 1 + stride]), at + 1 + stride
        for x in range(stride):
            left = line[x - 4] if x >= 4 else 0
            up = previous[x]
            corner = previous[x - 4] if x >= 4 else 0
            if filtered == 1:
                line[x] = (line[x] + left) & 255
            elif filtered == 2:
                line[x] = (line[x] + up) & 255
            elif filtered == 3:
                line[x] = (line[x] + (left + up) // 2) & 255
            elif filtered == 4:
                guess = left + up - corner
                near = min((abs(guess - left), left), (abs(guess - up), up), (abs(guess - corner), corner))
                line[x] = (line[x] + near[1]) & 255
        if any(line[3::4]):
            bottom = y + 1
        previous = line
    return bottom


def skill_table(scratch):
    """`NO → (파일글자, 시작칸, 칸수, 옷번호들)`. 주석(`;`)과 머리말은 건너뛴다."""
    run(["dump", str(LEGEND), scratch, "skill.tbl"])
    found = next(Path(scratch).rglob("skill.tbl"), None)
    if found is None:
        return {}

    table = {}
    for line in found.read_text(encoding="cp949", errors="replace").splitlines():
        if line.startswith(";"):
            continue
        parts = line.split()
        if len(parts) < 4 or not all(p.isdigit() for p in parts[:4]):
            continue
        number, file_number, start, count = (int(p) for p in parts[:4])
        clothes = [int(p) for p in parts[4:] if p.isdigit()]
        if file_number in FILES:
            table[number] = (FILES[file_number], start, count, clothes)
    return table


def wanted():
    """**게임이 실제로 시키는** 몸동작 번호. 없으면 팩 표에 적힌 것으로 대신한다."""
    if USED.exists():
        found = set(json.loads(USED.read_text(encoding="utf-8"))["채널"]["몸동작"])
    else:
        found = set()
        for entry in json.loads(EFFECTS.read_text(encoding="utf-8"))["밑말"].values():
            for level in entry["레벨"]:
                for motion in level.get("모션") or []:
                    found.add(motion[0])
    return sorted(n for n in found if n >= FIRST)


def main():
    for archive in (KHAN, LEGEND):
        if not archive.exists():
            print(f"원작 아카이브가 없습니다: {archive.relative_to(ROOT)}")
            return 1

    if not TOOL.exists():
        print(f"도구가 없습니다. 먼저: {DOTNET} build tools/dat-extract/DatExtract.csproj -c Release")
        return 1

    DEST.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory() as scratch:
        table = skill_table(scratch)

    if not table:
        print("skill.tbl 을 읽지 못했습니다.")
        return 1

    drawn, missing, ground = {}, [], 0
    for motion in wanted():
        entry = table.get(motion - FIRST)
        if entry is None:
            missing.append(motion)
            continue

        letter, start, count, clothes = entry
        # 앞 구간만 쓴다 — 등 구간이 먼저 오고 그다음이 앞 구간이다.
        frames = range(start + count, start + (count * 2))
        # 이 동작을 할 수 있는 첫째 옷. 없으면 맨몸이고, 원작에서도 그러면 깨져 보인다.
        outfit = f",mu{clothes[0]:03d}{letter}" if clothes else ""
        out = DEST / f"motion-{motion}.png"
        proc = run(["pose", str(KHAN), f"mb001{letter},mn001{letter}{outfit},MH285{letter.upper()}",
                    str(out), ",".join(str(n) for n in frames), "1", CELL])
        if not out.exists():
            print(f"  {motion} 을 그리지 못했습니다: {(proc.stderr or proc.stdout).strip()[:120]}")
            missing.append(motion)
            continue

        bottom = foot_line(out)
        ground = max(ground, bottom)
        drawn[str(motion)] = {"파일": out.name, "칸수": count, "직업": letter,
                              "옷": clothes[0] if clothes else 0, "발밑": bottom}

        # 등 구간도 한 장 더 뽑는다 — 반대쪽(서)을 보려면 **등 그림을 좌우로 뒤집어야** 한다.
        # 앞 그림만 뒤집으면 남쪽, 곧 90° 돈 것으로 읽힌다 (docs/original-sprite-animation.md §2).
        back = DEST / f"motion-{motion}-back.png"
        run(["pose", str(KHAN), f"mb001{letter},mn001{letter}{outfit},MH285{letter.upper()}",
             str(back), ",".join(str(n) for n in range(start, start + count)), "1", CELL])
        if back.exists():
            drawn[str(motion)]["등파일"] = back.name

    # **안 쓰는 그림이라도 지우지 않는다.** 이 폴더에는 문서가 쓰는 도판(`body-*.png`·`four-directions.png`)도
    # 함께 있고, 한 번 통째로 지워 `docs/original-sprite-animation.md` 의 그림이 다 날아갔다. 지우는 것은
    # 사람이 정할 일이고 생성기는 더하고 고치기만 한다(사용자, 2026-09-19).

    payload = {
        "생성": "scripts/build-body-motions.py",
        "출처": "khan.dat — mb001<글자>+mn001<글자>+mu<옷><글자>+MH285<글자> · 구간과 옷은 Legend.dat 의 skill.tbl",
        "칸": CELL_WIDE, "높이": CELL_TALL, "바닥": ground,
        "동작": drawn,
    }
    INDEX.write_text(json.dumps(payload, ensure_ascii=False, indent=1), encoding="utf-8")
    PAGE.write_text("window.LOD_BODY_MOTIONS = " + json.dumps(payload, ensure_ascii=False) + ";\n",
                    encoding="utf-8")

    print(f"몸동작 {len(drawn)}개(발밑 {ground}) → {DEST.relative_to(ROOT)} · {PAGE.relative_to(ROOT)}")
    if missing:
        print(f"  skill.tbl 에 없는 번호 {len(missing)}개: {', '.join(str(n) for n in missing)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
