#!/usr/bin/env python3
"""스크립트에 나오는 이름을 갈래 짓는다 — 무엇이 엔진 명령이고 무엇이 아닌가.

명령표(script-commands.json)에 없는 이름이 스크립트에 잔뜩 나온다. 그게 다 미지의
명령인 것은 아니다. 대부분은 변수와 라벨이다:

    if(@type1 == 3){        ← @type1 은 지역변수지 명령이 아니다
    go1:                    ← 라벨. goto 가 뛰어갈 자리다
    if(#EestQuest014==1){   ← 영속변수. 퀘스트 단계다

**증거로만 가른다.** `@name` 이 어딘가 있으면 지역변수, `#name` 이면 영속변수,
줄머리에 `name:` 이 있으면 라벨, 명령표에 있으면 엔진 명령이다. 아무 증거도 없으면
"가려내지 못함" 으로 남긴다 — 짐작해서 채우지 않는다.

  쓰는 법: python3 scripts/classify-script-names.py
"""
import json, re
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PACKS = ROOT / "data" / "server-packs"
OUT = PACKS / "extracted"

# 이 언어의 제어문. 명령표에 없지만 엔진이 직접 아는 말이다.
CONTROL = {"if", "else", "for", "while", "switch", "case", "default", "break",
           "continue", "goto", "return", "set", "end", "exit", "function"}

NAME = r'[a-z_][a-z0-9_]{1,30}'
CALL_PAREN = re.compile(rf'(?<![@#$\w]){NAME}(?=\s*\()')          # name(
CALL_STMT = re.compile(rf'(?:^|[;{{}}])\s*({NAME})\s+(?=["@#\d\-])')  # 줄머리 name "인자"
LOCAL = re.compile(rf'@({NAME})')
PERSIST = re.compile(r'#([A-Za-z_]\w*)')
LABEL = re.compile(rf'^\s*({NAME})\s*:\s*$', re.M)


def scan(db):
    called, local, persist, label = set(), set(), set(), set()
    for f in (db / "script").rglob("*.txt"):
        if f.name.endswith("_db.txt"):
            continue
        t = f.read_text(encoding="utf-8", errors="replace")
        # 문자열 안은 코드가 아니다 — 대사에 온갖 말이 들어 있다.
        # 주석도 코드가 아니다 — 막아 둔 줄에 엔진에 없는 이름이 남아 있다
        # (//set @mapxs, get_mapxx()-1; — get_mapxx 는 어느 팩 실행파일에도 없다).
        code = re.sub(r'"[^"]*"', '""', t)
        code = re.sub(r'//[^\n]*', '', code)
        code = re.sub(r'/\*.*?\*/', '', code, flags=re.S)
        called |= set(m.group(0) for m in CALL_PAREN.finditer(code))
        called |= set(CALL_STMT.findall(code))
        local |= set(LOCAL.findall(code))
        persist |= set(PERSIST.findall(code))
        label |= set(LABEL.findall(code))
    return called, local, persist, label


def main():
    for pack in sorted(d.name for d in PACKS.iterdir()
                       if d.is_dir() and (d / "db").exists()):
        cmdf = OUT / pack / "script-commands.json"
        if not cmdf.exists():
            print(f"   ({pack}: 명령표가 없다 — extract-script-commands.py 를 먼저)")
            continue
        known = {c["이름"] for c in json.loads(cmdf.read_text(encoding="utf-8"))["명령"]}
        called, local, persist, label = scan(PACKS / pack / "db")

        buckets = defaultdict(list)
        for n in sorted(called):
            if n in known:
                buckets["엔진명령"].append(n)
            elif n in CONTROL:
                buckets["제어문"].append(n)
            elif n in label:
                buckets["라벨"].append(n)
            elif n in local:
                buckets["지역변수"].append(n)
            elif n in persist:
                buckets["영속변수"].append(n)
            else:
                buckets["가려내지못함"].append(n)

        out = {
            "부르는이름수": len(called),
            "갈래": {k: len(v) for k, v in buckets.items()},
            "주의": "증거로만 갈랐다. @name 이면 지역변수, #name 이면 영속변수, "
                    "줄머리 name: 이면 라벨, 명령표에 있으면 엔진 명령이다. "
                    "'가려내지못함' 은 어느 증거도 없는 것이다 — 짐작해 채우지 않았다.",
            "이름": {k: v for k, v in sorted(buckets.items())},
            "엔진이가졌지만안쓰는명령": sorted(known - called),
        }
        (OUT / pack / "script-names.json").write_text(
            json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"══ {pack}: 부르는 이름 {len(called)}")
        print("   " + "  ".join(f"{k} {len(v)}" for k, v in sorted(buckets.items())))
        if buckets["가려내지못함"]:
            print(f"   가려내지 못함: {', '.join(buckets['가려내지못함'][:14])}")
        print(f"   엔진이 가졌지만 이 팩이 안 쓰는 명령 {len(known - called)}")
    print(f"\n→ extracted/<팩>/script-names.json")


if __name__ == "__main__":
    main()
