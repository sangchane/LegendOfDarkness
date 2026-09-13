#!/usr/bin/env python3
"""하데스가 싣는 영문 아이템을 서버 아이템 템플릿으로 넣는다.

규칙 1번대로 아이템의 베이스는 하데스 것이다(`origin/Zolian` 의 940장). 그런데 팩에서 온
989개를 **한 번에 빼면 세계가 끊긴다** — 실측:

  상점 재고  180가지 · 360항목   중 영문 이름에 있는 것 0개
  괴물 드롭   43가지 · 237항목   중 영문 이름에 있는 것 1개

재고가 `최하급체력포션` 같은 한글 이름이고 영문 940장에는 그런 것이 없다. 그래서 순서를
**더하기 먼저, 빼기 나중**으로 한다:

  1. (이 스크립트) 영문 940장을 얹는다. 이름이 3개만 겹쳐 충돌이 없다.
  2. 상점·드롭·퀘스트를 영문 이름으로 옮긴다. 이미지+착용자리로 짝을 찾는다.
  3. 옮겨진 뒤에 팩 것을 뺀다.

  쓰는 법: python3 scripts/write-hades-items.py [--write]
"""
import json, sys, glob, re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FORK = ROOT / "sources/wren11/Dark-Ages-Private-Server"
OUT = FORK / "database/server/templates/items"
SRC = ROOT / "data/game-data/items-hades.json"
TYPED = "Darkages.Types.{0}, Darkages.Server"
MARK = "하데스표"          # 우리가 얹은 것이라는 표. 다시 쓸 때 이것만 지운다.
BANNED = re.compile(r'[\\/:*?"<>|#\[\]^]')


def existing():
    """이미 있는 이름. 서버가 쓴 파일은 꼬리 쉼표가 있어 그대로는 못 읽는다."""
    out = {}
    for f in sorted(OUT.glob("*.json")):
        text = re.sub(r",(\s*[}\]])", r"\1", f.read_text(encoding="utf-8-sig"))
        try:
            d = json.loads(text)
        except Exception:
            continue
        out[d.get("Name") or f.stem] = (f, str(d.get("Group") or ""))
    return out


def main():
    write = "--write" in sys.argv
    rows = json.loads(SRC.read_text(encoding="utf-8-sig"))
    have = existing()

    # 전에 우리가 얹은 것은 지우고 다시 쓴다. 남의 것은 건드리지 않는다.
    stale = [f for f, group in have.values() if group.startswith(MARK)]

    added, clashed = 0, []
    for r in rows:
        name = r.get("Name")
        if not name:
            continue
        if name in have and not have[name][1].startswith(MARK):
            clashed.append(name)
            continue

        doc = {"$type": TYPED.format("ItemTemplate")}
        doc.update({k: v for k, v in r.items()})
        doc["Group"] = MARK
        doc.setdefault("ID", 0)

        if write:
            (OUT / f"{BANNED.sub('_', name).strip().lower()}.json").write_text(
                json.dumps(doc, ensure_ascii=False, indent=2), encoding="utf-8")
        added += 1

    print(f"하데스 영문 아이템 {len(rows)}장 · 얹을 것 {added}장")
    print(f"  이미 있는 이름이라 건너뛴 것 {len(clashed)}개: {', '.join(clashed[:6])}")
    print(f"  전에 얹었던 것 {len(stale)}장 (다시 쓰기 전에 지운다)")
    print(f"  기존 템플릿 {len(have)}장은 그대로 둔다 — 상점·드롭이 그 이름을 참조한다")

    if write:
        for f in stale:
            f.unlink()
        print(f"\n→ {OUT.relative_to(FORK)}  (얹음 {added}장)")
    else:
        print("\n(예행 연습이다. 쓰려면 --write)")


if __name__ == "__main__":
    main()
