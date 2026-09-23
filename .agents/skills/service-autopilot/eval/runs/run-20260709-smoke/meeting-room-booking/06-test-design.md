# Test Design - 사내 회의실 웹 예약

## 원칙

- 각 수용 기준은 구현 전에 실패하는 테스트로 먼저 작성한다.
- 하위 계층에서 검증 가능한 조건은 상위 E2E에서 반복 검증하지 않는다.
- 예약 충돌, 권한, 마스킹, 상태 전이는 회귀 위험이 높으므로 unit/integration/E2E를 모두 둔다.

## 수용 기준 시나리오 변환

| SC/FR ID | Gherkin 시나리오 | 레이어 | 데이터 모의 |
|---|---|---|---|
| FR-001 | Given 6인실 A와 12인실 B가 있고 B만 프로젝터가 있을 때, When 사용자가 8명+프로젝터 조건으로 조회하면, Then B만 예약 가능 후보로 표시된다. | integration, E2E | room fixture |
| FR-002 | Given 회의실 A에 10:00-11:00 예약이 있을 때, When 다른 사용자가 10:30-11:30 예약을 생성하면, Then 409 conflict가 반환된다. | integration | DB |
| FR-002 | Given 두 사용자가 같은 시간의 빈 회의실을 동시에 예약할 때, When 요청이 동시에 처리되면, Then 하나만 201이고 하나는 409이다. | integration | DB concurrency |
| FR-003 | Given 로그인한 직원이 유효한 예약 정보를 입력할 때, When 예약을 생성하면, Then 예약 상태는 confirmed이고 감사 로그가 생성된다. | unit, integration | auth stub |
| FR-004 | Given 예약자가 아닌 직원이 예약 변경을 시도할 때, When PATCH를 호출하면, Then 403을 반환하고 예약은 변경되지 않는다. | integration, E2E | auth stub |
| FR-005 | Given 예약이 생성/변경/취소될 때, Then 각 action의 before/after audit event가 기록된다. | integration | DB |
| FR-006 | Given 관리자가 최대 예약시간 2시간 정책을 설정했을 때, When 직원이 3시간 예약을 요청하면, Then 400 policy violation이 반환된다. | unit, integration | policy fixture |
| FR-007 | Given approval_required 회의실일 때, When 직원이 예약을 생성하면, Then status는 pending_approval이다. | integration | room policy |
| FR-008 | Given 체크인 유예시간이 지난 confirmed 예약이 있을 때, When release job이 실행되면, Then status는 auto_released가 된다. | unit, integration | clock fake |
| FR-009 | Given private 예약이 있을 때, When 참석자가 아닌 사용자가 목록을 조회하면, Then 제목과 참석자는 숨김 처리된다. | integration, E2E | auth stub |
| FR-010 | Given 한 달 예약 데이터가 있을 때, When 관리자가 CSV를 내보내면, Then 이용률 컬럼과 formula injection 이스케이프가 검증된다. | integration | CSV parser |
| SC-002 | Given 파일럿 사용자에게 테스트 계정이 있을 때, When 빈 방 검색 후 예약을 완료하면, Then 중앙값 30초 이하이다. | usability smoke | stopwatch |

## 계약 테스트

- 모든 엔드포인트는 OpenAPI schema validation을 통과해야 한다.
- 400/403/404/409/500 오류는 `application/problem+json` 스키마를 따른다.
- `POST /bookings`는 같은 `Idempotency-Key` 재시도 시 동일 응답을 반환한다.
- 예약 목록은 권한에 따라 masked/unmasked 응답 snapshot을 비교한다.

## E2E 후보

1. 직원: 오늘 빈 회의실 찾기 -> 예약 생성 -> 내 예약에서 확인 -> 취소.
2. 동시성/충돌: 이미 예약된 슬롯을 다른 사용자로 열람 후 예약 시도 -> 409/화면 오류 메시지.
3. 관리자: 회의실 생성 -> 정책 설정 -> 정책 위반 예약 차단 -> 이용률 CSV 다운로드.

## 리스크 기반 커버리지 목표

| 리스크 | 테스트 목표 |
|---|---|
| 중복 예약 | DB별 통합 테스트 100%, 동시 요청 테스트 포함 |
| 권한 우회 | 예약 수정/취소/관리자 API 403 케이스 전부 포함 |
| 상세 정보 노출 | masked response snapshot과 E2E 표시 검증 |
| 자동 해제 오작동 | clock fake 기반 상태 전이 테스트, 수동 복구 테스트 |
| CSV 주입 | export cell prefix 이스케이프 unit/integration 테스트 |

성공 기준과 P0/P1 FR 시나리오 누락: 0건.
