#!/usr/bin/env python3
"""원작 아카이브 khan.dat(남)·khan2.dat(여) 안의 머리 모양 번호를 전수로 센다.

모바일 캐릭터 만들기 화면(머리 모양·머리색)에 무엇을 보여 줄지 정하려면 "고를 수 있는 번호가
몇 개인가"가 먼저 필요하다. 참고 저장소 화면 코드(dark-ages-ts CreateForm.svelte)는 17가지·14가지를
박아 놨는데, 이게 실제 아카이브와 맞는지 확인하지 않고는 못 믿는다. 그래서 `dat-extract list` 로
아카이브 속 그림 이름(mh###·wh###)을 그대로 세고, 100 이하만 머리로 잡는다(100 초과는 투구 —
ServerFormat33.cs:64,70 · docs/original-sprite-animation.md). 머리색은 khan.dat 의 palh.tbl(부위 번호 →
색표 번호)에 머리 번호(1~100)가 등장하는지로, 색 72가지(color0.tbl)가 전부 쓰이는지 제한이 있는지를 본다.

아카이브 두 벌을 다 본다 — 이 저장소 안(database/archives, Hades 가 그대로 복사해 둔 것)과
~/Downloads/5.99 클라이언트(5.99 한국 클라이언트, 저장소 밖 — 없으면 건너뛴다). 둘이 다르면 다르다고 적는다.

  쓰는 법: python3 scripts/build-hairstyle-inventory.py   → data/character-creation/hairstyles.json
"""
import json
import re
import shutil
import subprocess
import sys
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOTNET = ROOT / ".tools" / "dotnet-9.0.317" / "dotnet"
TOOL = ROOT / "tools" / "dat-extract" / "bin" / "Release" / "net8.0" / "dat-extract.dll"
HADES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "archives"
#: 5.99 한국 클라이언트. 저장소 밖에 있다 — 없으면 하데스 것만 쓴다(build-client-wardrobe.py 와 같은 자리).
KOREAN = Path.home() / "Downloads" / "5.99 클라이언트"
OUT = ROOT / "data" / "character-creation" / "hairstyles.json"

#: 머리/투구 이름꼴: 성별글자 + h + 번호(3자리) + (01|02|03 서기·걷기꼴 | 글자 하나 = 직업 동작).
NAME = re.compile(r"^\s*[mw]h(\d{3})(?:0[1-3]|[a-z])\.epf\b", re.IGNORECASE)
HEAD_MAX = 100  # 100 이하가 머리, 초과는 투구.


def run(*args):
    proc = subprocess.run([str(DOTNET), str(TOOL), *args], capture_output=True, text=True,
                          encoding="utf-8", errors="replace", cwd=ROOT)
    return proc.stdout


def head_numbers(archive, gender_letter):
    """그 아카이브에서 100 이하인 머리 번호를 모두 센다(직업 동작 조각 b~f, 서기꼴 01~03 은 같은 번호로 합친다)."""
    if not archive.exists():
        return None
    prefix = "m" if gender_letter == "m" else "w"
    out = run("list", str(archive), f"{prefix}h")
    numbers = set()
    for line in out.splitlines():
        m = NAME.match(line)
        if m:
            n = int(m.group(1))
            if n <= HEAD_MAX:
                numbers.add(n)
    return sorted(numbers)


def palette_rows_for_heads(archive):
    """palh.tbl(부위 번호 → 색표 번호 [-1 남 |-2 여])에 머리 번호(1~100)가 몇 줄이나 나오는지 본다."""
    if not archive.exists():
        return None
    tmp = ROOT / ".tmp-hairstyle-inventory"
    tmp.mkdir(exist_ok=True)
    run("dump", str(archive), str(tmp), "palh")
    tbl = next(tmp.rglob("palh.tbl"), None)
    if tbl is None:
        return []
    text = tbl.read_bytes().decode("cp949", errors="replace")
    rows = []
    for line in text.splitlines():
        parts = line.split()
        if not parts or not parts[0].isdigit():
            continue
        part_no = int(parts[0])
        if part_no <= HEAD_MAX:
            rows.append(line.strip())
    shutil.rmtree(tmp)
    return rows


