# 평가 프로토콜 — service-autopilot이 "값을 하는지" 측정하는 법

> 목적: "스킬 없이 그냥 시키는 것"(베이스라인 A) 대비 "이 스킬로 시키는 것"(B)이
> 실제로 나은지를 **재현 가능한 절차**로 판정한다. 업계 선두도 기획 파이프라인은
> 이 방식(ablation: 있음 vs 없음)으로 증명한다 — MetaGPT는 역할 제거 시 HumanEval −4.2%p를
> 근거로 썼고, spec-kit은 정량 벤치마크 없이 "실험"임을 자인한다.
> ([MetaGPT arXiv:2308.00352](https://arxiv.org/pdf/2308.00352) · [spec-kit](https://github.com/github/spec-kit))

## 설계 근거 (조사 출처)

- **pairwise + 차원별 진단의 이원 구성**: 선택 결정은 pairwise가 민감, 디버깅은 rubric이 적합
  ([OpenAI eval best practices](https://developers.openai.com/api/docs/guides/evaluation-best-practices))
- **binary 승/무/패** (1~5점 금지): "3점과 4점의 차이를 일관되게 해석할 수 없다"
  ([Hamel Husain — LLM-as-a-Judge](https://hamel.dev/blog/posts/llm-judge/))
- **순서 스왑 후 불일치=tie**: position bias는 접전에서 승률 10~15%p를 흔든다
  ([MT-Bench arXiv:2306.05685](https://arxiv.org/abs/2306.05685))
- **생성 모델과 다른 계열의 judge**: self-preference bias 방어 (MT-Bench)
- **소표본 시작이 정석**: Anthropic 20~50과제, Hamel ~30예시, paired 비교는 분산 감소로 소표본에서도 검정력
  ([Anthropic — statistical approach](https://www.anthropic.com/research/statistical-approach-to-model-evals) ·
  [Anthropic — agent evals](https://www.anthropic.com/engineering/demystifying-evals-for-ai-agents))
- **채점 차원은 ISO/IEC/IEEE 29148 요구사항 품질 특성 + Requirements Smells를 문서 수준으로 번안**
  ([ISO 29148](https://www.iso.org/standard/45171.html) · [Smells arXiv:1611.08847](https://arxiv.org/pdf/1611.08847))

## 채점 6차원 (각각 doc1 승 / doc2 승 / tie)

| # | 차원 | 판정 질문 |
|---|---|---|
| 1 | **완전성** | 필수 설계 영역(사용자·엣지케이스·위협·API/데이터·테스트·운영/장애) 중 빠진 곳이 적은 쪽은? |
| 2 | **비모호성** | "적절히·빠르게·유연하게" 류 smell, 주어 없는 문장, 미결정 사항이 적은 쪽은? |
| 3 | **검증가능성** | 성공 기준·수용 기준이 pass/fail로 측정 가능한 쪽은? |
| 4 | **실행가능성** | 이 문서만 들고 다음 에이전트가 바로 구현에 착수할 수 있는 쪽은? |
| 5 | **근거성** | 가정이 명시되고 기술 선택에 근거(출처·트레이드오프)가 붙은 쪽은? |
| 6 | **범위 절제** | 과잉설계·장식·불필요한 길이가 없는 쪽은? (길이가 아니라 정보 밀도 기준 — verbosity bias 방어) |

## 절차 (7단계)

1. **생성** — `seeds.md`의 시드마다 2회 생성, 같은 모델·새 컨텍스트:
   - **A (베이스라인)**: 스킬 없이 `"다음 아이디어의 서비스 기획·설계 문서를 작성해줘: {시드}"` 만.
   - **B (스킬)**: service-autopilot 파이프라인으로 실행 (오토파일럿 모드, 질문은 추천안 자동 채택).
   - 원문을 `runs/run-YYYYMM/<시드번호>-{a|b}.md`로 저장.
2. **블라인드화** — 스킬 흔적(고정 헤더·파일명·단계 표기) 제거. 시드마다 동전던지기로
   doc1/doc2 라벨. 매핑은 `runs/run-YYYYMM/mapping.md`(판정 끝까지 열지 않음).
3. **인간 앵커** — judge 전에 사람이 시드 3개를 직접 판정 + 한 줄 비평 (critique shadowing 경량판).
4. **Judge 판정** — `judge-prompt.md`로, **생성과 다른 계열 모델**, 시드당 **2회(원순서+스왑)**. judge는 `fresh-reviewer` 에이전트 타입(읽기 전용, ponytail 미주입)으로 띄우고 판정문은 메인이 파일로 저장한다.
   두 순서 판정이 다르면 그 차원은 tie.
5. **정합성 확인** — judge와 인간 앵커 3건 비교. 불일치 시 루브릭 문구를 고치고 4를 재실행 (2~3회면 수렴).
6. **집계·해석** — `runs/run-YYYYMM/RESULT.md`에 표 한 장: 시드 × (종합 승자 + 6차원).
   - **9/10 이상 스왑-일관 승** = 통계적으로도 우세 (부호검정 p≈0.01)
   - **7~8/10** = 방향성 신호. 스킬 유지, 진 시드의 차원별 패인 분석
   - **6/10 이하** = 스킬이 값을 못 함. 차원별 승률로 어느 단계가 문제인지 진단 후 수정
7. **회귀 세트로 고정** — 스킬을 고칠 때마다 같은 10시드 재실행. 자동화가 필요해지면
   promptfoo `select-best`로 이식 ([문서](https://www.promptfoo.dev/docs/configuration/expected-outputs/model-graded/)).

## 스모크 런 (경량판)

전체 10시드가 부담이면 **시드 3개(쉬움1·중간1·어려움1)로 같은 절차**를 돌린다.
스모크 결과는 "증명"이 아니라 "방향 신호"로만 해석하고 RESULT.md에 스모크임을 명시한다.

## 변경 종류별 회귀 절차 (2026-09-09, 실측 비용 기준)

| 바꾼 것 | 회귀 | 5시간 창 기준 |
|---|---|---|
| 어조·문구·설명문·순서 (산출물 형식 불변) | lite 시드 1개 생성 + `check_package` CRITICAL 0 · HIGH 0. 판정 없음 | 약 10% |
| 규칙·템플릿 (산출물 절·표·ID 형식 변경) | 위 + **경량 판정**: judge 입력을 03·05·08만으로(`blind_prep.py --lite`), 2회(원순서+스왑) | 약 30% |
| 단계·파이프라인 구조 | 스모크 3시드 전체 절차 | 창 2~3개 |

경량 판정의 근거: 두 judge가 든 승패 이유는 거의 전부 03(요구·상수·미결정)·05(계약·커버리지)·08(닫힌 지적·선행 조건)에서 인용됐다
(run-20260908-trim 4회 판정). 전체 문서(20만 자)를 읽히면 회당 약 20%, 세 문서(약 6만 자)는 약 8%.
베이스라인 A처럼 파일 구분자가 없는 단일 문서는 `--lite`에서도 전문을 쓴다.

## 비용 가늠

10시드 전체 = 생성 20회 + judge 20회 ≈ 반나절. 스모크 = 생성 6회 + judge 6회 ≈ 1시간.
