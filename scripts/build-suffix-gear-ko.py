#!/usr/bin/env python3
"""접미사 반지·귀걸이 영문 템플릿("하데스표")에 한글 이름을 붙여 새 파일로 살린다.

  python3 scripts/build-suffix-gear-ko.py            # 무엇을 만드는지만 본다
  python3 scripts/build-suffix-gear-ko.py --쓰기      # 새 한글 템플릿을 더한다

**사용자 결정(2026-09-25)**: 사냥터 장비 드랍에서 기본템을 빼고 그 자리에 접미사·속성 장비를 채우려는데,
접미사 쪽(사람이름 계열)은 한글 이름이 `로오의반지`·`칸의목걸이` 둘뿐이다. 나머지 다섯(이아·메투스·
세토아·세오·셔스)은 하데스가 영문으로만 갖고 있던 496종("하데스표", `Group: "하데스표"`) 안에 있다.

**대응표**: `data/pack-compare/한글이름-검토.tsv`(영문·부위·요구레벨·제안·후보·등급). 접두사 대응은
사용자가 정했다 — Luathas=로오 · Glioca=이아 · Cail=메투스 · Ceannlaidir=세토아 · Deoch=세오 ·
Fiosachd=셔스 · Gramail=칸. **Sgrios 는 대응이 불확실해 쓰지 않는다**(TSV 에는 `뮤레칸`으로 올라 있지만
그 이름은 이미 혼수 보스 뮤레칸의 것이라 겹친다 — 사용자 지시).

**고르는 기준**: 위 일곱 접두사로 시작하고 반지(Ring)·귀걸이(Earrings)인 줄 중, `제안` 칸이 채워져
있고 `등급`이 `CONFLICT`·`NAME_CLASH`(같은 이름을 두 영문이 다툰다)가 아닌 것만. `Sgrios`처럼 이름이
겹쳐 후보가 갈리는 줄은 이 기준에서 자연히 빠진다.

**살리는 방법**: 영문 템플릿의 능력치를 그대로 복사하고 `Name`(과 파일 이름)만 한글로 바꾼다. 영문
템플릿은 지우지 않는다(`생성기는 더하고 고치기만` — 삭제 금지). `DropRate`(영문 템플릿에 남아 있는
0.1 — 아무도 안 쓰는 값)와 `ID`(0 뿐인 빈 칸)는 새 칸에서 뺀다. 실제로 어느 사냥터가 무엇을 얼마에
떨구는지는 `scripts/build-gear-drops.py` 가 정한다(이 생성기는 이름만 살린다).

**Group**: `하데스표/반지/공통반지` · `하데스표/귀걸이/공통귀걸이` — 영문 템플릿 자신의 `Group`
("하데스표")을 이어받아 갈래만 더 적는다. 5.99 팩에서 온 것이 아니므로 `5.99표`를 붙이지 않는다.
"""

import argparse
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
ITEMS = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server/templates/items"
TSV = ROOT / "data/pack-compare/한글이름-검토.tsv"

# 사용자가 정한 접두사 대응. Sgrios(→뮤레칸)는 이름이 겹쳐 뺀다.
PREFIXES = {
    "Luathas": "로오",
    "Glioca": "이아",
    "Cail": "메투스",
    "Ceannlaidir": "세토아",
    "Deoch": "세오",
    "Fiosachd": "셔스",
    "Gramail": "칸",
}

SKIP_GRADES = {"CONFLICT", "NAME_CLASH"}

LENIENT = re.compile(r",(\s*[\]}])")


def read(path):
    return json.loads(LENIENT.sub(r"\1", path.read_text(encoding="utf-8-sig")))


def load_items():
    """영문 이름(소문자) → 경로. 파일 이름 표기가 들쭉날쭉해 Name 칸으로 찾는다."""
    by_name = {}
    for path in ITEMS.rglob("*.json"):
        try:
            item = read(path)
        except json.JSONDecodeError:
            continue
        name = item.get("Name")
        if name:
            by_name[name.lower()] = path
    return by_name


def rows():
    """대응표에서 살릴 줄만. (영문 이름, 부위, 한글 이름)."""
    lines = TSV.read_text(encoding="utf-8").splitlines()
    out = []
    for line in lines[1:]:
        cols = line.split("\t")
        if len(cols) < 6:
            continue
        english, _part, _level, korean, _candidates, grade = cols[:6]
        prefix = english.split(" ", 1)[0]
        if prefix not in PREFIXES or not korean or grade in SKIP_GRADES:
            continue
        if not (english.endswith(" Ring") or english.endswith(" Earrings")):
            continue
        out.append((english, korean))
    return out


def group_for(english):
    return "하데스표/반지/공통반지" if english.endswith(" Ring") else "하데스표/귀걸이/공통귀걸이"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    by_name = load_items()
    made, missing, already = [], [], []

    for english, korean in rows():
        target = ITEMS / f"{korean}.json"

        if target.exists():
            already.append(korean)
            continue

        source = by_name.get(english.lower())

        if source is None:
            missing.append(english)
            continue

        item = read(source)
        item["Name"] = korean
        item.pop("DropRate", None)
        item.pop("ID", None)
        item["Group"] = group_for(english)

        if writing:
            target.write_text(json.dumps(item, ensure_ascii=False, indent=2), encoding="utf-8")

        made.append(f"{korean} ({english})")

    print(f"살릴 것 {len(made)}개 {'씀' if writing else '(미리 봄 — --쓰기 로 쓴다)'}: {', '.join(sorted(made))}")
    if already:
        print(f"이미 있어 건너뜀 {len(already)}개: {', '.join(sorted(already))}")
    if missing:
        print(f"대응표에는 있는데 템플릿을 못 찾음 {len(missing)}개: {', '.join(sorted(missing))}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
