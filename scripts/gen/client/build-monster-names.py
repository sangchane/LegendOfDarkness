#!/usr/bin/env python3
"""괴물 이름표 — 앱이 고른 괴물을 「괴물 92」 대신 이름으로 부르게(사용자 2026-10-05).

서버는 괴물을 원작 형식(ServerFormat07, 이름 칸 없음)으로 보내므로 앱은 맵과 그림 번호만 안다. 서버 괴물 템플릿의
`AreaID`(나오는 맵)·`Image`(그림)·`BaseName` 으로 「맵 · 그림 → 이름」 줄을 낸다. 이름 끝의 변형 번호(좀비1·좀비2)는 뗀다.
같은 맵·그림에 이름이 여럿이면 처음 것.

  쓰는 법: python3 scripts/gen/client/build-monster-names.py
  산출물:  mobile/client/assets/world/monster-names.txt  (맵<TAB>그림<TAB>이름)
"""
import re
import sys
import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from graphify_runtime import configure_utf8_stdio

from lib._paths import ROOT

TEMPLATES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates" / "monsters"
OUT = ROOT / "mobile" / "client" / "assets" / "world" / "monster-names.txt"

# 템플릿은 서버의 너그러운 파서에 맞춰 쓰여 있다(끝 쉼표, 따옴표 없는 16진수) — JSON 으로 읽지 않고 이 줄만 집는다.
FIELD = {key: re.compile(rf'"{key}"\s*:\s*"?([^",\r\n]*)"?') for key in ("Name", "BaseName", "AreaID", "Image")}

configure_utf8_stdio(sys.stdout, sys.stderr)


def number(text: str) -> int:
    return int(text, 16) if text.lower().startswith("0x") else int(text)


def main() -> None:
    names: dict[tuple[int, int], str] = {}

    for path in sorted(TEMPLATES.rglob("*.json")):
        text = path.read_text(encoding="utf-8-sig")
        found = {key: pattern.search(text) for key, pattern in FIELD.items()}

        if not (found["AreaID"] and found["Image"] and (found["BaseName"] or found["Name"])):
            continue

        raw = (found["BaseName"] or found["Name"]).group(1).strip()
        name = re.sub(r"\d+$", "", raw) or raw
        key = (number(found["AreaID"].group(1)), number(found["Image"].group(1)))
        names.setdefault(key, name)

    lines = [f"{area}\t{image}\t{name}" for (area, image), name in sorted(names.items())]
    OUT.write_text("# 맵\t그림\t이름 — scripts/gen/client/build-monster-names.py 가 만든다\n" + "\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(lines)}줄 → {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
