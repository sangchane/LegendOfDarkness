# 배포·운영 설계 — 요양시설 낙상 감지·알림 서비스 (FallGuard)
버전: v1.1 · 기준 03 v1.2
개정 v1.1: GATE 1차 반영 — 임계를 03 상수 이름으로, VM·Node 버전을 가정(A-8·A-9)으로 표기, 관측성 스택 축소(외부 업타임 모니터 + J-09 + SQL 뷰), 카나리 규칙, AL-03 조건, 런북 착수 시점, 착수 자산 4건 정정.

> 근거 (A7 진입 사전조사, 검색 1회, 2026-09-07)
> - **정량** — Mosquitto `persistence true`는 연결·구독·큐 메시지를 `mosquitto.db`에 쓰고 재시작 시 복원하며, 디스크 반영은 종료 시 + `autosave_interval`(초) 주기다. 공유 구독(`$share/<group>/<topic>`)의 구독/해제 불일치 버그는 2.0.19에서 수정 — [Mosquitto persistence 문서(DeepWiki)](https://deepwiki.com/eclipse-mosquitto/mosquitto/2.7-persistence-system), [mosquitto 이슈 #2030](https://github.com/eclipse/mosquitto/issues/2030), [DataCamp Mosquitto Docker](https://www.datacamp.com/tutorial/mosquitto-docker). → `autosave_interval`을 짧게(30초) 두고 `/mosquitto/data`를 볼륨으로, 버전은 2.0.19 이상 고정.
> - **정성** — ecc:deployment-patterns 프로덕션 준비도 체크리스트 "Runbook for common failure scenarios", "On-call rotation and escalation path defined" — 2~3인 팀은 당번이 곧 팀이므로 알람:런북 1:1과 "조치 가능한 알람만"이 생존 조건.
> - **사용자 영향** — 운영 알람은 시설 직원에게 보이지 않는다. 직원이 보는 것은 콘솔 배너(채널 상태·디바이스 오프라인)뿐이며, 배너는 항상 다음 행동을 제시한다(L-06). 운영자 알람(카나리·데드맨)이 먼저 울려 직원이 "알림이 안 와요"라고 전화하기 전에 복구한다.

## 배포

### 런타임·형상 (파일럿)
- **클라우드 VM 1대**(국내 리전, 2 vCPU/4GB — 가정 A-8, 실측 후 조정, 리눅스) + **Docker Compose**. 서비스: `edge`(Caddy — TLS 자동 발급·HTTP→API 리버스 프록시), `api`(NestJS HTTP + SSE), `worker`(같은 이미지, pg-boss 워커·mqtt.js 구독 프로세스 — API와 분리해 재시작 격리), `mosquitto`(8883 mTLS), `postgres`(16, 볼륨). 관리형 PostgreSQL이 있으면 `postgres` 컨테이너를 빼고 `DATABASE_URL`만 바꾼다(권고 — 백업·HA 위임).
- 모두 `restart: unless-stopped`, 이미지 태그 고정(`node:22-alpine`, `eclipse-mosquitto:2.0.19`, `postgres:16-alpine`), 비루트, `no-new-privileges`, `read_only` + `tmpfs`.
- 디바이스 게이트웨이(모듈 방식일 때만): RPi 5 read-only rootfs + `log2ram` + HW watchdog(≤15초) + 이벤트 즉시 전송, 로컬 링버퍼 `EDGE_BUFFER_MAX`. OTA는 P2(FR-024) — 파일럿은 수동 갱신.

```yaml
# infra/compose/docker-compose.yml (스케치)
services:
  edge:
    image: caddy:2-alpine
    ports: ["80:80", "443:443"]
    volumes: [./Caddyfile:/etc/caddy/Caddyfile:ro, caddy_data:/data]
    networks: [front]
  api:
    image: ghcr.io/ORG/fallguard-api:${IMAGE_TAG}
    env_file: [.env]
    environment: [ROLE=api]
    healthcheck: { test: ["CMD","wget","-qO-","http://localhost:3000/healthz"], interval: 30s, timeout: 3s, retries: 3 }
    depends_on: { postgres: { condition: service_healthy } }
    security_opt: [no-new-privileges:true]
    read_only: true
    tmpfs: [/tmp]
    networks: [front, back]
  worker:
    image: ghcr.io/ORG/fallguard-api:${IMAGE_TAG}
    env_file: [.env]
    environment: [ROLE=worker]        # pg-boss 워커 + mqtt.js 구독(수동 ack)
    depends_on: [postgres, mosquitto]
    networks: [back]
  mosquitto:
    image: eclipse-mosquitto:2.0.19
    ports: ["8883:8883"]              # 1883은 열지 않는다 (TM-02)
    volumes:
      - ./mosquitto/mosquitto.conf:/mosquitto/config/mosquitto.conf:ro
      - ./mosquitto/acl:/mosquitto/config/acl:ro
      - mq_certs:/mosquitto/certs:ro
      - mq_data:/mosquitto/data       # persistence true, autosave_interval 30
    networks: [front, back]
  postgres:
    image: postgres:16-alpine
    volumes: [pgdata:/var/lib/postgresql/data, ./db/init:/docker-entrypoint-initdb.d:ro]
    healthcheck: { test: ["CMD-SHELL","pg_isready -U $$POSTGRES_USER"], interval: 5s, retries: 5 }
    networks: [back]                  # 외부 포트 없음
volumes: { pgdata: {}, mq_data: {}, mq_certs: {}, caddy_data: {} }
networks: { front: {}, back: {} }
```

```dockerfile
# apps/api/Dockerfile (멀티스테이지 스케치)
# node:22 태그는 가정 A-9 — 착수 시 nodejs.org Active LTS 메이저로 갱신해 고정
FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build && npm prune --omit=dev
FROM node:22-alpine AS runner
WORKDIR /app
RUN addgroup -g 1001 -S app && adduser -S app -u 1001
USER app
COPY --from=build --chown=app:app /app/dist ./dist
COPY --from=build --chown=app:app /app/node_modules ./node_modules
ENV NODE_ENV=production
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://localhost:3000/healthz || exit 1
CMD ["node","dist/main.js"]           # ROLE 환경변수로 api/worker 분기
```

### CI/CD 단계
```
PR:    lint → typecheck → unit → integration(testcontainers: postgres·mosquitto·pg-boss) → contract(OpenAPI·M-01 스키마)
main:  위 전부 → build image(태그=git sha) → migrate(staging, expand-only) → deploy staging → smoke(카나리 M-01 발행→fall_detections 왕복 + /healthz)
       → 수동 승인 → migrate(prod) → deploy prod(compose pull && up -d, 서비스별 순차) → smoke → 태그 `prod-current` 이동
```
- **마이그레이션**: expand/contract — 컬럼 삭제·리네임은 2회 배포로 분리. 롤백 = 이전 이미지 태그로 `up -d`(DB 스키마는 하위 호환 유지). 실행 전 `pg_dump` 스냅샷.
- **모바일 앱**: Expo EAS 내부 배포 트랙(파일럿) → 스토어는 2주 버퍼(P4). 최소 버전은 E-04.
- **스모크 = SC-009 카나리와 같은 스크립트** (`scripts/canary-publish.ts`): 카나리 디바이스(`devices.is_canary`)로 M-01 발행 → `CANARY_ROUNDTRIP_MAX` 안 `fall_detections` 행 확인. 카나리는 이벤트·알림을 만들지 않고 집계에서 제외된다(05 M-01 규칙). 운영 중에는 J-09가 `CANARY_INTERVAL`마다 같은 스크립트를 돌리고 E-30 `canary_last_ok_at`을 갱신한다.

### 설정·비밀
- 전부 환경변수, 시작 시 zod 스키마로 검증(누락이면 기동 실패). 03 상수는 `packages/contracts/constants.ts`에 **이름 그대로** 두고 환경변수로 덮어쓰지 않는다(테스트 축소 값만 `TEST_*`).
- 비밀 저장: 클라우드 시크릿 매니저 → 배포 시 `.env` 생성(파일 권한 600, git 제외). 분기 1회 회전(TM-19). 디바이스 CA 개인키는 오프라인 보관, 발급 서버에는 중간 CA만.
- 폐쇄망 해당 없음(클라우드 SaaS, Q5).

## 관측성

### SLI / SLO — 가능한 한 적게
| SLI | SLO | 측정 |
|---|---|---|
| 수신 경로 가용성(카나리 MQTT→DB 왕복 성공률) + HTTP `/healthz` 200 비율 | ≥ `API_AVAILABILITY` (월) | `CANARY_INTERVAL` 카나리·외부 업타임 모니터 — SC-009. 파일럿 SC 집계 기간은 `PILOT_DURATION` |
| 알림 지연 `push_ack_at − received_at` p95 | ≤ `ALERT_LATENCY_P95` (주 단위 창) | SQL 집계(04 관측성 타임스탬프) — SC-001 |
| 알림 실패율(채널별 `failed / 전체`, 폴백 후 최종 실패) | ≤ `NOTIFY_FAIL_RATE_MAX` | `notifications` 집계 |
100% 금지. SLO 위반은 알람이 아니라 **주간 리뷰 항목**; 알람은 아래 증상 기반.

### 4 골든 시그널 계측
| 시그널 | 지표 | 어디서 |
|---|---|---|
| Latency | `/fall-events/*` p50/p95(성공·실패 분리), 파이프라인 단계별 지연(received→notified→push_ack→acked) | HTTP 미들웨어 히스토그램 + DB 타임스탬프 |
| Traffic | M-01 수신/분, heartbeat/분, 알림 발송/분, SSE 연결 수 | 카운터 |
| Errors | 5xx 비율, 채널별 발송 실패, MQTT ack 실패, J-01 CAS no-op 비율(중복 실행 지표) | 카운터 + `notifications.error_code` |
| Saturation | pg-boss 대기 잡 수·최고령 잡 나이, DB 커넥션 사용률, 디스크(`pgdata`·`mq_data`), 브로커 큐 길이 | pg-boss 테이블 쿼리, `pg_stat_activity`, J-09의 `df` 스크립트 |

수집(파일럿): **외부 업타임 모니터**(E-30 `/healthz` 1분 — 워커 데드맨·카나리 연속 실패 시 503) + **워커 J-09 `ops-alarm`**(AL 조건을 DB·`df`에서 평가) + **운영자용 SQL 뷰** `ops_*` 6개(미확인 이벤트·지연 p95·오프라인·채널 상태·큐 나이·디스크). Prometheus·Grafana·Loki는 다시설 단계 — 파일럿 VM에 동거시켜 R4 SPOF를 키우지 않는다(GATE M14). 운영자 SQL 뷰가 답할 질문 4개: ① 지금 미확인 이벤트가 있나(시설별 `detected/notified` 수·최고령) ② 오늘 알림 지연 p95는 ③ 오프라인 디바이스·시설은 ④ 채널 상태(E-27)와 최근 1시간 실패율.

### 로깅 전략
- **무엇을**: 구조화 JSON(pino) — `event_id·device_id·facility_id·notification_id·job·stage·latency_ms·error_code`. 이벤트 목록: 감지 수신/병합/드롭, 라우팅 결과(수신자 수), 발송 시도/결과, ack/판정, 에스컬레이션 실행/no-op, 오프라인 전이, 채널 상태 전이, 인증 실패, RLS 컨텍스트 누락(버그 신호).
- **개인정보 마스킹**: 이름·전화는 로그에 원문 금지 — 구조상 ID만 흐르고(04), 어댑터 경계에서 전화번호는 뒤 4자리만(TM-27). 감사 기록은 `audit_log`(DB)가 담당, 로그는 증거가 아니다.
- **어디에**: 컨테이너 stdout → Docker `json-file` 드라이버(`max-size 50m, max-file 10`), 파일럿은 VM 로컬(`docker compose logs`)만. 다시설 시 중앙 Loki. 게이트웨이(있을 때)는 `log2ram` + 중요 이벤트만 MQTT로 서버 전송(SD 마모 대책).
- **얼마나**: Docker 로컬 파일 500MB 로테이션(파일럿 부하 기준 약 30일분 추정), 다시설 시 중앙 90일. `audit_log`·`notifications`·`fall_detections`는 DB에서 `EVIDENCE_RETENTION`.

## 알림 (운영자 알람) — 전부 조치 가능, 알람:런북 = 1:1
| ID | 조건 (증상 기반) | 심각도 | 수신자 | 런북 |
|---|---|---|---|---|
| AL-01 | 카나리 왕복 `CANARY_FAIL_STREAK` 연속 실패(`CANARY_ROUNDTRIP_MAX` 초과 포함) | critical | 당번 운영자(전화+메신저) | RB-01 수신 경로 장애 |
| AL-02 | 워커 데드맨: `WORKER_DEADMAN` 동안 잡 처리 0 → E-30 503 (외부 업타임 모니터가 평가 — 워커는 자신을 감시할 수 없다) | critical | 당번 운영자 | RB-02 워커 정지 |
| AL-03 | `detected/notified` 이벤트가 `STALE_ALARM_DELAY` 넘게 `escalation_level` 진행 없음이고 `escalation_level < ESCALATION_LEVELS − 1`(최대 단계 도달분 제외) | critical | 당번 운영자 | RB-03 에스컬레이션 정지 |
| AL-04 | 시설 `offline` 전이(FR-028, `FACILITY_OFFLINE_DELAY`) | critical | 당번 운영자 + 해당 시설 관리자(관리자 알림 SMS, FR-028) | RB-04 시설 인터넷 두절 |
| AL-05 | 채널 5분 실패율 ≥ `CHANNEL_DEGRADED_FAIL_RATE` 또는 J-06 `down` 전이 | high | 당번 운영자 | RB-05 발송사 장애 |
| AL-06 | 디스크 사용률 > `DISK_ALARM_PCT` (`pgdata`·`mq_data`) | medium | 당번 운영자(메신저) | RB-06 디스크 |
| AL-07 | 야간 백업 실패 또는 24시간 내 성공 백업 없음 | high | 당번 운영자 | RB-07 백업 |
| AL-08 | 디바이스 인증서·중간 CA 만료 `CERT_EXPIRY_WARN_DAYS` 전 | medium | 당번 운영자 | RB-08 인증서 갱신 |
| AL-09 | HTTP 5xx 비율 > `HTTP_5XX_ALARM_PCT` | high | 당번 운영자 | RB-09 API 오류 |
원인 지표(커넥션 수·큐 길이·CPU)는 SQL 뷰로만 보고 알람을 울리지 않는다. 평가 주체: AL-02는 외부 업타임 모니터, 나머지는 J-09. 운영자 채널은 `.env` `OPS_ALARM_*`.

## 장애·복구

### 시나리오 표
| 장애 | 감지 | 영향 | 복구 절차 | RTO / RPO |
|---|---|---|---|---|
| Mosquitto 다운 | AL-01 카나리, `docker compose ps` | 새 감지 수신 불가. 디바이스는 QoS1·영속 세션으로 로컬/브로커 큐에 보관 → 복구 후 지연 플러시(`delayed`) | `docker compose restart mosquitto` → 30초 카나리 재확인 → `mq_data` 손상 시 볼륨 백업에서 복원 후 재시작. 디바이스 재접속은 자동 | 10분 / 0 (디바이스 버퍼 `EDGE_BUFFER_MAX` 한도 내) |
| PostgreSQL 다운·손상 | AL-01·AL-09, `pg_isready` | 수신·알림·콘솔 전부 정지. 워커는 재시도 대기 | `docker compose restart postgres` → 기동 실패 시 RB-07 절차로 최신 스냅샷 + WAL 복원 → `worker` 재시작 → 카나리 확인 | `RTO` / `RPO` |
| 워커 정지(pg-boss·MQTT 구독) | AL-02 데드맨, AL-03 스테일 | 에스컬레이션·오프라인 스캔·폴백 정지, 감지는 브로커에 큐잉 | `docker compose restart worker` → 재기동 시 pg-boss가 DB의 예약 잡을 이어 실행, 브로커가 미ack 메시지 재전달 → AL-03 해소 확인 | 5분 / 0 |
| FCM 장애 | AL-05 push, E-27 배너 | 푸시 미도달 → J-07이 `CHANNEL_FALLBACK_DELAY` 후 SMS 폴백(자동) | 조치 없음(자동 폴백). FCM 상태 페이지 확인, 30분 이상이면 시설에 공지(콘솔 배너 문구 갱신) | 자동 / — |
| SOLAPI 장애 | AL-05 alimtalk/sms | SMS 폴백·보호자 통지 실패 → 콘솔 사이렌이 마지막 수단(R3) | 어댑터 환경변수로 대체 발송사(P1) 전환 또는 시설에 전화 안내. 실패 알림은 `notifications`에 남아 복구 후 수동 재발송 `scripts/resend-failed.ts --since` | 30분 / 0 |
| 시설 인터넷 두절 | AL-04 | 해당 시설 감지·알림 전부 정지(R1). 클라우드 측은 정상 | 시설 관리자에게 SMS(자동) + 전화로 LTE 백업 회선 전환 안내. 복구 후 지연 플러시 이벤트가 "지연 N건"으로 1건 알림 | 시설 회선 복구 시간 / 0 |
| VM 손실 | 업타임 모니터·AL-01 | 전체 정지 | 새 VM에 compose + 시크릿 배치 → 오브젝트 스토리지에서 `pgdata` 스냅샷·WAL·`mq_data`·인증서 볼륨 복원 → DNS 전환 → 카나리 → 디바이스 재접속(호스트명 동일) | `RTO` / `RPO` |

### 백업
- **무엇을**: PostgreSQL 전체(`pg_dump` 일 1회 + WAL 아카이브 연속), `mq_data`(mosquitto.db, 일 1회), 디바이스 CA·발급 인증서 볼륨(변경 시), `.env`는 시크릿 매니저가 원본.
- **주기·보관처**: 매일 03:30 KST(J-05 파기 잡 뒤) → 국내 리전 오브젝트 스토리지, 저장 시 암호화(TM-23), 일간 35일 + 월간 12개월 보관. 접근은 백업 롤 1개.
- **복원 리허설**: `BACKUP_RESTORE_DRILL`마다 스테이징에 최신 스냅샷+WAL 복원 → `scripts/restore-verify.ts`(최근 `RPO` 이전 이벤트 조회, `audit_log` 행 수 대조) → 소요 시간을 `RTO`와 비교해 기록 (SC-012, TS-021).

### 런북 골격 (예: RB-02 워커 정지)
```
RB-02 워커 정지
- 메타: AL-02 연결 · 심각도 critical · 소유 당번 운영자
- 트리거·영향: 5분간 잡 처리 0 → 에스컬레이션·폴백·오프라인 스캔 정지. 감지는 브로커에 큐잉(소실 없음)
- 진단:
    docker compose ps worker; docker compose logs --tail=200 worker
    psql -c "select state, count(*), min(created_on) from pgboss.job group by 1"
    psql -c "select id, status, received_at from fall_events where status in ('detected','notified') order by received_at limit 20"
- 해결: docker compose restart worker → 60초 후 pgboss.job active 증가·AL-03 해소 확인
- 에스컬레이션: 10분 내 미복구 → 2번째 운영자 호출, 시설 관리자에게 "콘솔 보드로 확인 중" 공지
- 검증: 카나리 왕복 성공, 스테일 이벤트 0
- 롤백: 직전 배포가 원인이면 IMAGE_TAG=prod-previous docker compose up -d worker
```
RB-01·03~09는 **첫 작업 3 완료 전**에 같은 골격으로 `docs/runbooks/RB-0n.md`에 작성한다(J-09 알람 규칙과 같은 PR — 알람:런북 1:1은 그 시점에 성립). 설계 단계 산출은 이 골격 1개.

## 착수 자산

### 디렉터리 구조 (최상위 2단계)
```
fallguard/
├─ apps/
│  ├─ api/           NestJS 모놀리스 (ROLE=api|worker 분기), 모듈 = 04 컴포넌트 이름 그대로
│  ├─ console/       React 관리자 콘솔 (dial 8/2/3)
│  ├─ staff-app/     Expo 직원 앱 (dial 4/2/2), E-31 수신 보고 포함
│  └─ family-page/   보호자 열람 페이지 (서버 렌더, api 안 정적 라우트로 시작해도 됨)
├─ packages/
│  ├─ contracts/     OpenAPI(05)·M-01 JSON Schema·RFC 9457 슬러그 목록·03 상수 표(constants.ts)
│  └─ device-sim/    디바이스 시뮬레이터 (M-01/M-02 발행, delayed 플러시, 카나리 겸용)
├─ db/
│  ├─ migrations/    expand/contract 마이그레이션 (05 DDL이 0001)
│  └─ init/          롤 생성(app_rw·migrator·retention), RLS 정책
├─ infra/
│  ├─ compose/       docker-compose.yml·Caddyfile·mosquitto.conf·acl
│  └─ ops/           외부 업타임 모니터 설정(E-30), 운영자 SQL 뷰(ops_*) 정의, J-09 알람 임계는 packages/contracts/constants.ts 참조
├─ scripts/          canary-publish.ts · restore-verify.ts · resend-failed.ts · issue-device-cert.ts
├─ docs/
│  ├─ autopilot/     이 패키지(00~08)
│  └─ runbooks/      RB-01~09
└─ .github/workflows/ ci.yml (PR) · deploy.yml (main)
```

### `.env.example` (키 이름과 설명만 — 값은 어떤 산출물에도 쓰지 않는다)
```
NODE_ENV=                 # production|staging|development
ROLE=                     # api|worker
PORT=                     # HTTP 포트
DATABASE_URL=             # postgres://app_rw@host/db — RLS 적용 롤
DATABASE_URL_MIGRATOR=    # DDL 전용 롤 (CI에서만)
DATABASE_URL_RETENTION=   # J-05 파기 전용 롤
MQTT_URL=                 # mqtts://mosquitto:8883
MQTT_CLIENT_CERT_PATH=    # 서버 구독 클라이언트 인증서 경로 (볼륨)
MQTT_CLIENT_KEY_PATH=
MQTT_CA_PATH=
DEVICE_CA_INTERMEDIATE_PATH=   # 디바이스 인증서 발급용 중간 CA (E-06)
DEVICE_CA_INTERMEDIATE_KEY_PATH=
JWT_SIGNING_KEY=          # 32바이트 이상, 시크릿 매니저에서 주입
FAMILY_TOKEN_PEPPER=      # 보호자 링크 토큰 해시 페퍼
FCM_SERVICE_ACCOUNT_JSON_PATH=   # FCM HTTP v1 서비스 계정
SOLAPI_API_KEY=
SOLAPI_API_SECRET=
SOLAPI_SENDER_NUMBER=     # 발신번호(사전 등록)
ALIMTALK_TEMPLATE_FALL_CONFIRMED=   # 심사 완료 템플릿 코드
PUBLIC_BASE_URL=          # 보호자 링크 도메인
LOG_LEVEL=                # info|debug
METRICS_ENABLED=          # true|false
CANARY_FACILITY_ID=       # 카나리 테스트 시설
CANARY_DEVICE_ID=
OPS_ALARM_SMS_TO=         # 당번 운영자 번호 (J-09)
OPS_ALARM_WEBHOOK_URL=    # 메신저 웹훅 (J-09)
UPTIME_MONITOR_TOKEN=     # 외부 업타임 모니터 API 토큰
```

### 첫 작업 3개 — 워킹 스켈레톤 (Impact × Uncertainty 순)
1. **벤더 페이로드 계약 + 수신 보장 스파이크** — `packages/device-sim`으로 M-01(정상·중복·재부팅 후 같은 키·지연 플러시 200건)을 발행 → `worker`가 mqtt.js 수동 ack로 `fall_detections`·`fall_events`(디바이스당 활성 1개)까지 저장. 디바이스 인증서는 `scripts/issue-device-cert.ts`로 오프라인 발급(E-05/E-06·AuthModule은 작업 2). RED 먼저: TS-023·025·026·052·053·058·062·063. **벤더 실제 샘플 페이로드 1건을 얻어 어댑터를 맞추는 것이 이 작업의 완료 조건**(R2·A-7). 산출: `db/migrations/0001`, `IngestModule`, ACL·mTLS 설정.
2. **감지→푸시→ack 얇은 관통 + 같은 트랜잭션 에스컬레이션** — `EventModule`·`RoutingModule`(shifts 최소)·`NotifyModule`(FcmAdapter 페이크)·`EscalationModule`(J-01 CAS, J-02 스테일, J-07). 직원 앱은 푸시 수신→E-31 수신 보고→E-12 ack만 있는 1화면. AuthModule 최소(E-01·부트스트랩 함수, 시드 사용자 admin 1·staff 1) + E-05/E-06 클레임 + E-32 ack 링크 포함. RED: TS-001·003·004·005·006·007·022·029·031·054·056·060·064·065. 산출: 앱 `알림 상세` 화면(03 스케치, 4상태, `next_escalation_at` 카운트다운).
3. **콘솔 활성 보드(SSE)·사이렌 + 대응 기록→보호자 통지** — E-14 SSE(시설별 채널), 보드 4상태(빈/로딩/에러/stale), E-13 resolution → J-04 → SolapiAdapter 페이크 → E-16 보호자 페이지(410 포함). 생활실 매핑 이벤트의 `resident_id` 지정(EC-C4)과 FR-028 시설 오프라인 배너 포함. RED: TS-011·012·033·034·037·039·040·059·061. 산출: RLS 정책이 HTTP·워커·SSE 세 경로에 걸린 상태(TS-014·057), 런북 RB-01~09 골격, J-09 알람 규칙.

이 3개가 끝나면 US-1·2·3·4가 스테이징에서 E2E-1로 관통된다. 나머지 FR(관리 CRUD·통계·감사 조회·파기)은 그 뒤 sonnet 등급 작업.
