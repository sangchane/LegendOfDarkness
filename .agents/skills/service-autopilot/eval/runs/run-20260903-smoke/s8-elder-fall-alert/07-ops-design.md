# 배포·운영 설계 — FallGuard-Care (요양시설 낙상 감지·알림)

> 근거 (A7 진입 사전조사, 검색 0회 — 01/04 근거 재사용)
> - **정량** — Pi HW watchdog 타임아웃 상한 15초(초과 설정 시 무한 재부팅 루프), A/B 파티션 + 서명 검증 + 자동 롤백이 무인 현장 OTA 표준 ([Mender Pi 체크리스트](https://mender.io/blog/raspberry-pi-in-production) · [AWS IoT Lens](https://docs.aws.amazon.com/wellarchitected/latest/iot-lens/), 2026-09-03) · 카카오 알림톡 대행사 단가 약 8원/건 ([Solapi](https://solapi.com/pricing))
> - **정성** — "모든 알람은 조치 가능해야 한다 … 증상 기반으로 울리고 원인은 대시보드로" ([Google SRE Book — Monitoring](https://sre.google/sre-book/monitoring-distributed-systems/)) → 운영 알람은 9개로 제한, 각각 런북 1:1
> - **사용자 영향** — 배포·장애는 요양보호사에게 두 가지로만 보인다: "감지 중단" 알림(수동 순회) 또는 아무것도 아님. 업데이트 창은 낮 시간·순회 직후로 고정 (L-06)

스킬 적용 기록: `ecc:deployment-patterns` — 파이프라인 단계(lint→typecheck→test→build→staging→smoke→prod)·헬스체크 상세 엔드포인트·환경변수 zod 검증·롤백 체크리스트·준비도 체크리스트 채택; 전략은 롤링 대신 **클라우드 블루-그린(단일 인스턴스 스왑)·게이트웨이 A/B(Mender)** 로 특정. `ecc:docker-patterns` — 클라우드 compose(비루트·read_only·cap_drop·healthcheck·env_file) 채택; **게이트웨이는 컨테이너 대신 systemd 네이티브**(watchdog·RTC·GPIO·Mender 루트FS 업데이트와 컨테이너 레이어가 충돌, RPi 자원 절약 — decision-log #D-025).

## 배포

### 런타임·형상

| 대상 | 형상 | 비고 |
|---|---|---|
| 클라우드 (국내 리전 VM 1대, 파일럿) | Docker Compose: `cloud`(NestJS 모놀리스), `postgres:16`, `mosquitto:2`, `caddy`(TLS 종단·HSTS) | 파일럿 = VM 1대 + 관리형 PostgreSQL 대안 검토. 다시설 진입 시 컨테이너 오케스트레이션으로 이관 |
| 게이트웨이 (RPi 5, 시설당 1 + 예비 1) | Raspberry Pi OS Lite 64bit 기반 **Mender A/B 이미지**: rootfs read-only, `/data`(LUKS, SQLite·인증서), log2ram, HW watchdog(`RuntimeWatchdogSec=15`), chrony + RTC(DS3231), `fallguard-gw.service`, `mosquitto.service`, `mender-client` | 이미지에 시크릿 0. 클레임 토큰만 `/data/claim.token`(1회용) |
| 센서 (MR60FDA2 + ESP32C6) | ESPHome 펌웨어, 게이트웨이 전용 SSID, API PSK 센서별 | 펌웨어 OTA는 게이트웨이가 ESPHome OTA로 순차(1대씩, 실패 시 중단) |
| 직원 앱 (Android) | 관리형 Google Play(사내앱) 또는 서명 APK, 최소 지원 버전 API(E-04 426) | 포그라운드 서비스·배터리 최적화 제외 안내 화면 필수 |
| 관리 웹 (React) | `cloud` 컨테이너가 정적 서빙 | 외부 CDN·폰트 의존 0 |

### compose 스케치 (클라우드)

```yaml
services:
  cloud:
    image: ghcr.io/fallguard/cloud:${GIT_SHA}     # :latest 금지
    env_file: [.env.prod]                          # 시크릿은 호스트 시크릿 저장소에서 주입
    read_only: true
    tmpfs: [/tmp]
    security_opt: [no-new-privileges:true]
    cap_drop: [ALL]
    depends_on: { db: { condition: service_healthy }, mqtt: { condition: service_started } }
    healthcheck: { test: ["CMD","wget","-qO-","http://localhost:3000/health"], interval: 30s, timeout: 3s, retries: 3 }
    deploy: { resources: { limits: { cpus: "2.0", memory: 1G } } }
  db:
    image: postgres:16-alpine
    volumes: [pgdata:/var/lib/postgresql/data]
    healthcheck: { test: ["CMD-SHELL","pg_isready -U fallguard"], interval: 5s, retries: 5 }
  mqtt:
    image: eclipse-mosquitto:2
    volumes: [./mosquitto.conf:/mosquitto/config/mosquitto.conf:ro, mqttcerts:/certs:ro]
    ports: ["8883:8883"]                           # mTLS 전용, 1883 비노출
  caddy:
    image: caddy:2
    ports: ["443:443"]
volumes: { pgdata: {}, mqttcerts: {} }
```

### CI/CD 단계

```
PR:      lint → typecheck → unit(EventEngine·AuditLog 100% 분기 게이트) → integration(docker: db+mqtt+toxiproxy) → contract(openapi 스냅샷·Problem JSON·멱등성·RLS 매트릭스) → INV-07 문자열 스캔 → 시크릿 스캔
main:    위 전부 → cloud 이미지 빌드(태그=GIT_SHA) → staging 배포 → smoke(E-70, 가짜 게이트웨이 1대 DETECTED→CONFIRMED→알림톡 샌드박스) → 수동 승인 → prod 블루-그린 스왑 → 사후 smoke
gateway: 이미지 빌드(mender-artifact, 서명) → HIL 랙(RPi 2대 + 센서 2대 + 단말 1대) 24h 소크 → Mender 배포 그룹 "pilot-canary"(예비기) → 관찰 24h → "pilot" 그룹 → 실패 시 자동 롤백(부팅 후 fallguard-gw health 미확인 시 이전 파티션)
app:     unit → Maestro 플로우(알림→확인→결과·오프라인 큐) → 내부 트랙 배포 → 시설 단말 강제 업데이트는 야간 금지(06:00~18:00 창)
```

### 설정·비밀

- 클라우드: 환경변수 외부화 + 부팅 시 zod 스키마 검증(누락 시 기동 실패). 시크릿(DB 비밀번호, FCM 서비스 계정, 알림톡(Solapi)·음성(Twilio Programmable Voice, 발신번호 사전등록) API 키, 인증서 발급 CA 키, 전화번호 암호화 키)은 호스트 시크릿 저장소 → `.env.prod`(gitignore, 0600). 키 회전 분기 1회.
- 게이트웨이: 시크릿은 프로비저닝 시 발급(E-50), `/data`(LUKS)에만 저장. 이미지·리포지토리에 시크릿 0. 폐쇄망 미지원(non-goal)이므로 오프라인 설치 경로 없음 — 단, 예비 게이트웨이 교체는 인터넷 없이도 가능해야 하므로 예비기는 **사전 클레임 완료 + config 스냅샷 캐시** 상태로 비치.
- 앱: 시크릿 없음(토큰만). 서명 키는 CI 보관.

## 관측성

### SLI / SLO (가능한 한 적게, 100% 금지)

| SLI | SLO (월) | 유형 |
|---|---|---|
| 알림 경로 가용성 — DETECTED 발생 시 60초 내 직원 단말 1대 이상 `displayed` 수신 비율 | **99.5%** | 사용자 대면·가용성 |
| 감지→표시 지연 p95 | ≤ 10초 | 지연 |
| 감지 가동률 — 게이트웨이 텔레메트리 수신 분/전체 분 | **99.0%** (예비기 교체 4h 포함 여유) | 가용성 |
| 통보 정확성 — CONFIRMED 중 10분 내(수신 기준) 통보 발송 비율 | 99.0% (운영 월간 목표. 03 SC-005의 "100%"는 파일럿 30일 소표본 합격선으로 다른 지표) | 정확성·지연 |
| 오탐률 — FALSE_POSITIVE/(CONFIRMED+FALSE_POSITIVE) 주간 | ≤ 30% (품질 지표, SLO 아님 — 대시보드) | 정확성 |

### 4 골든 시그널 계측

| 시그널 | 계측 |
|---|---|
| Latency | `detected_at→displayed_at`(성공), `detected_at→ack_at`, `received_at→sent_at`(통보), 실패 요청 지연 별도 히스토그램 |
| Traffic | 시설별 DetectionSignal/시간, FallEvent/일, 통보/일, API RPS, MQTT msg/s |
| Errors | FCM 실패율, 알림톡→SMS 폴백율, 음성 발신 실패, API 5xx, MQTT 스키마 위반(DLQ), 체인 불일치 |
| Saturation | outbox 깊이, 게이트웨이 CPU/메모리/디스크 쓰기 MB/일, DB 연결 수, notification 큐 대기 |

수집: 게이트웨이 텔레메트리(60초, MQTT) → 클라우드 Prometheus 형식 `/metrics`(내부망) → Grafana(파일럿은 클라우드 VM 내). 대시보드 첫 화면은 운영자 질문 순: "지금 감지가 살아 있나(시설별 gateway 상태) → 미결 이벤트 → 오늘 오탐률 → 통보 실패".

### 로깅 전략

| 항목 | 내용 |
|---|---|
| 무엇을 | 전이 이벤트(구조화 JSON: event_id·상태·actor_id·at), 알림 발송·도달, 인증 성공/실패, 설정 변경(diff), 센서 heartbeat 요약(분 단위), 게이트웨이 systemd 저널 경고 이상 |
| 어디에 | 게이트웨이: **log2ram**(RAM 64MB, 1시간 주기 동기화, SD 쓰기 최소) + 중요 이벤트는 즉시 MQTT 텔레메트리로 클라우드 전송. 클라우드: 컨테이너 stdout → 파일(json) → 로테이션 |
| 얼마나 | 게이트웨이 로컬 7일 롤링(용량 상한 200MB), 클라우드 애플리케이션 로그 90일, 감사 로그(`audit_log` 테이블)·접근기록 **3년**(03 NFR-005) |
| 마스킹 | 전화번호 `010-****-1234`, 수급자 이름 → resident_id, 토큰·키 전부 제거(미들웨어). 로그에 낙상 부상 메모 본문 금지 |

## 알림 (모든 알람은 조치 가능, 런북 1:1, 증상 기반)

| 조건 | 심각도 | 수신자 | 런북 |
|---|---|---|---|
| 외부 모니터(클라우드 밖)에서 `/health` 2회 연속 실패 (60초 간격) | **P1** | 운영 온콜 (클라우드 알람 엔진이 죽었을 때를 위한 유일한 외부 페이지) | R-9 클라우드 복구 |
| 게이트웨이 텔레메트리 180초 미수신 (`gateway-silent`) | **P1** | 시설관리자 + 근무 직원(제품 알림) + 운영 온콜 | R-1 게이트웨이 교체 |
| DETECTED 후 60초 내 어느 단말도 `displayed` 없음 (알림 경로 실패) | **P1** | 운영 온콜 + 시설관리자 | R-2 알림 경로 진단 |
| 미확인 이벤트 180초 경과 + 음성 발신 실패 | **P1** | 운영 온콜 (시설장은 제품 경로로 이미 시도) | R-3 수동 전화 연락 |
| CONFIRMED 후 10분 내 통보 미발송 (알림톡·SMS 모두 실패) | P2 | 시설관리자 + 운영 온콜 | R-4 통보 수동 처리 |
| outbox 깊이 > 5만 또는 단절 > 1시간 | P2 | 운영 온콜 | R-5 시설 회선 점검 |
| 센서 heartbeat 5분 미수신 (센서 단위) | P3 | 시설관리자 | R-6 센서 점검 |
| 게이트웨이 해시체인 불일치 | P2 | 운영 온콜 + 보안 담당 | R-7 증거 무결성 조사 |
| 디스크 쓰기 > 100MB/일 또는 `/data` 사용률 > 80% | P3 | 운영 온콜 | R-8 저장 정리 |

원인 지표(CPU·메모리·DB 연결·FCM 실패율)는 알람이 아니라 대시보드.

## 장애·복구

### 시나리오 표

| 장애 | 감지 방법 | 영향 | 복구 절차 (요약, 상세는 런북) | RTO / RPO |
|---|---|---|---|---|
| 게이트웨이 하드웨어 사망 | `gateway-silent` 180초 | 시설 감지 전면 중단 | R-1: 예비기 전원·LAN 연결 → 부팅 후 자동 클레임(사전 완료) → E-51 config 수신 → 센서 재접속 확인(관리 웹 sensors_online) → 사망기 회수·인증서 폐기 | RTO 4h / RPO 0 (클라우드 미러분) + 단절 중 미동기화 outbox는 사망기 `/data`에서 회수 시도 |
| 시설 인터넷 단절 | outbox 증가, 텔레메트리 중단(게이트웨이는 살아 있음 — LAN 알림 정상) | 통보·전화 지연 | R-5: 회선 점검 요청, 복구 시 자동 재생. 1시간 초과 시 시설장에게 "전화 에스컬레이션 불가" 안내 | RTO 회선 의존 / RPO 0 |
| 클라우드 VM 장애 | `/health` 외부 모니터 2회 실패 | 통보·전화·관리 웹 중단, **LAN 알림은 정상** | R-9: 블루 인스턴스로 스왑 또는 스냅샷 복원 → DB 복원(WAL) → 게이트웨이 자동 재접속·재생 | RTO 1h / RPO ≤ 15분(WAL 아카이브) |
| PostgreSQL 손상 | 헬스체크·백업 검증 실패 | 이력·통보 중단 | R-10: 최신 베이스 백업 + WAL 복원 → 게이트웨이 `resync` 커맨드로 미러 재검증 | RTO 2h / RPO ≤ 15분 |
| 알림톡 대행사 장애 | 폴백율 급증, 대행사 상태 페이지 | 보호자 통보 지연 | 자동: SMS 폴백. R-4: 둘 다 실패 시 관리자 수동 연락 목록 출력 | RTO 즉시(폴백) |
| 게이트웨이 OTA 실패 | Mender 배포 상태 failed, health 미확인 | 없음(자동 롤백) | Mender 자동 롤백 → 실패 원인 HIL 재현 → 재배포 | RTO 10분 / RPO 0 |
| 직원 단말 앱 크래시·삭제 | `displayed` 누락 알람 | 해당 단말 알림 불가 | R-2: 다른 단말·에스컬레이션이 흡수, 단말 재설치 | — |

### 백업

| 무엇 | 주기 | 보관처 | 보관 기간 | 복원 리허설 |
|---|---|---|---|---|
| PostgreSQL 베이스 백업 + WAL | 베이스 일 1회, WAL 연속(15분 아카이브) | 오프사이트 오브젝트 스토리지(국내 리전, 암호화) | 베이스 35일, WAL 7일, 월 1회 스냅샷 3년(법적 보존) | **분기 1회** 스테이징에 복원 → E-61 체인 검증 통과 확인 |
| 게이트웨이 `/data` | 실시간 미러(transitions) + 주 1회 `device_credentials`·설정 스냅샷 | 클라우드 | 미러 3년 | 예비기 교체 리허설 반기 1회(현장) |
| 인증서·CA·암호화 키 | 발급 시 | 시크릿 저장소 + 오프라인 봉투 | 키 수명 | 연 1회 키 회전 리허설 |
| 앱·이미지 아티팩트 | 릴리스마다 | 레지스트리 + Mender 서버 | 최근 5버전 | 롤백 리허설 = 배포 파이프라인 자체 |

### 런북 골격 (R-1 예시, 나머지 동일 구조)

```
R-1 게이트웨이 교체
- 메타: 알람 `gateway-silent` (P1), 소유자 운영 온콜, 최근 개정일
- 트리거·영향: 텔레메트리 180초 미수신. 시설 낙상 감지 중단. 직원 단말에 "수동 순회 모드" 표시됨
- 진단: 관리 웹 gateways/{id} last_seen_at 확인 → 시설에 전화: 전원 LED·LAN 링크 확인 → 전원 재투입 1회(watchdog가 못 살린 경우)
  운영자 콘솔: `mender-cli devices list --status accepted` / 클라우드 `GET /gateways/{id}`
- 해결: 시설 직원이 예비기(라벨 "예비-<시설명>")에 전원·LAN 연결 → 3분 내 last_seen_at 갱신 확인 → sensors_online/total 확인
  → 관리 웹에서 사망기 "폐기" 처리(인증서·기기 자격증명 폐기) → 직원 단말 "감지 재개" 자동 표시
- 에스컬레이션: 15분 내 예비기도 미접속 → 시설 회선(R-5) → 현장 출동
- 검증: 테스트 낙상(연기) 1회 → DETECTED→ACK→FALSE_POSITIVE 전이가 관리 웹 이력에 표시
- 롤백: 예비기 문제 시 사망기 재투입 시도, 회수한 `/data`는 봉인해 증거 보관
```

## 준비도 체크리스트 (파일럿 배포 전, ecc:deployment-patterns 이식)

- [ ] A6 전 스위트 GREEN, EventEngine·AuditLog 분기 100%, 실측 프로토콜 SC-003/004 통과
- [ ] 코드·이미지·리포지토리 시크릿 0 (CI 시크릿 스캔), `.env.prod` 0600
- [ ] 로그 PII 마스킹 확인(샘플 100줄 grep 전화번호 0)
- [ ] `/health` 상세가 db·mqtt·notify_providers 상태를 반환
- [ ] 이미지 태그 = GIT_SHA, Mender 아티팩트 서명 검증됨
- [ ] 환경변수 zod 검증 기동 테스트(누락 시 실패)
- [ ] 리소스 제한(cloud 2 CPU/1G), TLS 전 엔드포인트, 1883·5432 비노출
- [ ] 알람 9개 각각 런북 존재·온콜 수신 확인(테스트 발화), 외부 `/health` 모니터가 클라우드 밖에서 동작
- [ ] 백업 복원 리허설 1회 완료 + E-61 검증
- [ ] 예비 게이트웨이 사전 클레임·비치, 시설 직원 교체 교육 30분
- [ ] 롤백: 클라우드 이전 이미지 태그 보존, DB 마이그레이션 후방 호환(파괴적 변경 0), 게이트웨이 A/B 롤백 HIL 확인
- [ ] 식약처 질의 접수·동의 서식 법률 검토(SC-010)
- [ ] 온콜 로테이션·에스컬레이션 경로(운영 온콜 → 개발 리드 → 시설 담당) 문서화
