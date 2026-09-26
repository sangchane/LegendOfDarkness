#!/usr/bin/env python3
"""게임 클라이언트가 그릴 기술 이펙트와 틀 소리를 원작 아카이브에서 뽑는다.

서버는 이펙트를 번호로 보낸다(0x29) — 그 번호가 곧 `roh.dat` 의 `efct###.epf` 다. 소리(0x19)도 번호가
`Legend.dat` 의 `N.mp3` 다. 관리페이지용(`build-ability-sprites.py`)은 한글 이름이 정해진 기술의 83개만
뽑으므로, 여기서는 **서버가 실제로 보낼 수 있는 번호를 다 모은다**:

- 5.99 스크립트(기술·마법·괴물 마법)의 `effect @대상, 쓴쪽그림, 대상그림` · 파티 그림(`group_hill` 등)
- 하데스로 옮긴 스크립트(`scripts/Pack599`)의 `p.Call("effect", …)` — 노바 번호로 바꾼 것(`build-nova-effects.py`)
- 하데스 기술·마법 템플릿의 `TargetAnimation`·`Animation`·`MissAnimation`
- 하데스 코드에 박힌 `SendAnimation(번호, …)` (디버프가 거는 그림 등) · 헛친 기술의 `SkillMiss` 상수

소리는 165개 다 해도 2MB 남짓이라 전부 넣는다.

캐릭터·괴물 그림과 같은 1배로 뽑는다(관리페이지는 2배). 색표는 `effpal.tbl` 이 정한다(자세한 까닭은 `build-ability-sprites.py`).

**`efct` 명령으로 자른다 — `epf` 가 아니다.** 연출은 제 바탕 위 어느 자리에 그려져 있고, 그 자리가 곧 어디에
터지는지다. 일음지(`efct042`)는 111x85 바탕의 (48,10) 에 놓인 13x13 반짝임이라 발밑 기준에서 48~60px 위 —
**머리 위**다. 조각만 떼면 13x13 그림이 되고, 그것을 몸통에 맞춰 늘리면 몸 전체를 덮는다.
기준점은 `efct###.tbl`(프레임마다 16비트 x·y) 이고 EFA(232+)는 프레임 머리말의 가운데 x·y 다.

프레임 수와 **바탕·기준점·틀 순서**는 옆 글자 파일(`effects.txt`, `번호 칸수 바탕가로 바탕세로 기준x 기준y 순서…` 줄)로
둔다 — 내보낼 때 이미 있는 `*.txt` 필터로 따라간다.
틀 순서는 원작 2005(= 5.99 클라이언트) `roh.dat` 의 `effect.tbl` 이다 — 첫 줄이 개수, 그다음 줄마다 이펙트 번호
차례로 칸 순서(203 = `0 1 1`). 원작 클라이언트가 이 순서로 튼다(docs/disassembly.md). 빈 칸도 번호를 차지하므로
`dat-extract` 가 빈 칸을 자리표시로 남긴 뒤에 뽑아야 순서가 맞는다.

  쓰는 법: python3 scripts/build-client-effects.py
  산출물:  mobile/client/assets/effect/efct###.png · effects.txt · mobile/client/assets/sound/N.mp3
"""
import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
HADES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server"
PACK = ROOT / "data" / "server-packs" / "5.99-server" / "db" / "script"
ROH = HADES / "database" / "archives" / "roh" / "roh.dat"
#: 5.99 한국 클라이언트. 저장소 밖에 있다 — 없으면 232 번 이상은 건너뛴다.
KOREAN_ROH = Path.home() / "Downloads" / "5.99 클라이언트" / "roh.dat"
LEGEND = HADES / "database" / "archives" / "legend" / "Legend.dat"
EFFECTS = ROOT / "mobile" / "client" / "assets" / "effect"
SOUNDS = ROOT / "mobile" / "client" / "assets" / "sound"
DOTNET = ROOT / ".tools" / "dotnet-9.0.317" / "dotnet"
TOOL = ROOT / "tools" / "dat-extract" / "bin" / "Release" / "net8.0" / "dat-extract.dll"

configure_utf8_stdio(sys.stdout, sys.stderr)

#: `efct`·`efa` 가 적어 주는 줄: 프레임 수 · 바탕 크기 · 기준점.
CUT = re.compile(r"프레임 (\d+)개 · 바탕 (\d+)x(\d+) · 기준 (-?\d+),(-?\d+)")


def run(*args):
    return subprocess.run([str(DOTNET), str(TOOL), *map(str, args)], capture_output=True, text=True,
                          encoding="utf-8", errors="replace", cwd=ROOT)


