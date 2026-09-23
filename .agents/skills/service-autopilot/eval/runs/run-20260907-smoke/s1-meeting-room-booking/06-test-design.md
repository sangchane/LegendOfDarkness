# 테스트 설계 — 사내 회의실 예약 (meeting-room-booking)
버전: v1.0 · 기준 03 v1.0 · 강도 lite (P0 수용기준만 시나리오화 — SC-001~005 전부 + P0 FR-001~004; P1은 계약 테스트로만 덮음)

## 원칙
- AC는 개발 시작 전에 존재한다. 각 시나리오는 처음엔 반드시 실패해야 한다 (RED 확인 후 구현 — tdd-workflow 게이트).
- 피라미드: unit 다수 → integration(DB 포함) → contract(05 엔드포인트 표) → E2E 최소. 비율 목표 ~60/25/10/5 — 이 서비스의 핵심 리스크(INV-1 동시성)는 unit으로 못 잡으므로 integration 비중을 표준(20)보다 올린다.
- "가능한 한 아래층으로" — 검증 규칙(INV-2)은 unit, EXCLUDE 경합은 integration(실 PostgreSQL, testcontainers 또는 compose의 test DB), 화면 흐름만 E2E.
- 커버리지 목표는 리스크 기반(아래 절). `tdd-workflow`의 80% 일률 목표는 채택하지 않는다.
- 테스트 DB: 매 테스트 트랜잭션 롤백 또는 스키마 재생성. 시각은 주입 가능한 `clock`으로 고정(`TZ` 기준 2026-09-08 09:00).

## 수용 기준 → 시나리오 변환표
| SC/FR ID | Gherkin 시나리오 (Given/When/Then) | 레이어 | 데이터/목킹 |
|---|---|---|---|
| SC-001 / FR-003 (INV-1) | Given Room 1이 비어 있음 When 서로 다른 사용자 100명이 같은 `[10:00,10:30)`을 동시에 POST(EP-05) Then 201은 정확히 1건, 나머지 99건은 409 `booking-conflict`, `SELECT` 겹침 검사 0행 | integration (실 PG) | 사용자 100 seed, `Promise.all`로 동시 발사, 목킹 없음 |
| SC-001 / FR-003 (03 E2) | Given 활성 예약 `[10:00,11:00)` When `[10:30,11:30)` POST Then 409 + `next_free=11:00` | integration | 실 PG |
| SC-001 / FR-003 (03 E3) | Given 활성 예약 `[10:00,11:00)` When `[11:00,11:30)` POST Then 201 (경계 맞닿음 허용) | integration | 실 PG |
| SC-001 / FR-004 (INV-4) | Given 활성 예약 `[10:00,10:30)`을 취소함 When 같은 구간을 다른 사용자가 POST Then 201 (취소 행은 EXCLUDE 제외) | integration | 실 PG |
| FR-003 (INV-2, 03 E4) | Given clock=14:10 When `[14:00,14:30)` POST Then 422 `errors[0].code=past` | unit (validator) | clock 주입 |
| FR-003 (INV-2, 03 E5) | When `[19:30,20:30)` POST Then 422 `outside-hours` | unit | — |
| FR-003 (INV-2) | When start가 `SLOT_MINUTES` 경계가 아님(10:05) / start≥end / 길이 > `MAX_BOOKING_MINUTES` / 시작 > 오늘+`MAX_ADVANCE_DAYS` / 날짜가 다름 Then 각각 422 `slot-boundary`/`validation`/`too-long`/`too-far`/`validation` | unit (파라미터화 5건) | — |
| FR-003 (03 E6) | Given 사용자 활성 예약 `MAX_ACTIVE_BOOKINGS_PER_USER`건 When 추가 POST Then 422 `quota` | integration | seed |
| FR-003 (INV-3) | Given 세션 사용자 A When body에 `owner_id=B`를 넣어 POST Then 201이고 owner는 A | integration | — |
| FR-003 (멱등성) | Given 같은 `Idempotency-Key`로 POST 2회 When 두 번째 요청 Then 첫 응답과 동일 201·같은 id, 예약은 1건. 헤더 없으면 428 | integration | — |
| FR-003 (404) | When 비활성 Room에 POST Then 404 `room-not-found` | integration | rooms.is_active=false |
| SC-003 / FR-004 | Given 활성 예약 When Owner가 DELETE(EP-06) 후 즉시 GET grid(EP-04) Then 200 `status=cancelled` 이고 grid에서 그 슬롯 bookings 비어 있음 | integration | — |
| FR-004 (03 E7) | Given B의 예약 When A가 DELETE Then 403 `not-owner`; Admin이 DELETE Then 200 + audit_events actor=Admin | integration | role seed |
| FR-004 (03 E8) | Given 취소된 예약 When 다시 DELETE Then 200 동일 본문, audit_events 건수 불변 | integration | — |
| SC-002 / FR-002 | Given 활성 Room `ROOMS_MAX`개 × 하루 슬롯 전부 예약 When GET grid 200회 Then p95 ≤ `GRID_P95_MS`, 응답 rooms 길이 = `ROOMS_MAX`, 비활성 Room 미포함 | integration (부하) | autocannon 또는 k6, seed 스크립트 |
| SC-002 (클릭 ≤3) | Given 로그인 상태 When 그리드에서 빈 칸 클릭 → 종료 선택 → "이 시간으로 예약하기" Then 3클릭 안에 예약 표시, 클릭 카운터 ≤ 3 | E2E | Playwright, 카운터는 POM에서 누적 |
| FR-002 (권한) | When 미로그인으로 GET grid Then 401. 날짜가 범위 밖 Then 422 | contract | — |
| FR-001 (03 E9) | When 외부 도메인 이메일로 POST magic-link Then 200 동일 본문, Mailer 호출 0회, 로그 `auth.link_rejected` | integration | Mailer 스파이 |
| FR-001 (03 E10) | Given 토큰 발급 후 clock을 `MAGIC_LINK_TTL_MINUTES`+1분 진행 When callback Then 400 `invalid-token`; Given 사용된 토큰 When 재사용 Then 400 | integration | clock 주입 |
| FR-001 (정상) | When 사내 도메인으로 magic-link → 메일의 링크로 callback Then 302, `Set-Cookie` HttpOnly·SameSite=Lax, sessions 행 expires=now+`SESSION_DAYS` | integration | Mailer 스파이에서 링크 추출 |
| FR-001 (레이트리밋) | When 같은 이메일로 `MAGIC_LINK_RATE_PER_HOUR`+1회 POST Then 마지막은 429 + `Retry-After` | integration | clock 고정 |
| FR-001 (03 E12) | Given Mailer가 3회 실패 When magic-link Then 503 `mail-unavailable`, 기존 세션의 GET grid는 200 | integration | Mailer 목 실패 |
| SC-004 | Given 도입 후 화이트보드 철거 When 4주 동안 매주 Admin이 체크리스트(① 시스템 외 예약 발생 여부 ② 중복·충돌 민원 건수) 기록 Then 4주 × 2항목 모두 0/없음 | 운영 검증 (수동) | 07 런북의 주간 체크리스트 |
| SC-005 | Given 전날 pg_dump 파일 When 빈 PostgreSQL 컨테이너에 복원 → 앱 기동 → GET grid Then 200이고 소요 ≤ `RTO_HOURS`, 복원된 최신 예약 시각과 덤프 시각 차 ≤ `RPO_HOURS` | 운영 리허설 (분기) + CI 스모크(복원 스크립트 실행) | 07 백업 절 |

