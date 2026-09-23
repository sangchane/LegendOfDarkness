#!/usr/bin/env python3
"""설계 패키지 자기 선언 검증 — "누락 0", "미결정 0건"을 모델이 아니라 스크립트가 센다.

용도: python scripts/check_package.py autopilot/<slug>
      GATE 전에 실행하고 출력을 검토관 <inputs>에 넣는다. CRITICAL이 있으면 종료코드 1 (GATE 진입 금지).

검사 (2026-09 스모크 런에서 실제로 새어 나간 결함만):
  C1 파일 9개 존재 (00~07 + decision-log; 08은 GATE가 만든다)
  C2 03의 P0·P1 FR 전부가 05 커버리지에 등장 (S4: FR-026 누락인데 "누락 0")
  C3 03의 SC 전부가 06에 등장 (S4: SC-009·013 누락)
  C4 03 "미결정" 절이 비었는지 + 03~07의 미정·TBD·추후 표기 (S8: "미결정 0건"인데 전화 수신자 미정)
  C5 02 register 축 행마다 Clear/Assumed/Asked 마킹, Asked ≤ 5 (규칙 3·4)
  C6 버전 헤더: 03에 `버전: vX`가 있고 04~07이 그 버전을 `기준 03 vX`로 참조 (S4: 03 v1.1이 04~07에 미전파)
  C7 03 상수 표에서 근거가 '미확인'인 상수가 성공 기준(SC) 절에 쓰임 (한삼국: 인물 120명 목표 vs 사료 실측 16명)
  C8 문서 머리의 `개정 노트 R# 적용 대기` 배지 — 상류 개정이 아직 전파되지 않은 문서
"""
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
FILES = ["00-seed", "01-recon", "02-blindspot-register", "03-prd", "04-architecture",
         "05-api-contract", "06-test-design", "07-ops-design", "decision-log"]
UNDECIDED = re.compile(r"(미정|미결정 항목|TBD|TODO|추후 결정|나중에 정|결정 필요|\(선택\))")


def expand_ranges(text: str, prefix: str) -> set:
    """FR-001, FR-016~020, FR-016~FR-020 → 개별 ID 집합"""
    ids = set(re.findall(rf"\b{prefix}-(\d{{2,3}})[a-z]?\b", text))
    for a, b in re.findall(rf"\b{prefix}-(\d{{2,3}})\s*[~～\-–]\s*(?:{prefix}-)?(\d{{2,3}})\b", text):
        ids.update(f"{n:0{len(a)}d}" for n in range(int(a), int(b) + 1))
    for first, rest in re.findall(rf"\b{prefix}-(\d{{2,3}})((?:\s*[·,/]\s*\d{{2,3}}\b)+)", text):  # SC-004·005/006
        ids.update(re.findall(r"\d{2,3}", rest))
    return {f"{prefix}-{i}" for i in ids}


def section(text: str, heading_kw: str) -> str:
    """제목 키워드가 든 절의 본문. 같은 레벨 이하의 다음 제목에서 끝난다 (하위 제목 ###은 절 안에 포함)."""
    m = re.search(rf"(?m)^(#+)[^\n]*{heading_kw}[^\n]*\n", text)
    if not m:
        return ""
    level = len(m.group(1))
    rest = text[m.end():]
    end = re.search(rf"(?m)^#{{1,{level}}} ", rest)
    return rest[:end.start()] if end else rest


