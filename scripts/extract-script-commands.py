#!/usr/bin/env python3
"""서버 실행파일에서 스크립트 명령표를 꺼낸다.

db/script/ 의 퀘스트는 자체 언어로 쓰여 있다. `item_add "…",1` 이 무엇을 하는지,
인자를 몇 개 받는지는 db 어디에도 없다 — 그 뜻은 서버가 쥐고 있다.
sources/ 의 저장소 16개에는 없다(그쪽은 Dark Ages 계열로 혈통이 다르다).
소스도 없다. 팩에 든 것은 PE 실행파일뿐이다.

그런데 혼든의 Trap_db.txt 주석이 길을 알려 준다:

    //{ buildin_trap_set, "trap_set", "siisiii" }, // 특정위치에 함정을 설치한다

이건 eAthena 계열 스크립트 엔진의 함수 등록표 생김새다. 표는 컴파일된 뒤에도
.rdata 에 { 함수포인터, 이름, 인자서명 } 3연으로 남는다. 그래서 PE 를 열어
.text 를 가리키는 포인터 + 식별자 꼴 이름 + 서명 꼴 문자열이 나란한 자리를 찾으면
명령표 전체가 그대로 나온다. 서명은 i=정수, s=문자열 이다.

**해석은 붙이지 않는다.** 이름과 인자 개수는 바이너리에 적힌 사실이고, 그 명령이
무엇을 하는지는 여기서 확인할 수 없다. 짐작해 적으면 다음 사람이 근거로 삼는다.

  쓰는 법: python3 scripts/extract-script-commands.py <서버.exe> <팩이름>
"""
import hashlib, json, re, struct, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "data" / "server-packs" / "extracted"

IDENT = re.compile(rb'^[a-z_][a-z0-9_]{1,30}$')
SIG = re.compile(rb'^[isv*?]{0,14}$')


def pe_sections(data):
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        sys.exit("PE 파일이 아니다")
    nsec = struct.unpack_from("<H", data, pe + 6)[0]
    optsz = struct.unpack_from("<H", data, pe + 20)[0]
    base = struct.unpack_from("<I", data, pe + 24 + 28)[0]
    tbl = pe + 24 + optsz
    secs = []
    for i in range(nsec):
        o = tbl + 40 * i
        name = data[o:o + 8].rstrip(b"\0").decode("ascii", "replace")
        vsize, va, rawsz, raw = struct.unpack_from("<IIII", data, o + 8)
        secs.append((name, va, vsize, raw, rawsz))
    return base, secs


def extract(path):
    data = Path(path).read_bytes()
    base, secs = pe_sections(data)

    def va2off(va):
        r = va - base
        for _, sva, vsz, raw, rawsz in secs:
            if sva <= r < sva + max(vsz, rawsz):
                return raw + (r - sva)
        return None

    def cstr(off, limit=64):
        if off is None or not (0 <= off < len(data)):
            return None
        end = data.find(b"\0", off, off + limit)
        return data[off:end] if end != -1 else None

    text = next((s for s in secs if s[0] == ".text"), None)
    if not text:
        sys.exit(".text 구획이 없다")
    lo, hi = base + text[1], base + text[1] + max(text[2], text[4])

    # 1단계 — 확실한 것만 찾아 표가 어디부터 어디까지인지 알아낸다.
    #          { 함수포인터, 이름, 인자서명 } 12바이트 한 칸이다.
    strict = []
    for _, sva, vsz, raw, rawsz in secs:
        n = min(max(vsz, rawsz), len(data) - raw)
        for off in range(raw, raw + n - 12, 4):
            p1, p2, p3 = struct.unpack_from("<III", data, off)
            if not (lo <= p1 < hi):          # 첫 칸은 코드 포인터여야 한다
                continue
            name, sig = cstr(va2off(p2)), cstr(va2off(p3))
            if name is None or sig is None:
                continue
            if IDENT.match(name) and SIG.match(sig):
                strict.append(off)
    if not strict:
        sys.exit("명령표를 못 찾았다")

    # 2단계 — 그 범위를 12바이트씩 걸어간다. 인자가 없는 명령은 서명 포인터가
    #          문자열로 풀리지 않는 팩이 있다(Novaonline). 확실한 것만 주웠다가는
    #          인자 0개 명령이 통째로 빠진다 — 이름은 읽히므로 버리지 않는다.
    runs, cur = [], [strict[0]]
    for o in strict[1:]:
        if o - cur[-1] <= 48:            # 한 칸 12바이트 — 몇 칸 건너뛰어도 같은 표다
            cur.append(o)
        else:
            runs.append(cur)
            cur = [o]
    runs.append(cur)

    def entry_at(off):
        """그 자리가 표의 한 칸인가 — 코드 포인터 + 식별자 이름이면 맞다."""
        if off < 0 or off + 12 > len(data):
            return None
        p1, p2, _ = struct.unpack_from("<III", data, off)
        if not (lo <= p1 < hi):
            return None
        nm = cstr(va2off(p2))
        return nm if nm is not None and IDENT.match(nm) else None

    found = {}
    for run in runs:
        if len(run) < 5:                 # 우연히 맞은 3연은 표가 아니다
            continue
        start, end = run[0], run[-1]
        # 인자가 없는 명령은 서명이 문자열로 안 풀려 확실한 항목에서 빠진다.
        # 그런 것이 연달아 나오면 구간이 끊기므로, 칸이 이어지는 동안 양쪽으로 늘린다.
        while entry_at(start - 12):
            start -= 12
        while entry_at(end + 12):
            end += 12
        for off in range(start, end + 12, 12):
            if off + 12 > len(data):
                break
            p1, p2, p3 = struct.unpack_from("<III", data, off)
            if not (lo <= p1 < hi):
                continue
            name = cstr(va2off(p2))
            if name is None or not IDENT.match(name):
                continue
            sig = cstr(va2off(p3))
            ok = sig is not None and SIG.match(sig)
            found.setdefault(name.decode(), sig.decode() if ok else None)
    return found, hashlib.sha256(data).hexdigest()


