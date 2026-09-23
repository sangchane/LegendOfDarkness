# 테스트 설계 — RoomBook (사내 회의실 웹 예약)

> 근거 (A6 진입 사전조사, 2026-09-03)
> - **정량**: 테스트 피라미드 — 단위 다수·통합 소수·E2E 최소, 좁은 통합 테스트는 실제 의존성을 로컬에서 띄워 검증 — https://martinfowler.com/articles/practical-test-pyramid.html (확인 2026-09-03). 실제 PostgreSQL 컨테이너로 통합 테스트: `testcontainers` + `@testcontainers/postgresql`, Vitest globalSetup — https://testcontainers.com/guides/getting-started-with-testcontainers-for-nodejs/ (확인 2026-09-03)
> - **정성**: "목(mock)이 놓치는 트랜잭션·제약 관련 통합 버그를 잡는다" — Testcontainers Postgres 가이드 (https://qaskills.sh/blog/testcontainers-postgres-node-guide, 확인 2026-09-03). INV-1(EXCLUDE 제약)은 목으로 검증 불가 → 실제 DB 필수.
> - **사용자 영향**: 에러 시나리오의 Then은 사용자가 보는 문구·복구 버튼까지 명시한다(UX-02 막다른 에러 0, L-06).

## 원칙
- AC는 개발 시작 전에 존재한다. 각 시나리오는 처음엔 **반드시 실패(RED)** 해야 하며, RED가 확인되기 전에는 프로덕션 코드를 쓰지 않는다 (ecc:tdd-workflow RED 게이트). Git 체크포인트: `test:` RED → `fix:` GREEN → `refactor:`.
- 피라미드 목표 비율 unit : integration(+contract) : E2E ≈ 70 : 20 : 10. "가능한 한 아래층으로" — 하위에서 검증된 것을 상위에서 반복하지 않는다.
- **INV-1은 반드시 실제 PostgreSQL에서** 검증한다(EXCLUDE 제약은 목으로 재현 불가). 통합 테스트는 Testcontainers로 마이그레이션까지 적용한 임시 DB 사용.
- 커버리지는 리스크 기반(아래 표). ecc:tdd-workflow의 "80% 일률"은 채택하지 않는다(라우팅 충돌 규칙).
- 시간 의존 로직은 시계 주입(`now()` 파라미터화)으로 단위 테스트. 메일은 outbox 테이블을 직접 읽어 검증(SMTP 목 불필요), E2E는 Mailpit 컨테이너.

## 수용 기준 → 시나리오 변환표
레이어: U=unit · I=integration(실 DB) · C=contract(API 스키마) · E=E2E(Playwright)

### 성공 기준 (SC)
| ID | 근거 | Gherkin | 레이어 | 데이터/목킹 |
|---|---|---|---|---|
| TS-001 | SC-001 | Given 회의실 R에 예약 없음 When 같은 구간 14:30~15:30 요청 100건을 동시에(Promise.all) 보내면 Then `confirmed`는 정확히 1건, 99건은 409 `booking-overlap` | I | Testcontainers PG, 사용자 100명 시드 |
| TS-002 | SC-002 | Given 로그인 상태 When 그리드 빈 칸 클릭 → 제목 입력 → "이 시간 예약하기" Then 3번의 사용자 결정으로 예약 블록이 그리드에 나타난다 | E | Mailpit, 시드 회의실 3개 |
| TS-003 | SC-003 | Given 예약 500건·회의실 30개 시드 When `POST /api/bookings` 200회 Then 서버 처리 p95 ≤ 1,000ms; `GET /api/bookings?from&to` p95 ≤ 300ms | I (perf) | k6, CI nightly |
| TS-004 | SC-004·005 | (운영 지표 — 자동 테스트 대상 아님) 파일럿 4주 후 총무 점검표로 판정 | — | 08 핸드오프에 운영 체크로 명시 |
| TS-005 | SC-006 | Given 매직링크 요청 When outbox 워커가 처리하면 Then 발송 `sent_at - created_at` p95 ≤ 60s (워커 폴링 5s 기준) | I | Mailpit, 워커 프로세스 실행 |
| TS-006 | SC-007 | Given 앱 기동 When `GET /api/health` Then 200 `{db:"ok"}`; DB 중단 시 503 | I | PG 컨테이너 stop/start |
| TS-007 | SC-008 | Given 에러 상태 목록(EC 전부) When 각 화면 렌더 Then 모든 에러 컴포넌트에 `data-testid="recovery-action"` 버튼이 존재 | U (컴포넌트) | RTL, Problem 픽스처 |

### 기능 요구사항 (P0·P1 FR 전부)
| ID | 근거 | Gherkin | 레이어 | 데이터/목킹 |
|---|---|---|---|---|
| TS-010 | FR-001 | Given 허용 도메인 `corp.example`, `a@corp.example` 미등록 When 링크 요청 Then 202, app_user 1건 JIT 생성(role=member, name="a"), login_token 1건(해시 저장·원문 없음·expires_at=+15m), outbox `magic_link` 1건(priority=high) | I | 시계 고정 |
| TS-011 | FR-001 | Given 유효 토큰 When `GET /auth/verify?token=` Then 200 확인 페이지, used_at 여전히 null; When `POST /auth/verify` Then 302 `/`, Set-Cookie HttpOnly·Secure·SameSite=Lax, session expires_at=+8h(admin 사용자는 +2h), used_at 기록 | I | member·admin 2케이스 |
| TS-012 | FR-002 | Given 회의실 3개·예약 2건 When `GET /api/bookings?from=월&to=일` Then 2건 반환, 각 항목에 title·owner.name 포함 | I | 시드 |
| TS-013 | FR-002 | Given 그리드 렌더 When 주 이동 버튼 클릭 Then from/to가 7일 이동하고 "오늘" 버튼으로 복귀 | U | RTL, fetch 목 |
| TS-014 | FR-003 | Given 빈 구간 When 유효 요청 Then 201 + Location + `status=confirmed`, audit `create` 1건, outbox `booking_confirmed` 1건 (같은 트랜잭션) | I | — |
| TS-015 | FR-003 | Given R 14:00~15:00 존재 When R 14:30~15:30 요청(60분) Then 409 + `suggestion={15:00,16:00}` (같은 길이의 가장 가까운 빈 구간) | I | — |
| TS-016 | FR-004 | Given 직원 A의 예약 When B가 조회 Then `owner.email` 없음; A 본인·admin 조회 시 있음 | I | 사용자 3명 |
| TS-017 | FR-005 | Given A의 미래 예약 When A가 PATCH title Then 200, audit `update` before/after 기록 | I | — |
| TS-018 | FR-005 | Given A의 미래 예약 When B가 PATCH Then 403 `forbidden`; When admin이 PATCH Then 200 | I | — |
| TS-019 | FR-006 | Given 14:00~15:00 confirmed, now=14:20 When 소유자 release Then 200, period upper=14:20, status released; 이어서 다른 사용자의 14:15~15:00 신규 예약 201(규칙 (a)(d) 통과, released 구간과의 겹침은 EXCLUDE 대상 아님), 14:20~15:00 요청은 422 `slot_alignment` | I | 시계 고정 |
| TS-020 | FR-007 | Given 예정 3건·31일 전 1건·10일 전 1건 When `GET /api/me/bookings?scope=past` Then 10일 전 1건만; `upcoming` 3건 시간순 | I | 시계 고정 |
| TS-021 | FR-008 | Given admin When 회의실 등록(이름·층·수용 4·features) Then 201, `GET /api/rooms`에 노출; 비활성화 시 그리드 목록에서 사라지고 미래 예약 소유자에게 outbox `room_deactivated` | I | — |
| TS-022 | FR-008 | Given 운영시간 PUT 08:00~20:00 When 07:30 예약 요청 Then 422 `business-hours` | I | — |
| TS-023 | FR-009 | Given 예약 생성→변경→취소 When admin이 `GET /api/admin/bookings/{id}/audit` Then 3건 시간순, 각 actor·before·after | I | — |
| TS-024 | FR-009 | Given 앱 DB 계정 When `UPDATE booking_audit` 시도 Then 권한 오류(42501) | I | roombook_app 계정 |
| TS-025 | FR-010 | Given SMTP 불가(Mailpit 중단) When 예약 생성 Then 201(예약 성립), outbox attempts 증가·next_attempt_at 백오프(5s→25s→125s), 3회 후 `failed`; Mailpit 복구 후 재시도 성공 | I | Mailpit stop/start |
| TS-026 | FR-011 | 정책 8항목 각각 (15분 경계 위반 / start≥end / 241분 / 과거 / 61일 후 / 운영시간 밖 / 비활성 방 / 미래 confirmed 21건째) When 요청 Then 422 `policy-violation` + `errors[].code` 해당 값 | U (PolicyValidator, 시계 주입) | 테이블 드리븐 8케이스 |
| TS-032 | FR-011 | Given R 14:00~15:00 예약, 운영시간 22:00 종료, R의 15:00~22:00 전부 예약됨 When R 14:30~15:30 요청 Then 409 + `suggestion: null` (후보가 운영시간 규칙을 통과 못 함) | I | 시드 |
| TS-033 | FR-003 | Given INSERT가 23P01로 실패 When 대안 탐색 Then 새 트랜잭션에서 수행되어 25P02 없이 응답(로그에 25P02 0건) | I | — |
| TS-027 | FR-011 | Given `2026-09-07T14:00+09:00` 입력 When 검증 Then Asia/Seoul 기준 15분 경계 통과(UTC 05:00), 경계 계산이 오프셋에 무관 | U | — |
| TS-028 | FR-012 | Given 뷰포트 375px When 그리드 렌더 Then 1일 뷰, 슬롯 높이 ≥ 32px | U + E(mobile-chrome) | — |
| TS-029 | FR-013 | Given 세션 When `POST /api/auth/logout` Then 204, 같은 쿠키로 `GET /api/me` 401; 만료 세션도 401 | I | 시계 고정 |
| TS-030 | FR-014 | Given admin이 타인 예약 취소 When reason 없음 Then 422 `reason-required`; reason 있음 Then 200, audit `admin_cancel(reason)`, outbox `booking_cancelled` payload에 reason | I | — |
| TS-031 | FR-015 | Given 사용자 비활성화 90일 경과 When RetentionJob(`roombook_retention` 롤) 실행 Then name=`(퇴사자)`, email=`anon-{id}@invalid`, anonymized_at 기록, 예약 이력은 유지; 1년 경과 booking·audit 삭제 성공(같은 삭제를 `roombook_app`으로 시도하면 42501 — TS-024와 정합) | I | 시계 고정, 롤 2개 |
| TS-058 | FR-015·EC-C7 | Given 미래 confirmed 예약 2건 보유 사용자 When admin이 `is_active=false` Then 예약 2건 `cancelled`(cancel_reason="퇴사 처리"), audit `admin_cancel` 2건, 그 사용자 세션 전부 삭제, 이후 매직링크 요청 202이나 토큰 미발급 | I | — |

### 엣지케이스 (03-prd EC 전부)
| ID | 근거 | Gherkin 요약 | 레이어 |
|---|---|---|---|
| TS-040 | EC-A1 | `x@gmail.com` 요청 → 422 `invalid-email-domain`, 응답 시간이 유효 도메인 기존 사용자 요청과 ±50ms 이내(열거 방지) | I |
| TS-041 | EC-A2 | 발급 16분 후 "로그인하기"(POST) → 302 `/login?error=expired-link`, 화면에 "새 링크 받기" 버튼 | I + U |
| TS-042 | EC-A3 | 같은 토큰 POST 2회 → 두 번째 302 expired, 첫 세션 유지 | I |
| TS-043 | EC-A4 | 10분 내 6번째 요청 → 429 + `Retry-After` | I |
| TS-059 | EC-A5 | 같은 링크 GET 3회 후 POST 1회 → GET 3회 모두 200·used_at null, POST 302 로그인 성공 | I |
| TS-044 | EC-B1 | = TS-001 (2명 동시) | I |
| TS-045 | EC-B2·B3 | now=14:07: 14:00~14:30 허용 / 13:45~14:15 거부 `past` | U |
| TS-046 | EC-B4·B5·B6·B7 | 255분 / 21:45~22:15 / 비활성 방 / 61자 제목 → 각 422 코드, UI는 입력값 보존·필드 인라인 오류 | U + U(컴포넌트) |
| TS-047 | EC-B8 | = TS-025 + `GET /api/me` `mail_degraded=true` + UI 배너 "메일이 늦게 갈 수 있어요" | I + U |
| TS-054 | EC-B9 | 미래 confirmed 20건 보유 → 21번째 422 `active-limit`, UI 문구 + 내 예약 링크 | I + U |
| TS-055 | EC-B10 | = TS-032 + UI "다른 회의실을 볼까요?" + 회의실 목록 | I + U |
| TS-048 | EC-C1 | 타인 예약 카드에 취소 버튼 미렌더(can_edit=false) + API 403 | U + I |
| TS-049 | EC-C2 | 진행 중 예약 cancel → 409 `booking-not-cancellable`, UI가 "지금 반납하기"로 안내 | I + U |
| TS-050 | EC-C3 | = TS-019 | I |
| TS-051 | EC-C4 | 변경 대상 구간에 타인 예약 → 409, 원 예약 불변 | I |
| TS-052 | EC-C5 | = TS-030 | I |
| TS-053 | EC-C6 | 종료된 예약 cancel → 409 `booking-already-closed`; 종료된 예약 patch → 409 `booking-not-editable` (두 단언 분리) | I |

| TS-056 | 04 outbox | Given 워커가 발송 중 강제 종료(sending 잔존) When 워커 재기동 Then 2분 경과분은 pending 복귀 후 1회만 발송(Mailpit 수신 1통) | I |
| TS-057 | 04 outbox | Given normal 50건 pending·high 1건 When 워커 1사이클 Then high(magic_link)가 먼저 발송 | I |

누락 확인: SC 8/8 · P0/P1 FR 15/15 · EC 22/22 → **누락 0**.

## 계약 테스트 (A5 엔드포인트 표 기준)
| 대상 | 검증 |
|---|---|
| 전 EP 응답 | `openapi.yaml` 스키마 검증(응답 본문·상태코드), `API-Version: 1` 헤더 존재 |
| 에러 | 4xx/5xx는 `application/problem+json`, `type`이 `https://roombook.internal/problems/` 로 시작, 본문에 `stack`·`sql` 문자열 없음 |
| EP-07 멱등성 | 같은 `Idempotency-Key` 2회 → 두 번째는 최초 201 본문·상태 재생, DB에 예약 1건; 본문 다르면 422 `idempotency-key-reused`; 키 없으면 400 |
| CSRF | `X-Requested-With` 없는 POST → 403; `Origin` 불일치 → 403 |
| 레이트리밋 | 61번째 요청 → 429, `X-RateLimit-Remaining: 0` |
| 권한 | member가 `/api/admin/*` 전부 → 403; 요청 본문 `role:"admin"` 주입 시 무시(INV-4) |
| 페이지네이션 | EP-12·16 `next_cursor` 로 끝까지 순회 시 중복·누락 0 |

## E2E 후보 (Playwright, 핵심 여정 3개만)
| ID | 여정 | 단계 | 판정 |
|---|---|---|---|
| E2E-1 | 로그인 → 예약 | 이메일 입력 → Mailpit API로 링크 추출 → 방문(확인 페이지) → "로그인하기" → 그리드 → 빈 칸 클릭 → 제목 → "이 시간 예약하기" | 그리드에 블록·내 예약에 1건, 스크린샷 |
| E2E-2 | 충돌 → 대안 채택 | 이미 찬 칸 시도 → 409 안내 "15:30~16:30은 비어 있어요" → 제안 버튼 클릭 | 제안 시간으로 예약 성립 |
| E2E-3 | 반납 → 재예약 | 진행 중 예약 "지금 반납하기" → 다른 사용자가 같은 방을 현재 15분 슬롯 시작(예: 14:15)부터 예약 | 두 번째 사용자 201 |
- POM: `LoginPage`(emailInput, sendButton) · `GridPage`(weekNav, slot(room,time), bookingBlock) · `BookingDialog`(titleInput, submit, suggestionButton) · `MyBookingsPage`(releaseButton). 셀렉터는 `data-testid`만.
- flaky 대책: `waitForTimeout` 금지, `waitForResponse('/api/bookings')` 사용; 시계는 `page.clock.setFixedTime`; 메일은 Mailpit REST 폴링(최대 10s); 테스트별 고유 회의실·사용자 시드로 격리; CI `retries: 2`, `trace: on-first-retry`; 반복 실패는 `test.fixme(이슈#)`로 격리 후 24h 내 수정.
- 프로젝트: chromium + mobile-chrome(FR-012). webkit/firefox는 사내 표준 브라우저가 Chrome/Edge라는 가정(Assumed)으로 제외.

## 리스크 기반 커버리지 목표
| 영역 | 목표 | 이유 |
|---|---|---|
| PolicyValidator·시간 계산(U) | 분기 100% | P0, 시간대·경계 버그가 가장 흔함 |
| BookingService 생성/변경/취소/반납 + INV-1 (I) | 시나리오 100% (TS-001·014·015·019·051) | G1 핵심, 위협모델 T·R |
| AuthService 매직링크·세션 (I) | 시나리오 100% + 열거 방지 타이밍 | 위협모델 상위 리스크 1 |
| 권한(IDOR·admin 경로) (I/C) | 전 EP × member/admin 매트릭스 100% | 위협모델 E·T |
| 아웃박스·워커 (I) | 정상·재시도·실패 3경로 | FR-010, 메일 장애 = 로그인 불가 |
| UI 컴포넌트 (U) | 에러 상태 100%(TS-007), 그 외 라인 ≥ 60% | SC-008 |
| 관리자 CRUD·설정 (I) | 정상 경로 + 권한 | P1, 저빈도 |
| E2E | 3 여정만 | 돈·법 없음, 핵심 여정만 |
전체 라인 커버리지는 보고만 하고 게이트로 쓰지 않는다. 게이트 = 위 표의 시나리오 100% + RED→GREEN 이력.
