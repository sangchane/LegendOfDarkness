#!/usr/bin/env python3
"""기술이 실제로 쓰는 이펙트 연출을 그림으로 뽑는다.

원작은 연출을 `roh.dat` 에 `efct###.epf` 로 싣는다. 스크립트가 부르는 번호가 곧 파일 번호다 —
`motion 136, 75` 와 `effect @mob, 0, 136, 75` 가 같은 136 을 가리키고, 그것이 `efct136.epf` 다.
하데스도 같은 번호를 `SendAnimation(ushort)` 로 클라이언트에 보낸다.

색표는 짝인 `efct###.tbl` 이 정한다. 프레임마다 두 값이 16진수로 적혀 있고 **둘째가 색표 번호**다 —
`efct136.tbl` 의 `7 a` → `eff010.pal`. 이것을 안 보고 `eff000.pal` 로 그리면 불 연출이 흰색으로
나온다(실제로 그렇게 나왔다).

394 번까지 있지만 기술이 실제로 부르는 것은 83 개뿐이라 그것만 뽑는다.

`투명` 과 `row` 를 붙여야 한다. 바탕이 있으면 샌드백 위에 검은 네모가 얹히고, 격자로 두면 칸이
남아 읽는 쪽이 `가로 ÷ 프레임수` 로 자를 때 빈 칸을 프레임으로 센다 — 네 프레임짜리가 열여섯 칸
판에 그려져 첫 칸만 그림이고 나머지는 바탕뿐이었다. 화면에서는 「정지된 그림」으로 보인다.

  쓰는 법: python3 scripts/build-ability-sprites.py
  산출물:  docs/ui/assets/ability-effects/efct###.png · ability-effects.json
"""
import json
import re
import subprocess
import sys
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
EFFECTS = ROOT / "data" / "game-data" / "ability-effects.json"
ARCHIVE = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives" / "roh" / "roh.dat"
VAULT = ROOT / "data" / "archives-vault" / "표"
DEST = ROOT / "docs" / "ui" / "assets" / "ability-effects"
INDEX = DEST / "index.json"

configure_utf8_stdio(sys.stdout, sys.stderr)

EFFECT_TAIL = re.compile(r",\s*(\d+)\s*,\s*\d+\s*$")


def used_numbers():
    """스크립트가 실제로 부르는 연출 번호. 394 개를 다 뽑을 이유가 없다."""
    data = json.loads(EFFECTS.read_text(encoding="utf-8"))["밑말"]
    found = set()
    for entry in data.values():
        for level in entry["레벨"]:
            for directive in level["이펙트"]:
                match = EFFECT_TAIL.search(directive)
                if match:
                    found.add(int(match.group(1)))
            for motion in level["모션"]:
                found.add(motion[0])
    return sorted(n for n in found if n > 0)


def palette_for(number):
    """`efct###.tbl` 의 프레임마다 적힌 두 값 중 **둘째**가 색표 번호다 (16진수).

    프레임마다 다를 수 있지만 자료에서는 한 연출이 한 색표를 쓴다. 첫 프레임 것을 쓴다.
    표가 없으면 `eff000.pal` 로 돌아간다 — 모양은 맞고 색만 기본값이 된다.
    """
    note = VAULT / f"roh — efct{number:03d}.tbl.md"
    if note.exists():
        body = note.read_text(encoding="utf-8")
        inside = body.partition("## 내용")[2]
        values = re.findall(r"\b([0-9a-fA-F]+)\b", inside.partition("```")[2].partition("```")[0])
        if len(values) >= 2:
            return int(values[1], 16)
    return 0


def main():
    if not ARCHIVE.exists():
        print(f"원작 아카이브가 없습니다: {ARCHIVE.relative_to(ROOT)}")
        return 1

    DEST.mkdir(parents=True, exist_ok=True)
    wanted = used_numbers()
    drawn, missing = {}, []
    for number in wanted:
        name = f"efct{number:03d}"
        palette = palette_for(number)
        out = DEST / f"{name}.png"
        proc = subprocess.run(
            ["dotnet", "run", "--project", str(ROOT / "tools" / "dat-extract"), "-c", "Release", "--",
             "epf", str(ARCHIVE), name, str(out), "1", "2", f"eff{palette:03d}.pal",
             "투명", "row"],
            capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)
        frames = re.search(rf"{name}\.epf: 프레임 (\d+)개", proc.stdout)
        if not frames or not out.exists():
            missing.append(number)
            continue
        drawn[str(number)] = {"프레임": int(frames.group(1)), "색표": palette,
                              "파일": f"{name}.png"}

    INDEX.write_text(json.dumps({
        "생성": "scripts/build-ability-sprites.py",
        "출처": "roh.dat — efct###.epf + eff###.pal (색표는 efct###.tbl 이 정한다)",
        "연출": drawn,
    }, ensure_ascii=False, indent=1), encoding="utf-8")

    print(f"연출 {len(drawn)}개 → {DEST.relative_to(ROOT)}")
    if missing:
        print(f"  아카이브에 없는 번호 {len(missing)}개: {', '.join(str(n) for n in missing)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
