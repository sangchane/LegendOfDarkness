---
name: service-autopilot
description: |
  서비스 기획·설계 오토파일럿 — 한 줄 아이디어를 구현 착수 가능한 설계 패키지(PRD·아키텍처+위협모델·
  API계약/ERD·테스트설계·IaC/관측성)로 바꾸는 8단계 파이프라인. solution-planner(구버전)를 대체한다.
  사용 시점: 신규 서비스/솔루션/기능 기획(신규 기획, 서비스 기획, 초기 기획, PRD, 기획서), 기술스택
  추천, MVP 범위, 도메인을 모르는 상태의 착수("IoT 카메라 서비스 만들고 싶어" 같은 한 줄 아이디어),
  위협모델·API 설계·배포/모니터링 설계가 필요할 때. 도메인 조사→블라인드스팟 심문(객관식 최대 5문항
  1회)→설계 산출물 생성을 사용자 개입 최소로 자동 진행한다. 구현 실행은 service-prompt-workflow가
  이어받는다 (이 스킬의 산출물이 그쪽 SPEC 입력).
argument-hint: "[spike|lite|full] [한 줄 아이디어]"
effort: high
metadata:
  version: "1.4.3"
  updated: "2026-09-09"
---

# Service Autopilot (서비스 기획·설계 오토파일럿)

한 줄 아이디어 → **구현 착수 가능한 설계 패키지**. 사용자가 빈칸을 채우는 게 아니라
**AI가 조사하고, 사각지대를 스스로 찾아 덮고, 가정으로 못 덮는 위험 결정만 객관식으로 1회 묻는다.**

- 대체: `solution-planner` (deprecated — 이 스킬이 후속)
- 후속: 산출물을 `service-prompt-workflow`의 SPEC 단계에 입력하여 구현 실행
- 근거: 모든 단계 구조·질문 프로토콜은 검증된 프레임워크(spec-kit, MetaGPT, BMAD)와 실무 표준
  (ISO 29148, STRIDE, AWS Well-Architected, Google SRE)의 실측 이식. 스타 수·확인일 같은 변동 수치는
  `references/evidence.md`에만 둔다.

## 규칙

각 규칙은 이유와 함께 쓴다. 규칙이 생긴 실측 사건은 `references/evidence.md`에 있다.

1. **근거 없는 추천 금지.** 모든 추천(기술·표준·범위·기본값)에 출처를 붙인다. 웹 검색으로 현재
   시점 데이터를 확인하고, 학습 지식만으로 스타 수·버전·규제를 단정하지 않는다. (`references/evidence-map.md`)
2. **인기 ≠ 적합.** fit 판단은 사용자 제약(팀 역량·운영 환경·규모·예산)과의 매칭이다.
3. **질문은 파이프라인 전체에서 배치 1회, 최대 5문항.** 형식은 객관식(2~4옵션) +
   `추천:` 표시 + 근거. 선정 기준은 **Impact × Uncertainty** — "틀리면 재작업이 비싸고, 조사로는
   답을 정할 수 없는 것"만 묻는다. 나머지는 전부 현업 기본값으로 가정하고 가정임을 명시. *(spec-kit /clarify 이식)*
4. **사각지대 전수 기록.** 심문 택소노미의 모든 축을 `02-blindspot-register.md`에
   Clear(해결)/Assumed(가정 채택)/Asked(질문) 중 하나로 마킹한다. 마킹 안 된 축이 남으면 게이트 통과 불가 —
   "덮었다"는 기록으로만 증명된다.
5. **모르면 조사, 조사해도 모르면 가정, 가정이 위험하면 질문.** 이 우선순위를 뒤집지 않는다.
6. **검증 기준 없는 질적 표현 금지.** "직관적인·빠른·유연한"은 측정 가능 기준으로 변환한다. (`references/quality-decomposition.md`)
7. **모든 결정은 decision-log에.** 무엇을·왜 선택했고 어떤 대안을 왜 버렸는지. 산출물은 생성 즉시
   파일로 저장(원자적) — 대화에만 남기지 않는다.
8. **최종 게이트는 적대적 자기검토.** fresh context 서브에이전트가 문제를 찾는 자세로
   산출물 정합성을 검사하고 PASS/CONCERNS/FAIL을 판정한다. 거짓 양성이 나올 수 있으므로 지적은
   반영 전에 타당성을 필터링한다. *(BMAD adversarial review + spec-kit /analyze 이식)*
