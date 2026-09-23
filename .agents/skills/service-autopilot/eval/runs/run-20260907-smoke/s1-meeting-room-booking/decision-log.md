# Decision Log — 사내 회의실 예약 (meeting-room-booking)
버전: v1.0 · 런 시작 2026-09-07 10:20 (KST) · 강도 lite (사용자 지정)

형식: `#n [단계] 결정 — 이유 — 버린 대안(왜)`. 스킬 사용 기록은 `[단계] 스킬 — 무엇이 달라졌나`.

## 결정
- #1 [A0] 강도 lite 채택 — 사용자 지정 + SKILL.md 신호 표 항목 0 (돈·안전·법·민감정보 없음, 외부 연동 없음, 동시 사용자 <100, 1인 운영 가능). 대안 full: 비용 4배 이상(스모크 실측), 사내 도구에 과잉.
- #2 [A1] Build(최소) 채택 — 대안 (a) M365/Google 자원 캘린더 Adopt: 그룹웨어 보유 여부가 입력에 없어 채택 불가, 대신 **착수 조건 #1**로 확인 강제(있으면 이 패키지 폐기). (b) MRBS Adopt/Extend: 기능 충족하나 GPLv2·PHP·모바일/알림 부재로 운영 스택을 물려받음. (c) Cal.com: 개인 일정 예약 도구라 회의실 그리드에 부적합. 핵심 범위가 작아 커스텀 비용이 낮다고 판단.
- #3 [A1] 스택 S1(Next.js + PostgreSQL) 추천 — 중복 차단을 DB EXCLUDE 제약으로 보장하기 위해 PostgreSQL 고정. 프론트+API 단일 코드베이스가 1인 운영에 맞음. 대안 S2(FastAPI+HTMX): 파이썬 팀이면 대체. 팀 역량 미기재 → Assumed(TS).

