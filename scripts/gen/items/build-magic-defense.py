#!/usr/bin/env python3
"""장비 마법방어(MrModifer, %)를 사용자가 기억하는 원작 값으로 채운다.

  python3 scripts/gen/items/build-magic-defense.py          # 무엇이 바뀌는지만 본다
  python3 scripts/gen/items/build-magic-defense.py --쓰기    # 아이템 템플릿에 적는다

**사용자 결정(2026-10-07)**: 「칸 접두는 10%가 맞고 기사단은 30이야 해골, 퇴마방패 같은게 20% 파파야방패가 20인가 30이었는데
헷갈리네」. 원작 팩 셋(5.99·혼든·노바)과 `docs/original-items/방패류.md` 에는 이 방패들의 마법방어가 없고, 카페 글
(`docs/darkages-cafe/item/520-…`)에는 「한국식 칸 — 마법방어 10%」만 있다.
  - 기사단방패와 접두 기사단방패(로오의·칸의 …): 30
  - 해골방패·퇴마방패: 20 — 지금 서버에 템플릿이 없어 건너뛴다(있으면 적는다)
  - 파파야방패: 20(사용자가 20·30 을 헷갈려 낮은 쪽)
  - 이름이 「칸의」로 시작하는 장비: 위 값 + 10

값은 올리기만 한다 — 이미 붙은 값이 더 크면 그대로 둔다. 하데스 마법방어는 0~70 %(`Sprite.Mr`)이고 서버는 마법을
그 확률로 빗나가게 한다(`Pack599.Resisted`).
"""

import argparse
import json
import sys

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from lib._paths import ROOT
from lib._io import read_lenient_json as read

ITEMS = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server/templates/items"

SHIELDS = {"해골방패": 20, "퇴마방패": 20, "파파야방패": 20}
KNIGHT, KNIGHT_MR = "기사단방패", 30
KHAN, KHAN_MR = "칸의", 10


def wanted(name):
    """이 이름의 장비가 가져야 할 마법방어."""
    base = KNIGHT_MR if name.endswith(KNIGHT) else SHIELDS.get(name, 0)
    return base + (KHAN_MR if name.startswith(KHAN) else 0)


def main():
    parser = argparse.ArgumentParser(description="장비 마법방어")
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    args = parser.parse_args()

    changed = []
    for path in sorted(ITEMS.glob("*.json")):
        try:
            body = read(path)
        except ValueError:
            continue
        if not isinstance(body, dict) or not body.get("Name") or not body.get("EquipmentSlot"):
            continue
        want = wanted(body["Name"])
        have = (body.get("MrModifer") or {}).get("Value", 0)
        if want <= have:
            continue
        body["MrModifer"] = {"$type": "Darkages.Types.StatusOperator, Darkages.Server", "Option": 0, "Value": want}
        changed.append(f"{body['Name']}: {have} → {want}")
        if args.writing:
            path.write_text(json.dumps(body, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    missing = [name for name in SHIELDS if not (ITEMS / f"{name}.json").exists()]
    print(f"마법방어 {len(changed)}종{'' if args.writing else ' (아직 안 씀 — --쓰기)'}"
          f"{' · 템플릿 없음: ' + ', '.join(missing) if missing else ''}")
    for line in changed:
        print(f"  {line}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