## 계약 테스트 (05 엔드포인트 표 기준)
- 전 EP: 응답 JSON이 05의 필드 집합과 일치(스키마 스냅샷), 에러는 RFC 9457 `type/title/status` 존재, `detail`에 스택·SQL 문구 없음.
- EP-05: `Idempotency-Key` 재생·428. EP-06: 멱등. EP-11: `cursor`로 2페이지 이어 읽기, `limit>100`은 422.
- 권한 매트릭스: user/admin × EP-09·10·11 → 403/200. 미인증 × 전 `/api/*` → 401.
- 상태 변경 요청에 다른 `Origin` → 403.

## E2E 후보 (2개 — 돈·안전·법 없음, 핵심 여정만)
1. **로그인 → 예약**: magic-link 요청 → 테스트용 메일 캡처(Mailpit)에서 링크 → 그리드 → 빈 칸 클릭 → 종료 선택 → 예약 → 칸이 "내 예약"으로 표시 (SC-002 클릭 카운트 포함).
2. **취소**: 내 예약 목록 → 취소 → 그리드 새로고침 시 빈 칸 (SC-003 화면 확인).
POM: `GridPage`(날짜·칸 locator `data-testid="slot-{roomId}-{HHmm}"`) · `MyBookingsPage` · `LoginPage`. flaky 대책: 임의 `waitForTimeout` 금지, `waitForResponse('/api/grid')` 후 단언, CI retries 1, 실패 시 trace/screenshot 보관. 브라우저는 chromium + mobile-chrome(반응형 확인).

## 리스크 기반 커버리지 목표
| 영역 | 목표 | 이유 |
|---|---|---|
| BookingService.create/cancel + validator | 분기 100% | INV-1~4, P0 경로, 04 위협모델 T·E |
| AuthService(토큰·세션·레이트리밋) | 분기 95% | 04 위협모델 S·D |
| Route Handler 계약 | 전 EP 스키마 스냅샷 1개 이상 | 05 커버리지 |
| 화면 컴포넌트 | 빈/로딩/에러 상태 렌더 테스트만 | frontend-design-taste Pre-Flight |
| 그 외(설정·로그 포맷) | 목표 없음 | 리스크 낮음 |
