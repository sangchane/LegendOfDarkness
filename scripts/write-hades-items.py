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

  쓰는 법: python3 scripts/write-hades-items.py [--write] [--korean]
"""
import json, sys, glob, re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FORK = ROOT / "sources/wren11/Dark-Ages-Private-Server"
KOREAN = ROOT / "data/pack-compare/item-korean-names.json"
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


def korean_names():
    """팩에서 온 한글 이름. **겹치지 않는 것만** 쓴다. `--korean` 을 줘야 쓴다.

    기본으로 켜지 않는 이유: 그림 번호로 이은 짝이라 **뜻이 틀릴 수 있다.** 실제로 10개를
    붙여 보니 `Leather Bracer→가죽팔찌` 는 맞았지만 `Loures Signet Ring→로오의반지`(도시
    루어스를 신 로오로), `Small Emerald Ring→사파이어반지`(에메랄드를 사파이어로) 처럼
    틀린 것이 절반이었다. 등급은 *팩끼리 합의했다* 는 뜻이지 짝이 맞다는 뜻이 아니다.
    사람이 `data/pack-compare/한글이름-검토.tsv` 를 보고 고른 뒤에 쓰는 것이 맞다.

    템플릿은 이름이 열쇠다 (`GlobalItemTemplateCache[template.Name] = template`).
    영문 여럿이 한 한글 이름으로 몰리면 뒤엣것이 앞엣것을 덮어 아이템이 조용히 사라진다.
    그림 하나를 아이템 여럿이 나눠 쓰는 일이 흔해서(보석 반지 12종이 그림 210 하나) 실제로 몰린다.
    """
    if not KOREAN.exists():
        return {}
    rows = json.loads(KOREAN.read_text(encoding="utf-8"))
    settled = [r for r in rows if r.get("한글이름")]
    taken = {}
    for r in settled:
        taken.setdefault(r["한글이름"], []).append(r["영문"])
    return {v[0]: k for k, v in taken.items() if len(v) == 1}


def main():
    write = "--write" in sys.argv
    rows = json.loads(SRC.read_text(encoding="utf-8-sig"))
    have = existing()
    korean = korean_names() if "--korean" in sys.argv else {}

    # 전에 우리가 얹은 것은 지우고 다시 쓴다. 남의 것은 건드리지 않는다.
    added, clashed, renamed = 0, [], []
    stale_paths = {f for f, group in have.values() if group.startswith(MARK)}
    for r in rows:
        name = r.get("Name")
        if not name:
            continue
        ko = korean.get(name)
        if ko and ko not in have:
            renamed.append((name, ko))
            name = ko
        if name in have and not have[name][1].startswith(MARK):
            clashed.append(name)
            continue

        doc = {"$type": TYPED.format("ItemTemplate")}
        doc.update({k: v for k, v in r.items()})
        doc["Name"] = name
        doc["Group"] = MARK
        doc.setdefault("ID", 0)

        if write:
            path = OUT / f"{BANNED.sub('_', name).strip().lower()}.json"
            path.write_text(json.dumps(doc, ensure_ascii=False, indent=2), encoding="utf-8")
            stale_paths.discard(path)      # 방금 쓴 것을 지우지 않는다
        added += 1

    print(f"하데스 영문 아이템 {len(rows)}장 · 얹을 것 {added}장")
    print(f"  이미 있는 이름이라 건너뛴 것 {len(clashed)}개: {', '.join(clashed[:6])}")
    print(f"  전에 얹었던 것 {len(stale_paths)}장 (다시 쓴 자리는 남기고 나머지만 지운다)")
    print(f"  기존 템플릿 {len(have)}장은 그대로 둔다 — 상점·드롭이 그 이름을 참조한다")
    print(f"  한글 이름을 붙인 것 {len(renamed)}장: "
          + ", ".join(f"{en}→{ko}" for en, ko in renamed[:6]))

    if write:
        for f in sorted(stale_paths):
            f.unlink()
        print(f"\n→ {OUT.relative_to(FORK)}  (얹음 {added}장)")
    else:
        print("\n(예행 연습이다. 쓰려면 --write)")


if __name__ == "__main__":
    main()
