# run-20260908-trim — 각본 뺀 스킬(1.4.1) A/B 런북

목적: Anthropic Fable 5.1 이전 체크리스트 "이전 모델용 각본을 뺀 버전과 A/B" 실행.
비교: **a** = 2026-09-07 S4 full 산출물(스킬 1.3.0, 각본·압력 어조 있음) vs **b** = 스킬 1.4.1(1단계 감사 적용)로 새로 생성.
시드: S4 "라즈베리파이로 IP 카메라를 제어하고 실시간 모니터링하는 서비스를 만들고 싶다." (동결 시드, 중간 난도)
판정: Opus judge 2회(원순서+스왑), `fresh-reviewer` 타입. 불일치 차원은 tie.
예산: 생성 약 30% + GATE 약 15% + 판정 2회 약 40% = 5시간 창의 약 85%. 한삼국 세션이 쉬는 시간에 돌린다.

## 변형: 주간 한도 소진 모드 (사용자가 `/limit-reset`을 쓴 경우)

목적이 "주간 리셋(15:05) 전에 이번 주 몫을 최대한 쓰기"이면 S4와 S8 생성을 **동시에** 띄운다
(`GEN-PROMPT.md`, `GEN-PROMPT-S8.md`, 각각 general-purpose · model fable). 두 생성 ≈ 창의 60%.
S4 생성이 끝나는 대로 S4 판정 2회를 이어서 띄우고, S8 판정은 창 여유를 보고(없으면 다음 창에서) 돌린다.
S8 비교 대상 `s8-a.md`는 0907 S8 산출물(1.3.0). 병합은 `python assemble.py S4 S8`, 블라인드화·집계는 `S4 S8` 인자.

## 절차 (메인 세션이 수행)

1. **생성 b** — Agent 도구, `subagent_type: general-purpose`, `model: fable`(0907 b와 같은 생성 모델), 프롬프트는 `GEN-PROMPT.md` 전문.
   산출물은 이 디렉터리의 `s4-rpi-ip-camera/`에 쌓인다. 429로 끊기면 같은 프롬프트에 "재개: 있는 파일은 다시 만들지 말고 없는 첫 파일부터"를 붙여 다시 띄운다.
2. **병합** — `python assemble.py` → `s4-b.md` (00~08 + decision-log를 파일명 구분자와 함께 이어 붙임). `s4-a.md`는 이미 복사돼 있다.
3. **블라인드화** — `python ../../blind_prep.py runs/run-20260908-trim S4` → `s4-doc1.md`, `s4-doc2.md`, `s4-judge-1.md`, `s4-judge-2.md`, `mapping.md`. mapping.md는 판정 끝까지 열지 않는다.
4. **판정** — Agent 도구 2회, `subagent_type: fresh-reviewer`, `model: opus`. 프롬프트:
   "`<이 디렉터리 절대경로>/s4-judge-{k}.md`를 Read로 전부 읽고(길면 offset으로 나눠서) 그 안의 <role>·<rules>·<dimensions>·<output_format>대로 판정하라.
   출력 형식은 차원마다 제목 줄 `## [n. 차원명]` 아래 doc1 근거 인용 / doc2 근거 인용 / `판정: doc1|doc2|tie` 한 줄, 마지막에 `## 종합 판정: doc1|doc2|tie`와 세 문장 총평. 파일은 만들지 말고 텍스트로 돌려줘라."
   돌아온 텍스트를 메인이 `s4-eval-{k}.md`로 저장한다 (머리에 `# 심사 판정 — s4 (doc1 vs doc2)` 한 줄).
5. **집계** — `python ../../aggregate.py runs/run-20260908-trim S4`. 표에서 A = 각본 있는 1.3.0, B = 각본 뺀 1.4.1로 읽는다.
6. **기록** — `RESULT.md`에 표 + 해석(각본 제거가 어느 차원을 잃었는지/얻었는지) + b의 비용(강도·검색·서브에이전트·시간·토큰) + 이탈 기록. `references/evidence.md` "2026-09-08 프롬프트 감사" 절의 "미완" 줄을 결과로 바꾼다. 커밋·푸시.
7. **보고** — 사용자에게 판정 표와 창 사용량(%)을 보고한다.

## 이 런의 알려진 한계 (RESULT에 그대로 적는다)

- a는 1.3.0이라 1.4.0 추가분(상수 근거 4종·NEXT.md·사용 맥락 축)과 1.4.1 감사분이 섞여 비교된다. S4는 게임이 아니라 P7·spike는 무관.
- 이 세션은 settings.json 변경 전에 시작돼 ponytail 매처가 적용되지 않는다 → 판정관에도 ponytail 페르소나가 주입된다(0907과 같은 조건).
- 판정 2회(원순서+스왑)만 — PROTOCOL 표준과 같고, 0907 런과도 같다.
