# 단계별 스킬 라우팅 (A0 ~ GATE)

각 단계가 "모델의 기억"이 아니라 **설치된 전문 스킬**을 근거로 판단하게 하는 정적 표.
설치 여부·미배정 스킬은 `python _tools/skill_catalog.py`(저장소 루트)가 검사한다.

## 목차
- 사용 규칙
- 단계 진입 사전조사 (A3~A7 공통)
- 라우팅 표
- 알려진 충돌과 우선순위
- 갱신 절차

## 사용 규칙

1. 단계 진입 시 이 표의 행을 읽는다. 1순위 스킬이 이 세션의 사용 가능 스킬 목록에 있으면 Skill 도구로
   호출하고, 없으면 "대체" 열을 따른다. 미설치를 이유로 단계를 건너뛰지 않는다.
2. 스킬은 조언자다. 산출물 형식과 하드 게이트는 `stage-templates.md`가 우선한다. 스킬이 별도 산출물
   (예: `docs/adr/`)을 요구하면 같은 내용을 이 파이프라인 산출물에 흡수한다.
3. 어떤 스킬을 썼고 무엇이 달라졌는지 `decision-log.md`에 한 줄 남긴다
   (`[A5] ecc:api-design — 에러 포맷을 RFC 9457로 통일`). 회귀 평가 때 어느 스킬이 값을 했는지 추적하기 위함이다.
4. 단계당 스킬 호출은 최대 2개. 셋 이상 필요하면 그 단계가 너무 크다는 신호다.
5. 스킬 본문은 단계 진입 시에만 로드한다. 여러 스킬을 미리 읽어 두지 않는다 (컨텍스트는 공용 자원).
6. 서브에이전트에 위임하는 단계의 모델은 `model-routing.md`로 고른다 (A1 조사 sonnet, A4 검토·GATE opus).
   판단 단계(A2~A5)는 위임하지 않는다. 사용 기록 줄에 모델을 병기한다.

## 단계 진입 사전조사 (A3~A7 공통)

A1 RECON이 도메인 전체를 조사한다. 이후 단계는 **그 단계의 결정에 필요한 것만** 짧게 조사한다
(검색 최대 3회). 결과는 산출물 상단 `근거` 블록 3줄로 남긴다:

- **정량** — 숫자 + 출처 URL + 확인일. 예: "p95 800ms — 유사 서비스 SLA 3곳 중앙값 (2026-09-03)"
- **정성** — 실무자·사용자의 불만 또는 요구 인용 1개 (이슈·포럼·리뷰). 사용자 친화 판단의 근거.
- **사용자 영향** — 이 단계의 결정이 최종 사용자에게 어떻게 보이는가 한 줄. `ux-principles-kr.md` 원칙 번호로 연결.

근거를 못 찾으면 "미확인"으로 적고 register에 `Assumed`로 마킹한다. 지어내지 않는다.

## 라우팅 표

