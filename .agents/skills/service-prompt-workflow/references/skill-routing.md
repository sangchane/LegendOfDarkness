# 단계별 스킬 라우팅 (0 BASE ~ 9 REFLECT)

각 단계가 설치된 전문 스킬을 근거로 실행하게 하는 정적 표. 설치 여부·미배정은
`python _tools/skill_catalog.py`(저장소 루트)가 검사한다. 사용 규칙은 service-autopilot과 같다:
진입 시 행을 읽고, 있으면 호출·없으면 대체, 단계당 최대 2개, 사용 기록은 decision-log 한 줄.
서브에이전트에 위임하는 작업의 모델은 `model-routing.md`(작업 클래스 → opus/sonnet/haiku)로 고른다.

## 목차
- 라우팅 표
- superpowers 경계 (설치 시)
- ponytail 배선 (BUILD·REVIEW)
- 알려진 충돌과 우선순위
- 갱신 절차

## 라우팅 표

| 단계 | 1순위 스킬 | 대체 | 무엇을 더 정확하게 만드나 |
|---|---|---|---|
| 0 BASE | `catch-up` (얇은 CLAUDE.md + AGENTS.md + NEXT 구조, 사용자 호출 전용) · 낯선 저장소면 `ecc:codebase-onboarding` | 템플릿 0) | 항상-로드 층을 얇게 |
| 1 FRAME | 저장소 안 bounded 변경·spike면 `superpowers:brainstorming` (질문 1개씩, 승인 게이트) · 새 서비스면 `service-autopilot` (brainstorming 생략) | `ecc:product-lens` | 성공기준·범위경계 |
| 2 EXPLORE | Explore 서브에이전트(내장) · `ecc:search-first` (기존 도구·패턴 우선) | 직접 Read/Grep | 수정 없는 탐색, file:line 인용 |
| 3 SPEC | `ecc:product-capability` · API면 `ecc:api-design` | 템플릿 3) | 제약·불변식·인터페이스 명시 |
| 4 PLAN | `superpowers:writing-plans` (2~5분 작업, 파일 경로·인터페이스·실패테스트→구현→커밋 사이클, 자리표시자 금지) · 다세션이면 `ecc:blueprint` | 템플릿 4) · `ecc:prp-plan` | 낯선 구현자가 그대로 실행 가능한 계획. **순서는 위험 우선 + 워킹 스켈레톤 먼저** (아래 경계 절) |
| 5 BUILD | `superpowers:subagent-driven-development` (작업마다 새 서브에이전트 + 2단계 리뷰) 또는 `superpowers:executing-plans` (같은 세션, 배치 체크포인트) + `ponytail:ponytail` 사다리 · 스택 패턴 1개 — `ecc:python-patterns` `ecc:golang-patterns` `ecc:rust-patterns` `ecc:kotlin-patterns` `ecc:dotnet-patterns` `ecc:frontend-patterns` `ecc:backend-patterns` `ecc:springboot-patterns` `ecc:django-patterns` `ecc:nestjs-patterns` `ecc:laravel-patterns` `ecc:swiftui-patterns` `ecc:dart-flutter-patterns` 중 해당 | 템플릿 5) `<ladder>` | 안 써도 되는 코드를 안 쓰게. 스택 관례 |
| 5 BUILD 테스트 | `superpowers:test-driven-development` (RED→GREEN→REFACTOR) + 언어별 `ecc:python-testing` `ecc:golang-testing` `ecc:rust-testing` `ecc:kotlin-testing` `ecc:csharp-testing` `ecc:cpp-testing` | `ecc:tdd-workflow` · 템플릿 5) | 실패 테스트 먼저. 커버리지는 SPEC 리스크 기반 |
| 5 BUILD 프론트 | `frontend-design-taste` | `anti-patterns.md` | AI 티 제거 |
| 6 VERIFY | `superpowers:verification-before-completion` (완료 선언 전 실제 실행) · `/verify` (빌드·실행으로 확인) · UI면 `ecc:browser-qa` | `ecc:verification-loop` · `/run` · 템플릿 6) | 실행 증거(로그·종료코드·스크린샷) |
| 6 프레임워크별 | `ecc:django-verification` `ecc:laravel-verification` `ecc:springboot-verification` | — | 마이그레이션·린트·커버리지·보안 일괄 |
| 6 실패 시 (디버깅) | `superpowers:systematic-debugging` (재현 → 원인 가설 → 호출자 전수 확인 → 공유 지점 1회 수정) | ponytail "root cause, not symptom" · 템플릿 6) | 증상 패치 금지. 3회 실패면 SPEC으로 돌아간다 |
| 7 REVIEW | `superpowers:requesting-code-review` (제출 전 자기 점검) → `/code-review` (정확성) → `ponytail:ponytail-review` (과잉설계만) → 지적 수용은 `superpowers:receiving-code-review` (맹종 금지, 검증 후 반영) | `/simplify` · 언어별 리뷰어 서브에이전트 `ecc:code-reviewer` `ecc:python-reviewer` `ecc:typescript-reviewer` `ecc:go-reviewer` `ecc:rust-reviewer` `ecc:java-reviewer` `ecc:csharp-reviewer` `ecc:kotlin-reviewer` `ecc:cpp-reviewer` | 정확성 1회 + 복잡도 1회. 셋 이상 금지 |
| 7 고위험 | `ecc:security-review` (인증·입력·시크릿·결제) · 돈·안전·법이면 `ecc:santa-method` | — | 독립 리뷰어 2명 |
| 8 SHIP | `superpowers:finishing-a-development-branch` (머지·PR 결정) · `ecc:git-workflow` · 배포 있으면 `ecc:deployment-patterns` · 배포 후 `ecc:canary-watch` | `ecc:prp-pr` · 템플릿 8) | 증분 커밋·한글 메시지 |
| 9 REFLECT | `ecc:architecture-decision-records` · `ecc:continuous-learning` (세션 패턴 → learned 스킬) · `catch-up` (NEXT/WORKLOG 갱신) | 템플릿 9) | 다음 세션이 재사용 |
| 병렬·격리 | 독립 작업 2개 이상이면 `superpowers:dispatching-parallel-agents` · 실험적 변경은 `superpowers:using-git-worktrees` | 순차 실행 | 공유 상태 없는 작업만 병렬 |

