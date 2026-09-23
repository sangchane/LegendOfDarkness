#!/usr/bin/env python3
"""PROTOCOL.md 2단계(블라인드화)와 4단계(judge 프롬프트) 준비.

용도: python eval/blind_prep.py runs/run-YYYYMMDD-smoke S1 S4 S8
      python eval/blind_prep.py --lite runs/... S4   → 경량 판정: 파일 구분자가 있는 문서는 03·05·08만 남긴다
입력: <run>/s<N>-a.md (베이스라인), <run>/s<N>-b.md (스킬)
출력: <run>/s<N>-doc1.md, s<N>-doc2.md (흔적 제거 + 무작위 배정)
      <run>/s<N>-judge-1.md (원순서), s<N>-judge-2.md (스왑) — judge에게 그대로 주는 프롬프트
      <run>/mapping.md — 판정 끝날 때까지 열지 않는다
"""
import random
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
HERE = Path(__file__).resolve().parent
SEEDS = {  # eval/seeds.md 동결 시드
    "S1": "사내 회의실 예약을 웹에서 하고 싶다. 지금은 화이트보드에 수기로 적는다.",
    "S4": "라즈베리파이로 IP 카메라를 제어하고 실시간 모니터링하는 서비스를 만들고 싶다.",
    "S8": "요양시설 어르신 낙상을 감지해 보호자와 직원에게 알리는 서비스를 만들고 싶다.",
}
# 스킬 흔적: 파일명 헤더·단계 표기·스킬/도구 이름. 내용 문장은 건드리지 않는다.
TRACES = [
    (r"^<!--.*?-->\s*$", ""),  # 병합 시 넣은 파일명 구분자
    (r"^#+\s*\d\d-[a-z-]+\.md.*$", ""),
    (r"^#+\s*(A[0-7]|GATE)\b[^\n]*", "## "),
    (r"(?<![A-Za-z0-9-])(service-autopilot|service-prompt-workflow|skill-routing(\.md)?|stage-templates(\.md)?|blindspot-checklists(\.md)?|decision-log(\.md)?|autopilot)(?![A-Za-z0-9-])", "(문서)"),
    (r"\b(ecc|ponytail):[a-z0-9-]+", "(참고자료)"),
    (r"autopilot/[A-Za-z0-9_-]+/", ""),
    (r"Skill 도구", "참고자료"),
    (r"\b\d\d-[a-z-]+\.md", "(문서)"),            # 본문 안 파일명 인용 (뒤에 한글 조사가 붙어도)
    (r"[A-Za-z]:/Users/[^\s)\]>,]*", "(경로)"),     # 핸드오프 프롬프트의 절대 경로
    (r"skill-routing|스킬 사용 기록", "참고자료 사용 기록"),
    (r"스킬", "참고자료"),
    (r"\bGATE\b", "최종 검토"),
    (r"fresh-reviewer", "독립 검토관"),
    (r"^.*강도\s*[:：]\s*\**(lite|full).*$", ""),      # v1.3 강도 표기 (굵게 표시 포함)
    (r"\s*·?\s*`?기준 03 v[\d.]*`?", ""),               # v1.3 버전 참조 (숫자 없는 언급 포함)
    (r"check_package(\.py)?", "(검증 스크립트)"),
    (r"^(버전: v[\d.]+).*$", r"\1"),                   # 버전 줄에 딸려온 템플릿 지시문
    (r"\(?(SKILL\.md )?규칙 \d+\)?", ""),
    (r"\bA[0-7](?=[^0-9A-Za-z])", "단계"),          # 단계 표기 A0~A7
]


LITE_KEEP = ("03-prd", "05-api-contract", "08-readiness-report")


def lite_extract(text: str) -> str:
    """assemble.py 구분자(<!-- ===== name.md ===== -->)가 있으면 03·05·08 부분만, 없으면 전문."""
    parts = re.split(r"(?m)^<!-- ===== ([0-9a-z-]+)\.md ===== -->\s*$", text)
    if len(parts) < 3:
        return text
    keep = [parts[i + 1] for i in range(1, len(parts) - 1, 2) if parts[i] in LITE_KEEP]
    return "\n".join(keep) if keep else text


def blind(text: str) -> str:
    for pat, rep in TRACES:
        text = re.sub(pat, rep, text, flags=re.M)
    return text


def main(run_dir: Path, seeds, lite: bool = False):
    judge_tmpl = (HERE / "judge-prompt.md").read_text(encoding="utf-8")
    judge_tmpl = judge_tmpl.split("```", 1)[1].rsplit("```", 1)[0].strip()  # 코드블록 안 프롬프트만
    mapping_path = run_dir / "mapping.md"
    mapping = [] if mapping_path.exists() else ["# Blind Mapping (판정 종료 전 열지 말 것)"]
    for s in seeds:
        n = s.lower()
        a = (run_dir / f"{n}-a.md").read_text(encoding="utf-8")
        b = (run_dir / f"{n}-b.md").read_text(encoding="utf-8")
        if lite:
            a, b = lite_extract(a), lite_extract(b)
        a, b = blind(a), blind(b)
        first_is_a = random.random() < 0.5
        doc1, doc2 = (a, b) if first_is_a else (b, a)
        mapping.append(f"- {s}: doc1 = {'a' if first_is_a else 'b'}, doc2 = {'b' if first_is_a else 'a'}")
        (run_dir / f"{n}-doc1.md").write_text(doc1, encoding="utf-8")
        (run_dir / f"{n}-doc2.md").write_text(doc2, encoding="utf-8")
        for k, (d1, d2) in enumerate(((doc1, doc2), (doc2, doc1)), start=1):
            prompt = (judge_tmpl.replace("{{시드 한 줄 브리프}}", SEEDS[s])
                      .replace("{{문서 1 전문}}", d1).replace("{{문서 2 전문}}", d2))
            (run_dir / f"{n}-judge-{k}.md").write_text(prompt, encoding="utf-8")
        print(f"{s}: a={len(a):,}자 b={len(b):,}자 → doc1/doc2 배정 완료, judge 프롬프트 2개")
    with mapping_path.open("a", encoding="utf-8") as f:
        f.write("\n".join(mapping) + "\n")
    print("mapping.md 저장 (판정 끝나기 전에 열지 않는다)")


if __name__ == "__main__":
    args = [x for x in sys.argv[1:] if x != "--lite"]
    lite = "--lite" in sys.argv
    main(HERE / args[0] if not Path(args[0]).is_absolute() else Path(args[0]), args[1:] or list(SEEDS), lite)