## 스킬 사용 기록
- [A0] 없음 (라우팅 표: 정규화는 모델만으로 충분)
- [A1] ecc:research-ops — 증거 경계 표기([출처]/[사용자]/[추론]/[추천])와 확인일을 recon 전체에 강제. 검색 5회로 상한 준수
- [A1] ecc:search-first — Adopt/Extend/Build 판정 절차 적용 → Build(최소) + 착수 조건(그룹웨어 확인). 서브에이전트 미사용(lite)
- #4 [A2] 인증 = 매직링크(사내 이메일 도메인 화이트리스트), 비밀번호 없음 — 저장 비밀 0으로 S/I 위협 축소, SSO는 email 식별자로 열어둠. 대안 (a) 로그인 없음(이름만 입력): 남의 예약 취소·부인 방지 불가 → 화이트보드 문제 재현. (b) 비밀번호: 리셋 흐름·해시 관리 부담이 1인 운영에 과잉.
- #5 [A2] 반복 예약·체크인 자동해제는 v1 non-goal(P2) — 유령 예약 대책은 가치 있으나(recon 30~40%) MVP 성립과 무관. 반복 예약은 유령 예약 원인이라 오히려 제외.
- #6 [A2] 질문 0건 — lite 상한. 가장 비싼 가정 2개(그룹웨어 부재·TS 역량)는 08 착수 조건으로 이관.
- [A2] ecc:product-lens — 7문항 진단을 register 머리에 흡수, go 판정에 "그룹웨어 있으면 no-go" 조건 추가. 질문 형식·1회 배치 규칙은 autopilot 우선(질문 0)
- #7 [A3] 목표 2개(충돌 없는 예약·자리에서 보이는 현황), 스토리 3, FR 10(P0 4·P1 4·P2 2), SC 5 — lite 상한 준수. 보존·파기(FR-008)를 P1로 올린 이유: 개인정보 최소 보존은 법 근거가 있어 MVP 직후 필요. 자동 해제(FR-009)는 P2.
- #8 [A3] UI dial DENSITY 6 / MOTION 2 / VARIANCE 3 — 제품/앱 UI 프로파일에서 그리드 밀도만 +1. 대안 관제 프로파일(8): 폼·목록이 답답해짐.
- #9 [A4] 중복 차단 = PostgreSQL EXCLUDE(btree_gist) + 23P01→409 변환 — 대안 (a) 앱 층 SELECT-then-INSERT: READ COMMITTED 경합 누락, (b) SQLite: 범위 EXCLUDE 없음, (c) 실시간 푸시: SC-003이 요구하지 않음(YAGNI). 위협모델은 lite 한 단락(신호 없음); 인터넷 노출 결정 시 full STRIDE 표로 승격.
- [A3] ecc:product-capability — 불변식 INV-1~5 절 추가(EXCLUDE 조건·소프트 취소·서버측 owner). 미결정 0건 강제
- [A3] frontend-design-taste — dial 3축 수치 고정 + UX-01~04 측정 기준 발급 (ux-principles-kr L-03/06/07/10)
- [A4] ecc:architecture-decision-records — "검토한 대안" 표를 Nygard 형식(장점·단점·왜 아닌가)으로 작성, 별도 docs/adr 없이 이 로그에 흡수(#9)
- [A4] ecc:security-review — 위협모델 ③ 대책에 쿠키 HttpOnly/SameSite·Origin 검사·zod 검증·파라미터 쿼리·스택 미노출·로그 PII 금지 항목 추가. 서브에이전트(ecc:architect) 미사용(lite)
- #10 [A5] 에러 포맷 RFC 9457 · URL 버저닝 안 함 · Idempotency-Key 필수(POST bookings) — ecc:api-design은 URL 경로 버저닝(/api/v1)과 `{error:{code}}` 포맷을 권하지만 stage-templates(Zalando #115·RFC 9457)가 우선. 사내 단일 클라이언트라 무버전 시작.
- #11 [A5] 취소는 DELETE → 200 + 취소된 자원(204 대신) — 03 E8의 "200 멱등"과 맞추고 클라가 cancelled_at을 바로 표시하게 함. 대안 204: 03 개정 필요(버전 전파 비용).
- [A5] ecc:api-design — 상태코드 표(409/422/428/429) · 커서 페이지네이션(감사 로그만) · 권한 스코프 표기 · CSRF Origin 검사. 버저닝·에러 포맷은 충돌 → 템플릿 우선(#10)
- [A5] ecc:postgres-patterns — bigint identity · timestamptz · 부분 인덱스(status='active') · 커버링 없는 최소 인덱스 4개. EXCLUDE DDL은 raw SQL 마이그레이션
- #12 [A6] integration 비중을 표준보다 올림(60/25/10/5) — 핵심 리스크(EXCLUDE 경합)는 실 PostgreSQL에서만 검증 가능. 커버리지는 리스크 기반(BookingService 100%·Auth 95%·나머지 목표 없음); tdd-workflow의 80% 일률은 채택 안 함(skill-routing 충돌 규칙).
- [A6] ecc:tdd-workflow — RED 확인 게이트·clock 주입·독립 테스트 원칙을 원칙 절에 반영. 80% 임계는 미채택
- [A6] ecc:e2e-testing — POM 3개·data-testid 규약·waitForResponse·retries 1·trace 보관을 E2E 절에 반영. 브라우저는 chromium+mobile-chrome 2개로 축소(사내 도구)
- #13 [A7] 단일 VM Docker Compose 운영 — docker-patterns는 "오케스트레이션 없는 compose 프로덕션"을 안티패턴으로 보지만, 사내 도구·1인 운영·RTO 4h에서는 k8s/Swarm이 과잉. `restart: always` + 롤백(직전 sha) + 일 백업으로 수용. 인터넷 노출·팀 확장 시 재검토.
- #14 [A7] 로그 스택 없음(docker json-file + 주간 집계 스크립트) — Loki/Grafana는 대시보드 필요가 생기면 추가(YAGNI). 알람 4개는 전부 런북 1:1.
- [A7] ecc:deployment-patterns — CI 단계 순서·롤백 체크리스트(역호환 마이그레이션)·env zod 검증·준비도 항목을 배포 절에 반영
- [A7] ecc:docker-patterns — non-root·read_only·tmpfs·named volume·db 포트 비노출·mailpit 개발 서비스·json-file 로테이션 반영
- #15 [GATE 자기점검] 04의 "Drizzle 또는 Kysely 중 하나" 표기는 위장된 미결정 → Drizzle로 확정(drizzle-kit 마이그레이션 + raw SQL EXCLUDE). 근거는 공식 문서만(검색 예산 소진, 인기 수치 미인용). 교체해도 05 계약 불변이므로 Impact 낮음 → Assumed.
- [GATE] 서브에이전트 없음(lite) — check_package 2회(수정 전/후 모두 CRITICAL 0·HIGH 0) + 자기 점검 5건(HIGH 1 반영, MEDIUM 2 Accept, LOW 2 거짓 양성 필터링). 판정 PASS

## 비용 기록
강도 lite (사용자 지정) · 검색 5회 (상한 5) · 서브에이전트 0 · 소요 시간 2026-09-07 10:20 → 10:36 (약 15분) · 스킬 호출 11회(A1 2·A2 1·A3 2·A4 2·A5 2·A6 2·A7 2, 단계당 ≤2) · 토큰 미측정(스킬 본문 로드가 컨텍스트의 다수를 차지)
