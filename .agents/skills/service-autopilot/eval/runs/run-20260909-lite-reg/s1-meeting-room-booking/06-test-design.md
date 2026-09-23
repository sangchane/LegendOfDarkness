# 테스트 설계 — 사내 회의실 예약
버전: v1.1 · 기준 03 v1.1

lite 범위: P0 FR(FR-001·002·003·004·006)과 SC-001~005 전부를 시나리오화. P1·P2(FR-005·007·008·009·010)는 계약 테스트 행으로만 덮는다.
도구: Vitest(단위·통합, 통합은 testcontainers PostgreSQL 16 실 DB — EXCLUDE·23P01은 목으로 검증 불가) · Playwright(E2E) · k6(부하 SC-004).

## 수용 기준 → 시나리오 변환표
| SC/FR ID | Gherkin 시나리오 (Given/When/Then) | 레이어 | 데이터/목킹 |
|---|---|---|---|
| FR-001 | Given 허용 도메인 이메일 / When AUTH-1 / Then 202, magic_links 행 1개(token_hash·expires_at=now+MAGIC_LINK_TTL_MIN), 메일 어댑터 호출 1회 | 통합 | 실 DB, mailer 목 |
| FR-001 | Given 허용 밖 도메인 / When AUTH-1 / Then 422 `email-domain-not-allowed`, 메일 호출 0 (EC-9) | 통합 | mailer 목 |
| FR-001 | Given 미등록이지만 허용 도메인 이메일 / When AUTH-1 / Then 202 (등록 이메일과 응답 본문·지연 동일) | 통합 | — |
| FR-001 | Given 같은 이메일 MAGIC_LINK_RATE 초과 / When AUTH-1 / Then 429 + Retry-After | 통합 | fake timer |
| FR-001 | Given 유효 토큰 / When AUTH-2 / Then 302 `/timeline`, Set-Cookie HttpOnly·Secure·SameSite=Lax, sessions 행 expires_at=now+SESSION_DAYS, magic_links.used_at 채워짐 | 통합 | 실 DB |
| FR-001 | Given 사용된·만료된·위조 토큰 각각 / When AUTH-2 / Then 세 경우 모두 302 `/login?reason=link-expired`, 세션 없음 (EC-8) | 통합 | fake timer |
| FR-001 | Given SMTP 어댑터가 MAIL_RETRY_MAX회 실패 / When AUTH-1 / Then 503 `mail-unavailable`, 로그 `auth.mail_failed` MAIL_RETRY_MAX건 (EC-10) | 통합 | mailer 목 reject |
| FR-001 | Given disabled 사용자 / When AUTH-1 / Then 202이지만 메일 0, 링크 행 0 | 통합 | — |
| FR-002 | Given active 회의실 3·inactive 회의실 1, 오늘 예약 2 / When BK-1 from=to=오늘 / Then active 회의실의 active 예약만, 각 항목에 user.name 포함, email 미포함 | 통합 | 시드 픽스처 |
| FR-002 | Given from~to TIMELINE_RANGE_MAX_DAYS+1일 / When BK-1 / Then 422 `range-too-wide` | 통합 | — |
| FR-002 | Given 달력일 2026-09-10(KST) / When BK-1 / Then 서버가 UTC 2026-09-09T15:00Z~09-10T15:00Z 구간으로 조회 (INV-5) | 단위 | `calendarDayToUtcRange` 순수 함수 |
| FR-002 | Given 타임라인 화면 / When 날짜 이동 / Then 화면당 강조 CTA 1개, 예약 블록 탭 시 예약자 이름 표시 | E2E | Playwright |
| FR-003 | Given 빈 슬롯 / When BK-2 {10:00–10:30} + Idempotency-Key / Then 201 + Location, bookings 행 during=`[10:00,10:30)` status=active | 통합 | 실 DB |
| FR-003 | Given 회의실 A 10:00–11:00 active / When BK-2 회의실 A 10:30–11:30 / Then 409 `booking-overlap`, conflicts에 기존 예약자 이름 (EC-1) | 통합 | 실 DB |
| FR-003 | Given 회의실 A 10:00–11:00 active / When BK-2 회의실 A 11:00–11:30 / Then 201 (반열린 구간 경계 — 겹침 아님) | 통합 | 실 DB |
| FR-003 | Given 회의실 A 10:00–11:00 **cancelled** / When BK-2 회의실 A 10:00–11:00 / Then 201 (부분 제약 WHERE status='active') | 통합 | 실 DB |
| FR-003 | Given 빈 슬롯 / When 동시 100 요청(같은 room·구간, 서로 다른 사용자) / Then 201 정확히 1, 409 99, DB에 active 행 1 (EC-2, SC-002a) | 통합 | Promise.all, 실 DB |
| FR-003 | Given now=14:07 / When start=14:00 / Then 422 `booking-in-past` (EC-3) | 단위 | `validateBookingWindow` fake now |
| FR-003 | When start=now+LEAD_MAX_DAYS+1일 / Then 422 `booking-too-far` (EC-4); start=now+LEAD_MAX_DAYS 정각 / Then 통과 | 단위 | 경계값 |
| FR-003 | When start=10:07 / Then 422 `booking-off-grid`; end−start=BOOKING_MAX_MIN+SLOT_MIN / Then `booking-too-long`; BOOKING_MAX_MIN 정확히 / Then 통과 | 단위 | 경계값 |
| FR-003 | When 07:45–08:15 / Then 422 `booking-outside-hours`; 19:45–20:00 / Then 통과 | 단위 | BUSINESS_HOURS 경계 |
| FR-003 | Given active 예약 MAX_ACTIVE_PER_USER건 / When BK-2 / Then 422 `booking-quota` (EC-5); cancelled는 세지 않음 | 통합 | 픽스처 |
| FR-003 | Given inactive 회의실 / When BK-2 / Then 409 `room-inactive` | 통합 | — |
| FR-003 | Given 같은 Idempotency-Key로 2회 / When BK-2 / Then 두 번째도 201 동일 본문, bookings 행 1 | 통합 | — |
| FR-003 | Given Idempotency-Key 없음 / When BK-2 / Then 400 `idempotency-key-required` | 통합 | — |
| FR-003 | Given 요청 본문에 userId 위조 / When BK-2 / Then 무시하고 세션 user로 생성 (INV-4) | 통합 | — |
| FR-003 | Given 마이그레이션 적용 DB / When `pg_constraint` 조회 / Then `bookings_no_overlap` 존재·contype='x' (04 잔여 리스크 1) | 통합 | 실 DB |
| FR-004 | Given 본인 active 예약 / When BK-3 / Then 200 status=cancelled, cancelled_by=본인, cancelled_at≈now | 통합 | — |
| FR-004 | Given 이미 end 지난 예약 / When BK-3 / Then 409 `booking-ended` (EC-6) | 통합 | fake timer |
| FR-004 | Given member B, A의 예약 / When B가 BK-3 / Then 403 `forbidden`, 로그 `auth.forbidden` (EC-7) | 통합 | — |
| FR-004 | Given 이미 cancelled / When BK-3 재호출 / Then 200 동일 상태(멱등) | 통합 | — |
| FR-004 | Given 취소 직후 / When BK-1 / Then 해당 슬롯 빈 슬롯으로 표시 (US-2) | E2E | Playwright |
| FR-006 | Given admin / When ROOM-2 {name, capacity} / Then 201, ROOM-1에 즉시 포함 | 통합 | — |
| FR-006 | Given member / When ROOM-2 / Then 403 | 통합 | — |
| FR-006 | Given 같은 이름 회의실 존재 / When ROOM-2 / Then 409 `room-name-taken` | 통합 | — |
| FR-006 | Given ROOM_MAX개 존재 / When ROOM-2 / Then 422 `room-limit` | 통합 | — |
| FR-006 | Given 회의실에 active 예약 있음 / When ROOM-3 status=inactive / Then 200, 기존 예약 유지(BK-4에 남음), 신규 BK-2는 409 `room-inactive` | 통합 | — |
| FR-006 | Given 회의실 0개 / When 타임라인 진입 / Then member는 "아직 등록된 회의실이 없어요", admin은 [등록하기] (03 빈 상태) | E2E | Playwright |
| SC-001 | Given 도입 4주째 / When 관리자가 5일간 화이트보드 사진 대조 / Then 화이트보드 기록 0·시스템 예약 ≥1/일 — **운영 측정, 자동 테스트 아님.** 체크리스트를 07 런북에 둔다 | 운영 | — |
| SC-002 | (a) FR-003 동시 100 시나리오 통과 (b) Given 운영 DB / When 주 1회 `SELECT a.id,b.id FROM bookings a JOIN bookings b ON a.room_id=b.room_id AND a.id<b.id AND a.during && b.during WHERE a.status='active' AND b.status='active'` / Then 0행 | 통합 + 운영 | 07 런북 |
| SC-003 | Given 로그인 상태 타임라인 / When 예약 완료까지 사용자 클릭 경로 기록 / Then 결정 지점 = 날짜·회의실 선택(1) → 슬롯 탭(2) → 예약하기(3), 3 이하 | E2E | Playwright 클릭 카운트 |
| SC-004 | Given 회의실 ROOM_MAX·예약 BOOKING_ROWS_PER_YEAR 행·동시 USER_MAX VU / When BK-1 60초 / Then p95 ≤ TIMELINE_P95_MS, 오류 0 | 부하 | k6 + 시드 스크립트 |
| SC-005 | Given 최신 pg_dump / When 07 복원 절차 실행(타이머 시작) / Then RTO_HOURS 내 완료, `SELECT count(*) FROM bookings`가 덤프 시 기록과 일치 | 운영 리허설 | 07 런북 |

