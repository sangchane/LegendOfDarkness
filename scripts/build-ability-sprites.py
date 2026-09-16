#!/usr/bin/env python3
"""기술이 실제로 쓰는 이펙트 연출을 그림으로 뽑는다.

원작은 연출을 `roh.dat` 에 `efct###.epf` 로 싣는다. 스크립트가 부르는 번호가 곧 파일 번호다 —
`motion 136, 75` 와 `effect @mob, 0, 136, 75` 가 같은 136 을 가리키고, 그것이 `efct136.epf` 다.
하데스도 같은 번호를 `SendAnimation(ushort)` 로 클라이언트에 보낸다.

색표는 **`effpal.tbl`** 한 장이 정한다. `<이펙트번호> <색표번호>` 줄이고, 없는 번호는 `eff000.pal`
이다 — 원작 코드(`DADataViewer/EffectsForm.cs` + `PaletteTable.cs`)가 그렇게 한다. 실제로 쓰는
연출 82개 중 79개가 표에 없어 기본 색표를 쓴다.

짝인 `efct###.tbl` 은 색표가 **아니다.** UTF-16 로 `7a` 가 프레임 수만큼 되풀이될 뿐이고 원작은
읽지 않는다. 그것의 둘째 글자를 16진수로 보고 색표를 고른 적이 있는데, 그러면 크래셔의 참격이
원작의 푸른색 대신 누렇게 나온다.

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
import tempfile
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


def palette_table(scratch):
    """색표를 정하는 표. 아카이브에 **`effpal.tbl` 한 장**뿐이다.

    줄 모양은 `<이펙트번호> <색표번호>` 이고, 셋째 값이 있으면 앞 둘이 범위다(셋째가 음수면 그것은
    갈래 표시이고 둘째가 색표다). 원작이 읽는 방법이 그대로
    `sources/wren11/DADataViewer/DADataViewer/PaletteTable.cs` 에 있고, `EffectsForm` 은 이 표
    하나만 얹은 뒤 `GetPaletteNumber(파일번호)` 로 묻는다.

    **`efct###.tbl` 은 색표가 아니다.** 예전 이 함수는 그것을 읽어 `7 a` 의 둘째 글자를 16진수로
    보고 `eff010` 을 골랐다. 그 파일은 UTF-16 로 `7a` 가 프레임 수만큼 되풀이될 뿐이고 원작 코드는
    쳐다보지도 않는다. 그래서 82개 중 79개가 원작과 다른 색으로 그려졌다 — 크래셔의 참격이 원작
    에서는 푸른데 누렇게 나왔다.
    """
    subprocess.run(
        ["dotnet", "run", "--project", str(ROOT / "tools" / "dat-extract"), "-c", "Release", "--",
         "dump", str(ARCHIVE), scratch, "effpal"],
        capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)

    found = next(Path(scratch).rglob("effpal.tbl"), None)
    table = []
    if found is None:
        return table

    for line in found.read_text(encoding="cp949", errors="replace").splitlines():
        parts = line.split()
        if len(parts) < 2 or not all(p.lstrip("-").isdigit() for p in parts[:2]):
            continue
        low, second = int(parts[0]), int(parts[1])
        if len(parts) == 2:
            table.append((low, low, second))
        elif parts[2].lstrip("-").isdigit():
            third = int(parts[2])
            table.append((low, low, second) if third < 0 else (low, second, third))
    return table


def palette_for(number, table):
    """표에 없으면 0 이다 — 원작도 그렇게 하고(`GetPaletteNumber` 의 마지막 줄), 실제로 대부분이
    표에 없다. 1000 이상은 1000 을 뺀다(`EffectsForm.cs` 가 그렇게 한다)."""
    for low, high, palette in table:
        if low <= number <= high:
            return palette - 1000 if palette >= 1000 else palette
    return 0


def main():
    if not ARCHIVE.exists():
        print(f"원작 아카이브가 없습니다: {ARCHIVE.relative_to(ROOT)}")
        return 1

    DEST.mkdir(parents=True, exist_ok=True)
    wanted = used_numbers()

    with tempfile.TemporaryDirectory() as scratch:
        table = palette_table(scratch)

    if not table:
        print("effpal.tbl 을 읽지 못했습니다 — 색이 전부 기본값이 됩니다.")

    drawn, missing = {}, []
    for number in wanted:
        name = f"efct{number:03d}"
        palette = palette_for(number, table)
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
        "출처": "roh.dat — efct###.epf + eff###.pal (색표는 effpal.tbl 이 정한다)",
        "연출": drawn,
    }, ensure_ascii=False, indent=1), encoding="utf-8")

    print(f"연출 {len(drawn)}개 → {DEST.relative_to(ROOT)}")
    if missing:
        print(f"  아카이브에 없는 번호 {len(missing)}개: {', '.join(str(n) for n in missing)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
