#!/usr/bin/env python3
"""아이템 자료를 만든다 — `ItemInfo` 가 있는 **모든 폴더**를 읽는다.

`ItemInfo` 라는 이름의 메타파일이 여섯 곳에 있고 **내용이 두 갈래다.** 하나는 2,110개이고
설명이 `All Lev45, Wt 20` 꼴이다. 다른 하나(`server/metafile/more`)는 5,188개이고 설명에
**방어력과 능력치**가 들어 있다 — `Both All Lev45 (AC -10), Wt 20`.

**둘 다 쓴다.** 전에 `more` 를 "생성물" 로 보고 버렸는데 근거가 약했다:
`MetafileManager.GenerateItemInfoMeta` 가 `BatchesOf(712)` 로 만든다는 것을 근거로 삼았으나
`more/ItemInfo1` 이 **714줄**이라 그 셈으로는 만들 수 없다. 그리고 어느 폴더가 원본 게임에서
온 것인지 가릴 근거가 없다 — `assets/MetaFiles` 를 읽는 코드조차 없다(코드가 읽는 곳은
`ServerContext.StoragePath/metafile` 하나이고, 거기서도 `ItemInfo` 는 건너뛴다).

그래서 **버리지 않고 출처를 적는다.** 이름이 여러 곳에 있고 값이 다르면 `differs` 로
표시하고 출처별 원시 값을 다 남긴다. 칸은 더 많이 읽히는 쪽을 채택한다(방어력이 있는 쪽).

  쓰는 법: python3 scripts/build-item-base.py [--write]
"""
import collections, glob, io, json, os, re, struct, sys, zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FORK = ROOT / "sources/wren11/Dark-Ages-Private-Server"
# `ItemInfo` 가 있는 모든 곳. 버리지 않고 출처를 적는다.
FOLDERS = [FORK / "database/assets/MetaFiles",
           FORK / "database/assets/MetaFiles/backup",
           FORK / "database/assets/MetaFiles/backup/old",
           FORK / "database/server/metafile",
           FORK / "database/server/metafile/more",
           FORK / "game/metafile"]
OUT = ROOT / "data/game-data/items.json"

# 설명 문자열이 **원작 수치를 들고 있다.** 꼴이 몇 가지다:
#   "All Lev5, Wt 3"
#   "Female Rogue Lev99 (AC -53), Wt 7"     ← 방어력이 여기 있다
#   "Male Monk   Lev 7 (AC -13), Wt 4"      ← 공백이 여러 개
#   "Monk, Lev33, Wt 4"
# 그래서 방어력을 팩에서 가져올 필요가 없다. 규칙 2번(원작 아카이브)으로 끝난다.
#   "All, Lev51, Weight 6"                  ← Weight 를 다 적은 것
#   "Male Summoner AB 20 (AC -54), Wt 7"    ← 2차 직업은 레벨이 아니라 어빌리티(AB)다
#   "Female 이벤트용의상 Lev11 (AC - 2), Wt 4"  ← 한글이 섞이고 AC 뒤에 공백
DESCRIBES = re.compile(
    r"^(?P<who>.*?),?\s*(?:Lev\s*(?P<level>\d+)|AB\s*(?P<ab>\d+))\s*"
    r"(?:\(\s*AC\s*(?P<ac>-?\s*\d+)\s*\))?\s*,\s*W(?:t|eight)\s*(?P<weight>\d+)")


def entries(path):
    """메타파일 한 장. zlib 을 풀면 `줄수`, 그 뒤로 `이름`과 값 목록이 이어진다."""
    body = io.BytesIO(zlib.decompress(Path(path).read_bytes()))
    count = struct.unpack(">H", body.read(2))[0]
    out = []
    for _ in range(count):
        name = body.read(body.read(1)[0]).decode("cp949", "replace")
        values = [body.read(struct.unpack(">H", body.read(2))[0]).decode("cp949", "replace")
                  for _ in range(struct.unpack(">H", body.read(2))[0])]
        out.append((name, values))
    return out


def read(values):
    """한 줄에서 읽어낼 수 있는 칸. 못 읽으면 None."""
    describes = values[4] if len(values) > 4 else ""
    m = DESCRIBES.match(describes)

    if m is None:
        return None

    got = {"describes": describes,
           "weight": int(m.group("weight")),
           "who": re.sub(r"\s+", " ", m.group("who")).strip() or "All"}

    if m.group("level") is not None:
        got["level"] = int(m.group("level"))
    if m.group("ab") is not None:
        # 2차 직업 능력 요구다. 캐릭터 레벨이 아니라 어빌리티 레벨이므로 칸을 달리 둔다.
        got["abilityLevel"] = int(m.group("ab"))
        got["level"] = 99
    if m.group("ac") is not None:
        got["ac"] = int(m.group("ac").replace(" ", ""))

    return got


def main():
    write = "--write" in sys.argv
    sources = collections.defaultdict(dict)   # 이름 → {출처: 원시값}
    order, counted = [], []

    for folder in FOLDERS:
        for f in sorted(glob.glob(str(folder / "ItemInfo*"))):
            if not os.path.isfile(f):
                continue
            rows = entries(f)
            where = str(Path(f).relative_to(FORK))
            counted.append((where, len(rows)))
            for name, values in rows:
                if name not in sources:
                    order.append(name)
                sources[name][where] = values

    items, with_ac, differ, unread = [], 0, 0, 0
    kinds = collections.Counter()

    for name in order:
        seen = sources[name]
        row = {"name": name, "sources": seen}

        # 출처끼리 값이 다른가.
        distinct = {tuple(v) for v in seen.values()}
        if len(distinct) > 1:
            row["differs"] = True
            differ += 1

        # 더 많이 읽히는 쪽을 채택한다 — 방어력이 있는 읽기가 이긴다.
        best, best_values = None, None
        for values in seen.values():
            got = read(values)
            if got is None:
                continue
            if best is None or len(got) > len(best):
                best, best_values = got, values

        if best is None:
            row["unread"] = True
            unread += 1
            row["raw"] = next(iter(seen.values()))
        else:
            row.update(best)
            row["kind"] = best_values[3] if len(best_values) > 3 else None
            kinds[row["kind"]] += 1
            if "ac" in best:
                with_ac += 1

        items.append(row)

    print(f"읽은 메타파일 {len(counted)}장:")
    for f, n in counted:
        print(f"  {f:52} {n:>5}줄")
    print(f"\n서로 다른 아이템 **{len(items)}개**")
    print(f"  칸을 읽은 것 {len(items) - unread}개 · 못 읽은 것 {unread}개")
    print(f"  **방어력(AC)이 있는 것 {with_ac}개**")
    print(f"  출처끼리 값이 다른 것 {differ}개")
    print(f"  종류 {len(kinds)}가지. 상위: " + " · ".join(f"{k} {n}" for k, n in kinds.most_common(6)))

    if write:
        OUT.write_text(json.dumps(items, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"\n→ {OUT.relative_to(ROOT)}  ({len(items)}개)")
    else:
        print("\n(예행 연습이다. 쓰려면 --write)")


if __name__ == "__main__":
    main()
