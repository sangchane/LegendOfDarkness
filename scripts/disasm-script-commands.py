#!/usr/bin/env python3
"""명령표의 함수 401개를 기계적으로 뜯어 증거를 붙인다.

명령이 무엇을 하는지는 기계어를 읽어야 알 수 있다. 401개를 손으로 읽는 것은
현실적이지 않다. 대신 **사람 손을 타지 않는 사실만** 전부에 붙인다:

  참조하는 문자열   오류 메시지가 뜻을 가장 크게 알려 준다 (한글이면 CP949 다)
  부르는 함수       같은 helper 를 부르는 것끼리 무리가 진다 (item_* · guild_* …)
  건드리는 오프셋   구조체 어디를 읽고 어디에 쓰나. 쓰는 자리가 그 명령이 바꾸는 것이다
  크기·인자 읽기    st->start+2+i 자리에서 값을 꺼낸 횟수

**뜻은 적지 않는다.** 여기서 나오는 것은 "이 함수가 0x10150 에 더한다" 까지다.
0x10150 이 돈이라는 것은 코드가 말해 주지 않는다 — 그건 다음 사람이 판단할 몫이다.

  쓰는 법: scratchpad/venv/bin/python scripts/disasm-script-commands.py <서버.exe> <팩이름>
           (capstone · pefile 필요)
"""
import json, re, sys
from collections import Counter, defaultdict
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_32, CS_OP_IMM, CS_OP_MEM

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "data" / "server-packs" / "extracted"

IDENT = re.compile(rb'^[a-z_][a-z0-9_]{1,30}$')
SIG = re.compile(rb'^[isv*?]{0,14}$')
PRINTABLE = re.compile(r'^[\x20-\x7e가-힣㄰-㆏]{3,}$')


class Image:
    def __init__(self, path):
        self.pe = pefile.PE(str(path), fast_load=True)
        self.base = self.pe.OPTIONAL_HEADER.ImageBase
        self.secs = []
        for s in self.pe.sections:
            name = s.Name.rstrip(b"\0").decode("ascii", "replace")
            self.secs.append((name, self.base + s.VirtualAddress,
                              max(s.Misc_VirtualSize, s.SizeOfRawData), s.get_data()))
        t = next(s for s in self.secs if s[0] == ".text")
        self.text_lo, self.text_hi = t[1], t[1] + t[2]
        self.text = t[3]

    def read(self, va, n):
        for _, lo, size, data in self.secs:
            if lo <= va < lo + size:
                off = va - lo
                return data[off:off + n]
        return b""

    def cstr(self, va, limit=96):
        raw = self.read(va, limit)
        if not raw:
            return None
        end = raw.find(b"\0")
        if end <= 0:
            return None
        b = raw[:end]
        for enc in ("ascii", "cp949"):
            try:
                t = b.decode(enc)
            except UnicodeDecodeError:
                continue
            if PRINTABLE.match(t):
                return t
        return None


def find_table(img):
    """{ 함수, 이름, 서명 } 12바이트 한 칸. extract-script-commands.py 와 같은 방법."""
    strict = []
    for _, lo, size, data in img.secs:
        for off in range(0, len(data) - 12, 4):
            p1 = int.from_bytes(data[off:off + 4], "little")
            if not (img.text_lo <= p1 < img.text_hi):
                continue
            p2 = int.from_bytes(data[off + 4:off + 8], "little")
            p3 = int.from_bytes(data[off + 8:off + 12], "little")
            n, s = img.read(p2, 40), img.read(p3, 20)
            if not n or not s:
                continue
            n = n.split(b"\0")[0]
            s = s.split(b"\0")[0]
            if IDENT.match(n) and SIG.match(s):
                strict.append(lo + off)
    runs, cur = [], [strict[0]]
    for o in strict[1:]:
        (cur if o - cur[-1] <= 48 else runs.append(cur) or cur).append(o) if False else None
        if o - cur[-1] <= 48:
            cur.append(o)
        else:
            runs.append(cur); cur = [o]
    runs.append(cur)

    def entry(va):
        d = img.read(va, 12)
        if len(d) < 12:
            return None
        p1 = int.from_bytes(d[0:4], "little")
        if not (img.text_lo <= p1 < img.text_hi):
            return None
        nm = img.read(int.from_bytes(d[4:8], "little"), 40).split(b"\0")[0]
        return (p1, nm.decode()) if IDENT.match(nm) else None

    table = {}
    for run in runs:
        if len(run) < 5:
            continue
        a, b = run[0], run[-1]
        while entry(a - 12):
            a -= 12
        while entry(b + 12):
            b += 12
        for va in range(a, b + 12, 12):
            e = entry(va)
            if e:
                table.setdefault(e[1], e[0])
    return table


def walk(img, md, entry_va, limit=6000):
    """함수 한 덩어리를 따라간다. 갈래는 양쪽 다 밟고, ret 에서 멈춘다."""
    seen, todo, insns = set(), [entry_va], []
    while todo:
        va = todo.pop()
        if va in seen or not (entry_va - 0x40 <= va < entry_va + limit):
            continue
        chunk = img.read(va, 400)
        if not chunk:
            continue
        for ins in md.disasm(chunk, va):
            if ins.address in seen:
                break
            seen.add(ins.address)
            insns.append(ins)
            m = ins.mnemonic
            if m.startswith("j"):
                for op in ins.operands:
                    if op.type == CS_OP_IMM:
                        todo.append(op.imm)
                if m == "jmp":
                    break
            elif m in ("ret", "retf", "iret"):
                break
            elif m == "int3":
                break
            if len(insns) > 3000:
                break
        if len(insns) > 3000:
            break
    return sorted(insns, key=lambda i: i.address)


