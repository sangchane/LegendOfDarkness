# Ops Design - 요양시설 낙상 감지 알림 서비스

## 배포

- 서버: 컨테이너 기반 API, worker, scheduler, DB, message queue
- 시설: 게이트웨이 장비 1대 이상, 센서 BLE/Zigbee/Wi-Fi 연결, 로컬 경보 장치
- 폐쇄망 옵션: 온프레미스 API/DB/queue 패키지와 오프라인 라이선스 파일 제공
- CI/CD: lint -> unit -> integration -> contract -> build -> security scan -> deploy -> smoke
- OTA: 게이트웨이는 서명된 업데이트, canary 시설, 자동 롤백을 지원한다.

## 설정·비밀

- JWT secret, provider API key, device CA, SMS/Kakao credential은 환경변수 또는 secret manager에 저장한다.
- 시설별 알림 정책, escalaton 시간, 보호자 채널은 DB 설정으로 관리하고 변경 감사로그를 남긴다.

## 관측성

### SLI/SLO

| SLI | SLO |
|---|---:|
| sensor_event ingest availability | 월 99.9% |
| 직원 알림 p95 latency | 10초 이하 |
| 보호자 알림 p95 latency | 30초 이하 |
| 센서 heartbeat online ratio | 월 99% 이상 |
| 게이트웨이 재동기화 성공률 | 99.5% 이상 |

### 4 golden signals

- Latency: sensor ingest, alert enqueue, provider delivery, app ack
- Traffic: sensor events/min, alert jobs/min, active sensors
- Errors: provider failures, auth failures, duplicate storm, gateway offline
- Saturation: queue depth, DB connection pool, worker lag, gateway buffer size

## 로깅

- 중앙 로그: API request id, facility_id, actor_id, event_id, action, result, latency
- 로컬 로그: 게이트웨이 상태, 센서 연결, 버퍼 크기, OTA 결과
- 보관: 운영 로그 90일, 감사로그 계약 기간, 원시 센서 이벤트 기본 30일
- 마스킹: 보호자 전화번호, 입소자 이름 일부 마스킹, 토큰/키 원문 금지

## 알림

| 알림 | 조건 | 심각도 | 수신자 | runbook |
|---|---|---|---|---|
| 직원 알림 지연 | p95 10초 초과 5분 지속 | high | 운영팀/시설 관리자 | queue/provider 상태 확인 |
| 미확인 이벤트 | 후보 이벤트 3분 초과 | critical | 책임자/보호자 정책 | 현장 전화 확인 |
| 센서 오프라인 | heartbeat 10분 초과 | medium | 시설 관리자 | 배터리/통신 점검 |
| 게이트웨이 오프라인 | 3분 초과 | high | 운영팀/시설 관리자 | 네트워크/전원 확인 |
| provider 장애 | 발송 실패율 5% 초과 | high | 운영팀 | SMS fallback 전환 |

## 장애·복구

| 장애 | 감지 | 영향 | 복구 절차 | RTO/RPO |
|---|---|---|---|---|
| 중앙 API 장애 | health check, 5xx | 클라우드 알림 지연 | 게이트웨이 로컬 알림 유지, API 롤백/재시작 | RTO 30분, RPO 0 중앙 수신분 |
| 알림 provider 장애 | callback 실패율 | 보호자/직원 알림 지연 | SMS fallback, provider failover | RTO 10분 |
| 시설 네트워크 장애 | gateway offline | 중앙 동기화 지연 | 로컬 알림, 24h buffer, 복구 후 재전송 | RTO 현장 알림 0분, RPO 24h 내 0 |
| DB 장애 | DB health | 조회/기록 중단 | PITR 복구, read-only 공지 | RTO 1h, RPO 5분 |
| 센서 배터리 고갈 | heartbeat/battery | 감지 누락 | 14일/7일/1일 교체 알림, 예비 센서 | RTO 시설 조치 |

## 백업

- 운영 DB: 매일 전체 백업, 5분 WAL/PITR, 월 1회 복구 리허설
- 감사로그: append-only 백업, 해시 검증
- 게이트웨이 설정: 중앙 등록정보 기준 재프로비저닝 가능해야 한다.