def effect_numbers():
    found = set()
    for path in list((PACK / "Skill").glob("*.txt")) + [PACK / "Mob_Spell.txt"]:
        text = path.read_text(encoding="utf-8", errors="replace")
        for a, b in re.findall(r"\beffect\s+@\w+\s*,\s*(\d+)\s*,\s*(\d+)", text):
            found.update((int(a), int(b)))
        found.update(int(n) for n in re.findall(r"\b(?:group_hill\s+[^,;]+,|group_mob(?:sor|nar)_end|god_bless)\s*(\d+)", text))
    for path in (HADES / "database" / "server" / "templates").glob("s*/*.json"):
        try:
            template = json.loads(path.read_text(encoding="utf-8-sig"))
        except ValueError:
            continue
        for key in ("TargetAnimation", "Animation", "MissAnimation"):
            if isinstance(template.get(key), int):
                found.add(template[key])
    for path in list((HADES / "database" / "server" / "scripts").rglob("*.cs")) + list((HADES / "src").rglob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        # 16진수로 적힌 것도 있다(beag ioc fein 의 `SendAnimation(0x04, …)`).
        found.update(int(n, 0) for n in re.findall(r"SendAnimation\((0x[0-9A-Fa-f]+|\d+)", text))
        # 헛친 기술의 「Miss」 그림은 코드의 상수다(`MonkStrike.SkillMiss`).
        found.update(int(n) for n in re.findall(r"const ushort \w*Miss\w* = (\d+)", text))
        # 옮긴 5.99 스크립트의 `effect` — 노바 번호로 바뀐 것이 여기 있다(`build-nova-effects.py`, 2026-09-26).
        for a, b in re.findall(r'p\.Call\("effect", [^,]+, \(V\)(\d+)L, \(V\)(\d+)L', text):
            found.update((int(a), int(b)))
    return sorted(n for n in found if 0 < n < 1000)


def palettes(scratch):
    """`<이펙트번호> <색표번호>` 줄, 셋째 값이 있으면 앞 둘이 범위(셋째가 음수면 둘째가 색표)."""
    run("dump", ROH, scratch, "effpal")
    found = next(Path(scratch).rglob("effpal.tbl"), None)
    table = []
    for line in (found.read_text(encoding="cp949", errors="replace").splitlines() if found else []):
        parts = line.split()
        if len(parts) < 2 or not all(p.lstrip("-").isdigit() for p in parts[:2]):
            continue
        low, second = int(parts[0]), int(parts[1])
        if len(parts) == 2:
            table.append((low, low, second))
        elif parts[2].lstrip("-").isdigit():
            third = int(parts[2])
            table.append((low, low, second) if third < 0 else (low, second, third))

    def palette(number):
        for low, high, value in table:
            if low <= number <= high:
                return value - 1000 if value >= 1000 else value
        return 0

    return palette


def effect_orders(scratch):
    """이펙트 번호 → 칸 순서. 5.99 한국 클라이언트(= 원작 2005)의 `effect.tbl` 을 먼저, 없으면 하데스 것."""
    for archive in (KOREAN_ROH, ROH):
        if not archive.exists():
            continue
        folder = Path(scratch) / archive.parent.name
        run("dump", archive, folder, "effect.tbl")
        found = next((p for p in folder.rglob("*") if p.name.lower() == "effect.tbl"), None)
        if not found:
            continue
        lines = found.read_text(encoding="cp949", errors="replace").splitlines()[1:]
        return {number: [int(x) for x in line.split() if x.isdigit()] for number, line in enumerate(lines, start=1)}
    return {}


def main():
    if not TOOL.exists():
        print(f"도구가 없습니다. 먼저: {DOTNET} build tools/dat-extract/DatExtract.csproj -c Release")
        return 1

    EFFECTS.mkdir(parents=True, exist_ok=True)
    SOUNDS.mkdir(parents=True, exist_ok=True)

    wanted = effect_numbers()
    drawn, missing, efa = [], [], set()
    with tempfile.TemporaryDirectory() as scratch:
        palette = palettes(scratch)
        orders = effect_orders(scratch)
        for number in wanted:
            name = f"efct{number:03d}"
            out = EFFECTS / f"{name}.png"
            proc = run("efct", ROH, name, out, 1, f"eff{palette(number):03d}.pal")
            cut = CUT.search(proc.stdout)
            if not cut and KOREAN_ROH.exists():
                # 232 번부터는 한국 5.99 클라이언트에만 있고 형식도 EFA 다.
                proc = run("efa", KOREAN_ROH, name, out, 1)
                cut = CUT.search(proc.stdout)
                efa.add(number)
            if not cut or not out.exists():
                missing.append(number)
                continue
            drawn.append((number, *(int(g) for g in cut.groups())))

    (EFFECTS / "effects.txt").write_text(
        "# 번호 칸수 바탕가로 바탕세로 기준x 기준y 순서 — scripts/build-client-effects.py (순서는 effect.tbl)\n"
        # EFA 는 자기 칸 수·간격을 파일에 갖고, effect.tbl 의 그 번호 줄은 "0" 한 칸뿐이다 — 순서를 적지 않고 차례로 튼다.
        + "".join(
            f"{n} {f} {w} {h} {x} {y} {'' if n in efa else ' '.join(map(str, orders.get(n, [])))}".rstrip() + "\n"
            for n, f, w, h, x, y in drawn),
        encoding="utf-8")
    print(f"이펙트 {len(drawn)}개 → {EFFECTS.relative_to(ROOT)}")
    if missing:
        print(f"  아카이브에 없는 번호 {len(missing)}개: {', '.join(map(str, missing))}")

    with tempfile.TemporaryDirectory() as scratch:
        proc = run("dump", LEGEND, scratch, ".mp3")
        if proc.returncode != 0:
            print(proc.stderr.strip() or proc.stdout.strip())
            return 1
        taken = 0
        for source in Path(scratch).rglob("*.mp3"):
            if source.stem.isdigit():
                shutil.copyfile(source, SOUNDS / source.name)
                taken += 1
    print(f"소리 {taken}개 → {SOUNDS.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