WRITE_MN = {"mov", "add", "sub", "or", "and", "xor", "inc", "dec", "lea"}


def evidence(img, md, name, va):
    insns = walk(img, md, va)
    calls, strings, reads, writes = [], [], Counter(), Counter()
    for ins in insns:
        if ins.mnemonic == "call":
            for op in ins.operands:
                if op.type == CS_OP_IMM and img.text_lo <= op.imm < img.text_hi:
                    calls.append(op.imm)
        for i, op in enumerate(ins.operands):
            if op.type == CS_OP_IMM:
                t = img.cstr(op.imm)
                if t:
                    strings.append(t)
            elif op.type == CS_OP_MEM:
                d = op.mem.disp
                if op.mem.base != 0 and 0 < d < 0x200000:
                    (writes if (i == 0 and ins.mnemonic in WRITE_MN) else reads)[d] += 1
                elif op.mem.base == 0 and op.mem.index == 0:
                    t = img.cstr(d)
                    if t:
                        strings.append(t)
    return {
        "이름": name,
        "함수주소": f"0x{va:08X}",
        "명령어수": len(insns),
        "바이트": (max(i.address + i.size for i in insns) - va) if insns else 0,
        "부르는함수": [f"0x{c:08X}" for c in sorted(set(calls))],
        "부른횟수": len(calls),
        "참조문자열": sorted(set(strings))[:20],
        "쓰는오프셋": [f"0x{o:X}" for o, _ in writes.most_common(12)],
        "읽는오프셋": [f"0x{o:X}" for o, _ in reads.most_common(12)],
    }


def main():
    if len(sys.argv) != 3:
        sys.exit("쓰는 법: disasm-script-commands.py <서버.exe> <팩이름>")
    exe, pack = sys.argv[1], sys.argv[2]
    img = Image(exe)
    md = Cs(CS_ARCH_X86, CS_MODE_32)
    md.detail = True
    table = find_table(img)
    rows = [evidence(img, md, n, va) for n, va in sorted(table.items())]

    # 같은 helper 를 부르는 것끼리 묶는다 — 무리는 자료가 정한다.
    # 다만 거의 모두가 부르는 것은 프롤로그·인자 꺼내기라 아무것도 가르지 못한다.
    # 그런 것은 "공통" 으로 빼고, 갈래를 가르는 helper 만 무리로 삼는다.
    n = len(rows)
    by_callee = defaultdict(list)
    for r in rows:
        for c in r["부르는함수"]:
            by_callee[c].append(r["이름"])
    common = {c: len(v) for c, v in by_callee.items() if len(v) > n * 0.4}
    groups = [{"helper": c, "명령수": len(v), "명령": sorted(v)}
              for c, v in sorted(by_callee.items(), key=lambda kv: -len(kv[1]))
              if 3 <= len(v) <= n * 0.4]

    # 같은 문자열을 참조하는 것끼리도 묶인다 — 오류 메시지가 같으면 하는 일이 닮았다
    by_string = defaultdict(list)
    for r in rows:
        for t in r["참조문자열"]:
            by_string[t].append(r["이름"])
    string_groups = [{"문자열": t, "명령수": len(v), "명령": sorted(v)}
                     for t, v in sorted(by_string.items(), key=lambda kv: -len(kv[1]))
                     if len(v) >= 2]

    for r in rows:                      # 명령마다 공통 helper 는 빼고 남긴다
        r["가르는helper"] = [c for c in r["부르는함수"] if c not in common]

    out = {
        "출처실행파일": Path(exe).name,
        "명령수": len(rows),
        "주의": "여기 있는 것은 기계가 읽은 사실뿐이다 — 이 함수가 어느 주소를 부르고, 어떤 "
                "문자열을 참조하고, 구조체 어느 오프셋에 쓰는가. 그 오프셋이 무엇인지, 그 명령이 "
                "무엇을 하는지는 코드가 말해 주지 않는다. 짐작을 적지 않았다.",
        "거의모두가부르는함수": [{"helper": c, "명령수": v} for c, v in
                            sorted(common.items(), key=lambda kv: -kv[1])],
        "명령": rows,
        "같은helper를부르는무리": groups[:80],
        "같은문자열을쓰는무리": string_groups[:60],
    }
    d = OUT / pack
    d.mkdir(parents=True, exist_ok=True)
    (d / "script-command-evidence.json").write_text(
        json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")

    withstr = sum(1 for r in rows if r["참조문자열"])
    withwrite = sum(1 for r in rows if r["쓰는오프셋"])
    print(f"══ {pack} ← {Path(exe).name}")
    print(f"   명령 {len(rows)}개 뜯음")
    print(f"   문자열을 참조하는 것 {withstr} · 구조체에 쓰는 것 {withwrite}")
    print(f"   갈래를 가르는 helper {len(groups)}개 (거의 모두가 부르는 것 {len(common)}개는 뺐다)")
    print(f"   같은 문자열을 쓰는 무리 {len(string_groups)}개")
    alone = sum(1 for r in rows if not r["가르는helper"] and not r["참조문자열"])
    print(f"   증거가 하나도 안 붙은 명령 {alone}개")
    print(f"   → extracted/{pack}/script-command-evidence.json")


if __name__ == "__main__":
    main()
