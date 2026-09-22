#!/usr/bin/env python3
"""기술이 실제로 쓰는 이펙트 연출을 그림으로 뽑는다.

원작은 연출을 `roh.dat` 에 `efct###.epf` 로 싣는다. 스크립트가 부르는 번호가 곧 파일 번호다 —
`motion 136, 75` 와 `effect @mob, 0, 136, 75` 가 같은 136 을 가리키고, 그것이 `efct136.epf` 다.
하데스도 같은 번호를 `SendAnimation(ushort)` 로 클라이언트에 보낸다.

색표는 **`effpal.tbl`** 한 장이 정한다. `<이펙트번호> <색표번호>` 줄이고, 없는 번호는 `eff000.pal`
이다 — 원작 코드(`DADataViewer/EffectsForm.cs` + `PaletteTable.cs`)가 그렇게 한다. 실제로 쓰는
연출 82개 중 79개가 표에 없어 기본 색표를 쓴다.

짝인 `efct###.tbl` 은 색표가 **아니다.** 그것을 색표로 읽어 둘째 글자를 16진수로 보고 고른 적이
있는데, 그러면 크래셔의 참격이 원작의 푸른색 대신 누렇게 나온다. 그 파일은 **기준점**이다 — 아래 참조.

394 번까지 있지만 **게임이 실제로 쏘는 것만** 뽑는다 —
`build-ability-page-data.py` 가 `data/game-data/ability-presentation.json` 에 적어 둔 번호다.
한동안 노바온라인 팩 표의 번호로 뽑았는데, 그것은 우리 서버가 쏘는 것과 딴판이었다: 프라보는 팩 표가
43·33 인데 서버는 **257**, 쿠로는 21 이 아니라 **267**, 데프레코는 18·33 이 아니라 **243** 이다
(사용자, 2026-09-19 — "직자 스팰 관련 이펙트가 제대로 된거 같지 않은데?").
232 번부터는 EPF 가 아니라 EFA 이고 한국 5.99 클라이언트에만 있다 — `build-client-effects.py` 와 같은 길이다.

**`efct` 명령으로 자른다 — `epf` 가 아니다.** `epf` 는 프레임을 제일 큰 것 크기의 칸에 가운데·아래로
맞춰 담는다. 옷 조각에는 맞고 연출에는 틀리다: 일음지(`efct042`)는 111x85 바탕의 (48,10) 에 놓인
13x13 반짝임인데, 그것만 떼어 내면 13x13 그림이 되고 화면은 그것을 몸통 크기로 늘려 버린다.
바탕째로 자르면 반짝임은 발밑 기준에서 48~60px 위 — 머리 위에 그대로 남는다.

기준점은 **`efct###.tbl`** 이다. 프레임마다 16비트 x·y 두 개이고, 일음지는 111x85 바탕의 `55,70` —
가로 한가운데, 발이 닿는 높이다. 원작은 이름에 `Efct` 가 들어갈 때만 이 표를 읽는다(4.51 `0x44ba04`,
`docs/disassembly.md`). 아래 옛 주석은 이 파일을 UTF-16 로 읽어 "7F" 가 되풀이될 뿐이라고 적었지만
**글자가 아니다** — 좌표다.

  쓰는 법: python3 scripts/build-ability-sprites.py
  산출물:  docs/ui/assets/ability-effects/efct###.png · index.json · docs/ability-effects-data.js
           docs/ui/assets/ability-sounds/<번호>.mp3
"""
import json
import re
import shutil
import subprocess
import tempfile
import sys
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
EFFECTS = ROOT / "data" / "game-data" / "ability-effects.json"
USED = ROOT / "data" / "game-data" / "ability-presentation.json"
#: 5.99 한국 클라이언트. 저장소 밖에 있다 — 없으면 232 번 이상(EFA)은 건너뛴다.
KOREAN_ROH = Path.home() / "Downloads" / "5.99 클라이언트" / "roh.dat"
SOUNDS_FROM = ROOT / "mobile" / "client" / "assets" / "sound"
SOUNDS_TO = ROOT / "docs" / "ui" / "assets" / "ability-sounds"
ARCHIVE = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives" / "roh" / "roh.dat"
VAULT = ROOT / "data" / "archives-vault" / "표"
DEST = ROOT / "docs" / "ui" / "assets" / "ability-effects"
INDEX = DEST / "index.json"
PAGE = ROOT / "docs" / "ability-effects-data.js"
# `build-client-effects.py` 와 같은 자리를 쓴다 — 시스템 PATH 에 dotnet 이 없는 맥에서도 돌게.
DOTNET = ROOT / ".tools" / "dotnet-9.0.317" / "dotnet"
TOOL = ROOT / "tools" / "dat-extract" / "bin" / "Release" / "net8.0" / "dat-extract.dll"


def run(*args):
    return subprocess.run([str(DOTNET), str(TOOL), *map(str, args)],
                          capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)

configure_utf8_stdio(sys.stdout, sys.stderr)

EFFECT_TAIL = re.compile(r",\s*(\d+)\s*,\s*\d+\s*$")

#: 원작 그대로 1배다 — 게임(`build-client-effects.py`)도 몸동작(`build-body-motions.py`)도 1배라,
#: 셋을 한 무대에 세우려면 자가 같아야 한다. 화면에서 크게 보이게 하는 것은 CSS 가 무대째로 확대해서 한다.
ZOOM = 1

#: `efct` 명령이 적어 주는 줄: 프레임 수 · 바탕 크기 · 기준점.
CUT = re.compile(r"프레임 (\d+)개 · 바탕 (\d+)x(\d+) · 기준 (-?\d+),(-?\d+)")


