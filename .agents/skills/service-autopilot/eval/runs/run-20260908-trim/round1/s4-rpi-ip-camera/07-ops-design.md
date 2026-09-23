# 배포·운영 설계 — PiCam Watch
버전: v1.0 · 기준 03 v1.2
스킬: `ecc:deployment-patterns`(CI/CD 단계·헬스체크·롤백·준비도 체크리스트) · `ecc:docker-patterns`(compose 보안 옵션·비밀·볼륨). 충돌: docker-patterns "compose를 프로덕션에 쓰지 말라"는 규모(SITES_MAX·VPS 1대·팀 1~2명) 근거로 채택하지 않는다(ADR-7).

**근거 (진입 사전조사, 검색 1회)**
- 정량 — log2ram 기본값: RAM 로그 폴더 `128M`, 디스크 동기화는 `log2ram-daily.timer`(일 1회), 설치 전 journald `SystemMaxUse=20M` 권고 (github.com/azlux/log2ram README, 2026-09-08). → PI_LOG_RAM_MB·JOURNAL_MAX_MB의 출처.
- 정성 — 같은 README: "/var/log이 RAM보다 크면 log2ram이 시작에 실패할 수 있다" — 로그 상한을 정하지 않으면 마모 대책 자체가 죽는다. 상한을 이미지에 굽는다.
- 사용자 영향 — 운영자는 SD 카드 교체 출동을 하지 않는다(P1 SD 마모, SC-013). 문제가 생기면 화면의 기기 상태와 푸시로 먼저 알고, 런북의 첫 단계는 "원격 재시작" 버튼이다(L-06 막다른 에러 0).

## 배포

### 런타임·형상
| 계층 | 형상 | 구성 |
|---|---|---|
| **엣지 (Pi 5)** | Raspberry Pi OS Lite 64-bit 골든 이미지 + Docker compose | 컨테이너 3개: `agent`(Python 3.12), `go2rtc`(공식 바이너리 이미지), `updater`(Python). OS 계층: Overlay FS(raspi-config P3, 부팅 파티션 쓰기 보호), log2ram(PI_LOG_RAM_MB), journald SystemMaxUse=JOURNAL_MAX_MB, HW watchdog(WATCHDOG_TIMEOUT_SEC), chrony + chrony-wait, unattended-upgrades(security만, 자동 재부팅 없음). 쓰기 구역: USB SSD `/mnt/data`(clips, agent.db, certs), tmpfs 링버퍼(RINGBUF_TMPFS_MB) |
| **서버 (VPS 1대)** | Docker compose | `caddy`(TLS 자동, 443 → api), `api`(FastAPI + mqtt-bridge + ws-hub + ca), `purge-job`(cron 컨테이너), `mosquitto`(8883 mTLS), `coturn`(3478 + UDP 릴레이 범위), `postgres:16`. 볼륨: pgdata, thumbs, clipcache, secrets(read-only) |
| **웹** | 정적 번들 | caddy가 서빙, PWA(service worker + 웹 푸시) |

### 엣지 골든 이미지 (A/B는 앱 계층 — ADR-4)
- 빌드: CI에서 `pi-gen` 스테이지로 굽는다 — 공식 Lite 이미지 + Docker + log2ram + 설정(overlay·watchdog·chrony·journald) + updater 서비스 + cosign 공개키. 산출물 `picam-os-<ver>.img.xz` + SHA-256 게시.
- 첫 부팅(`firstrun`): USB SSD 포맷·마운트 → 클레임 토큰(이미지 굽기 시 Raspberry Pi Imager 커스터마이즈로 주입) → E11 claim → 인증서 저장 → compose pull(digest 고정) → Overlay FS 활성 후 재부팅.
- 앱 업데이트: Admin이 E17 → `cmd/deploy` → updater가 GHCR에서 digest pull → cosign 검증 → `docker compose up -d` → 헬스체크(HEALTHCHECK_INTERVAL_SEC × HEALTHCHECK_FAIL_MAX) 실패 시 previous_digest로 복귀(OTA_ROLLBACK_MAX_MIN 안), 재시도 OTA_RETRY_MAX.
- OS 업데이트: security만 자동. 커널 등 대형 변경은 새 골든 이미지 + 현장 SD 교체(유인 현장, Q5) — 런북 RB-4.