## 계약 테스트 (05 엔드포인트 표 기준)
| 항목 | 검사 |
|---|---|
| 스키마 | HTTP 엔드포인트 15개(JOB-1 제외) 응답을 zod 스키마로 검증(camelCase·ISO 8601 `Z`·uuid). email 필드는 AUTH-4·USER-1에서만 존재 |
| 에러 포맷 | 모든 4xx/5xx가 Problem JSON(type·title·status·detail·instance), `type`이 05 slug 목록 21개 중 하나, 본문에 stack·sql 문자열 없음 |
| 인증 | 공개 3개(AUTH-1·AUTH-2·OPS-1) 외 전부 쿠키 없이 401 `unauthenticated` |
| 권한 | ROOM-2·3, USER-1·2·3, ROOM-1?status=all을 member가 호출하면 403 |
| CSRF | POST/PATCH에 Origin 불일치 → 403 |
| 레이트리밋 | BK-1 API_RATE_PER_MIN+1회/분 → 429 + Retry-After |
| 멱등성 | BK-2 동일 키 재시도 → 최초 응답 재생, IDEMPOTENCY_TTL_HOURS 뒤 키 만료(JOB-1) |
| FR-005 | BK-4가 end>now·active·본인 것만, start 오름차순 |
| FR-007 | admin이 타인 예약 BK-3 → 200, cancelled_by=admin id |
| FR-008 | booking.created·booking.cancelled·room.updated·auth.login·auth.failed 로그 event가 actor_id 포함, email 미포함 |
| FR-009 | JOB-1 실행 후: end+RETENTION_DAYS 지난 booking 삭제, 아직인 것 유지; disabled+RETENTION_DAYS 지난 user name/email=`deleted-<id>` |
| FR-010 | DB 정상 → 200 `{db:ok}`, DB 연결 끊김(컨테이너 정지) → 503 |

