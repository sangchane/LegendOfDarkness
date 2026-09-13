#!/usr/bin/env python3
"""원작 아이템의 베이스를 만든다 — `ItemInfo0~11` **열두 개 전부**.

`scripts/build-game-data.ps1` 은 `ItemInfo8~11` **넷만** 읽어 2,110개를 냈다. 그런데
메타파일은 세 곳에 흩어져 있고 합쳐서 열여섯 장이다:

  database/assets/MetaFiles/      ItemInfo0~3
  database/server/metafile/       ItemInfo8~11   (위 넷과 같은 내용, 번호만 다르다)
  database/server/metafile/more/  ItemInfo0~7    ← 통째로 빠져 있었다

서로 다른 이름으로 세면 **6,199개**다. 2,110 은 그 셋 중 한 묶음만 본 수였고, 그 수를
근거로 "하데스에 아이템이 없다" 고 판단해 팩 989개를 베이스로 삼은 일이 있었다.

이 맥에서 PowerShell 이 돌지 않아 파이썬으로 옮긴다. 아이템에 대해서는 이 스크립트가
`build-game-data.ps1` 의 해당 절을 대신한다.

**꾸미지 않는다.** 설명 문자열에서 읽히는 것만 칸으로 만들고, 안 읽히면 `raw` 로 남긴다.

  쓰는 법: python3 scripts/build-item-base.py [--write]
"""
import collections, glob, io, json, os, re, struct, sys, zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FORK = ROOT / "sources/wren11/Dark-Ages-Private-Server"
FOLDERS = [FORK / "database/assets/MetaFiles",
           FORK / "database/server/metafile",
           FORK / "database/server/metafile/more"]
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


def main():
    write = "--write" in sys.argv
    seen, where, read = {}, {}, []

    for folder in FOLDERS:
        for f in sorted(glob.glob(str(folder / "ItemInfo*"))):
            if not os.path.isfile(f):
                continue
            rows = entries(f)
            read.append((str(Path(f).relative_to(FORK)), len(rows)))
            for name, values in rows:
                # 먼저 본 것을 남긴다. 같은 이름이 여러 장에 있고 내용이 같다.
                if name not in seen:
                    seen[name] = values
                    where[name] = Path(f).name

    items, parsed, armoured = [], 0, [0]
    kinds = collections.Counter()
    for name, values in seen.items():
        row = {"name": name, "book": where[name], "raw": values}

        if len(values) > 3:
            row["kind"] = values[3]
            kinds[values[3]] += 1

        describes = values[4] if len(values) > 4 else ""
        row["describes"] = describes

        if (m := DESCRIBES.match(describes)) is not None:
            if m.group("level") is not None:
                row["level"] = int(m.group("level"))
            if m.group("ab") is not None:
                # 2차 직업 능력 요구다. 캐릭터 레벨이 아니라 어빌리티 레벨이므로 칸을 달리 둔다.
                row["abilityLevel"] = int(m.group("ab"))
                row["level"] = 99
            row["weight"] = int(m.group("weight"))
            row["who"] = re.sub(r"\s+", " ", m.group("who")).strip() or "All"
            if m.group("ac") is not None:
                row["ac"] = int(m.group("ac").replace(" ", ""))
                armoured[0] += 1
            parsed += 1
        else:
            # 읽히지 않는 꼴이다. 지어내지 않는다 — raw 로 남기고 표시한다.
            row["unread"] = True

        items.append(row)

    print(f"읽은 메타파일 {len(read)}장:")
    for f, n in read:
        print(f"  {f:50} {n:>5}줄")
    print(f"\n서로 다른 아이템 **{len(items)}개**")
    print(f"  설명에서 직업·요구레벨·무게를 읽은 것 {parsed}개 · 못 읽은 것 {len(items) - parsed}개")
    print(f"  그중 **방어력(AC)까지 적혀 있는 것 {armoured[0]}개**")
    print(f"  종류 {len(kinds)}가지. 상위: " + " · ".join(f"{k} {n}" for k, n in kinds.most_common(8)))
    print("\n못 읽은 설명 표본:")
    for row in [r for r in items if r.get("unread")][:6]:
        print(f"  {row['name'][:28]:28} {row['raw']}")

    if write:
        OUT.write_text(json.dumps(items, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"\n→ {OUT.relative_to(ROOT)}  ({len(items)}개)")
    else:
        print(f"\n(예행 연습이다. 쓰려면 --write)")


if __name__ == "__main__":
    main()
