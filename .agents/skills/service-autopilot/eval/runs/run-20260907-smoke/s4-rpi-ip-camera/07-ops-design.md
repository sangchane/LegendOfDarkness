# 배포·운영 설계 — PiCam Hub
버전: v1.1 · 기준 03 v1.3 · 작성 2026-09-07 (세션 재개 후) · v1.1(GATE 반영): healthcheck 8443·관측용, 자가복구 exit(1), docker data-root SSD, 운영 기본값 상수명, SLI 정정, offline-images 삭제, 첫 작업 갱신

## 근거 (단계 진입 사전조사 — 추가 검색 0회)
- **정량** — Pi 5 공식 전원 5V/5A(25W), 주변기기 600mA 제한은 15W 전원일 때 (raspberrypi.com 문서, 2026-09-07) → USB SSD를 달려면 27W 정품 전원이 설치 체크리스트 필수.
- **정성** — Frigate 사용자들이 SD 마모를 피하려 tmpfs를 직접 마운트한다(01 이해관계자 #2) → 로그·캐시는 기본 설치 스크립트가 RAM에 두고, 사용자가 손대지 않게 한다.
- **사용자 영향** — 업데이트·롤백은 명령 1개(`picam update` / `picam rollback`), 실패해도 이전 이미지로 자동 복귀(L-06: 운영자에게 막다른 상태 없음).

## 배포

### 런타임·형상
- 호스트: Raspberry Pi 5 4GB, Raspberry Pi OS Lite 64-bit(Bookworm), rootfs SD(A1 이상 — 01: A2 IOPS 미확인) + USB SSD(`/data`, ext4, `noatime`). Pi 4 4GB는 같은 이미지로 동작(성능 하향, MAX_CAMERAS 재검토는 lab 결과로).
- 컨테이너: docker compose 2서비스, **둘 다 `network_mode: host`**(04 ADR-6). 호스트 데몬: `tailscaled`, `log2ram`, `systemd-timesyncd`, HW watchdog(`RuntimeWatchdogSec=`HW_WATCHDOG_S), `unattended-upgrades`.
- 엣지 A/B 파티션·서명 검증은 non-goal(02 P1 축소) — 대신 **이미지 태그 2세대 보관 + 헬스체크 실패 시 자동 롤백**.

```yaml
# compose.yaml (스케치 — 값은 .env)
services:
  hub-api:
    image: ghcr.io/<org>/picam-hub:${PICAM_TAG}      # arm64, 태그 핀
    network_mode: host                                # 8443 · go2rtc 127.0.0.1:1984 공유
    user: "1001:1001"
    read_only: true
    tmpfs: [ /tmp ]
    cap_drop: [ ALL ]
    security_opt: [ no-new-privileges:true ]
    env_file: .env
    volumes:
      - /data:/data                                   # SSD: hub.db · snapshots · clips · backup
      - /etc/picam/master.key:/run/secrets/master.key:ro
      - /etc/picam/tls:/run/tls:ro                    # 자체 서명 인증서 (install.sh 생성)
    healthcheck:                                      # 관측용 — docker는 unhealthy를 표시만 함. 재시작은 내부 감시 exit(1) + restart 정책
      test: ["CMD", "python", "-c", "import urllib.request,ssl,sys; c=ssl._create_unverified_context(); sys.exit(0 if urllib.request.urlopen('https://127.0.0.1:8443/api/health', timeout=3, context=c).status==200 else 1)"]
      interval: 30s                                   # = HEALTHCHECK_INTERVAL_S (03)
      timeout: 5s
      retries: 3                                      # = HEALTHCHECK_RETRIES
      start_period: 20s                               # = HEALTHCHECK_START_S
    restart: unless-stopped
    depends_on: [ go2rtc ]
    deploy: { resources: { limits: { memory: 512M } } }   # = HUB_API_MEM_LIMIT_MB
  go2rtc:
    image: alexxit/go2rtc:${GO2RTC_TAG}               # 버전 핀 (시그널링 API 호환)
    network_mode: host
    read_only: true
    tmpfs: [ /tmp ]
    cap_drop: [ ALL ]
    volumes:
      - ./go2rtc.yaml:/config/go2rtc.yaml:ro          # listen 설정만: api 127.0.0.1:1984 · rtsp 비활성 · webrtc :8555
    restart: unless-stopped
  ntfy:                                               # 자가호스트 선택
    profiles: [ selfhost ]
    image: binwiederhier/ntfy:${NTFY_TAG}
    command: serve
    network_mode: host                                # 127.0.0.1:2586 또는 Tailnet IP
    volumes: [ /data/ntfy:/var/cache/ntfy ]
    restart: unless-stopped
```
hub-api는 8443(TLS) 하나만 리슨한다(INV-5). healthcheck는 같은 포트를 인증서 검증 없이 친다(자체 서명). 자체 서명 TLS는 `install.sh`가 생성하고 Tailscale 경로는 WireGuard가 추가로 감싼다. compose의 숫자(interval·retries·memory)는 03 상수 표 값을 `deploy/render.sh`가 템플릿에서 치환해 생성한다 — 손으로 두 곳을 고치지 않는다.

```dockerfile
# Dockerfile (스케치)
FROM python:3.12-slim AS builder
WORKDIR /app
RUN pip install --no-cache-dir uv
COPY pyproject.toml uv.lock ./
RUN uv sync --frozen --no-dev
FROM python:3.12-slim
RUN useradd -r -u 1001 picam
WORKDIR /app
COPY --from=builder /app/.venv /app/.venv
COPY src/ ./src/
COPY static/ ./static/                                # video-rtc.js · htmx · 로컬 폰트 (CDN 0)
ENV PATH=/app/.venv/bin:$PATH PYTHONUNBUFFERED=1
USER picam
CMD ["uvicorn", "picam.main:app", "--host", "0.0.0.0", "--port", "8443", "--workers", "1", "--ssl-keyfile", "/run/tls/key.pem", "--ssl-certfile", "/run/tls/cert.pem"]
```

### 설치·프로비저닝 (`install.sh`, 기기 1대)
1. SSD 마운트(`/data`, `noatime`) · **docker `data-root: /data/docker`**(이미지·컨테이너·json-file 로그가 SD를 쓰지 않게, GATE #6) · swap off · `log2ram`(`/var/log`) · `RuntimeWatchdogSec` · `unattended-upgrades` · `tailscale up`
2. `/etc/picam/master.key` 생성(32B, 0600) · TLS 자체 서명 · `.env` 생성(`.env.example` 복사)
3. `docker compose pull && docker compose up -d` → `curl -k https://localhost:8443/api/health`
4. 브라우저에서 첫 접속 → 관리자 비밀번호 설정(E-01) → 카메라 찾기(E-05)
설치 체크리스트(운영자에게 표시): 27W 정품 전원 · 카메라·허브 전용 VLAN/SSID(04 A4-I) · 안내판(FR-019).

### CI/CD 단계 (GitHub Actions, `main` 머지 시)
| 단계 | 내용 | 실패 시 |
|---|---|---|
| lint | `ruff` · `mypy --strict` | 중단 |
| test | 06 unit + integration(sim-camera·sim-rtsp·sim-ntfy·go2rtc 컨테이너) — RED→GREEN 게이트 | 중단 |
| contract | `schemathesis run openapi.yaml` + 리스너 경계(T-056 컨테이너 버전) | 중단 |
| build | `docker buildx --platform linux/arm64` → `ghcr.io/<org>/picam-hub:<sha>` + `:vX.Y.Z` | 중단 |
| e2e | Playwright 3여정(06) — compose 스택 + sim, arm64 이미지를 QEMU 또는 self-hosted Pi 러너에서 | 중단(태그 미승격) |
| release | `:stable` 태그 승격 (수동 승인) | — |
| deploy | **Pi가 pull 한다**(인바운드 없음): 운영자가 `picam update` 실행 → `compose pull` → `up -d` → 헬스체크 3회 통과 대기 → 실패 시 이전 태그로 `compose up -d` 자동 복귀 | 롤백 |
| smoke | `GET /api/health` 200 · `GET /api/cameras` 200 · 카메라 1대 WS 101 | 롤백 |

### 설정·비밀
- 전부 환경변수(`.env`, 0600, git 제외). 검증은 기동 시 pydantic-settings로 fail-fast.
- 비밀 위치: `master.key`(SD, `/etc/picam`), ntfy 토큰·카메라 자격증명은 DB 안 AES-GCM(05). `.env`에는 키 이름만(아래 `.env.example`), 값은 어떤 산출물에도 쓰지 않는다.
- 폐쇄망 아님(02) → 오프라인 설치 경로 없음.

## 관측성

### SLI / SLO (가능한 한 적게, 100% 금지)
| SLI | 정의 | SLO |
|---|---|---|
| 허브 가용성 | 5분 창에서 `GET /api/health` 200 비율 (외부 uptime 체크: 운영자 PC 크론 또는 Tailnet 내 다른 기기) | 월 AVAILABILITY_SLO_MONTHLY_PCT |
| 라이브 성공률 | E-18 WS 101 후 go2rtc가 해당 스트림 소비자를 등록하고 첫 RTP를 받기까지 ≤ 10s인 비율(서버측 측정 — 브라우저 첫 프레임은 lab SC-001에서만) | 월 SLO_LIVE_PCT |
| 알림 적시성 | `event.ts` → `alert.sent_at` ≤ ALERT_DELIVERY_MAX_S 비율 (SC-003 기산점과 동일) | 월 SLO_ALERT_PCT |
| 프로브 성공률 | 카메라 state=online인 창에서 프로브 성공 비율 — 회선·카메라 품질 지표(허브 자기 타임스탬프 비교가 아님) | 월 SLO_PROBE_PCT |

### 4 골든 시그널 계측
| 시그널 | 무엇을 | 어디서 |
|---|---|---|
| Latency | E-11 PTZ 처리 시간(성공/실패 분리), 프로브 `probe_ms`, WS 오픈→소비자 등록 | 구조화 로그 필드 + E-22 `last_probe_age_s` |
| Traffic | 활성 뷰어·WS 수·MSE 릴레이 스트림 수, 알림 발송 수/시간 | E-22 `viewers`·`mse_streams` |
| Errors | 5xx 수, `camera-unreachable` 수, `alert.dropped` 수, healthcheck 실패 | 로그 `level=error` 카운트 + 이벤트 테이블 |
| Saturation | CPU 5분 평균(CPU_5M_MAX_PCT), 가용 RAM(RAM_MIN_FREE_MB), 디스크 잔여(DISK_MIN_FREE_PCT), SoC 온도(HUB_TEMP_MAX_C), Pi 업링크 사용률(스트림 수×비트레이트) | J-01 허브 프로브 → `health_sample(camera_id=null)` → E-22 |
대시보드는 설정 화면의 "허브 상태" 패널 1장(E-22 값 + 카메라별 24h 가용률)으로 대신한다 — 1인 운영자에게 Grafana는 YAGNI(04 검토 #12). Prometheus는 P2.

### 로깅 전략
- **무엇을**: 요청 로그(경로·상태·ms·세션 ID 해시), 도메인 이벤트(카메라 상태 전이·알림 발송/실패/폐기·reconcile·보존 작업 결과·healthcheck 503 사유), 감사(DB 테이블, 로그 아님), 예외(스택은 로그에만, 응답에는 금지).
- **어디에**: stdout JSON → docker `json-file`(`max-size`=LOG_FILE_MB, `max-file`=LOG_FILE_COUNT) — docker data-root가 SSD `/data/docker`라 컨테이너 로그는 SD를 쓰지 않는다(GATE #6). 호스트 `/var/log`(journald·tailscale)는 log2ram(RAM, 1h 동기화) → SD 쓰기 ≤ SD_WRITE_MAX_MB_DAY. go2rtc 로그는 `warn` 이상만.
- **얼마나**: 컨테이너 로그 LOG_FILE_COUNT×LOG_FILE_MB 롤링(≈수일), 이벤트 테이블 EVENT_RETENTION_DAYS, 감사 무기한(INV-6).
- **마스킹**: 자격증명·RTSP URL·ntfy 토큰·세션 쿠키 값은 로거 필터가 `***`로 치환(INV-3, T-011). 영상·스냅샷 바이트는 로그 금지. 카메라 이름(사용자 별칭)은 허용.

## 알림 ("모든 알람은 조치 가능해야 한다" — 알람:런북 = 1:1, 증상 기반)
| 조건 | 심각도 | 수신자 | 런북 |
|---|---|---|---|
| 카메라 1대 offline (OFFLINE_AFTER_FAILURES 연속 실패) | 경고 | 운영자 ntfy | RB-01 |
| 전체 카메라 동시 offline (그룹핑 1건) | 장애 | 운영자 ntfy | RB-02 |
| 디스크 잔여 < DISK_MIN_FREE_PCT | 경고 / < DISK_STOP_PCT 장애 | 운영자 ntfy | RB-03 |
| CPU 5분 > CPU_5M_MAX_PCT 또는 온도 > HUB_TEMP_MAX_C | 경고 | 운영자 ntfy | RB-04 |
| **데드맨**: 허브 하트비트(매일 DEADMAN_TIME "허브 정상 · 카메라 N/MAX_CAMERAS online") 미수신 | 장애 | 운영자(사람이 인지) | RB-05 |
| 알림 큐 폐기 발생(`alert.dropped`) — 채널 복구 후 1건 | 경고 | 운영자 ntfy | RB-06 |
원인 지표(프로브 ms, reconcile 횟수, WS 수)는 알람이 아니라 허브 상태 패널로만 본다. 알림 본문에 영상·스냅샷 첨부 없음, 링크는 Tailnet 주소(04 A6-I).

## 장애·복구

### 시나리오 표
| 장애 | 감지 | 영향 | 복구 절차 (복붙 수준) | RTO / RPO |
|---|---|---|---|---|
| SD 카드 손상·부팅 불능 | 데드맨(RB-05) + 현장 확인 | 전부 중단 | 새 SD에 OS 플래시 → `install.sh --restore /data/backup/latest`(DB·키·.env·go2rtc.yaml 복원; 키는 SSD 백업본 사용) → `picam update` | RTO_H / RPO_H |
| go2rtc 크래시·스트림 소실 | J-01 go2rtc 생존·목록 대조 → `hub.stream_resynced`; healthcheck | 라이브 중단 수초~HEALTH_PROBE_INTERVAL_S | 자동: `restart: unless-stopped` + reconcile. 수동: `docker compose restart go2rtc` | 1분 / 0 |
| hub-api 프로브 태스크 사망(무음 알림 중단) | 내부 감시 태스크(프로브 경과 > PROBE_STALE_FACTOR×HEALTH_PROBE_INTERVAL_S) → `exit(1)` → restart; healthcheck는 unhealthy 표시 | 최대 PROBE_STALE_FACTOR×HEALTH_PROBE_INTERVAL_S 알림 공백 | 자동 재시작. 반복 시 `docker compose logs hub-api --tail 200` → 이슈 | 2분 / 0 |
| SSD 가득 참 | RB-03 | 스냅샷 생성 거부 | 자동 oldest-first 삭제. 수동: `picam prune --days 7` → 잔여 확인 | 10분 / 0 |
| 카메라 비밀번호 변경·IP 변경 | 카메라 offline 지속 + `camera-auth-rejected` 로그 | 그 카메라만 | 설정 → 카메라 수정(E-09)로 자격증명·IP 갱신 (IP 변경은 시리얼 대조) | 5분 / 0 |
| Tailscale 다운 | 원격 접속 불가(LAN은 정상) | 원격만 | `tailscale status` → `sudo tailscale up` → 키 만료면 재인증 | 10분 / 0 |
| 업데이트 실패(헬스체크 3회 실패) | `picam update`가 감지 | 없음(자동 롤백) | 자동 `PICAM_TAG=<이전>` 재기동. 수동 `picam rollback` | 3분 / 0 |
| 전원 급차단 | 데드맨·현장 | 재부팅까지 | 자동 부팅·fsck·WAL 복구·reconcile. `PRAGMA integrity_check`가 ok 아니면 백업 복원 | 5분 / 최대 1일(DB) · 0(스냅샷 파일) |

### 백업
- **무엇을**: `/data/hub.db`(SQLite `.backup` API로 일관 스냅샷), `/etc/picam/master.key`, `/etc/picam/tls`, `.env`, `go2rtc.yaml`, compose 파일. 스냅샷·클립은 보존 기간이 짧아(≤ RETENTION_MAX_DAYS) 백업 대상에서 제외 — 법적 보관 상한을 백업이 넘기지 않도록(INV-2).
- **주기·보관처**: 매일 BACKUP_TIME `/data/backup/YYYY-MM-DD.tar.gz`(SSD, BACKUP_LOCAL_GENERATIONS세대) + BACKUP_OFFSITE_INTERVAL 운영자 PC로 Tailnet `rsync`(오프사이트). 키 파일이 SSD 백업에 들어가므로 백업 tar는 운영자 PC에서만 열람.
- **복원 리허설**: RESTORE_DRILL_INTERVAL — 예비 SD에 `install.sh --restore`로 복원 후 카메라 MAX_CAMERAS대 그리드 재생·PTZ 확인(체크리스트 07 부록). 리허설 결과는 감사 로그에 `restore.drill`로 기록.
- RPO_H(DB) / RTO_H(전체 재설치 포함). 비밀 회전 절차(A7-I 탈취 인지 시): `install.sh --rotate-secrets`로 세션·서명 비밀 재생성 → 전 세션 무효화, 카메라 비밀번호는 카메라에서 변경 후 E-09로 갱신.

### 런북 골격 (예: RB-01 카메라 offline)
```
메타: 알람 "카메라 {name} 연결이 끊겼어요" ↔ RB-01 · 심각도 경고
트리거·영향: OFFLINE_AFTER_FAILURES회 연속 프로브 실패. 해당 카메라 타일 stale, 나머지 정상
진단:
  1. 설정 → 카메라 상세: last_seen_at, 최근 24h 가용률
  2. ssh pi; ping <카메라 IP>; curl -s -m 5 http://<카메라 IP>/onvif/device_service -o /dev/null -w '%{http_code}'
  3. docker compose logs hub-api --since 10m | grep <camera_id>   (camera-unreachable / camera-auth-rejected 구분)
해결:
  - ping 불가 → 카메라 전원·PoE·케이블 → 복구 시 자동 online + 복구 스냅샷
  - ping 가능·auth-rejected → 카메라 비밀번호 변경됨 → E-09로 갱신
  - ping 가능·unreachable → 카메라 재부팅(웹 UI) → 그래도 실패면 카메라 RTSP 설정(sub 프로파일) 확인
에스컬레이션: 같은 카메라 주 3회 이상 → 케이블·스위치 포트 교체 검토(하드웨어)
검증: 타일 재생 재개, 이벤트 camera.online, 알림 "다시 연결됐어요" 수신
롤백: 해당 없음(설정 변경 시 이전 값은 감사 로그에서 확인)
```
RB-02~06도 같은 골격. RB-05(데드맨)만 "허브 자체가 죽었다"는 전제라 진단 1단계가 "Tailscale ping → 현장 LED·전원"이다.

## 착수 자산

### 디렉터리 구조 (최상위 2단계)
```
picam-hub/
├─ src/picam/            # FastAPI 앱 — main.py(라우터 조립) · auth/ · cameras/ · ptz/ · streams/(go2rtc 클라·프록시·reconcile) · health/ · alerts/ · snapshots/ · settings/ · audit/ · retention/ · db/(스키마·마이그레이션)
├─ static/               # video-rtc.js · htmx.min.js · 로컬 폰트 · CSS 토큰 (외부 CDN 0)
├─ templates/            # 그리드 · 카메라 상세 · 설정 (서버 렌더)
├─ tests/                # unit/ · integration/ · contract/ · e2e/ · lab/(체크리스트 md)
├─ sim/                  # sim-camera(ONVIF 스텁) · sim-rtsp(ffmpeg+MediaMTX) · sim-ntfy compose
├─ deploy/               # compose.yaml · go2rtc.yaml · Dockerfile · install.sh · picam(update/rollback/prune CLI) · systemd/(watchdog·log2ram 설정)
├─ docs/                 # 이 패키지 03·05 사본(08은 GATE 후 추가) + 런북 RB-01~06 + 복원 리허설 체크리스트
├─ openapi.yaml          # 05 계약 전문(구현 중 완성)
├─ pyproject.toml · uv.lock · .env.example · .github/workflows/ci.yml
```

### `.env.example` (키 이름과 설명만 — 값 없음)
```
PICAM_TAG=            # 배포할 hub-api 이미지 태그 (예: stable)
GO2RTC_TAG=           # go2rtc 이미지 태그 핀 (시그널링 API 호환 검증된 버전)
NTFY_TAG=             # selfhost 프로파일 사용 시 ntfy 이미지 태그
PICAM_BIND_ADDR=      # hub-api TLS 바인딩 주소 (기본 0.0.0.0 — host 네트워크, 인바운드 공인 없음)
PICAM_HTTPS_PORT=     # 기본 8443
PICAM_DATA_DIR=       # SSD 마운트 경로 (기본 /data)
PICAM_MASTER_KEY_FILE=# 자격증명 암호화 키 경로 (컨테이너 안 /run/secrets/master.key)
PICAM_TLS_DIR=        # 자체 서명 인증서 디렉터리
PICAM_SESSION_SECRET= # 세션 쿠키 서명 키 (install.sh가 생성)
PICAM_SNAPSHOT_SIGN_SECRET= # 스냅샷 서명 URL HMAC 키
PICAM_GO2RTC_API=     # 기본 http://127.0.0.1:1984
PICAM_LOG_LEVEL=      # info | debug
PICAM_TZ=             # 화면 표시 시간대 (기본 Asia/Seoul; 저장은 UTC)
PICAM_ENV=            # production | test — constants.py는 test일 때만 tests/constants_override.py를 읽는다(HEALTH_PROBE_INTERVAL_S·PROBE_TIMEOUT_S 축소용, 03 상수 표는 그대로)
```
상수(03 상수 표)는 환경변수가 아니라 코드 상수 모듈 `picam/constants.py`에 03과 같은 이름으로 둔다 — 운영자가 바꾸는 값이 아니다(RETENTION_MAX_DAYS 등 법 상한 포함).

### 첫 작업 3개 (워킹 스켈레톤 — Impact×Uncertainty 큰 것부터)
1. **등록→스트림→그리드 관통** — compose(hub-api+go2rtc host 네트워크) 기동, E-20 health(8443), E-01/E-02 setup·login, E-05/E-06 등록(sim-camera), J-04 go2rtc 스트림 등록, E-18 WS 프록시 + **오디오 필터(SDP/MSE)**, 그리드 1타일 WebRTC 재생(T-007·T-015·T-016·T-018·T-042 GREEN). 여기서 **검증 대기 가정(03) 4개를 함께 해소**: onvif-zeep-async·ntfy 라이선스(`pip show`·LICENSE), 오디오 필터 실효(T-042 ffprobe), hub-api↔go2rtc localhost 공유(T-056), 그리고 내부 감시 exit(1)+restart(T-058).
2. **헬스→offline→알림→복구** — J-01 병렬 프로브(PROBE_TIMEOUT_S), 상태 기계, J-02 dedup·큐, E-19 SSE, E-23 복구 스냅샷, healthcheck `last_probe_age_s`, reconcile(T-024·027·033·041·057·058 GREEN).
3. **PTZ + lab 실측** — E-11/E-12 스로틀·hold 타이머·ContinuousMove Timeout, E-13~17 프리셋(T-020~023 GREEN); 실 Pi 5 + 카메라 1대로 SC-001 지연·업링크 대역·MSE 릴레이 CPU를 먼저 재서(T-045·T-048) MAX_VIEW_SESSIONS·MSE_MAX_STREAMS·HUB_API_MEM_LIMIT_MB를 **검증**한다 — 미달이면 03 상수 표 개정 → 하류 전파(규칙 9).