| 단계 | 1순위 스킬 | 대체 (미설치·부적합 시) | 무엇을 더 정확하게 만드나 |
|---|---|---|---|
| A0 SEED | 없음 | — | 정규화는 모델만으로 충분하다. 스킬 호출은 낭비 |
| A1 RECON | `ecc:research-ops` (현재 시점 사실·비교, 근거 우선) · `ecc:market-research` (경쟁·시장, 출처 표기) | WebSearch/WebFetch + `evidence-map.md` 절차 | 조사 시점 기록, 교차 확인, 출처 URL 강제 |
| A1 스택 후보 | `ecc:search-first` (커스텀 코드 전에 기존 도구·라이브러리 탐색) | `evidence-map.md` fit_score | "만들지 말고 사라" 판단. 인기≠적합 |
| A2 INTERROGATE | `ecc:product-lens` (만들기 전 "왜" 검증) | `blindspot-checklists.md`만 | 질문 후보의 Impact 판단 보강. 질문 형식·1회 배치 규칙은 이 스킬이 우선 |
| A3 PRD | `ecc:product-capability` (PRD 의도 → 제약·불변식·인터페이스가 드러난 계획) | `stage-templates.md` A3 | 요구사항 풀의 불변식·제약 누락 방지 |
| A3 화면 있으면 | `frontend-design-taste` (dial·프로파일) | `ux-principles-kr.md` | UI 방향을 측정 가능한 dial로 고정 |
| A4 ARCHITECT | `ecc:architecture-decision-records` (결정·대안·근거) · `ecc:security-review` (인증·입력·시크릿·엔드포인트) | STRIDE 표만 | "검토한 대안" 섹션과 위협모델 ②의 누락 탐지 |
| A4 스택별 | `ecc:backend-patterns` · `ecc:hexagonal-architecture` (경계 분리 필요 시) · `ecc:cost-aware-llm-pipeline` (LLM 포함 시) | — | 스택 관례. 해당할 때만 |
| A4 검토자 | `ecc:architect` (서브에이전트) | fresh-context 일반 서브에이전트 | 확장성·트레이드오프 독립 검토 |
| A5 CONTRACT | `ecc:api-design` (자원 명명·상태코드·페이지네이션·에러·버저닝·레이트리밋) · `ecc:postgres-patterns` (스키마·인덱스) | Zalando 가이드 (`evidence.md`) | 엔드포인트 표·ERD 관례 일관성 |
| A5 기존 데이터 있으면 | `ecc:database-migrations` (스키마 변경·롤백·무중단) · `ecc:database-reviewer` (서브에이전트) | — | 이관 리스크 |
| A6 TEST-DESIGN | `ecc:tdd-workflow` (피라미드·단위/통합/E2E) · `ecc:e2e-testing` (Playwright POM·flaky 전략) | Fowler 피라미드 (`evidence.md`) | 시나리오 매트릭스 관례. 커버리지 목표는 **이 스킬의 리스크 기반**이 우선 |
| A7 OPS-DESIGN | `ecc:deployment-patterns` (CI/CD·헬스체크·롤백·준비도 체크리스트) · `ecc:dashboard-builder` (운영자 질문에 답하는 대시보드) | Google SRE (`evidence.md`) | 배포·관측성의 실무 체크리스트 |
| A7 컨테이너 | `ecc:docker-patterns` | — | 컨테이너 배포일 때만 |
| GATE | fresh-context 서브에이전트(기본) · 돈·안전·법 도메인이면 `ecc:santa-method` (독립 리뷰어 2명 모두 통과) | 서브에이전트 1명 적대적 검토 | 거짓 양성은 필터링 후 반영 |
| 핸드오프 | `service-prompt-workflow` SPEC 단계 | — | 03~07 산출물이 입력 |

## 알려진 충돌과 우선순위

- **질문 정책**: `ecc:prp-prd`는 되묻기 왕복 방식이라 채택하지 않는다 (이 스킬은 1회 배치 ≤5문항). 루브릭으로만 참고.
- **커버리지**: `ecc:tdd-workflow`의 "80%+" 일률 목표 대신 A6의 리스크 기반 목표 (P0 경로·위협모델 상위 리스크 상향, 나머지 최소).
- **산출물 중복**: ADR 스킬이 요구하는 별도 파일은 `decision-log.md`에 같은 형식으로 흡수한다.
- **연구 스킬 MCP 의존**: `ecc:deep-research`는 firecrawl/exa MCP가 필요하다. 없으면 `ecc:research-ops`로 대체한다.

## 갱신 절차

1. `python _tools/skill_catalog.py` — 참조 스킬 미설치·미배정 목록 확인.
2. 새 스킬을 배정할 때는 "무엇을 더 정확하게 만드나"를 표에 쓰고 `eval/PROTOCOL.md` 스모크 회귀를 돌린다.
3. 외부 스킬 채택 기준은 저장소 README "외부 스킬 흡수 기준".