9. **상류 개정은 하류로 전파한다.** 산출물마다 머리에 `버전: vX.Y` (04~07은 `· 기준 03 vX.Y`)를 적고, 03·04·05를 고치면
   그 버전을 참조하는 하류 문서를 같은 턴에 재검토해 버전을 올린다. 개정이 문서 3개 이상에 걸치면 전부 다시 쓰지 말고
   `REVISIONS.md`(R1, R2 … 결정·영향·사용자 원문)를 두고 영향받는 문서 머리에 `개정 노트 R# 적용 대기`를 적는다 — 검증 스크립트가 그 배지를 HIGH로 보고한다. 상한·기간·임계·용량 같은 숫자는 03의 **상수 표**에만 쓰고
   다른 문서는 이름으로 참조한다. 이유: 상류만 고치면 하류에 옛 값이 남아 같은 상수가 여러 값으로 공존한다.
10. **자기 선언은 스크립트로 검증한다.** "누락 0", "미결정 0건"은 모델이 세지 않는다. GATE 전에
    `python scripts/check_package.py autopilot/<slug>`를 돌려 출력을 검토관 입력에 넣고, CRITICAL이 남으면 GATE에 들어가지 않는다.
    이유: 모델의 자기 카운트는 실측에서 틀렸다.

## 파이프라인 (A0~A7 + GATE)

사용자 7단계 비전과의 대응: A3=①PRD, A4=②아키텍처·위협모델, A5=③API·스키마, A6=④TDD 설계,
A7=⑥IaC·⑦관측성. ④실행·⑤구현은 핸드오프 후 service-prompt-workflow BUILD가 담당.

