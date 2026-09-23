# 배포·운영 설계 — PiCam Watch

> 근거 (A7 진입 사전조사, 검색 1회, 2026-09-03)
> - **정량**: BCM2835 HW 워치독 기본·최대 타임아웃 15초 ([Pi 포럼 watchdog](https://forums.raspberrypi.com/viewtopic.php?t=353094)); raspi-config 오버레이는 루트 전체를 initramfs 단계에서 읽기 전용화 ([Pi 포럼 read-only 배포](https://forums.raspberrypi.com/viewtopic.php?t=331887)) → 워치독 타임아웃 10s, 서비스 헬스는 호스트 타이머(30s × 3회 = 90s)가 먼저 처리.
> - **정성**: "overlayfs 켜니 Docker가 `invalid cross-device link`로 못 뜬다" ([grafolean — Docker on read-only Raspbian](https://grafolean.medium.com/run-docker-on-your-raspberry-pi-read-only-file-system-raspbian-1360cf94bace)) → Docker `data-root`를 NVMe `/data/docker`로, tailscaled 상태도 `/data/tailscale`로 옮기고 SD 루트만 오버레이.
> - **사용자 영향**: 운영자는 대시보드 1장에서 "지금 건강한가 / 어디가 막혔나 / 무엇을 해야 하나"를 30초 안에 알아야 한다(L-03 힉 — 화면당 결정 1개, dashboard-builder "운영자 질문에서 시작").

스킬: `ecc:deployment-patterns`(파이프라인 단계·헬스체크 2단·롤백 체크리스트·준비도 체크리스트 적용; 롤링/카나리는 단일 기기라 해당없음) · `ecc:dashboard-builder`(운영자 질문 4개 → 패널 최소 세트, 허영 패널 제거). `ecc:docker-patterns`는 단계당 2개 상한으로 미호출.
개정: **v1.2 (2026-09-03, GATE 반영)** — v1.1 값 동기화(tmpfs 128MB, 세션 16, ICE 후보, local_auth, `-an`, 설정 토큰, 감사 FULL, 부팅 정합성), go2rtc.yaml 비밀 0, 8443 + nftables 리다이렉트, 호스트 헬스 타이머(90s), tailscale 상태 경로, RTC 배터리·외부 USB BOM, age 암호화 백업·RPO 정직 기재, 웹훅 알림(P2)·RB-5 감지 정직 기재, `/metrics` → `/health/rollup`, fstrim·감사 아카이브·audit verify CLI.

## 배포

### 하드웨어 BOM (필수)
Raspberry Pi 5 8GB · NVMe HAT + NVMe 256GB(사이트가 바쁘면 512GB 권장, 02 Q4 산정) · **RTC 배터리(Pi 5 내장 RTC용)** · **외부 USB 백업 매체 ≥ 32GB** · 공식 전원 어댑터 · 잠금 함체(물리 보안, A4 /data-I). 선택: 카메라 VLAN 스위치.

### 런타임·형상 (엣지 단일 기기)
- **OS**: Raspberry Pi OS Lite 64-bit. SD카드 = 루트(raspi-config **overlay read-only**), NVMe = `/data`(ext4, `noatime`, 쓰기 전용 구역), USB = `/mnt/backup`(`noexec,nodev`). swap off, log2ram(journald → RAM, 1h마다 `/data/log` 동기화). Docker `data-root: /data/docker`, tailscaled `--statedir /data/tailscale`, chrony drift 파일 `/data/chrony`.
- **Docker**: 컨테이너 3개, compose 스케치:
```yaml
services:
  go2rtc:
    image: ghcr.io/alexxit/go2rtc@sha256:<digest>      # 다이제스트 고정
    network_mode: host                                  # WebRTC 8555/udp+tcp, RTSP 8554, loopback 공유
    volumes: ["/data/config/go2rtc.yaml:/config/go2rtc.yaml:ro"]   # api/rtsp/webrtc 설정만 — 카메라 URL·비밀 없음
    environment: [GO2RTC_API_USER=api, GO2RTC_API_PASS_FILE=/run/secrets/go2rtc_pass]
    secrets: [go2rtc_pass]
    user: "1002:1002"
    read_only: true
    tmpfs: ["/tmp"]
    security_opt: ["no-new-privileges:true"]
    restart: always
    mem_limit: 512m
  api:
    image: ghcr.io/<org>/picam-api@sha256:<digest>
    network_mode: host                                  # :8443 서빙(비루트), 127.0.0.1:1984 프록시·런타임 등록
    volumes:
      - "/data/db:/data/db"
      - "/data/clips:/data/clips"
      - "/data/config:/data/config"                     # master.key·setup-token 0600, tls/, previous.env
      - "/data/backups:/data/backups"
      - "/mnt/backup:/mnt/backup"                       # 외부 USB (age 아카이브)
      - "/run/picam/ring:/ring:ro"                      # 링 상태 보고용
    environment: [PICAM_ENV=prod, PICAM_DATA=/data, PICAM_PORT=8443, GO2RTC_URL=http://127.0.0.1:1984]
    secrets: [go2rtc_pass]
    user: "1001:1001"
    read_only: true
    tmpfs: ["/tmp"]
    security_opt: ["no-new-privileges:true"]
    healthcheck: {test: ["CMD", "curl", "-fsk", "https://127.0.0.1:8443/health"], interval: 30s, timeout: 3s, retries: 3, start_period: 20s}   # 상태 표시용 — 재시작은 호스트 타이머가 수행
    restart: always
    mem_limit: 1g
  recorder:                                             # ffmpeg 링버퍼 + 클립 조립 (api와 분리: 좀비 격리)
    image: ghcr.io/<org>/picam-recorder@sha256:<digest>
    network_mode: host
    volumes: ["/data/clips:/data/clips", "/run/picam/ring:/ring", "/data/db:/data/db"]
    user: "1003:1003"
    read_only: true
    security_opt: ["no-new-privileges:true"]
    restart: always
    mem_limit: 512m
    pids_limit: 64
secrets:
  go2rtc_pass: {file: /data/config/go2rtc_pass}         # 0600, 첫 부팅 생성
```
`/run/picam/ring`은 호스트 `tmpfs size=128m`. go2rtc.yaml 예: `api: {listen: "127.0.0.1:1984", local_auth: true, username: api, password: <env>}`, `rtsp: {listen: "127.0.0.1:8554"}`, `webrtc: {listen: ":8555", candidates: [<LAN_IP>:8555, <TAILNET_IP>:8555, "<LAN_IP>:8555/tcp"]}` — candidates는 API가 부팅 시 생성.
- **호스트 systemd**: `picam.service`(compose up, `Restart=always`) · `picam-health.timer`(30s, `curl -fsk https://127.0.0.1:8443/health` 3회 연속 실패 = 90s → `docker compose restart api`, 감사 `ops.restart`) · `watchdog.service`(`/dev/watchdog`, timeout 10s, `max-load-1 24`) · `chrony`(pool 2개 이상) · `tailscaled`(statedir `/data/tailscale`) · `fstrim.timer`(일 1회 `/data`) · `picam-backup.timer`(03:00) · `picam-audit-archive.timer`(연 1회, Owner 승인 플래그 필요).
- **네트워크(nftables)**: 인바운드 허용 = LAN CIDR·tailnet CIDR(100.64.0.0/10)에서 443/tcp(→ `redirect to :8443`)·8555/udp·8555/tcp; 3702/udp 멀티캐스트는 카메라 세그먼트 인터페이스에서만; 카메라 CIDR→Pi 인바운드 전부 차단(Pi→카메라 아웃바운드만); 그 외 인바운드 drop. 공개 포트 0. TLS: `tailscale cert <node>.<tailnet>.ts.net`(월 1회 타이머로 갱신 실행) + LAN 접속용 self-signed(첫 설정·비상).
- **첫 부팅**: `setup-token`을 콘솔(HDMI/시리얼)과 `/data/config/setup-token`(0600)에 기록 → Owner가 `https://<LAN_IP>`에서 마법사 진입 → Owner 생성 + **백업 복구 키 1회 표시**(복사 확인 필수) → Tailscale 인증 키 입력 → MagicDNS origin으로 안내.
- **엣지 갱신(OTA-lite)**: 이미지 태그 교체는 Owner가 설정 화면에서 "업데이트 확인 → 적용"으로 수동 승인. 이전 다이제스트를 `/data/config/previous.env`에 보관 → 롤백 = `compose up` 재실행 1명령. A/B 파티션은 P2.

### CI/CD 단계
`lint(ruff·eslint) → typecheck(mypy·tsc) → unit → integration(mock 카메라 compose) → contract(schemathesis 1.2.0) → build(arm64, 다이제스트 서명) → E2E(mock, chromium) → 24h 소크(주 1회, SC-009) → 릴리스 노트 + 다이제스트 게시 → (Owner 수동) 적용 → smoke(`/health` 200 + 카메라 전부 online ≤ 60s + 스트림 재등록 확인)`. HW rig 스위트(06)는 주 1회 수동 트리거. main 브랜치 보호: 위 게이트 전부 green.

### 설정·비밀
- 환경변수 + `/data/config/*.yaml`. 비밀 4종: `master.key`(카메라 자격증명 암호화), 세션 서명키, `go2rtc_pass`, TLS 키 — 모두 첫 부팅 생성·0600. 복구 키는 서버에 해시만. 코드·이미지·저장소·설정 파일·백업 평문에 비밀 0.
- 시작 시 설정 스키마 검증(Pydantic Settings) — 실패 시 기동 거부 + 명확한 로그(fail fast).
- 폐쇄망 아님이 전제(Q3). 인터넷 없이도 LAN 기능은 동작(NTP 미동기 시 E-11 보정 모드, RTC 배터리로 정상 시계 유지).

## 관측성

### SLI / SLO (적게, 100% 금지, 사용자 기대 기준)
| SLI | 정의 | SLO |
|---|---|---|
| 스트림 가용성 | 카메라별 `last_frame_at` ≤ 10s인 분(分)의 비율 | ≥ 99.0%/30일 (≈ 7.2h 허용); 출시 전 24h 소크 ≥ 99.5%(SC-009) |
| 라이브 시작 지연 | WHEP 201 → 첫 프레임 디코드 | p95 ≤ 3s |
| PTZ 성공률 | 202 응답 중 fault 없이 stop까지 완료 | ≥ 99% (fault·범위 거부는 분리 집계) |
| 클립 정확성 | 이벤트 대비 클립 stored 비율 | ≥ 99% |
| 보존 준수 | 만료 후 1h 내 purged 비율 | 100% (법정 — 예외적으로 100%, 위반 = 즉시 알람) |

### 4 골든 시그널 계측
- **Latency**: `http_request_seconds{route,status}`(성공/실패 분리), `whep_first_frame_seconds`, `ptz_roundtrip_seconds`
- **Traffic**: `sessions_active{user}`, `events_total{camera}`, `ptz_commands_total{camera,result}`
- **Errors**: `http_errors_total{route,code}`, `camera_reconnects_total{camera}`, `ptz_faults_total{camera}`, `ptz_range_denied_total{camera}`, `clip_failures_total`, `backup_failures_total`
- **Saturation**: `disk_used_ratio{mount="/data"}`, `ring_buffer_bytes`, CPU·메모리(API가 `/proc` 읽음), `go2rtc_streams_active`, `rtsp_sessions{camera}`
- 노출: `GET /health/details`(현재값) + **15분 롤업을 SQLite `metrics_rollup`에 24h 보관**(`GET /health/rollup`). 외부 수집기 없음(non-goal).

### 로깅 전략
- **무엇을**: 구조화 JSON — `auth.*`, `camera.state`(online/offline/ptz_fault 전이), `ptz.*`, `event.*`, `clip.*`(created/purged/failed + sha256), `retention.run`, `disk.warn`, `ntp.state`, `backup.*`, `update.apply/rollback`, `ops.restart`, 요청 로그(경로·상태·소요·user_id, 본문 없음). 감사로그는 DB(append-only)가 원본이고 로그는 부본.
- **어디에**: stdout → journald → **log2ram(RAM)** → 1h마다 `/data/log/`(NVMe)로 동기화. SD카드에는 쓰지 않는다(P1 SD 마모).
- **얼마나**: `/data/log/` 14일 로테이션(logrotate, 총 상한 500MB). 감사로그 DB 1년(05 아카이브 절차). 디버그 레벨(ONVIF raw XML)은 카메라별 스위치로 24h 자동 해제.
- **마스킹**: 비밀번호·세션 쿠키·RTSP URL 자격증명 부분(`rtsp://user:***@`) — 로그 파이프라인 단 정규식 마스킹 + 06 로그 grep 게이트.

### 대시보드 (dashboard-builder — 운영자 질문 4개, 패널 최소 세트)
| 질문 | 패널 | 단위·임계 |
|---|---|---|
| 건강한가? | 카메라별 상태 타일(online/offline/ptz_fault, 마지막 프레임 경과) · 스트림 가용성 30일 % · 보존 준수(만료 미파기 건수) · 마지막 백업 성공 시각 | 경과 >10s 경고, 미파기 >0 위험, 백업 >48h 위험 |
| 어디가 막혔나? | 라이브 시작 p95 · PTZ 왕복 p95 · 재접속 횟수/카메라 · 활성 세션(전역 상한 16) · RTSP 세션/카메라 | p95 >3s 경고 |
| 무엇이 변했나? | 최근 24h 이벤트/클립 생성 추이 · 범위 거부 횟수 · 최근 업데이트 적용 시각·다이제스트 · NTP 동기 상태 | — |
| 무엇을 해야 하나? | 디스크 사용률(80% 경고/90% 위험) · 링버퍼 크기 · 활성 알람 목록(런북 링크) | 80/90% |
허영 패널(요청 수 총합, CPU 그래프 상시 등)은 상세 화면으로 내리고 첫 화면에서 제외.

## 알림 ("조치 가능한 알람만", 알람:런북 = 1:1)
수신 경로: **앱 내 배너(P1, 항상)** + **웹훅 URL 1개(P2, FR-028 — Owner가 등록한 https 수신처; 5분 heartbeat 포함)**. 이메일·SMS·메신저 개별 연동은 non-goal(웹훅 수신처에서 변환).
| 조건 | 심각도 | 수신자 | 런북 |
|---|---|---|---|
| 카메라 offline ≥ 5분 (카메라별) | 경고 | Owner 배너 + 웹훅 | RB-1 카메라 오프라인 |
| 만료 클립 미파기 > 0 (1h 이상) | 위험 | Owner 배너 + 웹훅 | RB-2 보존 파기 실패 |
| `/data` 사용률 ≥ 90% | 위험 | Owner 배너 + 웹훅 | RB-3 디스크 풀 |
| `ptz_fault` 발생 | 경고 | Owner 배너 + 웹훅 | RB-4 PTZ 정지 실패 |
| 서비스 다운 | 위험 | **Pi는 스스로 알릴 수 없음** — (P1) Owner가 접속 실패로 인지 / (P2) 웹훅 heartbeat 10분 부재를 수신처가 감지 | RB-5 서비스 다운 |
| NTP 미동기 ≥ 30분 | 경고 | Owner 배너 + 웹훅 | RB-6 시간 미동기 |
| 백업 실패 또는 48h 이상 성공 없음 | 경고 | Owner 배너 + 웹훅 | RB-7 백업 실패 |
| 로그인 실패 IP 차단 발생 | 정보 | 감사로그만(알람 아님) | — |
원인 지표(CPU·메모리·재접속 횟수)는 대시보드로만, 알람으로 울리지 않는다.

## 장애·복구

### 시나리오 표
| 장애 | 감지 | 영향 | 복구 절차 | RTO / RPO |
|---|---|---|---|---|
| 카메라 1대 오프라인 (RB-1) | camera.state offline, 알람 5분 | 해당 타일만 | ① `curl -sk https://127.0.0.1:8443/health/details` ② 카메라 ping/`ffprobe rtsp://…` ③ 카메라 전원 재인가 ④ 자격증명 3회 실패로 disabled면 설정 화면에서 재입력(StreamRegistrar 재등록) | 10분 / 0 (그 사이 이벤트 미수집) |
| 보존 파기 실패 (RB-2) | 만료 미파기 >0 | 법정 보존기간 초과 | ① `docker compose logs api \| grep retention.run` ② `touch /data/clips/.w`로 쓰기 가능 확인 ③ `docker compose exec api picam retention run --now` ④ `GET /clips/{id}` 410 확인 | 1h / 0 |
| 디스크 90% (RB-3) | disk.warn | 신규 클립 저장 실패 위험 | ① `df -h /data` ② 자동정리 로그 확인 ③ `picam retention purge --oldest 10%` ④ 보존기간 단축 또는 NVMe 증설 검토 | 30분 / 0 |
| PTZ 정지 실패 (RB-4) | ptz.fault | 카메라 목적 외 방향 고정 우려 | ① 카메라 웹UI로 직접 Stop/홈 프리셋 복귀 ② 전원 재인가 ③ 검증 카메라 목록에서 해당 모델 PTZ 비활성 ④ 감사로그 확인 | 15분 / 0 |
| 서비스 다운 (RB-5) | 접속 실패(P1) / heartbeat 부재(P2) / 워치독 재부팅 | 전체 | ① `systemctl status picam picam-health.timer` ② `docker compose ps` ③ `docker compose restart` ④ 재발 시 롤백: `cp /data/config/previous.env /data/config/current.env && docker compose up -d` ⑤ SD 이미지 재작성(설정·DB는 /data) | 15분 / 0 |
| NTP 미동기 (RB-6) | ntp.state | 보존 계산 보정 모드 | ① `chronyc tracking` ② 방화벽 123/udp 확인 ③ RTC 배터리 확인 ④ 동기 후 `picam retention run --now` | 1h / 0 |
| 백업 실패 (RB-7) | backup.fail | 복구 가능 시점 정체 | ① `mount \| grep /mnt/backup` ② USB 재장착 ③ `picam backup run --now` ④ 복구 키 보관 여부 재확인 | 1h / 최대 48h |
| NVMe 고장 | 부팅 실패·I/O 에러 | 클립·DB·설정 전부 | ① 새 NVMe 장착 ② USB 최신 아카이브를 `picam restore --from /mnt/backup/<파일> --recovery-key`로 복원(DB·config·tailscale 상태) ③ 클립은 소실(단일 사본 정책, 사용자 고지) | 4h / **24h(외부 USB 정상 시), USB 미장착·고장 시 복구 불능** |
| SD카드 고장 | 부팅 실패 | OS만 | 이미지 재작성(오버레이 읽기 전용이라 마모 최소) → 부팅 → `/data` 자동 마운트 | 1h / 0 |
| 감사로그 체인 불일치 | `picam audit verify` 실패·AUD-2 | 부인방지 신뢰 상실 | ① 불일치 첫 행 확인 ② 백업 매니페스트의 마지막 `row_hash`와 대조해 변조 시점 특정 ③ 변조 사실을 감사 `audit.tamper-detected`로 기록(체인은 그 지점부터 새 앵커) ④ 물리 접근 이력 점검 | 4h / 0 |

### 백업
- **무엇을**: `/data/db/picam.sqlite`(`sqlite3 .backup` 온라인 스냅샷), `/data/config/`(master.key·TLS·go2rtc_pass·compose·현재/이전 다이제스트), `/data/tailscale/`, 매니페스트(감사 마지막 `row_hash`·파일 해시). **클립은 제외**(단일 사본 정책 — decision-log #22). 전체를 tar → **age 암호화**(복구 키; 서버에는 해시만).
- **주기·보관처**: 매일 03:00 → **외부 USB `/mnt/backup/`(30세대, 필수 BOM)** + NVMe `/data/backups/`(7세대 부본). USB 미장착 시 `backup.fail` 알람(RB-7). 감사로그 연 1회 아카이브(05 절차)도 같은 매체.
- **복원 리허설**: **분기 1회** — 예비 SD + 빈 NVMe에 이미지 재작성 → `picam restore` → `/health` 200 + 카메라 online + `picam audit verify` ok + 매니페스트 일치. 결과를 감사 `ops.restore-drill`로 기록.

### 런북 골격 (RB-1~7 공통)
```
# RB-n <제목>
메타: 알람 <조건> / 심각도 / 담당 Owner
트리거·영향: <무엇이 울렸고 사용자가 무엇을 못 하나>
진단: <복붙 명령 3~5개, 기대 출력>
해결: <단계별 명령>
에스컬레이션: 30분 내 미해결 → 카메라 벤더 지원 / 커뮤니티 이슈 등록 (검증 카메라 목록 갱신)
검증: /health 200, 대시보드 해당 패널 정상, 감사로그 ops.* 기록
롤백: previous.env로 compose up -d
```

## 준비도 체크리스트 (deployment-patterns 번안, 출시 전)
- [ ] 06 스위트 전부 green(MVP 게이트 SC), HW rig 1회 통과(SC-001/001b/003/004/011), 24h 소크 ≥ 99.5%(SC-009)
- [ ] 이미지 3개 다이제스트 고정·서명, 비루트(UID 3종)·read_only·no-new-privileges
- [ ] 코드·이미지·저장소·go2rtc.yaml·백업 아카이브(평문)에 비밀 0 (gitleaks + 06 grep 게이트)
- [ ] `/health`·`/health/details`·`/health/rollup` 노출 범위 확인(공개 포트 0, nftables 규칙·443→8443 리다이렉트 적용)
- [ ] 알람 7종 실제 발화 테스트(웹훅은 P2) + 런북 7개 링크 유효, `picam-health.timer` 3회 실패 재시작 실측(90s)
- [ ] 백업 잡 1회 성공(USB) + 복원 리허설 1회 완료 + 복구 키 보관 확인
- [ ] 롤백 1회 실제 수행(이전 다이제스트로 up → health 200)
- [ ] 오버레이 읽기 전용 + Docker data-root·tailscale statedir=/data 상태에서 재부팅 3회 정상, RTC 배터리 장착 후 오프라인 재부팅 시계 유지
- [ ] 안내판 5항목(FR-011) 입력 안내 + 운영·관리 방침 템플릿 문서 동봉, PTZ 허용 범위 설정 안내
