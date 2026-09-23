# Test Design - Raspberry Pi IP Camera Control & Monitoring

## 원칙

- PRD의 AC/SC는 구현 전에 실패하는 테스트로 존재해야 한다.
- 테스트 피라미드는 unit 70%, integration/contract 20%, E2E 10%를 목표로 한다.
- 보안·권한·멱등성·장치 offline 경로는 happy path와 같은 수준으로 검증한다.

## 시나리오 변환표

| SC/FR ID | Gherkin 시나리오 | 레이어 | 데이터/목 |
|---|---|---|---|
| FR-001 | Given 유효한 enrollment token When gateway가 enroll Then 인증서와 bootstrap config가 발급된다 | integration | fake gateway fingerprint |
| FR-001 | Given 만료된 token When enroll Then 401 Problem JSON이 반환된다 | integration | expired token |
| FR-002 | Given ONVIF mock camera When discovery job 실행 Then capabilities가 저장된다 | integration | ONVIF mock |
| FR-003/SC-001 | Given online camera When stream session 생성 Then p95 3초 이내 첫 프레임 metric이 기록된다 | E2E/perf smoke | media bridge stub + test stream |
| FR-004/SC-002 | Given PTZ camera When move command Then ack p95 1초 이하이고 audit가 남는다 | integration/E2E | ONVIF PTZ mock |
| FR-004/SC-006 | Given 같은 Idempotency-Key When PTZ command 재시도 Then 카메라 호출은 1회만 발생한다 | integration | command spy |
| FR-005/SC-003 | Given camera:control이 없는 사용자 When PTZ command Then 403과 거부 감사로그가 남는다 | integration | RBAC fixture |
| FR-006 | Given 사용자가 stream 시작 When session 생성 Then audit action `stream.start`가 기록된다 | integration | audit repository |
| FR-007/SC-004 | Given gateway heartbeat 없음 When 60초 경과 Then gateway offline event와 UI 상태가 표시된다 | unit/E2E | fake clock |
| FR-008/SC-008 | Given 권한 없는 사용자 When snapshot download Then 403 Problem JSON | integration/E2E | snapshot fixture |
| FR-009 | Given gateway offline buffer에 이벤트 존재 When 연결 복구 Then 순서 보존 재전송과 중복 제거가 수행된다 | gateway integration | local buffer |
| SC-005 | Given 저장소 검사 When credential fields 조회 Then 평문 credential이 없다 | security test | DB dump scanner |
| SC-007 | Given stream session active When browser closes Then session cleanup command가 gateway에 전송된다 | E2E | Playwright + mock gateway |

## 단위 테스트

- Capability parser: Profile T/S feature flags, PTZ 지원 여부, RTSP URI 선택.
- Command validator: action enum, duration limit, preset existence.
- Authz policy: view/control/admin 권한 조합.
- Idempotency service: same key replay, conflict body mismatch.
- Offline state machine: heartbeat timeout, reconnect, degraded.
- Audit event builder: 민감정보 마스킹.

## 통합/계약 테스트

- REST Problem JSON schema 검증.
- Gateway channel heartbeat/config delta 계약.
- ONVIF mock server against discovery/control adapter.
- Media bridge lifecycle: start/stop 실패, duplicate stop, process crash.
- DB migration: append-only audit, FK, unique idempotency key.

## E2E 후보

1. 설치 기사 플로우: enrollment token 생성 -> gateway 등록 -> camera discovery -> camera 저장.
2. 운영자 플로우: 로그인 -> camera grid -> live open -> PTZ command -> audit 확인.
3. 보안 플로우: 권한 없는 사용자로 live/PTZ/snapshot 접근 차단 확인.

## 리스크 기반 커버리지 목표

| 리스크 | 테스트 |
|---|---|
| ONVIF 제조사 편차 | mock profile matrix + 인증 장비 smoke list |
| Pi 성능 한계 | gateway당 1/2/4 stream soak test |
| 영상 권한 누출 | RBAC negative tests + signed URL expiry tests |
| 중복 제어 | idempotency integration tests |
| offline/복구 | heartbeat timeout + buffer replay tests |

누락된 SC/P0/P1 시나리오: 0건.