### compose 스케치 (서버, 보안 옵션 포함)
```yaml
services:
  caddy:    { image: caddy:2, ports: ["443:443","80:80"], volumes: [./Caddyfile:/etc/caddy/Caddyfile:ro, caddy_data:/data, ./web/dist:/srv:ro] }
  api:
    image: ghcr.io/<org>/picam-api@sha256:<digest>
    env_file: [/etc/picam/api.env]
    volumes: [thumbs:/data/thumbs, clipcache:/data/clipcache, /etc/picam/secrets:/run/secrets:ro]
    read_only: true
    tmpfs: [/tmp]
    security_opt: [no-new-privileges:true]
    cap_drop: [ALL]
    healthcheck: { test: ["CMD","python","-c","import urllib.request;urllib.request.urlopen('http://localhost:8000/health')"], interval: 30s, timeout: 3s, retries: 3 }
    depends_on: { postgres: { condition: service_healthy }, mosquitto: { condition: service_started } }
  purge-job: { image: ghcr.io/<org>/picam-api@sha256:<digest>, command: ["python","-m","picam.jobs.purge","--daily"], env_file: [/etc/picam/api.env], volumes: [thumbs:/data/thumbs, clipcache:/data/clipcache] }
  mosquitto: { image: eclipse-mosquitto:2, ports: ["8883:8883"], volumes: [./mosquitto.conf:/mosquitto/config/mosquitto.conf:ro, /etc/picam/secrets/mqtt:/mosquitto/certs:ro] }
  coturn:    { image: coturn/coturn:4, network_mode: host, volumes: [./turnserver.conf:/etc/coturn/turnserver.conf:ro], env_file: [/etc/picam/turn.env] }
  postgres:  { image: postgres:16-alpine, volumes: [pgdata:/var/lib/postgresql/data], env_file: [/etc/picam/db.env], healthcheck: { test: ["CMD-SHELL","pg_isready -U picam"], interval: 5s, retries: 5 } }
volumes: { pgdata: {}, thumbs: {}, clipcache: {}, caddy_data: {} }
```
엣지 compose는 `agent`(devices: USB SSD 마운트만, `cap_drop: ALL`, read_only + tmpfs), `go2rtc`(1984/8555는 localhost 바인딩), `updater`(Docker 소켓 접근이 필요한 유일한 컨테이너 — 그래서 agent와 분리). Dockerfile은 python:3.12-slim 멀티스테이지 + non-root(deployment-patterns 패턴).

### CI/CD 단계
`lint(ruff·eslint) → typecheck(mypy·tsc) → unit → integration(compose test 프로파일: ONVIF 시뮬레이터·MediaMTX·mosquitto·coturn·postgres) → contract(schemathesis) → build(멀티아치 arm64/amd64, digest 고정) → cosign sign → push GHCR → deploy staging(VPS staging compose pull+up) → smoke(E01·E27 가짜 agent·E39) → deploy prod(compose pull+up, 헬스체크 실패 시 이전 digest로 `compose up`) → 릴리스 등록(E19 RELEASE 행)`
기기 배포는 CI가 아니라 Admin의 E17이 트리거한다(단계적: 사이트 1곳 → 나머지). 실기 계층(06)은 릴리스 게이트로 수동 실행.

### 설정·비밀
- 전부 환경변수(pydantic-settings로 시작 시 검증, 누락 시 기동 실패). 값은 어떤 산출물에도 쓰지 않는다.
- 서버 비밀: `/etc/picam/secrets/`(600, root) — DB 비밀번호, VAPID 키쌍, TURN 공유 비밀, 세션 서명 키, **내부 CA 키**(가장 민감 — 오프라인 백업 1부). 백업은 age 암호화.
- 기기 비밀: USB SSD `/mnt/data/certs/`(600) — 기기 인증서·키, 카메라 비밀번호(기기 키 파생 암호화). SD 카드에는 비밀 없음.
- 폐쇄망 아님 — 오프라인 설치 경로 없음.

## 관측성

### SLI·SLO-lite (사용자 대면 = 가용성·지연 / 엣지 파이프라인 = E2E 지연 / 공통 = 정확성)
| SLI | SLO (창 SLO_WINDOW_DAYS) | 측정 |
|---|---|---|
| 라이브 성공률 = E27 201 후 LIVE_FIRST_FRAME_P95 안에 첫 프레임 도달한 세션 비율 | ≥ SLO_LIVE_SUCCESS_PCT | 브라우저가 WS로 `first_frame_ms` 보고 → api 지표 |
| 라이브 첫 프레임 지연 p95 | ≤ LIVE_FIRST_FRAME_P95 | 같은 보고 |
| 기기 온라인 비율 (사이트별) | ≥ SLO_DEVICE_ONLINE_PCT | DEVICE.status 전이 로그 |
| 이벤트→푸시 지연 p95 | ≤ ALERT_DELIVERY_P95 | EVENT.started_at vs 푸시 발송 시각 |
| 푸시 발송 성공률 | ≥ SLO_PUSH_SUCCESS_PCT | VAPID 응답 코드 |
| 파기 정확성 = 기한 초과 미파기 행 | 0 (INV-1) | purge-job 대사 |

