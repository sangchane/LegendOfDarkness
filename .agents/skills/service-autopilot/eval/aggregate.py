#!/usr/bin/env python3
"""PROTOCOL.md 6단계 집계: s<N>-eval-1.md(원순서)·s<N>-eval-2.md(스왑) → 차원별 A/B/tie 표.

용도: python eval/aggregate.py runs/run-YYYYMMDD-smoke S1 S4 S8
규칙: 스왑 판정은 doc1↔doc2를 뒤집어 원순서 기준으로 환산. 두 순서가 다르면 tie.
      mapping.md로 doc→a(베이스라인)/b(스킬) 역산. 종합은 '종합 판정' 줄로 같은 규칙.
"""
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
HERE = Path(__file__).resolve().parent
DIMS = ["완전성", "비모호성", "검증가능성", "실행가능성", "근거성", "범위 절제"]
FLIP = {"doc1": "doc2", "doc2": "doc1", "tie": "tie"}


def verdicts(text: str) -> dict:
    """[차원명] ... 판정: doc1|doc2|tie 를 차원별로, 마지막 '종합 판정: x'를 '종합'으로."""
    # 절 경계: 모든 제목 줄("## 1. 완전성", "## [1. 완전성]", "[완전성]", "## 종합 판정: x").
    # 차원명이 든 제목부터 다음 제목까지를 그 차원의 절로 보고, 절의 마지막 '판정: x'를 판정으로.
    heads = []
    for m in re.finditer(r"(?m)^(?:#+[^\n]*|\[[^\n]*\])$", text):
        name = next((d for d in DIMS + ["종합"] if d in m.group(0)), None)
        heads.append((m.start(), name))
    out = {d: "?" for d in DIMS + ["종합"]}
    for i, (pos, name) in enumerate(heads):
        end = heads[i + 1][0] if i + 1 < len(heads) else len(text)
        found = re.findall(r"판정\s*[:：]\s*\**\s*(doc1|doc2|tie)", text[pos:end], re.I)
        if found and name in out:
            out[name] = found[-1].lower()
    for d in DIMS:  # 제목 없이 "[완전성] … / 판정: x" 한 줄 형식(2026-07 런)
        if out[d] == "?":
            m = re.search(r"\[" + re.escape(d) + r"\][^\n]*판정\s*[:：]\s*\**\s*(doc1|doc2|tie)", text, re.I)
            out[d] = m.group(1).lower() if m else "?"
    if out["종합"] == "?":  # 제목 없이 '종합 판정: x' 한 줄로 끝나는 형식(2026-07 런)
        m = re.findall(r"종합\s*판정\s*[:：]\s*\**\s*(doc1|doc2|tie)", text, re.I)
        out["종합"] = m[-1].lower() if m else "?"
    return out


def main(run_dir: Path, seeds):
    mapping = {}
    for line in (run_dir / "mapping.md").read_text(encoding="utf-8").splitlines():
        m = re.match(r"- (S\d+): doc1 = (a|b), doc2 = (a|b)", line)
        if m:
            mapping[m.group(1)] = {"doc1": m.group(2), "doc2": m.group(3)}
    rows = []
    for s in seeds:
        n = s.lower()
        v1 = verdicts((run_dir / f"{n}-eval-1.md").read_text(encoding="utf-8"))
        v2 = verdicts((run_dir / f"{n}-eval-2.md").read_text(encoding="utf-8"))
        row = {}
        for k in DIMS + ["종합"]:
            a, b = v1[k], FLIP.get(v2[k], "?")
            doc = a if a == b else "tie"
            row[k] = {"doc1": mapping[s]["doc1"].upper(), "doc2": mapping[s]["doc2"].upper()}.get(doc, doc)
            row[k] += "" if a == b else f"(불일치 {v1[k]}/{v2[k]})"
        rows.append((s, row))
    hdr = "| 시드 | 종합 | " + " | ".join(DIMS) + " |"
    print(hdr)
    print("|" + "---|" * (len(DIMS) + 2))
    for s, row in rows:
        print(f"| {s} | **{row['종합']}** | " + " | ".join(row[d] for d in DIMS) + " |")
    print("\nA = 베이스라인(스킬 없음), B = service-autopilot. 스왑 불일치는 tie로 처리(괄호 안 원판정/스왑판정).")


if __name__ == "__main__":
    p = Path(sys.argv[1])
    main(p if p.is_absolute() else HERE / p, sys.argv[2:] or ["S1", "S4", "S8"])