def used_numbers():
    """**게임이 실제로 쏘는** 연출 번호. `build-ability-page-data.py` 가 적어 둔다.

    팩 표의 번호로 뽑으면 화면과 게임이 다른 그림을 보여 준다 — 사제 마법이 특히 그랬다.
    """
    if not USED.exists():
        print(f"{USED.relative_to(ROOT)} 이 없습니다. 먼저: python3 scripts/build-ability-page-data.py")
        return []
    return json.loads(USED.read_text(encoding="utf-8"))["채널"]["이펙트"]


def used_sounds():
    if not USED.exists():
        return []
    return json.loads(USED.read_text(encoding="utf-8"))["채널"]["소리"]


def copy_sounds():
    """소리는 `build-client-effects.py` 가 이미 `Legend.dat` 에서 165개 다 뽑아 두었다 — 쓰는 것만 옮긴다."""
    SOUNDS_TO.mkdir(parents=True, exist_ok=True)
    taken, missing = 0, []
    for number in used_sounds():
        source = SOUNDS_FROM / f"{number}.mp3"
        if source.exists():
            shutil.copyfile(source, SOUNDS_TO / source.name)
            taken += 1
        else:
            missing.append(number)
    return taken, missing


def palette_table(scratch):
    """색표를 정하는 표. 아카이브에 **`effpal.tbl` 한 장**뿐이다.

    줄 모양은 `<이펙트번호> <색표번호>` 이고, 셋째 값이 있으면 앞 둘이 범위다(셋째가 음수면 그것은
    갈래 표시이고 둘째가 색표다). 원작이 읽는 방법이 그대로
    `sources/wren11/DADataViewer/DADataViewer/PaletteTable.cs` 에 있고, `EffectsForm` 은 이 표
    하나만 얹은 뒤 `GetPaletteNumber(파일번호)` 로 묻는다.

    **`efct###.tbl` 은 색표가 아니다.** 예전 이 함수는 그것을 읽어 `7 a` 의 둘째 글자를 16진수로
    보고 `eff010` 을 골랐다. 그래서 82개 중 79개가 원작과 다른 색으로 그려졌다 — 크래셔의 참격이
    원작에서는 푸른데 누렇게 나왔다. 그 파일은 **기준점**이고(프레임마다 16비트 x·y), 색과는 상관이
    없다. 그것을 글자로 읽으면 "7F" 가 되풀이되는 것처럼 보이지만 글자가 아니다.
    """
    run("dump", ARCHIVE, scratch, "effpal")

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

    if not TOOL.exists():
        print(f"도구가 없습니다. 먼저: {DOTNET} build tools/dat-extract/DatExtract.csproj -c Release")
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
        proc = run("efct", ARCHIVE, name, out, ZOOM, f"eff{palette:03d}.pal")
        cut = CUT.search(proc.stdout)
        if not cut and KOREAN_ROH.exists():
            # 232 번부터는 한국 5.99 클라이언트에만 있고 형식도 EFA 다.
            proc = run("efa", KOREAN_ROH, name, out, ZOOM)
            cut = CUT.search(proc.stdout)
        if not cut or not out.exists():
            missing.append(number)
            continue
        # 배율이 걸리면 바탕과 기준도 같이 커진다. 화면은 그 값 그대로 얹으면 된다.
        drawn[str(number)] = {
            "프레임": int(cut.group(1)), "색표": palette, "파일": f"{name}.png",
            "바탕": [int(cut.group(2)) * ZOOM, int(cut.group(3)) * ZOOM],
            "기준": [int(cut.group(4)) * ZOOM, int(cut.group(5)) * ZOOM],
        }

    # **안 쓰는 그림이라도 지우지 않는다.** 한때 "게임이 안 쏘는 번호는 치운다"고 지웠다가 소리 24개와
    # 그림 71장이 사라졌다(사용자, 2026-09-19 — "사운드는 왜 없앤거야?"). 지우는 것은 사람이 정할 일이고,
    # 생성기는 **더하고 고치기만** 한다. 무엇을 쓰는지는 `index.json` 의 「연출」이 말해 준다.

    payload = {
        "생성": "scripts/build-ability-sprites.py",
        "출처": "roh.dat — efct###.epf + eff###.pal (색표는 effpal.tbl, 기준점은 efct###.tbl)",
        "배율": ZOOM,
        "연출": drawn,
    }
    INDEX.write_text(json.dumps(payload, ensure_ascii=False, indent=1), encoding="utf-8")
    PAGE.write_text("window.LOD_ABILITY_EFFECTS = " + json.dumps(payload, ensure_ascii=False) + ";\n",
                    encoding="utf-8")

    print(f"연출 {len(drawn)}개 → {DEST.relative_to(ROOT)} · {PAGE.relative_to(ROOT)}")
    if missing:
        print(f"  아카이브에 없는 번호 {len(missing)}개: {', '.join(str(n) for n in missing)}")

    spare = len(list(DEST.glob("efct*.png"))) - len(drawn)
    if spare > 0:
        print(f"  지금 화면이 안 쓰는 그림 {spare}장이 함께 있습니다 (지우지 않습니다)")

    taken, quiet = copy_sounds()
    print(f"소리 {taken}개 → {SOUNDS_TO.relative_to(ROOT)}")
    if quiet:
        print(f"  아직 안 뽑은 소리 {len(quiet)}개: {', '.join(str(n) for n in quiet)}"
              f" (python3 scripts/build-client-effects.py)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