## superpowers 경계 (설치 시)

superpowers는 SessionStart 훅으로 `superpowers:using-superpowers`를 매 세션에 주입한다 ("1%라도 해당되면 스킬 호출", "만들자 → brainstorming 먼저",
"가벼운 일 예외 없음"). 그대로 두면 새 서비스 기획에서 brainstorming과 service-autopilot이 둘 다 뜬다. superpowers 자신이
"사용자 지시 > 스킬"이라 하므로 경계는 `~/.claude/CLAUDE.md`에 사용자 지시로 둔다. 요지:

- **설계 = service-autopilot, 저장소 안 구현 = superpowers.** brainstorming의 architectural 경로는 autopilot이 대신한다.
  bounded 변경·spike는 brainstorming 그대로.
- **autopilot 핸드오프를 붙여 넣은 것이 설계 승인.** 그 뒤 brainstorming 없이 SPEC → `superpowers:writing-plans`.
- **강도**: superpowers의 spike/bounded/architectural 분류가 곧 구현 쪽 강도다. autopilot lite는 architectural 안에서의 상한이라 충돌하지 않는다.
- **PLAN 순서 규칙** (writing-plans가 주지 않는 것): ① 첫 작업 3개는 워킹 스켈레톤 — 핵심 여정 하나를 입력→저장→조회까지 얇게 끝까지.
  ② 그다음은 Impact×Uncertainty가 큰 작업부터 (로그인·화면은 보통 마지막). ③ 작업의 "됐다" = 빈/에러 상태 있음 + 저장 후 재조회 됨 + 다음 화면 스텁 있음.
  로그인까지만 예쁘고 그 뒤에서 막히는 패턴은 ①②를 어겨서 생긴다.
- **템플릿 4)~8)은 대체용.** superpowers가 없는 PC에서만 `prompt-templates.md`의 해당 블록을 쓴다.

## ponytail 배선 (BUILD·REVIEW)

- **설치**: `/plugin marketplace add DietrichGebert/ponytail` 다음 프롬프트에서 `/plugin install ponytail@ponytail`.
  설치되면 훅이 매 세션·매 서브에이전트에 사다리를 주입하고 `/ponytail lite|full|ultra|off`로 강도를 바꾼다. 기본 full.