### 4 골든 시그널 계측 (Prometheus, api `/metrics` 내부망만)
- **Latency**: `live_first_frame_ms`(히스토그램, 성공/실패 라벨), `command_roundtrip_ms{name}`, `push_delivery_ms`
- **Traffic**: `live_sessions_active{site,relayed}`, `events_total{type}`, `commands_total{name}`
- **Errors**: `commands_failed_total{slug}`(command-timeout 별도), `push_failed_total`, `purge_mismatch_total`, `cert_renew_failed_total`
- **Saturation**: `turn_relay_mbps`(coturn 지표 → 대비 TURN_RELAY_MAX_MBPS), `disk_used_pct{volume}`, `device_cpu_pct{device}`, `device_disk_free_pct{device}`, `offline_queue_len{device}`(대비 OFFLINE_QUEUE_MAX), `mqtt_connected_devices`

### 로깅
- **무엇**(구조화 JSON, 이벤트 목록): api 요청(경로·상태·지연·actor_id — 본문 없음), 명령 발행/응답/타임아웃, MQTT 연결/LWT, 세션 시작/종료, 파기 실행·대사 결과, 배포 상태 전이, 인증서 발급/갱신, 푸시 발송 결과, agent: 카메라 연결 전이·이벤트 생성·클립 저장 결과·큐 길이·업데이트 단계.
- **어디**: 서버 = 컨테이너 stdout → journald → `/var/log/picam/*.jsonl` 로테이션. 엣지 = stdout → journald(SystemMaxUse=JOURNAL_MAX_MB) → log2ram(PI_LOG_RAM_MB, 일 1회 SSD 동기화 — **SD 쓰기 0**) + 오류만 MQTT `log/error`로 서버 전송.
- **얼마나**: 서버 앱 로그 LOG_RETENTION_DAYS, 감사 로그는 DB(AUDIT_RETENTION_DAYS). 엣지 로그는 PI_LOG_RAM_MB 상한 순환.
- **마스킹**: 비밀번호·토큰·TURN 자격증명·카메라 비밀번호·SDP 본문·영상/썸네일 바이트는 기록하지 않는다. 이메일은 해시 앞 8자. IP는 감사 로그에만.

## 알림 (조치 가능한 알람만, 알람:런북 = 1:1, 증상 기반)
| 조건 | 심각도 | 수신자 | 연결 런북 |
|---|---|---|---|
| 사이트 기기 offline > DEVICE_OFFLINE_DETECT_SEC (사용자 알림과 별개로 운영자에게도) | P2 | 운영자 푸시 | RB-1 기기 오프라인 |
| `/health` 실패 HEALTHCHECK_FAIL_MAX회 연속 또는 라이브 성공률(1시간 창) < SLO_LIVE_SUCCESS_PCT | P1 | 운영자 푸시+이메일 | RB-2 서버 장애 |
| `turn_relay_mbps` > TURN_RELAY_MAX_MBPS × ALARM_WARN_PCT/100 | P2 | 운영자 | RB-3 릴레이 포화 |
| `disk_used_pct` > ALARM_WARN_PCT (서버 볼륨 또는 기기 SSD) | P2 | 운영자 | RB-5 디스크 |
| purge-job 실패 또는 `purge_mismatch_total` > 0 | P1 (법) | 운영자 이메일 | RB-6 파기 실패 |
| 백업 실패 또는 BACKUP_INTERVAL_HOURS × 2 동안 백업 없음 | P1 | 운영자 이메일 | RB-7 백업 |
| 인증서 만료 DEVICE_CERT_RENEW_BEFORE_DAYS 안인데 미갱신 | P2 | 운영자 | RB-8 인증서 |
| 배포 `rolled_back`/`failed` | P2 | Admin 푸시 | RB-9 배포 롤백 |
같은 알람은 ALERT_OPS_DEDUP_MIN 동안 중복 억제. 원인 지표(CPU·큐 길이·명령 실패율)는 대시보드로만.

## 장애·복구

