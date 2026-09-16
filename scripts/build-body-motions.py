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

  쓰는 법: python3 scripts/build-body-motions.py
  산출물:  docs/ui/assets/motion/body-<글자>-skill.png · motions.json
"""
import json
import subprocess
import sys
import tempfile
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
EFFECTS = ROOT / "data" / "game-data" / "ability-effects.json"
KHAN = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives" / "khan" / "khan.dat"
LEGEND = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives" / "legend" / "Legend.dat"
DEST = ROOT / "docs" / "ui" / "assets" / "motion"
INDEX = DEST / "motions.json"

configure_utf8_stdio(sys.stdout, sys.stderr)

#: `skill.tbl` 머리말이 적어 둔 것: "FN : 스킬 그림이 들어 있는 파일의 번호 (b = 1, c = 2, ....)"
FILES = {1: "b", 2: "c", 3: "d", 4: "e", 5: "f"}

#: 몸동작 번호는 NO 에 이만큼 더한 값이다. 하데스가 보내는 `0x80`(성직자 시전)이 NO 0 이다.
FIRST = 128

#: 한 칸의 크기. `build-client-assets.ps1` 이 걷기·평타를 뽑을 때 쓰는 것과 같다.
CELL = "80x88"
CELL_WIDE, CELL_TALL = 80, 88


def run(args):
    return subprocess.run(
        ["dotnet", "run", "--project", str(ROOT / "tools" / "dat-extract"), "-c", "Release", "--", *args],
        capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)


def skill_table(scratch):
    """`NO → (파일글자, 시작칸, 칸수)`. 주석(`;`)과 머리말은 건너뛴다."""
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
        if file_number in FILES:
            table[number] = (FILES[file_number], start, count)
    return table


def wanted():
    """기술이 실제로 시키는 몸동작 번호."""
    data = json.loads(EFFECTS.read_text(encoding="utf-8"))["밑말"]
    found = set()
    for entry in data.values():
        for level in entry["레벨"]:
            for motion in level.get("모션") or []:
                found.add(motion[0])
    return sorted(found)


def main():
    for archive in (KHAN, LEGEND):
        if not archive.exists():
            print(f"원작 아카이브가 없습니다: {archive.relative_to(ROOT)}")
            return 1

    DEST.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory() as scratch:
        table = skill_table(scratch)

    if not table:
        print("skill.tbl 을 읽지 못했습니다.")
        return 1

    # 직업별로 쓰는 앞 구간만 모은다. 한 직업 파일에 동작이 여럿 들어 있으므로, 쓰는 것만 이어
    # 붙이고 그 안에서의 자리를 따로 적는다 — 화면은 이 자리부터 칸수만큼 넘긴다.
    per_file, drawn, missing = {}, {}, []
    for motion in wanted():
        entry = table.get(motion - FIRST)
        if entry is None:
            missing.append(motion)
            continue

        letter, start, count = entry
        frames = per_file.setdefault(letter, [])
        place = len(frames)
        frames.extend(range(start + count, start + count + count))   # 앞 구간
        drawn[str(motion)] = {"파일": f"body-{letter}-skill.png", "자리": place, "칸수": count}

    for letter, frames in sorted(per_file.items()):
        out = DEST / f"body-{letter}-skill.png"
        proc = run(["pose", str(KHAN), f"mb001{letter},mi001{letter},MH285{letter.upper()}",
                    str(out), ",".join(str(n) for n in frames), "1", CELL])
        if not out.exists():
            print(f"  body-{letter} 를 그리지 못했습니다: {proc.stderr.strip()[:120]}")
            continue
        for info in drawn.values():
            if info["파일"] == out.name:
                info["전체"] = len(frames)

    INDEX.write_text(json.dumps({
        "생성": "scripts/build-body-motions.py",
        "출처": "khan.dat — mb001<글자>+mi001<글자>+MH285<글자> · 구간은 Legend.dat 의 skill.tbl",
        "칸": CELL_WIDE, "높이": CELL_TALL,
        "동작": drawn,
    }, ensure_ascii=False, indent=1), encoding="utf-8")

    print(f"몸동작 {len(drawn)}개 · 직업 {len(per_file)}갈래 → {DEST.relative_to(ROOT)}")
    if missing:
        print(f"  skill.tbl 에 없는 번호 {len(missing)}개: {', '.join(str(n) for n in missing)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