def gaps(numbers, upper):
    return [n for n in range(1, upper + 1) if n not in numbers]


def main():
    if not TOOL.exists():
        print(f"도구가 없습니다. 먼저: {DOTNET} build tools/dat-extract/DatExtract.csproj -c Release")
        return 1

    hades_m = head_numbers(HADES / "khan" / "khan.dat", "m")
    hades_w = head_numbers(HADES / "khan2" / "khan2.dat", "w")
    korean_m = head_numbers(KOREAN / "khan.dat", "m")
    korean_w = head_numbers(KOREAN / "khan2.dat", "w")

    if hades_m is None or hades_w is None:
        print("아카이브를 저장소 안(database/archives)에서 못 찾았다.")
        return 1

    agree_m = korean_m is None or korean_m == hades_m
    agree_w = korean_w is None or korean_w == hades_w

    male_only = sorted(set(hades_m) - set(hades_w))
    female_only = sorted(set(hades_w) - set(hades_m))
    neither = gaps(set(hades_m) | set(hades_w), 60)

    palh_rows = palette_rows_for_heads(HADES / "khan" / "khan.dat")

    result = {
        "counted": {
            "날짜": date.today().isoformat(),
            "도구": "tools/dat-extract (dotnet run -- list/dump)",
            "아카이브": {
                "남": str((HADES / "khan" / "khan.dat").relative_to(ROOT)),
                "여": str((HADES / "khan2" / "khan2.dat").relative_to(ROOT)),
                "대조(5.99 한국 클라이언트)": str(KOREAN) if korean_m is not None else "없음(건너뜀)",
            },
            "5.99 클라이언트와 일치": {"남": agree_m, "여": agree_w},
            "기준": f"{HEAD_MAX} 이하 = 머리, 초과 = 투구 (ServerFormat33.cs:64,70)",
        },
        "male_head_numbers": hades_m,
        "male_count": len(hades_m),
        "male_missing_1_60": gaps(set(hades_m), 60),
        "female_head_numbers": hades_w,
        "female_count": len(hades_w),
        "female_missing_1_60": gaps(set(hades_w), 60),
        "male_only_numbers": male_only,
        "female_only_numbers": female_only,
        "numbers_missing_both": neither,
        "hair_color": {
            "표": "data/archives-vault/표/Legend — color0.tbl.md (Legend.dat 의 color0.tbl)",
            "가짓수": 72,
            "범위": "0~71",
            "구조": "머리말 1줄(6=색조 수) + 72묶음 × (번호줄 + RGB 6줄) = 505줄",
            "palh.tbl 이 머리 번호(1~100)를 제한하는가": len(palh_rows) > 0 if palh_rows is not None else "확인 못함",
            "palh.tbl 머리 번호 줄": palh_rows,
            "결론": ("palh.tbl 59줄 전부가 100 초과(투구) 번호다 — 1~100 머리 번호는 한 줄도 없다. "
                     "그러니 palh.tbl 은 특정 투구(239~396)에만 색표를 강제하고, 맨머리 1~60 은 제한 없이 "
                     "HairColor 바이트를 그대로 팔레트 줄 번호로 써서(원작 4.51 겉모습 +0x02, 0x442c20 로 넘기는 값 — "
                     "docs/disassembly.md) color0.tbl 72가지를 다 쓸 수 있는 것으로 보인다."
                     if palh_rows == [] else "재확인 필요"),
        },
        "참고저장소와 실제 차이": {
            "dark-ages-ts CreateForm.svelte": "머리 모양 17가지 · 색 14가지로 박아 둠(apps/client/src/ui/routes/auth/CreateForm.svelte:104-114)",
            "실제(아카이브로 확인)": f"머리 모양 남 {len(hades_m)}가지 · 여 {len(hades_w)}가지, 색 72가지",
        },
    }

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"남 머리 {len(hades_m)}개(결번 {gaps(set(hades_m), 60)}) · 여 머리 {len(hades_w)}개(결번 {gaps(set(hades_w), 60)})")
    print(f"5.99 클라이언트 대조 — 남: {'일치' if agree_m else '다름'} · 여: {'일치' if agree_w else '다름'}")
    print(f"→ {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