### 시나리오 표
| 장애 | 감지 | 영향 | 복구 절차 (복붙 수준) | RTO/RPO |
|---|---|---|---|---|
| **서버 VPS 다운** | `/health` 실패, 모든 기기 offline 동시 | 라이브·알림 불가. 엣지는 녹화·큐잉 계속(FR-022) | ① 공급자 콘솔 재부팅 ② `docker compose ps` → 미기동 서비스 `docker compose up -d` ③ 복구 불가 시 새 VPS: 이미지 pull → `/etc/picam` 복원(age) → `pg_restore` 최신 백업 → thumbs rsync 복원 → DNS 전환 | RTO_SERVER_MIN / RPO_SERVER_HOURS |
| **PostgreSQL 손상** | api 500 급증, healthcheck 실패 | 전체 | `docker compose stop api purge-job` → `pg_restore --clean` 최신 백업 → 이벤트는 기기 outbox 재전송으로 RPO 이후분 회복(ULID 멱등) → `up -d` | RTO_SERVER_MIN / RPO_SERVER_HOURS (이벤트는 0에 근접) |
| **Pi 부팅 불능·SD 손상** | 기기 offline 지속, 원격 재시작 무응답 | 해당 사이트 전체 | 현장(유인): ① 전원 재투입 ② 실패 시 새 SD에 골든 이미지 굽기(Imager, 클레임 토큰 재발급 E11용) ③ 기존 USB SSD 그대로 연결(클립·인증서·카메라 설정 보존) ④ 부팅 → 자동 재클레임 → Admin 승인(E14) | RTO_DEVICE_MIN / 클립 RPO 0(SSD 보존) |
| **카메라 교체·IP 변경** | `camera_disconnected` 지속 | 카메라 1대 | E20 재탐색 → E22 재등록(같은 ONVIF hardware_id면 갱신) → 프리셋 재저장 | 즉시 / — |
| **coturn 장애** | STUN 차단 환경 세션 실패율 급증, `turn_relay_mbps` 0 | 릴레이 필요한 시청자만 | `docker compose restart coturn` → 자격증명 공유 비밀 확인 → 방화벽 UDP 범위 확인 | RTO_SERVER_MIN / — |
| **GHCR 장애** | E17 배포 pending 지속 | 배포만 불가, 운영 무영향 | 대기. 긴급 시 `docker save` 이미지를 scp → `docker load` (updater 수동 경로) | — |

### 백업
| 무엇 | 주기 | 보관처 | 보존 | 복원 리허설 |
|---|---|---|---|---|
| PostgreSQL `pg_dump -Fc` | BACKUP_INTERVAL_HOURS | 오프사이트 오브젝트 스토리지, age 암호화 | BACKUP_RETENTION_DAYS | RESTORE_DRILL_INTERVAL_DAYS마다 staging에 복원 → E39 조회 스모크 |
| `/etc/picam` (비밀·CA 키·설정) | 변경 시 + BACKUP_INTERVAL_HOURS | 같은 곳 + CA 키는 오프라인 1부 | 최근 BACKUP_RETENTION_DAYS | 같은 리허설에서 복원 |
| thumbs | BACKUP_INTERVAL_HOURS rsync | 같은 곳 | CLIP_RETENTION_DAYS(법 초과 보존 금지 — 백업도 파기 잡 대상) | — |
| 클립(Pi USB SSD) | **백업 없음** — 설계(Q3): 30일 순환·개인정보 최소 보관. SSD 보존이 복구 수단 | — | — | — |
| 기기 SQLite | 백업 없음 — 재클레임으로 재구성 | — | — | — |

### 런북 골격 (전 런북 공통 구조) + RB-1 예시
`메타(알람 연결·심각도) → 트리거·영향 → 진단(명령) → 해결 → 에스컬레이션 → 검증 → 롤백`
**RB-1 기기 오프라인** — 메타: 알람 "기기 offline > DEVICE_OFFLINE_DETECT_SEC", P2. 트리거: LWT. 영향: 사이트 라이브·알림 불가, 녹화는 계속.
진단: ① Admin 화면 E13 `last_seen_at`·마지막 metrics(uplink_mbps, temp_c) ② 같은 사이트 다른 기기 없음 → 사이트 인터넷 의심 ③ `mosquitto_sub -t 'devices/<id>/state'` 최근 retained 값.
해결: ① 사이트 인터넷 확인 요청(전화) ② 복귀 후 자동 재접속·큐 전송 확인(`offline_queue_len` → 0) ③ 30분 넘게 인터넷 정상인데 offline → 현장 전원 재투입 안내 → 그래도 안 되면 RB-4(SD 재굽기).
에스컬레이션: 팀 1~2명 — 없음(운영자 본인). 검증: E13 online + 라이브 첫 프레임. 롤백: 해당 없음.

## 착수 자산

