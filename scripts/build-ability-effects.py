#!/usr/bin/env python3
"""기술·마법의 아이콘·레벨·이펙트·사운드를 서버팩에서 뽑는다.

하데스가 싣는 기술 613개에는 **이펙트도 사운드도 없다** (`templates/skills/*.json` 은 이름·선행·
요구스탯뿐이다). 그 둘은 한글 팩에만 있다 — 팩의 기술 DB 가 `script_do` 로 스크립트를 가리키고,
그 스크립트 본문에 `sound 35,0;` 과 `effect @ta` 가 적혀 있다.

  db/skill/default.txt   { 이름 · 이미지 · 레벨 · script_do }
  db/spell/spell.txt     { 이름 · 이미지 · 타입 · 딜레이 · 레벨 · script_do }
  db/script/**/*.txt     <머리말> <script_do> { ... sound N,M; ... effect @대상 ... }

**아이템의 접사 자리에 기술은 레벨이 온다.** `홀리볼트`·`홀리볼트(Lev1~4)` 가 전부 같은 아이콘
(img71)을 쓴다 — 실측으로 기술 10종·마법 14종이 그렇고, 아이콘이 같은 비율이 90%·100% 다.
그래서 밑말로 묶고 레벨은 속성으로 붙인다. 카드를 레벨마다 만들면 같은 그림으로 도배된다.

한 아이콘을 서로 다른 밑말이 나눠 쓰기도 한다(기술 32종·마법 24종). 아이콘만으로는 못 가린다 —
아이템의 그림 충돌과 같은 일이다.

  쓰는 법: python3 scripts/build-ability-effects.py
  산출물:  data/game-data/ability-effects.json
"""
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
PACK = ROOT / "data" / "server-packs" / "novaonline" / "db"
OUT = ROOT / "data" / "game-data" / "ability-effects.json"

configure_utf8_stdio(sys.stdout, sys.stderr)

LEVEL_TAIL = re.compile(r"\(Lev(\d+)\)$")


def parse_blocks(text):
    """`{ 키<탭>값 ... }` 덩어리를 행으로 읽는다. 주석(`//`)과 빈 줄은 건너뛴다."""
    rows, current = [], None
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("//"):
            continue
        if stripped == "{":
            current = {}
        elif stripped == "}":
            if current and current.get("이름"):
                rows.append(current)
            current = None
        elif current is not None and "\t" in stripped:
            key, _, value = stripped.partition("\t")
            current[key.strip()] = value.strip()
    return rows


def split_level(name):
    """`홀리볼트(Lev3)` → (`홀리볼트`, 3). 레벨이 아닌 괄호 꼬리는 그대로 둔다."""
    found = LEVEL_TAIL.search(name)
    return (name[:found.start()], int(found.group(1))) if found else (name, None)


def script_media(text, wanted):
    """이름이 `wanted` 인 스크립트 한 덩어리에서 사운드 번호와 이펙트 지시를 뽑는다.

    스크립트는 `<머리말><탭><이름><탭>{` 로 열리고 첫 열의 `}` 로 닫힌다. 다른 스크립트의
    소리를 섞지 않으려면 그 구간만 봐야 한다 — 파일 하나에 수백 개가 들어 있다.
    """
    lines = text.splitlines()
    inside, body = False, []
    for line in lines:
        if not inside:
            if f"\t{wanted}\t" in line or line.strip().startswith(f"{wanted}\t"):
                inside = True
            continue
        if line.startswith("}"):
            break
        body.append(line)
    joined = "\n".join(body)

    sounds, effects, motions = [], [], []
    # **`sound` 가 아니라 `game_sound` 다.** 팩에 `sound` 단독 지시는 하나도 없고
    # `game_sound` 가 3,989 회 쓰인다. 처음에 `sound` 로 세어 0 개가 나왔다.
    for number in re.findall(r"\bgame_sound\s+(\d+)", joined):
        if int(number) not in sounds:
            sounds.append(int(number))
    for directive in re.findall(r"\beffect\s+(@[^\n;]*)", joined):
        directive = directive.strip()
        if directive and directive not in effects:
            effects.append(directive)
    # `motion <번호>, <지속>` — 기술마다 따로 있다. `skill.tbl` 의 직업 동작과는 다른 층이다.
    for number, span in re.findall(r"\bmotion\s+(\d+)\s*,\s*(\d+)", joined):
        pair = [int(number), int(span)]
        if pair not in motions:
            motions.append(pair)
    return {"사운드": sounds, "이펙트": effects, "모션": motions}


def read(path, *encodings):
    for encoding in encodings:
        try:
            return path.read_text(encoding=encoding)
        except (UnicodeDecodeError, LookupError):
            continue
    return path.read_text(encoding="utf-8", errors="replace")


def main():
    scripts = ""
    for path in sorted((PACK / "script").rglob("*.txt")):
        scripts += read(path, "utf-8", "cp949") + "\n"

    grouped = defaultdict(lambda: {"아이콘": None, "레벨": [], "갈래": None})
    for kind, name in (("기술", "skill/default.txt"), ("마법", "spell/spell.txt")):
        source = PACK / name
        if not source.exists():
            continue
        for row in parse_blocks(read(source, "utf-8", "cp949")):
            base, level = split_level(row["이름"])
            entry = grouped[base]
            entry["갈래"] = entry["갈래"] or kind
            if entry["아이콘"] is None and (row.get("이미지") or "").isdigit():
                entry["아이콘"] = int(row["이미지"])
            media = (script_media(scripts, row["script_do"]) if row.get("script_do")
                     else {"사운드": [], "이펙트": [], "모션": []})
            entry["레벨"].append({"이름": row["이름"], "레벨": level,
                                "딜레이": row.get("딜레이"), "타입": row.get("타입"),
                                **media})

    payload = {"생성": "scripts/build-ability-effects.py",
               "출처": "data/server-packs/novaonline/db (기술 DB + 스크립트 본문)",
               "밑말": {k: v for k, v in sorted(grouped.items())}}
    OUT.write_text(json.dumps(payload, ensure_ascii=False, indent=1), encoding="utf-8")

    total = sum(len(v["레벨"]) for v in grouped.values())
    with_sound = sum(1 for v in grouped.values() if any(l["사운드"] for l in v["레벨"]))
    with_effect = sum(1 for v in grouped.values() if any(l["이펙트"] for l in v["레벨"]))
    with_motion = sum(1 for v in grouped.values() if any(l["모션"] for l in v["레벨"]))
    levelled = sum(1 for v in grouped.values() if len(v["레벨"]) > 1)
    print(f"밑말 {len(grouped)}종 · 레벨까지 {total}개 → {OUT.relative_to(ROOT)}")
    print(f"  레벨이 여럿인 것 {levelled}종 · 사운드 {with_sound}종 · 이펙트 {with_effect}종 · 모션 {with_motion}종")


if __name__ == "__main__":
    main()