## E2E 후보 (돈·안전·법 없음 → 핵심 여정 2개)
1. **예약 여정** (US-1, SC-003): 로그인 링크 → 타임라인 → 슬롯 탭 → 예약하기 → 블록에 내 이름 → 다른 브라우저 컨텍스트에서 같은 슬롯 시도 → "이미 잡혔어요" 배너 + 다른 슬롯 제안(막다른 에러 0).
2. **취소·관리자 여정** (US-2·3): 내 예약 → 취소하기 → 타임라인 빈 슬롯 / admin 회의실 등록 → 타임라인 행 추가 → 비활성화 → 예약 시도 409.
E2E는 매직링크를 테스트 전용 `MAIL_TRANSPORT=file`로 받아 링크를 파일에서 읽는다.

## 리스크 기반 커버리지 목표
| 영역 | 목표 | 이유 |
|---|---|---|
| `validateBookingWindow`·`calendarDayToUtcRange` (INV-2·5) | 분기 100% | 순수 함수, 경계값이 곧 버그 |
| booking 모듈 + DDL 제약 (INV-1) | 통합 시나리오 전부 + 제약 존재 테스트 | 위협모델 상위 리스크 1 |
| auth 모듈 (S·E) | 통합 시나리오 전부 | 상위 리스크 2 |
| 화면 컴포넌트 | E2E 2개로 대체, 단위 테스트 없음 | 내부 도구, 화면은 얇다 |
| 전체 라인 커버리지 | 목표 수치 없음 | tdd-workflow의 일률 80% 대신 위 리스크 기반(skill-routing 충돌 규칙) |
