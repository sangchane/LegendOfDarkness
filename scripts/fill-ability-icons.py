#!/usr/bin/env python3
"""기술·마법 템플릿에 빠진 아이콘 번호(`Icon`)를 원작 번호로 채운다.

템플릿 대부분에 `Icon` 칸이 없어 서버가 0 을 보내고, 앱은 시트 0번(칼 그림)을 그렸다(사용자 2026-09-26: "스킬 아이콘이
원작과 다른 게 많다"). 칸이 **없는** 템플릿만 채우고, 적힌 값은 건드리지 않는다(적힌 81개는 모두 원작과 맞았다).

근거(AGENTS.md 자료 출처 우선순위):
  1. 원작 아카이브 SClass — `data/game-data/abilities.json` 의 `raw[1]` 첫 값(영문 이름)
  2. 5.99 서버팩 — `db/skill/Skill.txt`·`db/spell/spell.txt` 의 `이미지`(한글 이름, 한 이름에 번호가 하나일 때만)
둘이 같은 이름을 함께 가진 경우는 없다. 근거가 없는 템플릿(5.99 스크립트에만 있는 금강불괴·주먹단련 등)은 그대로 둔다.

**예외 — 노바 팩의 `이미지` 가 먼저다**(사용자 2026-09-26: "아이콘은 노바가 맞다"). 노바 `skill/default.txt`·`spell/spell.txt`
에 같은 이름이 있으면 그 번호를 쓰고, 이미 적힌 값이 다르면 고친다(금강불괴 53 · 바투 38 · 찔러휘비기 6 · 슬레쉬 64 …).

**같은 그림 규칙**(`SAME_ICON`) — 어느 자료에도 번호가 없어 사람이 정한 것. 쿠라노토는 쿠로토와 같은 번호
(사용자 2026-09-27: "쿠로토랑 같았던 것 같다/비슷하게 생겼거나"). 이 규칙이 노바·SClass·5.99 보다 먼저다.

채운 뒤 `python3 scripts/build-auto-learn.py --쓰기` 로 앱의 자동 습득 표를 다시 만든다.

    python3 scripts/fill-ability-icons.py          # 무엇을 채울지 세기만
    python3 scripts/fill-ability-icons.py --쓰기
"""

import collections
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TEMPLATES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates"
ABILITIES = ROOT / "data" / "game-data" / "abilities.json"
PACK = ROOT / "data" / "server-packs" / "5.99-server" / "db"
NOVA = ROOT / "data" / "server-packs" / "novaonline" / "db"
#: (종류, 이름) → 같은 그림을 쓰는 (종류, 이름). 위 설명.
SAME_ICON = {("spell", "쿠라노토"): ("spell", "쿠로토")}


def template_icon(kind, name):
    for path in (TEMPLATES / f"{kind}s").rglob("*.json"):
        template = json.loads(path.read_bytes().decode("utf-8-sig"))
        if isinstance(template, dict) and template.get("Name") == name:
            return template.get("Icon")
    return None


def original():
    """(종류, 소문자 이름) → SClass 아이콘 번호."""
    out = collections.defaultdict(set)
    for ability in json.loads(ABILITIES.read_text(encoding="utf-8-sig")):
        out[(ability["kind"], ability["name"].lower())].add(int(ability["raw"][1].split("/")[0]))
    return {key: next(iter(icons)) for key, icons in out.items() if len(icons) == 1}


def pack(path, kind):
    """(종류, 이름) → 5.99 `이미지`. 같은 이름에 번호가 갈리면 버린다."""
    raw = path.read_bytes()
    try:
        text = raw.decode("utf-8-sig")  # 저장소 사본(data/server-packs)은 UTF-8 로 옮겨 두었다
    except UnicodeDecodeError:
        text = raw.decode("cp949", errors="replace")
    out = collections.defaultdict(set)
    for block in re.findall(r"\{(.*?)\}", text, re.S):
        fields = dict(line.strip().split("\t", 1) for line in block.splitlines() if "\t" in line.strip())
        fields = {k.strip(): v.strip() for k, v in fields.items()}
        if "이름" in fields and fields.get("이미지", "").isdigit():
            out[(kind, fields["이름"])].add(int(fields["이미지"]))
    return {key: next(iter(icons)) for key, icons in out.items() if len(icons) == 1}


def main():
    write = "--쓰기" in sys.argv
    sclass = original()
    five = {**pack(PACK / "skill" / "Skill.txt", "skill"), **pack(PACK / "spell" / "spell.txt", "spell")}
    nova = {**pack(NOVA / "skill" / "default.txt", "skill"), **pack(NOVA / "spell" / "spell.txt", "spell")}
    counts = collections.Counter()

    for kind in ("skill", "spell"):
        for path in sorted((TEMPLATES / f"{kind}s").rglob("*.json")):
            raw = path.read_bytes()
            bom = raw.startswith(b"\xef\xbb\xbf")
            text = raw.decode("utf-8-sig")
            template = json.loads(text)
            name = template.get("Name", "")

            if name.startswith("Monster_"):
                counts["괴물"] += 1
                continue

            same = SAME_ICON.get((kind, name))
            if same or (kind, name) in nova:
                icon, source = (template_icon(*same), "같은 그림 규칙") if same else (nova[(kind, name)], "노바")
                if icon is None:
                    sys.exit(f"{same} 템플릿의 Icon 이 없습니다")
                if template.get("Icon") == icon:
                    counts[f"{source}{'와' if source == '노바' else '과'} 같음"] += 1
                    continue
                if "Icon" in template:
                    counts[f"{source}로 고침"] += 1
                    if write:
                        fixed, n = re.subn(r'^(\s*"Icon":\s*)-?\d+', lambda m: f"{m.group(1)}{icon}", text, count=1, flags=re.M)
                        if n != 1:
                            sys.exit(f"Icon 줄을 못 찾았습니다: {path}")
                        json.loads(fixed)
                        path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + fixed.encode("utf-8"))
                    continue
            elif "Icon" in template:
                counts["있음"] += 1
                continue
            else:
                icon, source = sclass.get((kind, name.lower())), "SClass"
            if icon is None:
                icon, source = five.get((kind, name)), "5.99"
            if icon is None:
                counts["근거 없음"] += 1
                continue

            counts[source] += 1
            if write:
                # 줄 하나만 끼운다 — 나머지 모양(들여쓰기·BOM·줄끝)은 그대로.
                filled, n = re.subn(r'^(\s*)"Name":[^\n]*\n', lambda m: f'{m.group(0)}{m.group(1)}"Icon": {icon},\n',
                                    text, count=1, flags=re.M)
                if n != 1:
                    sys.exit(f"Name 줄을 못 찾았습니다: {path}")
                json.loads(filled)
                path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + filled.encode("utf-8"))

    print(("채웠습니다 " if write else "채울 것 ") + " · ".join(f"{k} {v}" for k, v in sorted(counts.items())))


if __name__ == "__main__":
    main()