NAME = r'[a-z_][a-z0-9_]{1,30}'
CALL_PAREN = re.compile(rf'(?<![@#$\w]){NAME}(?=\s*\()')
CALL_STMT = re.compile(rf'(?:^|[;{{}}])\s*({NAME})\s+(?=["@#\d\-])')


def used_in(pack):
    """이 팩의 스크립트가 실제로 부르는 이름.

    @name·#name 은 변수지 명령이 아니고, 문자열과 주석은 코드가 아니다.
    느슨하게 세면 if(@type1 == 3) 의 type1 까지 명령으로 잡힌다 —
    classify-script-names.py 와 같은 잣대를 쓴다.
    """
    db = ROOT / "data" / "server-packs" / pack / "db"
    seen = set()
    for f in (db / "script").rglob("*.txt"):
        if f.name.endswith("_db.txt"):
            continue
        code = f.read_text(encoding="utf-8", errors="replace")
        code = re.sub(r'"[^"]*"', '""', code)
        code = re.sub(r'//[^\n]*', '', code)
        code = re.sub(r'/\*.*?\*/', '', code, flags=re.S)
        seen |= set(m.group(0) for m in CALL_PAREN.finditer(code))
        seen |= set(CALL_STMT.findall(code))
    return seen


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__.strip().splitlines()[-1])
    exe, pack = sys.argv[1], sys.argv[2]
    cmds, digest = extract(exe)
    used = used_in(pack)
    rows = []
    for name, sig in sorted(cmds.items()):
        rows.append({"이름": name,
                     "인자서명": sig if sig is not None else "(확인불가)",
                     "인자수": len(sig) if sig is not None else None,
                     "이팩이쓰나": name in used})
    out = {
        "출처실행파일": Path(exe).name,
        "sha256": digest,
        "명령수": len(rows),
        "이팩이쓰는것": sum(1 for r in rows if r["이팩이쓰나"]),
        "주의": "이름과 인자 개수는 바이너리에 적힌 사실이다. 각 명령이 무엇을 하는지는 "
                "여기서 확인되지 않았다 — 뜻은 적지 않았다. i=정수, s=문자열.",
        "명령": rows,
    }
    d = OUT_DIR / pack
    d.mkdir(parents=True, exist_ok=True)
    (d / "script-commands.json").write_text(
        json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
    unknown = sorted(used - set(cmds))
    print(f"══ {pack} ← {Path(exe).name}")
    print(f"   엔진이 가진 명령 {len(rows)}개 · 이 팩이 쓰는 것 {out['이팩이쓰는것']}개")
    unresolved = sum(1 for r in rows if r["인자수"] is None)
    print(f"   인자수 분포: " + ", ".join(
        f"{k}개:{sum(1 for r in rows if r['인자수'] == k)}" for k in range(0, 6))
        + (f", 서명 확인불가:{unresolved}" if unresolved else ""))
    print(f"   표에 없는데 스크립트가 부르는 이름 {len(unknown)}개 (제어문·사용자함수 포함)")
    print(f"   → extracted/{pack}/script-commands.json")


if __name__ == "__main__":
    main()
