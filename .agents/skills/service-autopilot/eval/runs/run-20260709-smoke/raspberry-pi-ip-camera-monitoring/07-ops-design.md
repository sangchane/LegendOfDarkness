# Ops Design - Raspberry Pi IP Camera Control & Monitoring

## 배포

- 중앙 서버: Docker Compose 또는 온프레미스 VM 설치. 구성요소는 API server, PostgreSQL, object storage, broker, reverse proxy.
- Gateway: Raspberry Pi OS 기반 immutable image. agent + media bridge + watchdog service를 systemd로 실행.
- 폐쇄망: 이미지/컨테이너/패키지 단일 번들 제공, 외부 CDN/API 의존 없음.
- OTA: A/B partition, signed update, canary gateway 1대 -> site 10% -> 전체 rollout. 실패 시 자동 rollback.

## CI/CD

1. lint/static analysis
2. unit tests
3. API contract tests
4. gateway integration tests with ONVIF mock
5. build server image/gateway image
6. security scan and license check
7. staging deploy
8. smoke: enroll, discovery, live session, PTZ mock, audit

## 설정과 비밀

- 서버 비밀: 환경변수 또는 secret manager.
- Gateway 비밀: enrollment 이후 장치별 인증서, 카메라 credential은 gateway 로컬 암호화 저장.
- 로그에는 RTSP URI credential, token, 인증서 private key, snapshot 파일 본문을 남기지 않는다.

## 관측성

### SLI/SLO-lite

| SLI | 목표 |
|---|---|
| API availability | 월 99.5% 이상 |
| stream startup latency | p95 <= 3s |
| PTZ ack latency | p95 <= 1s |
| gateway heartbeat freshness | online gateway p95 <= 15s |
| command failure rate | 5분 rolling <= 2% |
| media process crash loop | 0회/일 목표, 발생 시 알림 |

### Golden signals

- Latency: stream start, PTZ ack, API request, discovery job duration.
- Traffic: active sessions, commands/min, gateway heartbeat/min, RTSP reconnect count.
- Errors: ONVIF auth failure, stream start failure, command failure, Problem JSON type count.
- Saturation: CPU, memory, temperature, disk write rate, network throughput, media process fd count.

## 로깅

- 중앙: API access log, audit log, command log, gateway status log.
- Gateway: agent log는 journald + size cap, 상세 media log는 tmpfs rolling 후 중요 이벤트만 중앙 전송.
- 보존: audit 1년, operational logs 30일, gateway local logs 7일 또는 256MB cap.
- SD 보호: persistent write 최소화, noatime, swap 비활성 또는 제한, SSD/NVMe 권장.

## 알림

| 알림 | 조건 | 심각도 | 수신 | 대응 |
|---|---|---|---|---|
| Gateway offline | heartbeat 60초 없음 | High | 운영자 | 전원/네트워크 확인, 로컬 접속 |
| Camera offline storm | 같은 site에서 5분 내 5대 이상 offline | High | 운영자 | 스위치/PoE/망 점검 |
| Stream failure surge | stream failure rate 5분 10% 초과 | Medium | 운영자 | media bridge 재시작/리소스 확인 |
| PTZ command failure | 특정 카메라 5분 5회 이상 실패 | Medium | 운영자 | ONVIF credential/capability 확인 |
| Disk write high | gateway disk write > 기준치 10분 지속 | Medium | 시스템 운영 | 로그 cap/SSD/프로세스 점검 |
| OTA rollback | 업데이트 자동 rollback 발생 | High | 시스템 운영 | 릴리스 중지/로그 수집 |

모든 알림은 조치 가능한 runbook 링크와 owner를 가져야 한다.

## 장애/복구

| 장애 | 감지 | 영향 | 복구 절차 | RTO/RPO |
|---|---|---|---|---|
| 중앙 서버 다운 | health check 실패 | 원격 UI 불가, gateway local 제한 모드 | Docker compose 재기동, DB 상태 확인, 최근 배포 rollback | RTO 30분, RPO 5분 |
| Gateway offline | heartbeat timeout | 해당 site 카메라 원격 접근 불가 | 전원/네트워크 확인, watchdog reboot, 현장 교체 | RTO 4시간, RPO bounded buffer |
| Media bridge crash | process exit metric | live stream 실패 | systemd restart, crash loop이면 stream 제한/rollback | RTO 5분 |
| DB 손상 | backup/health 실패 | 설정/감사 조회 불가 | PITR 또는 일 백업 복원, audit integrity check | RTO 2시간, RPO 24시간 또는 WAL 기준 |
| SD 카드 장애 | boot failure/log missing | gateway 부팅 불가 | 예비 이미지 SD/SSD 교체, enrollment 재사용 제한 정책 | RTO 4시간 |

## 백업

- DB: 매일 전체 백업 + WAL/PITR 가능 시 5분 RPO, 월 1회 복원 리허설.
- Snapshot storage: 보존 정책에 따라 lifecycle 삭제, 일 단위 메타데이터 검증.
- Gateway: stateless에 가깝게 유지. 카메라 credential 복구는 서버 escrow 또는 재등록 절차로 처리.

## 운영 runbook 초안

1. 알림 확인: site, gateway, camera, correlationId 확인.
2. 영향 범위: active sessions, offline cameras, 최근 배포 여부 확인.
3. 진단: gateway metrics, media bridge logs, ONVIF auth failures, network latency 확인.
4. 해결: 프로세스 재시작, gateway reboot, credential 재검증, rollout 중지.
5. 검증: stream smoke, PTZ mock/live command, audit log 기록 확인.
6. 사후: incident note와 decision-log 업데이트.