### 디렉터리 구조 (최상위 2단계)
```
picam/
├─ server/            FastAPI api·mqtt-bridge·ws-hub·ca·purge-job (Python 3.12, uv)
│  ├─ picam/          패키지: api/ · bridge/ · ca/ · jobs/ · models/ · config(constants.yaml 로더)
│  ├─ migrations/     alembic (05 DDL)
│  └─ tests/          unit/ · integration/ · contract/ (06)
├─ agent/             Pi 에이전트 (Python 3.12): onvif/ · relay/(go2rtc 제어) · clips/ · outbox/ · updater/
│  └─ tests/          unit/ · integration/(ONVIF 시뮬레이터 + MediaMTX)
├─ web/               React + Tailwind + Zustand PWA: live/ · clips/ · devices/ · admin/ · push/
│  └─ e2e/            Playwright POM (06)
├─ deploy/            compose(server·device·test 프로파일) · Caddyfile · mosquitto.conf · turnserver.conf
├─ image/             pi-gen 스테이지 · firstrun · overlay/watchdog/log2ram 설정
├─ config/            constants.yaml (03 상수 표, source 필드) · problems.yaml (05 slug 표)
├─ tools/             onvif-simulator/ · latency-clock/ · hw-tests/(06 실기 스크립트)
└─ docs/              autopilot 산출물 링크 · 런북 RB-1~9 · 안내판 템플릿
```

### `.env.example` (키 이름과 설명만 — 값 없음)
```
# server/api
DATABASE_URL=            # postgres 접속 문자열 (db.env와 동일 계정)
SESSION_SIGNING_KEY=     # 세션 쿠키 서명 키 (32바이트 이상)
VAPID_PUBLIC_KEY=        # 웹 푸시 공개키
VAPID_PRIVATE_KEY=       # 웹 푸시 개인키
VAPID_SUBJECT=           # mailto: 연락처
MQTT_URL=                # mqtts://mosquitto:8883
MQTT_SERVER_CERT=        # 서버 계정 클라이언트 인증서 경로
MQTT_SERVER_KEY=
CA_CERT_PATH=            # 내부 CA 인증서
CA_KEY_PATH=             # 내부 CA 키 (600)
TURN_SHARED_SECRET=      # coturn use-auth-secret와 동일
TURN_URLS=               # turn:host:3478?transport=udp, ...
THUMBS_DIR=              # /data/thumbs
CLIPCACHE_DIR=           # /data/clipcache
PUBLIC_BASE_URL=         # https://...
CONSTANTS_PATH=          # config/constants.yaml
LOG_LEVEL=
# agent (Pi)
PICAM_API_URL=
PICAM_MQTT_URL=
DEVICE_CERT_PATH=        # /mnt/data/certs/device.crt
DEVICE_KEY_PATH=
DATA_DIR=                # /mnt/data
RINGBUF_DIR=             # /run/picam/ringbuf (tmpfs)
GO2RTC_API_URL=          # http://127.0.0.1:1984
COSIGN_PUBKEY_PATH=
```

### 첫 작업 3개 = 워킹 스켈레톤 (Impact×Uncertainty 큰 것부터)
1. **R-1 실증: NAT 뒤 Pi → 브라우저 첫 프레임** — Pi 5 + go2rtc(테스트 RTSP 소스) + coturn + mosquitto + 최소 api(E27·E29·`cmd/webrtc.offer/ice/close`) + 최소 UI(`<video>` 1개). 성공 = STUN 차단 LTE 폰에서 첫 프레임 ≤ LIVE_FIRST_FRAME_P95, 기기 TURN 자격증명 회전 중 세션 유지. 실패 시 04 R-1 대안으로 분기하고 03 개정(R#).
2. **이벤트 파이프라인 얇게 끝까지** — ONVIF 시뮬레이터 MotionAlarm → agent(쿨다운·클립 copy·썸네일) → MQTT `events` → api(EVENT INSERT, 푸시) → E39 목록 → E42/E43 재생. 성공 = SC-005 통합 버전 + FR-012 클립 길이 검증 + outbox 재전송(SC-009 통합).
3. **프로비저닝 + 감사 fail-closed** — E11 클레임 → ca 발급 → E14 승인 → mosquitto ACL `%u` 접속 → E27 발급이 AuditLog INSERT와 한 트랜잭션(INV-2 장애 주입 테스트) → E49 조회. 성공 = FR-001·FR-025·FR-026 통합 시나리오 GREEN.
실기 장비(06 실기 계층): Pi 5 4GB + 고내구 microSD + USB SSD, ONVIF PTZ 카메라 1대(H.264 sub 프로파일), 시험용 NAT 공유기, LTE 스마트폰, 밀리초 LED 시계.
