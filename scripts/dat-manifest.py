#!/usr/bin/env python3
"""어둠의전설 클라이언트 `.dat` 아카이브의 목록(이름·크기·md5)을 JSON 으로 적고, 원하면 일부를 꺼낸다.

윈도우와 맥에서 똑같이 돈다(파이썬 표준 라이브러리만). 노바 클라이언트와 우리 클라이언트의 이펙트 그림이 같은지
가리려고 만들었다(docs/nova-client-work-order.md, 2026-09-26).

형식: uint32 개수 N, 그 뒤 N 칸 × (uint32 시작 위치, 13바이트 이름). 마지막 칸은 끝 위치만 쓰는 빈 이름이라
실제 항목은 N-1 개이고, 항목 i 의 크기는 (다음 칸 시작 - 이 칸 시작)이다.

    python3 scripts/dat-manifest.py roh.dat --out roh-manifest.json
    python3 scripts/dat-manifest.py roh.dat --out roh-manifest.json --extract "efct*" "effect*.tbl" "eff*.pal" --to 꺼낸폴더
    python3 scripts/dat-manifest.py --compare a.json b.json       # 이름별로 같음/다름/한쪽에만
"""

import argparse
import fnmatch
import hashlib
import json
import struct
import sys
from pathlib import Path


def entries(path):
    data = Path(path).read_bytes()
    (count,) = struct.unpack_from("<I", data, 0)
    table = [struct.unpack_from("<I13s", data, 4 + i * 17) for i in range(count)]
    for (start, raw), (end, _) in zip(table, table[1:]):
        name = raw.split(b"\0", 1)[0].decode("ascii", errors="replace")
        yield name, data[start:end]


def manifest(path):
    return {name: {"크기": len(body), "md5": hashlib.md5(body).hexdigest()} for name, body in entries(path)}


def compare(a_path, b_path):
    a, b = (json.loads(Path(p).read_text(encoding="utf-8"))["항목"] for p in (a_path, b_path))
    same = [n for n in a if n in b and a[n]["md5"] == b[n]["md5"]]
    diff = [n for n in a if n in b and a[n]["md5"] != b[n]["md5"]]
    only_a, only_b = [n for n in a if n not in b], [n for n in b if n not in a]
    print(f"같음 {len(same)} · 다름 {len(diff)} · {a_path} 에만 {len(only_a)} · {b_path} 에만 {len(only_b)}")
    for label, names in (("다름", diff), ("앞에만", only_a), ("뒤에만", only_b)):
        if names:
            print(f"{label}: " + " ".join(sorted(names)[:200]))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("dat", nargs="?")
    parser.add_argument("--out")
    parser.add_argument("--extract", nargs="*", default=[])
    parser.add_argument("--to")
    parser.add_argument("--compare", nargs=2)
    args = parser.parse_args()

    if args.compare:
        compare(*args.compare)
        return
    if not args.dat:
        parser.error("아카이브(.dat) 경로가 필요합니다")

    items = manifest(args.dat)
    body = {"아카이브": Path(args.dat).name, "크기": Path(args.dat).stat().st_size,
            "md5": hashlib.md5(Path(args.dat).read_bytes()).hexdigest(), "항목": items}
    text = json.dumps(body, ensure_ascii=False, indent=1)
    if args.out:
        Path(args.out).write_text(text, encoding="utf-8")
    print(f"{args.dat}: 항목 {len(items)}개")

    if args.extract:
        if not args.to:
            sys.exit("--extract 에는 --to 폴더가 필요합니다")
        out = Path(args.to)
        out.mkdir(parents=True, exist_ok=True)
        taken = 0
        for name, data in entries(args.dat):
            if any(fnmatch.fnmatch(name.lower(), pattern.lower()) for pattern in args.extract):
                (out / name).write_bytes(data)
                taken += 1
        print(f"꺼냄 {taken}개 → {out}")


if __name__ == "__main__":
    main()