- **BUILD 진입**: 코드를 쓰기 전에 사다리를 탄다 (`prompt-templates.md` 5)의 `<ladder>`). 문제를 다 읽은 뒤에 탄다 — 사다리는 해법을 줄이지 읽기를 줄이지 않는다.
- **REVIEW**: `/code-review`로 정확성을 본 뒤 `ponytail:ponytail-review`로 삭제 후보만 한 줄씩 받는다 (`net: -N lines`).
- **내장 사다리 (미설치 대체)**: 1 필요한가(YAGNI) → 2 이 코드베이스에 이미 있나 → 3 표준 라이브러리 → 4 플랫폼 네이티브
  (`<input type="date">`, CSS, DB 제약) → 5 이미 설치된 의존성 → 6 한 줄로 되나 → 7 그제야 최소 코드.
  첫 번째로 성립하는 단에서 멈춘다. 절대 줄이지 않는 것: 신뢰 경계의 입력 검증, 데이터 손실을 막는 에러 처리, 보안,
  접근성, 명시 요청. 의도적 단순화에는 상한과 업그레이드 경로를 주석으로 남긴다.
  출처: DietrichGebert/ponytail v4.9.0 (MIT), `references/evidence.md`.

## 알려진 충돌과 우선순위

- **이중 트리거**: `superpowers:using-superpowers` 훅의 "만들자 → brainstorming"과 service-autopilot 자동 트리거가 겹친다.
  → `~/.claude/CLAUDE.md` 경계 규칙(사용자 지시)이 우선. 설계는 autopilot, 저장소 안 변경은 brainstorming.
- **질문 방식**: superpowers "한 번에 하나 + 구현 전 승인" vs autopilot "배치 1회 ≤5". → 단계가 소유한다. autopilot 안에서는 배치, 그 밖에서는 하나씩.
- **가벼운 일 예외 없음**(superpowers) vs lite(autopilot). → 충돌 아님. superpowers 분류가 구현 강도, autopilot lite는 설계 강도.

- **테스트 양**: ponytail "검증 하나면 충분, 프레임워크 금지" vs `ecc:tdd-workflow` "80%+ 커버리지".
  → **SPEC의 테스트 계획이 결정**한다 (autopilot A6 리스크 기반). ponytail은 프로덕션 코드 양을, SPEC이 테스트 범위를 정한다.
  사소한 한 줄은 테스트 없음 (양쪽 동의).
- **리뷰 횟수**: ecc 언어별 리뷰어의 "MUST BE USED" + `/code-review` + `ponytail:ponytail-review` = 변경당 3회.
  → 정확성 1회 (`/code-review` 또는 언어별 리뷰어 중 하나) + 복잡도 1회 (`ponytail:ponytail-review`). santa-method는 고위험만.
- **질문 방식**: ponytail "기본값으로 진행하고 같은 응답에서 묻는다" vs ETHOS 1 "방향 전환은 한 번에 하나".
  → 범위 안의 구현 결정은 ponytail 방식, **범위·방향 변경은 ETHOS 1**.
- **서브에이전트 주입**: ponytail 훅은 모든 서브에이전트에 사다리를 넣는다. REVIEW·GATE 판정자가 "짧은 쪽 선호" 편향을
  가질 수 있다. 판정 프롬프트에 "길이는 품질이 아니다"가 있어야 한다 (autopilot `judge-prompt.md`에 있음).
  코딩 에이전트로 한정하려면 `PONYTAIL_SUBAGENT_MATCHER` 환경변수.
- **중복이지만 충돌 아님**: `andrej-karpathy-skills:karpathy-guidelines`(외과적 변경·추측 금지)는 ponytail·ETHOS 10과 같은 방향.
  현재 DeviceAgent 프로젝트에만 설치돼 있어 여기서는 비활성.

## 갱신 절차

1. `python _tools/skill_catalog.py` — 참조 스킬 미설치·미배정 확인.
2. 배정 변경 시 `eval/` 대리 A/B 또는 autopilot 스모크 회귀.
3. 외부 스킬 채택 기준은 저장소 README "외부 스킬 흡수 기준".
