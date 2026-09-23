# Test Design - 요양시설 낙상 감지 알림 서비스

## 원칙

- PRD의 SC와 P0/P1 FR은 구현 전 실패 테스트로 먼저 작성한다.
- 테스트 피라미드는 unit 70%, integration/contract 20%, E2E 10%를 목표로 한다.
- 생명안전 알림 경로는 정상, 경계, 실패, 권한 없음, 중복, 지연을 모두 포함한다.

## 시나리오 변환표

| SC/FR | Gherkin 시나리오 | 레이어 | 데이터 |
|---|---|---|---|
| FR-002/SC-001 | Given 센서가 낙상 신호를 보냈을 때 When API가 이벤트를 수신하면 Then 직원 알림 job이 10초 SLO 태그로 생성된다 | integration | sensor_event |
| FR-003 | Given 동일 입소자 이벤트가 30초 안에 반복될 때 When 수신하면 Then fall_event는 1건이고 중복 카운트만 증가한다 | unit/integration | duplicate events |
| FR-004/SC-001 | Given push provider가 실패할 때 When 5초 내 ack가 없으면 Then SMS fallback이 발송된다 | contract/integration | provider mock |
| FR-005/SC-006 | Given 직원이 실제 낙상으로 응답할 때 When 조치 내용을 저장하면 Then 상태, 조치자, 시각, 감사로그가 저장된다 | integration | staff response |
| FR-006/SC-002 | Given fall_event가 confirmed_fall일 때 When 보호자 알림 정책을 평가하면 Then 보호자 알림이 30초 SLO 태그로 생성된다 | unit/integration | guardian |
| FR-006 | Given 낙상 후보가 3분간 미확인일 때 When escalator가 실행되면 Then 보호자와 책임자에게 미확인 알림이 생성된다 | integration | scheduler |
| FR-007 | Given 이벤트 상태가 변경될 때 When 저장하면 Then append-only audit log는 이전 행을 수정하지 않는다 | integration | audit table |
| FR-008/SC-005 | Given 센서 heartbeat가 임계시간을 초과할 때 When health job이 실행되면 Then offline 상태와 관리자 알림이 생성된다 | unit/integration | heartbeat |
| FR-009/SC-007 | Given 타 시설 직원이 이벤트를 조회할 때 When API 호출하면 Then 403과 감사로그를 반환한다 | contract/security | tenant ids |
| FR-010 | Given 월간 이벤트 데이터가 있을 때 When 리포트를 요청하면 Then 평균 확인 시간과 오탐률이 계산된다 | unit/integration | report fixture |
| FR-011 | Given 영상 모듈이 비활성일 때 When 보호자 화면을 조회하면 Then 영상 URL 필드는 없다 | contract | guardian projection |
| FR-012/SC-008 | Given 중앙 연결이 끊겼을 때 When 게이트웨이가 이벤트를 수신하면 Then 로컬 알림과 버퍼 저장이 수행된다 | edge integration | gateway |

## 계약 테스트

- API-003은 device_event_id와 Idempotency-Key 중복 시 409 또는 동일 처리 결과를 반환한다.
- 모든 오류 응답은 Problem JSON 필수 필드 type, title, status, detail, instance를 가진다.
- 보호자 조회 API는 shareToken 만료 시 410을 반환하고 입소자 범위 외 데이터를 포함하지 않는다.
- 알림 provider callback은 서명 검증 실패 시 401을 반환한다.

## E2E 후보

1. 야간 낙상 후보 -> 직원 push -> 직원 확정 -> 보호자 알림 -> 관리자 리포트 반영
2. 낙상 후보 -> 직원 미응답 3분 -> escalaton -> 보호자 장기 미확인 알림
3. 타 시설 사용자 로그인 -> 이벤트 접근 차단 -> 감사로그 확인

## 위험 기반 커버리지 목표

- 미탐/오탐 로직: 단위 테스트 분기 90% 이상
- 권한·멀티테넌트: 모든 read/write API 교차 시설 테스트
- 알림 실패: push, SMS, provider timeout, callback 위조 테스트
- 게이트웨이 장애: 오프라인 24시간 버퍼, 재전송 순서, 중복 재전송 테스트

