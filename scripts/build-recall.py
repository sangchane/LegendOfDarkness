#!/usr/bin/env python3
"""이름 없는 `리콜` 아이템 템플릿을 쓴다 — 쓰면 아무 마을의 정해진 자리로 간다.

  python3 scripts/build-recall.py          # 무엇이 바뀌는지만 본다
  python3 scripts/build-recall.py --쓰기    # 서버 정의에 적는다

**무엇을 하나.** 원작(사용자 설명): 마을 이름 리콜(밀레스리콜 …)은 그 마을의 정해진 자리로, 그냥 `리콜` 은 정해지지
않은 마을의 정해진 자리로 보낸다. 마을 이름 리콜은 이미 있다(`scripts/build-pack-consumables.py`, 5.99 `Recoll.txt`).
그냥 `리콜` 만 없었다 — 5.99 에는 그 아이템이 없다.

**어디서 무엇을 가져오나**
  - 동작 → 사용자 설명. 팩 둘은 서로 다르다(혼든 `ITEM_SCRIPT.txt` 는 국적별 마을 여관 가운데 무작위, 노바
    `이동npc.txt` 의 `리콜입니다` 는 10레벨 이하 노비스마을·그 위 용자의공원으로 정해져 있다) → 세 팩이 맞지 않으니 버린다.
  - 어느 마을로 가나 → **이 생성기가 정하지 않는다.** 서버 스크립트 `scripts/Items/Recall.cs` 의 `Villages` 한 곳에
    있고, 자리는 그 마을 리콜 템플릿의 RecallArea·RecallX·RecallY 를 그대로 빌린다.
  - 그림·값·깃발 → 하데스에 이미 있는 `노비스마을리콜` 템플릿을 그대로 쓴다. 팩의 `리콜` 은 서로 다르다
    (혼든 이미지 146·1000원, 노바 이미지 7·값 없음) → 버린다.
  - 파는 곳 → `tools/pack-import/import.py` 의 상점 결합(베이가@노비스잡화상점)이 더한다. 이 생성기는 아이템만 쓴다.

**지우지 않는다.** 이 생성기는 `templates/items/리콜.json` 한 장만 만들고 고친다.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ITEMS = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates" / "items"
LOOK_LIKE = ITEMS / "노비스마을리콜.json"
OUT = ITEMS / "리콜.json"


def template():
    base = json.loads(LOOK_LIKE.read_text(encoding="utf-8"))
    made = {"$type": base["$type"], "Name": "리콜"}
    made.update({k: base[k] for k in ("DisplayImage", "Flags", "CanStack", "MaxStack", "Value")})
    made["ScriptName"] = "Recall"            # scripts/Items/Recall.cs
    made["Group"] = "귀환/아무마을"
    return made


def main():
    write = "--쓰기" in sys.argv
    body = json.dumps(template(), ensure_ascii=False, indent=2)
    same = OUT.exists() and OUT.read_text(encoding="utf-8") == body
    if write and not same:
        OUT.write_text(body, encoding="utf-8")
    state = "그대로" if same else ("씀" if write else "바뀜 (--쓰기 로 쓴다)")
    print(f"리콜 템플릿 {state} → {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