| # | 단계 | 하는 일 | 산출물 | 하드 게이트 |
|---|---|---|---|---|
| A0 | **SEED** | 한 줄 아이디어 정규화: 서비스 유형·주/인접 도메인·감지된 제약 분류. 되묻지 않음 | `00-seed.md` | 유형·도메인 분류 완료 |
| A1 | **RECON** | 근거 조사: 도메인 업무 흐름, 이해관계자, 규제/표준, 유사 솔루션 3~5개, 스택 후보 | `01-recon.md` | 전 항목 출처 URL |
| A2 | **INTERROGATE** | 심문 택소노미(`references/blindspot-checklists.md`) 전축 스캔 → 축별 Clear/Assumed/Asked 마킹 → Impact×Uncertainty 상위 최대 5문항을 **객관식 배치 1회**로 질문 → 답변·가정을 register에 반영 | `02-blindspot-register.md` | 전 축 마킹 완료 (규칙 4) |
| A3 | **PRD** | 제품 목표(≤3·직교) · 유저스토리(3~5, P1만으로 MVP 성립) · 엣지케이스(Given/When/Then) · 요구사항 풀(P0/P1/P2) · 측정 가능 성공기준(SC-###) · 가정 목록 | `03-prd.md` | 성공기준 전부 pass/fail 판정 가능 |
| A4 | **ARCHITECT** | 구현 접근·컴포넌트 구조(mermaid) · 데이터 흐름(sequenceDiagram) · **위협모델**: 4질문 프레임 + STRIDE 6범주 각각 검토(해당없음도 근거 명시) + 상위 리스크 완화책 | `04-architecture.md` | STRIDE 6범주 전수 검토 |
| A5 | **CONTRACT** | API 계약: 엔드포인트 표(메서드·경로·요청/응답·에러 포맷·버저닝·페이지네이션·멱등성) · ERD(mermaid erDiagram) · **요구사항↔엔드포인트 커버리지 매핑**(매핑 0건 요구사항 = 결함) | `05-api-contract.md` | P0/P1 요구사항 커버리지 100% |
| A6 | **TEST-DESIGN** | 수용기준 전부를 실패하는 테스트 시나리오(Given/When/Then)로 변환 · 단위/통합/E2E 매트릭스 · 리스크 기반 커버리지 목표 | `06-test-design.md` | 수용기준→시나리오 누락 0 |
| A7 | **OPS-DESIGN** | 배포 설계(런타임·IaC 스케치·CI/CD 단계) · 관측성(무엇을·어디에·얼마나 로깅, 핵심 지표+알림 조건, SLO-lite) · 장애 시나리오별 복구 절차 | `07-ops-design.md` | 로그·백업·복구 각각 "어디에·얼마나·어떻게" 답변됨 |
| G | **GATE** | fresh context 적대적 검토: 산출물 교차 정합성(중복·모호·커버리지 공백·용어 드리프트) + 준비도 판정 → 핸드오프 프롬프트 생성 | `08-readiness-report.md` | PASS 또는 CONCERNS(사유 명시). FAIL이면 해당 단계 재실행 |

각 단계의 상세 산출물 템플릿과 프롬프트 블록은 `references/stage-templates.md`.
단계 진입 시 어떤 전문 스킬을 호출할지는 `references/skill-routing.md` — 설치된 스킬이 있으면 그것을
근거로 쓰고(없으면 대체), A3~A7은 정량·정성·사용자 영향 3줄 사전조사를 산출물 상단에 남긴다.

## 강도 (spike / lite / full)

한 바퀴 비용이 규모와 무관하게 같으면 작은 일에 큰 도구를 쓰게 된다. A0에서 강도를 판정해 `00-seed.md`에 `강도: spike|lite|full (사유)`로 적는다.
사용자가 `/service-autopilot lite …`처럼 지정하면 그것이 우선한다.

| 신호 — 하나라도 있으면 full | 없으면 lite |
|---|---|
| 돈·안전·법·민감정보가 걸림 · 외부 시스템 연동 2개 이상 · 동시 사용자 100명 이상이거나 데이터 볼륨이 설계 변수 · 하드웨어/엣지 · 팀 2명 이상이 이어받음 · 사용자가 "정식/본격/납품"이라 말함 | 사내 도구, 1인 운영, 프로토타입, 기존 시스템의 bounded 변경 |

| 단계 | lite 상한 | full |
|---|---|---|
| A1 | 검색 ≤5회, 유사 솔루션 3개, 스택 후보 2개 | 현행 |
| A2 | 질문 0 (전부 Assumed. 단 Impact가 데이터 손실·법이면 1문항 허용) | 최대 5문항 1회 |
| A3 | 목표 ≤2, 스토리 ≤3, FR ≤10, SC ≤5. 진입 사전조사 3줄은 A3·A4만 | 현행 |
| A4 | 신호 표 항목이 있을 때만 STRIDE 전수, 없으면 "해당 없음 + 사유" 한 단락 | STRIDE 전수 |
| A5 | 엔드포인트 표 + 커버리지 매핑 + ERD는 **컬럼·제약까지**(DDL 스케치). OpenAPI 스케치만 생략 | 현행 |
| A6 | P0 수용기준만 시나리오화 | 전부 |
| A7 | 배포·로그·백업·복구 각 1~2줄 + 착수 자산 | 현행 |
| GATE | 메인이 `scripts/check_package.py` + 자기 점검, 서브에이전트 없음 | 서브에이전트 검토 1회 (돈·안전·법이면 santa 2인) |
| 실측·상한 토큰 | 실측 20만 (2026-09-07). 상한 25만 | 실측 약 70만, 3.5시간 (2026-09-07). 상한 80만(서브에이전트 포함). 넘으면 멈추고 사용자에게 보고. 세부는 `references/evidence.md` |

lite의 바닥은 스킬 본문 읽기와 도구 호출 횟수다 (문서 분량이 아니다). 더 줄이려면 스킬 호출을 단계당 1개로, 파일 쓰기를 묶어서.
이 스킬은 `effort: high`로 돈다 — 세션이 xhigh여도 20~30KB 문서를 쓰는 단계에서는 xhigh가 초안을 thinking에서 한 번 더 써 출력이 두 배가 된다 (Anthropic Fable 5.1 가이드).

**spike** — 플랫폼·범위·핵심 루프 중 하나라도 미확정이면 신호 표와 무관하게 spike 먼저. 미확정 위에 쓴 문서는 하루 만에 뒤집힌다. spike = A0(해석 목록) · A1(검색 ≤5, 참조 제품 규칙 깊이·데이터 공급 표본) · A2(질문 최대 2: 사용 맥락·최상위 해석) ·
03은 상수 표 + INV + SC 3개 · 04는 구현 접근 한 단락 · 05는 핵심 스키마 3개 · 06은 시뮬 지표 3개 · 07은 착수 자산만 · GATE 자기 점검.
목표 10만 토큰. 워킹 스켈레톤(엔진 + 헤드리스 시뮬)이 돌아 사실이 쌓인 뒤 lite/full로 문서를 확정한다.

산출물 9개의 파일명·형식은 강도와 무관하게 같다 (핸드오프·스크립트 호환). lite 중에 신호가 발견되면 그 단계부터 full로
올리고 decision-log에 적는다. *(superpowers brainstorming의 spike/bounded/architectural 3등급에서 착안 — `references/evidence.md`)*

## 질문 프로토콜 (A2 전용 — 개입 최소화의 핵심)

1. 택소노미 전축을 스캔해 축마다 Clear/Partial/Missing 상태를 매긴다.
2. Partial/Missing 축 중 **Impact(틀리면 재작업·데이터 손실·법적 문제·하드웨어 재구매) ×
   Uncertainty(조사로 못 정함 — 답이 사용자의 실제 환경·예산·운영 인력에 달림)** 상위 최대 5개만 질문으로 승격.
3. 질문 형식 (spec-kit 표준 이식):
   - 객관식 2~4옵션, 각 옵션에 근거·트레이드오프 한 줄.
   - 첫 옵션 = 추천안, `(추천)` 표시 + 추천 이유. 단 **사용자 환경 질문**(플랫폼·팀·판매·기기)은 추천이 답을 앵커링하므로
     사용 맥락(축 11)을 먼저 묻고, 각 옵션에 "되돌릴 때 비용"을 적는다. 질문 1칸은 A0 해석 목록의 최상위 리스크에 예약한다.
     규모·중앙 서버·멀티사이트처럼 시드에 없는 것을 세우는 **확장 해석**은 가정으로 확정하지 않고 이 칸에 올린다. 기본값은 축소 쪽
     (단일 사이트·로컬 우선·1인 운영) — 확장 해석 위에 쌓인 설계는 범위 절제에서 일관되게 감점된다.
   - 도구 사용 가능 환경이면 AskUserQuestion으로 배치 제시. 아니면 마크다운 표 1회 출력.
4. **배치 1회로 끝.** 무응답·스킵된 문항은 추천안을 가정으로 채택하고 register에 `Assumed(무응답)` 마킹.
5. 답변은 받는 즉시 register와 영향받는 산출물에 반영하고 파일 저장(원자적).
6. 예외: 사용자가 "단계별로 보자"고 명시하면 단계마다 게이트에서 확인받는 대화형 모드로 전환.

## 실행 절차 (오토파일럿 기본)

1. 파이프라인 표의 순서대로 A0부터 GATE까지 논스톱으로 진행한다. A0에서는 묻지 않고, 질문은 A2의 배치 1회뿐이다.
   산출물은 만들 때마다 저장한다. 단계 진입 시 `references/skill-routing.md`의 행대로 스킬을 쓰고 사용 기록을 decision-log에 남긴다.
   서브에이전트에 맡기는 단계(A1 조사·A4 검토·GATE)는 `references/model-routing.md`의 등급으로 `model`을 명시하고 기록 줄에 병기한다.
   판단 단계(A2~A5)는 위임하지 않는다. GATE 판정과 핸드오프는 `08-readiness-report.md`에 담는다.
2. **예산과 재실행.** GATE FAIL은 해당 단계 재실행 1회까지. CONCERNS라도 HIGH 이상이 남으면 **패치 1회**(타당성 필터링 → 반영 →
   버전 전파 → check_package 재실행)를 거친 뒤 08을 쓴다. 선행 조건으로 넘기는 것은 MEDIUM 이하만. 이유: 판정관과 구현자 모두
   "지적이 닫혔는가"를 가장 무겁게 본다 — 같은 문서가 패치 라운드 유무만으로 4패와 무승부로 갈렸다. 두 번째도 FAIL이면 CONCERNS로
   마감하고 잔여 지적을 08과 핸드오프 선행 조건에 넘긴다. 검토 서브에이전트는 full에서 1명(돈·안전·법이면 santa 2명), 재검토는 1회.
3. **중단과 재개.** 세션 한도·오류로 끊기면 `autopilot/<slug>/`의 산출물을 읽고 **없는 첫 파일의 단계부터** 이어 간다.
   있는 파일은 다시 만들지 않는다. 재개 사실과 시각을 decision-log에 적는다. 서브에이전트의 컨텍스트 상한은 20만 토큰이라
   full 한 바퀴는 중간 압축을 거친다 — 파일과 NEXT.md가 진실원이고, 기억은 아니다.
4. **비용 기록.** 런이 끝나면 decision-log 말미에 `강도 · 검색 횟수 · 서브에이전트 수 · 소요 시간`(알면 토큰도)을 적는다.
   다음 강도 판정의 근거가 된다.
5. **단계마다 `NEXT.md` 갱신.** 각 단계를 마칠 때 `autopilot/<slug>/NEXT.md`에 현재 상태 1줄 · 다음 할 일 3줄 · 재개 명령 1줄을 덮어쓴다.
   세션이 끊기면 이 파일이 재개 포인터다(실행 절차 3). 08이 없어도 다른 세션이 이어받을 수 있어야 한다.
6. 최종 보고는 (0) **해석 목록**(A0에서 내가 정한 것 — "아니면 지금 고치세요"), (a) 판정, (b) 핵심 결정 5줄 요약,
   (c) 질문에서 가정으로 채택된 항목 목록, (d) service-prompt-workflow로 넘어가는 복붙 프롬프트로 구성한다.

### 산출물 디렉토리

```
autopilot/<service-slug>/
├─ 00-seed.md            ├─ 04-architecture.md
├─ 01-recon.md           ├─ 05-api-contract.md
├─ 02-blindspot-register.md  ├─ 06-test-design.md
├─ 03-prd.md             ├─ 07-ops-design.md
├─ 08-readiness-report.md    ├─ decision-log.md
└─ NEXT.md (단계마다 갱신)    └─ REVISIONS.md (대규모 개정 시)
```

### 핸드오프 (구현으로)

GATE 통과 후 사용자가 08의 핸드오프 프롬프트를 붙여 넣는 것이 **설계 승인**이다 (superpowers brainstorming의 승인 게이트와
같은 역할 — 그 뒤 brainstorming을 다시 하지 않는다). service-prompt-workflow SPEC의 입력은 `03-prd.md`(요구사항·상수 표) +
`05-api-contract.md`(계약) + `08`의 착수 조건·첫 작업 3개(워킹 스켈레톤)만이다. `04·06·07`은 경로만 넘기고 필요할 때 읽는다
(핸드오프 130KB → 입력 40KB 목표). UI가 포함되면 BUILD·REVIEW에서 `frontend-design-taste` 스킬을 함께 적용한다.
08의 핸드오프 블록에는 구현 작업 클래스별 `<model_hints>`(opus/sonnet/haiku — `references/model-routing.md` 하단 표)를
붙여, service-prompt-workflow PLAN이 tasks.md `model:` 태그의 초기값으로 쓰게 한다.

## 참조 파일

- `references/blindspot-checklists.md` — 심문 택소노미(공통 축 + 도메인 프로파일). A2 진입 전에 읽는다.
- `references/stage-templates.md` — A0~GATE 단계별 산출물 템플릿·프롬프트 블록. 각 단계 진입 시 읽는다.
- `references/skill-routing.md` — 단계별 스킬 라우팅 표 + 진입 사전조사 프로토콜 + 충돌 우선순위. 각 단계 진입 시 해당 행을 읽는다.
- `references/model-routing.md` — 단계별 모델 라우팅(어느 단계를 어느 등급 서브에이전트에 맡기나) + 핸드오프 모델 힌트. 서브에이전트를 띄우기 전에 읽는다.
- `scripts/check_package.py` — 자기 선언 검증(FR→05 커버리지, SC→06 시나리오, 미결정, register 마킹, 버전 전파, 미확인 근거 상수의 SC 사용, 개정 노트 배지). GATE 전에 실행한다 (규칙 10).
- `references/evidence.md` — 단계·규칙별 출처 매핑(무엇을 어디서 이식했고 무엇을 왜 바꿨는지). 스킬 수정 시 읽는다.
- `references/evidence-map.md` — RECON 조사 항목별 근거 소스 매핑 (solution-planner 승계).
- `references/quality-decomposition.md` — 질적 표현→측정 기준 변환 규칙 (승계).
- `references/ux-principles-kr.md` — UI/UX 기획 원칙 (승계). 화면이 포함된 기획이면 A3에서 읽는다.
- `eval/PROTOCOL.md` — 이 스킬이 "값을 하는지" 측정하는 A/B 평가 절차. 스킬을 수정하면 동결 시드로 회귀 평가.
- `evals/evals.json` — skill-creator 호환 평가 케이스(스모크 시드 3개 + 하드 게이트 단언).

버전·갱신일은 프론트매터 `metadata`. 규칙이나 템플릿을 바꾸면 `eval/PROTOCOL.md` 동결 시드로 회귀 평가한다.