def main(d: Path):
    docs = {f: (d / f"{f}.md").read_text(encoding="utf-8") if (d / f"{f}.md").is_file() else None for f in FILES}
    crit, high, info = [], [], []

    missing = [f for f, t in docs.items() if t is None]
    if missing:
        crit.append(f"C1 파일 없음: {', '.join(missing)}")
    prd, api, tests, reg = docs["03-prd"] or "", docs["05-api-contract"] or "", docs["06-test-design"] or "", docs["02-blindspot-register"] or ""

    # C2 FR(P0/P1) → 05 커버리지
    fr_prio = {}
    for m in re.finditer(r"^\|\s*(FR-\d{2,3}[a-z]?)\s*\|[^\n]*?\|\s*(P[0-2])\s*\|", prd, re.M):
        fr_prio[m.group(1)] = m.group(2)
    if not fr_prio:
        high.append("C2 03에서 `| FR-nnn | … | P0 |` 형식의 요구사항 표를 찾지 못함 — 표 형식 확인")
    covered = expand_ranges(api, "FR")
    unmapped = sorted(f for f, p in fr_prio.items() if p in ("P0", "P1") and f not in covered)
    if unmapped:
        crit.append(f"C2 05 커버리지에 없는 P0/P1 요구사항 {len(unmapped)}건: {', '.join(unmapped)}")
    info.append(f"C2 FR 총 {len(fr_prio)} (P0 {sum(p=='P0' for p in fr_prio.values())} · P1 {sum(p=='P1' for p in fr_prio.values())} · P2 {sum(p=='P2' for p in fr_prio.values())}), 05 참조 {len(covered & set(fr_prio))}")

    # C3 SC → 06
    scs = expand_ranges(section(prd, "성공 기준") or prd, "SC")
    sc_in_tests = expand_ranges(tests, "SC")
    sc_missing = sorted(scs - sc_in_tests)
    if sc_missing:
        crit.append(f"C3 06에 시나리오가 없는 성공 기준 {len(sc_missing)}건: {', '.join(sc_missing)}")
    info.append(f"C3 SC 총 {len(scs)}, 06 참조 {len(scs & sc_in_tests)}")

    # C4 미결정
    und = section(prd, "미결정")
    body = [l for l in und.splitlines() if l.strip() and not re.search(r"(0건|없음|none)", l, re.I)]
    items = [l for l in body if re.match(r"^\s*([-*|]|\d+[.)])", l) and not re.search(r"(해소|반영됨|처리됨|→)", l)]
    if items:
        crit.append(f"C4 03 '미결정' 절에 항목 {len(items)}건: " + " / ".join(l.strip()[:60] for l in items[:5]))
    elif body:
        high.append(f"C4 03 '미결정' 절이 '0건/없음'이 아니라 설명문 {len(body)}줄 — 해소됐으면 '0건'으로 정리: " + body[0].strip()[:80])
    for f in ["03-prd", "04-architecture", "05-api-contract", "06-test-design", "07-ops-design"]:
        for i, line in enumerate((docs[f] or "").splitlines(), 1):
            if UNDECIDED.search(line) and not re.search(r"(미결정 —|미결정 절|0건|스캔)", line):
                high.append(f"C4 {f}.md:{i} 미정 표기 — {line.strip()[:90]}")

    # C5 register 마킹
    # 형식 무관: 상태어(Clear/Assumed/Asked/Partial/Missing)가 든 표 행이 축 행이다
    rows = [l for l in reg.splitlines() if l.startswith("|") and re.search(r"\b(Clear|Assumed|Asked|Partial|Missing)\b", l)
            and not re.match(r"^\|\s*-", l)]
    unmarked = [l.strip()[:50] for l in rows if not re.search(r"\b(Clear|Assumed|Asked)\b", l)]
    if unmarked:
        high.append(f"C5 마킹 없는 축 {len(unmarked)}건: " + " / ".join(unmarked[:5]))
    asked = len(re.findall(r"(?m)^#+\s*Q\d\b", section(reg, "질문 배치") or ""))  # 제목만 센다(본문의 Q1·Q2 언급 제외)
    if asked > 5:
        crit.append(f"C5 질문 {asked}문항 — 상한 5")
    info.append(f"C5 축 행 {len(rows)}, 마킹 {len(rows) - len(unmarked)}, 질문 {asked}")

    # C6 버전 전파
    v03 = re.search(r"버전\s*[:：]\s*v?(\d+(?:\.\d+)+)", prd[:600])
    if not v03:
        high.append("C6 03 머리에 `버전: vX.Y` 없음 (개정 전파 규칙)")
    else:
        for f in ["04-architecture", "05-api-contract", "06-test-design", "07-ops-design"]:
            head = (docs[f] or "")[:600]
            if not re.search(rf"03\s*v?{re.escape(v03.group(1))}\b", head):
                high.append(f"C6 {f}.md 머리가 `기준 03 v{v03.group(1)}`을 참조하지 않음 — 상류 개정 미전파 의심")

    # C7 미확인 근거 상수 → SC 사용
    const_sec = section(prd, "상수 표")
    unverified = [m.group(1) for m in re.finditer(r"^\|\s*([A-Z][A-Z0-9_ /]+?)\s*\|[^\n]*\|[^\n]*\|[^\n]*미확인[^\n]*\|\s*$", const_sec, re.M)]
    names = [n.strip() for grp in unverified for n in grp.split("/")]
    sc_sec = section(prd, "성공 기준")
    used = sorted(n for n in names if n and re.search(rf"\b{re.escape(n)}\b", sc_sec))
    if used:
        crit.append(f"C7 근거 '미확인' 상수가 성공 기준에 쓰임 {len(used)}건: {', '.join(used)} — 실측·출처를 붙이거나 SC에서 빼라")
    info.append(f"C7 상수 {len(names)}개 근거 미확인" if names else "C7 근거 미확인 상수 없음")

    # C8 개정 노트 배지
    for f, t in docs.items():
        if t and re.search(r"개정 노트 R\d+[^\n]*적용 대기", t[:800]):
            high.append(f"C8 {f}.md 머리에 개정 노트 적용 대기 배지 — 전파 후 배지를 지워라")

    print(f"# check_package — {d}")
    for label, items in (("CRITICAL", crit), ("HIGH", high), ("INFO", info)):
        print(f"\n## {label} ({len(items)})")
        for it in items:
            print(f"- {it}")
    return 1 if crit else 0


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    sys.exit(main(Path(sys.argv[1])))
