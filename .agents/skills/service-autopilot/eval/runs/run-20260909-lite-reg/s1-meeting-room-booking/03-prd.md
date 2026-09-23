# PRD — 사내 회의실 예약 (meeting-room-booking)
버전: v1.1
개정: v1.1 — GATE 패치 R1: 04~07에 값으로 흩어져 있던 상수 10개를 상수 표로 이관하고 하류는 이름 참조로 교체 (DL-22)

> 근거 (진입 사전조사 3줄 — 웹 예산 소진으로 01의 근거를 재사용)
> - 정량: 상용 대안 Skedda EMEA/APAC Starter $99/월(공간 15) — https://www.skedda.com/pricing (2026-09-09). 자체 구축의 비용 상한 참고치
> - 정성: MRBS README — "physical booking systems를 대체하는 중앙 디지털 솔루션" (https://github.com/meeting-room-booking-system/mrbs-code, 2026-09-09). 화이트보드 대체가 이 도구 범주의 원형 요구
> - 사용자 영향: 임직원은 회의실 앞에 가지 않고 폰·PC에서 빈 슬롯을 보고 3단계 안에 예약한다 — ux-principles L-03(화면당 결정 1개)·L-07(계산·기억 0)

## 배경
지금은 화이트보드에 회의실×시간 칸을 수기로 적는다. 사무실 밖에서 못 보고, 취소가 전파되지 않고, 겹쳐 쓴 기록으로 분쟁 시 근거가 없다 (01 업무 흐름). 무료 OSS(MRBS·LibreBooking)가 같은 요구를 덮지만, 화이트보드 수준의 무설명 UX와 사내 표준 스택(TypeScript)을 이유로 자체 구축을 설계한다 — 단 착수 전 MRBS 데모 30분 사용이 선행 조건(DL-4).

**역량 한 문단 (product-capability)**: 임직원 ≤USER_MAX명이 폰·PC 브라우저에서 사내 이메일 매직링크로 로그인해, 회의실 ≤ROOM_MAX개의 타임라인에서 빈 SLOT_MIN 단위 슬롯을 골라 예약하고 본인 예약을 취소한다. 관리자 1명은 회의실을 등록·비활성화하고 타인 예약을 정리한다. 배포 후 바뀌는 결과: 화이트보드가 철거되고, 이중예약이 DB 제약으로 0건이 되며, 누가 언제 잡고 취소했는지 기록이 남는다.

## 제품 목표 (≤2, 직교)
- G1. 화이트보드를 4주 안에 완전히 대체한다 (SC-001).
- G2. 이중예약을 구조적으로 0건으로 만든다 — 앱 로직이 아니라 DB 제약으로 (SC-002).

## 유저 스토리 (≤3, P1만으로 MVP 성립)
| ID | 우선순위 | 스토리 | 독립 테스트 |
|---|---|---|---|
| US-1 | P1 | As a 임직원, I want 회의실 타임라인에서 빈 슬롯을 골라 예약하고 싶다, so that 회의실 앞에 가지 않고 자리를 확보한다 | 로그인 → 타임라인 → 슬롯 선택 → 예약 확인까지 E2E |
| US-2 | P1 | As a 임직원, I want 내 예약을 보고 취소하고 싶다, so that 안 쓰는 방이 바로 빈 방으로 돌아간다 | 내 예약 목록에서 취소 → 타임라인에서 즉시 빈 슬롯 |
| US-3 | P1 | As a 관리자, I want 회의실을 등록·비활성화하고 타인 예약을 정리하고 싶다, so that 회의실 구성 변경과 노쇼 정리를 화이트보드 지우듯 할 수 있다 | 회의실 추가 → 타임라인 노출 / 비활성화 → 신규 예약 불가·기존 예약 유지 |

## 용어
회의실(room) · 예약(booking) · 슬롯(slot, SLOT_MIN 분 격자) · 취소(cancel — booking 상태 전이 active→cancelled, 행 삭제 아님) · 비활성화(deactivate — room 상태 전이, 삭제 아님) · 관리자(admin) · 임직원(member).

## 불변식 (INV — 구현이 반드시 지켜야 하는 것)
- INV-1. 같은 room의 active 예약 두 건의 `[start, end)` 구간은 겹치지 않는다. **DB EXCLUDE 제약으로 보장**(05 DDL). 앱 검사는 UX용 사전 안내일 뿐 최종 방어가 아니다.
- INV-2. start < end, 둘 다 SLOT_MIN 격자 위, end − start ≤ BOOKING_MAX_MIN, start ≥ 현재 시각(서버 기준), start ≤ 현재 + LEAD_MAX_DAYS, 구간이 BUSINESS_HOURS 안.
- INV-3. 예약·회의실은 하드삭제하지 않는다 — 상태 전이만. 하드삭제는 FR-009 파기 잡만 수행.
- INV-4. 권한 판단은 서버 세션의 role로만 한다. 클라이언트가 보낸 role·user_id는 신뢰하지 않는다.
- INV-5. 시각은 UTC로 저장, 표시는 Asia/Seoul. 클라이언트 시계로 INV-2를 판정하지 않는다.

## 상태와 전이
- booking: `active` —(본인 또는 admin 취소)→ `cancelled`. 종료 시각 경과는 상태가 아니라 조회 필터(과거 예약). cancelled에서 되돌리기 없음(새로 예약).
- room: `active` —(admin)→ `inactive` —(admin)→ `active`. inactive room에는 신규 예약 불가, 기존 active 예약은 유지·취소 가능.
- user: `active` —(admin)→ `disabled` (퇴사). disabled는 로그인 불가, 기존 예약은 관리자가 정리.

## 요구사항 풀 (≤10)
| ID | 요구사항 (EARS) | 우선순위 | 출처 |
|---|---|---|---|
| FR-001 | 사내 이메일 도메인(ALLOWED_EMAIL_DOMAINS)의 주소를 입력하면, 시스템은 MAGIC_LINK_TTL_MIN분 유효한 단회 링크를 메일로 보내고, 클릭 시 SESSION_DAYS일 세션을 연다. 같은 이메일에는 MAGIC_LINK_RATE 상한을 적용한다 | P0 | US-1·2·3, INV-4 |
| FR-002 | 로그인한 사용자가 날짜를 고르면, 시스템은 active 회의실 전부의 그날 BUSINESS_HOURS 타임라인을 SLOT_MIN 격자로 보여주고, 각 예약에 예약자 이름을 표시한다. TIMELINE_RANGE_MAX_DAYS일까지의 주간 보기도 제공한다 | P0 | US-1 |
| FR-003 | 사용자가 room·start·end를 제출하면, 시스템은 INV-2를 검증하고 INV-1(DB EXCLUDE)로 겹침을 거부하며, 성공 시 예약을 active로 만든다. 활성 예약이 MAX_ACTIVE_PER_USER를 넘으면 거부한다 | P0 | US-1, INV-1·2 |
| FR-004 | 예약 소유자가 취소를 요청하면, 시스템은 booking을 cancelled로 전이하고 cancelled_by·cancelled_at을 기록한다. 이미 종료된 예약은 취소할 수 없다 | P0 | US-2, INV-3 |
| FR-005 | 사용자가 내 예약을 열면, 시스템은 본인의 예정 예약(active, end > now)을 시작 시각 순으로 보여준다 | P1 | US-2 |
| FR-006 | admin이 회의실을 등록·이름 변경·수용 인원 입력·비활성화/재활성화하면, 시스템은 즉시 타임라인에 반영한다. inactive 회의실은 신규 예약을 거부한다 | P0 | US-3, INV-3 |
| FR-007 | admin이 타인의 active 예약을 취소하면, 시스템은 FR-004와 같이 전이하되 cancelled_by에 admin을 기록한다 | P1 | US-3 |
| FR-008 | 예약 생성·취소·회의실 변경·로그인 성공/실패가 일어나면, 시스템은 actor·대상·시각을 booking/room 행의 감사 필드와 구조화 로그에 남긴다 | P1 | 축 R, 01 분쟁 기록 |
| FR-009 | 매일 1회, 시스템은 end 또는 cancelled_at이 RETENTION_DAYS를 지난 booking을 하드삭제하고, disabled 후 RETENTION_DAYS가 지난 user의 이름·이메일을 익명화한다 | P2 | DL-9 |
| FR-010 | 운영자가 `/healthz`를 호출하면, 시스템은 DB 연결 확인 후 200/503을 돌려준다 | P2 | 07 관측성 |

## 엣지케이스 (Given/When/Then)
**예약 (FR-003)**
- EC-1. Given 회의실 A에 10:00–11:00 active 예약이 있다 / When 다른 사용자가 A에 10:30–11:30을 제출한다 / Then 409 `booking-overlap`을 받고, 타임라인에 겹친 예약자 이름이 표시된다 (막다른 에러 0 — 다른 슬롯 제안).
- EC-2. Given 두 사용자가 같은 빈 슬롯을 동시에 제출한다 / When 두 INSERT가 같은 트랜잭션 창에 도달한다 / Then 정확히 1건만 active가 되고 나머지는 DB 제약 위반(23P01)을 409로 받는다.
- EC-3. Given 현재 14:07 / When 14:00–14:30을 제출한다 / Then INV-2(start ≥ now) 위반으로 422 `booking-in-past`.
- EC-4. Given LEAD_MAX_DAYS=14 / When 15일 뒤 슬롯을 제출한다 / Then 422 `booking-too-far`.
- EC-5. Given 사용자의 active 예약이 MAX_ACTIVE_PER_USER건 / When 하나 더 제출 / Then 422 `booking-quota`와 내 예약 링크.
**취소 (FR-004·007)**
- EC-6. Given 예약이 이미 end를 지났다 / When 소유자가 취소한다 / Then 409 `booking-ended` — 과거 기록은 유지.
- EC-7. Given member B가 A의 예약 id를 안다 / When B가 취소 API를 호출한다 / Then 403 `forbidden`, 로그에 시도 기록(FR-008).
**로그인 (FR-001)**
- EC-8. Given 링크가 MAGIC_LINK_TTL_MIN을 지났거나 이미 쓰였다 / When 클릭 / Then 로그인 화면으로 돌아오며 "새 링크를 보내 드릴게요" 버튼 (복구 경로).
- EC-9. Given 허용 도메인 밖 이메일 / When 제출 / Then 422 `email-domain-not-allowed`, 메일은 보내지 않는다.
- EC-10. Given SMTP 장애 / When 링크 요청 / Then 503 `mail-unavailable` + "관리자에게 링크 요청" 안내, 서버는 MAIL_RETRY_MAX회 재시도 후 로그.

## 성공 기준 (≤5, 전부 pass/fail)
| ID | 기준 | 측정 방법 |
|---|---|---|
| SC-001 | 도입 4주째 주간에 화이트보드 기록 0건, 시스템 예약 ≥ 1건/업무일 | 관리자가 4주째 매일 화이트보드를 사진으로 남겨 대조 (5일 전부 0건이면 pass) |
| SC-002 | 이중예약 0건: 같은 room에 겹치는 active 예약 행이 존재하지 않는다 | (a) 06의 동시 100요청 테스트에서 성공 1건 (b) 운영 DB에 겹침 조회 쿼리 주 1회 0행 |
| SC-003 | 예약 완료까지 사용자 결정 지점 ≤ 3 (날짜/회의실 선택 → 슬롯 선택 → 예약하기) | 화면 플로우에서 결정 지점 카운트 (quality-decomposition UX-01) |
| SC-004 | 타임라인 조회 p95 ≤ TIMELINE_P95_MS (동시 USER_MAX 사용자, 회의실 ROOM_MAX, 예약 BOOKING_ROWS_PER_YEAR 행) | 06 부하 시나리오, 서버 로그 latency 히스토그램 |
| SC-005 | 최신 백업에서 RTO_HOURS 내 복원 성공, 복원 후 예약 건수가 덤프 시점과 일치 | 분기 리허설 (07 절차) |

## UI 방향
- frontend-design-taste dial: **밀도 중간 · 모션 낮음 · 색 1개 강조(예약하기 CTA) · 다크 없음(1벌)**. 프로파일 "내부 도구".
- 원칙 ID: L-03 화면당 결정 1개 · L-06 막다른 에러 0(모든 에러에 다음 행동 버튼) · L-07 겹침·격자 계산은 시스템이 · L-10 클릭 피드백 ≤ 400ms · T-08 버튼 라벨은 다음 행동("예약하기"·"취소하기") · T-09 해요체 · T-04 진입 시 모달 없음.
- UX-01 (근거 L-03): 예약 결정 지점 ≤ 3 = SC-003.

## 화면 스케치 (핵심 화면 1 — 타임라인, 폰 세로 기준)
```
+------------------------------+
| 9/9 (화)  < 오늘 >   [내 예약] |
|------------------------------|
|        09  10  11  12  13  14|
| 회의실A ....####........####.|  ####=예약(탭하면 예약자 이름)
| 회의실B ........########.....|  ....=빈 슬롯(탭->아래 시트)
| 회의실C .....................|
|------------------------------|
| ^ 시트: 회의실A 10:00-10:30    |
|   [끝 시각 v 11:00]           |
|   [ 예약하기 ]                |
+------------------------------+
빈 상태: "아직 등록된 회의실이 없어요. 관리자에게 알려 주세요" (member) / "첫 회의실을 등록해 보세요 [등록하기]" (admin)
로딩: 행 스켈레톤 3줄 (400ms 안에 표시)
에러: 인라인 배너 + "다시 불러오기" 버튼
```

## 범위 밖 (non-goals)
반복 예약 · 참석자 초대·알림 메일 · Google/Outlook 캘린더 동기화 · SSO/IdP 연동 · 장비(빔·화상) 예약 · 멀티사이트/층 평면도 · 회의실 앞 태블릿·키오스크 · 체크인/노쇼 자동 해제 · 승인 워크플로우 · 다국어.

## 가정 목록
02 register의 Assumed 전부: 규모(DL-2) · 인증 매직링크(DL-7) · 배포 단일 호스트(DL-8) · 보존기간(DL-9) · 팀 언어 TypeScript(DL-5) · 사용 맥락 PC+폰 반응형(00 해석 4) · 업무시간·슬롯·최대 길이·선예약 기간(아래 상수, DL-11).

## 상수 표 (단일 출처 — 04~07은 이름으로만 참조)
| 이름 | 값 | 단위 | 근거 |
|---|---|---|---|
| ROOM_MAX | 10 | 개 | 설계 결정 DL-2 |
| USER_MAX | 50 | 명 | 설계 결정 DL-2 |
| SLOT_MIN | 15 | 분 | 설계 결정 DL-11 |
| BOOKING_MAX_MIN | 240 | 분 | 설계 결정 DL-11 |
| LEAD_MAX_DAYS | 14 | 일 | 설계 결정 DL-11 |
| BUSINESS_HOURS | 08:00–20:00 | Asia/Seoul | 설계 결정 DL-11 |
| MAX_ACTIVE_PER_USER | 10 | 건 | 설계 결정 DL-11 |
| ALLOWED_EMAIL_DOMAINS | 환경변수(.env) | 목록 | 설계 결정 DL-7 |
| MAGIC_LINK_TTL_MIN | 10 | 분 | 설계 결정 DL-7 |
| MAGIC_LINK_RATE | 3회/10분/이메일 | — | 설계 결정 DL-7 |
| SESSION_DAYS | 30 | 일 | 설계 결정 DL-7 |
| RETENTION_DAYS | 365 | 일 | 설계 결정 DL-9 (법령 원문은 08 선행 조건에서 확인 → SC에 쓰지 않음) |
| TIMELINE_P95_MS | 1000 | ms | 설계 결정 DL-12 (ux-principles L-10 400ms 피드백 + 스켈레톤 허용) |
| BACKUP_RETENTION_DAYS | 14 | 일 | 설계 결정 DL-8 |
| RPO_HOURS | 24 | 시간 | 설계 결정 DL-8 |
| RTO_HOURS | 4 | 시간 | 설계 결정 DL-8 |
| LOG_RETENTION_DAYS | 90 | 일 | 설계 결정 DL-12 |
| TIMELINE_RANGE_MAX_DAYS | 7 | 일 | 설계 결정 DL-22 |
| API_RATE_PER_MIN | 60 | 회/분/사용자 | 설계 결정 DL-22 |
| MAIL_RETRY_MAX | 3 | 회 | 설계 결정 DL-22 |
| IDEMPOTENCY_TTL_HOURS | 24 | 시간 | 설계 결정 DL-16 (blindspot-checklists의 Stripe 멱등성 관행 참고) |
| TOKEN_BYTES | 32 | 바이트 | 설계 결정 DL-22 |
| BOOKING_ROWS_PER_YEAR | 50000 | 행 | 설계 결정 DL-2 (ROOM_MAX × 20건/일 × 250일 추정) |
| SLO_AVAILABILITY_PCT | 99 | %/월 (BUSINESS_HOURS 기준) | 설계 결정 DL-20 |
| SLO_5XX_MAX_PCT | 0.5 | % | 설계 결정 DL-20 |
| BACKUP_DEADMAN_HOURS | 26 | 시간 | 설계 결정 DL-8 |
| DISK_FREE_MIN_PCT | 15 | % | 설계 결정 DL-20 |

## 미결정
0건.
