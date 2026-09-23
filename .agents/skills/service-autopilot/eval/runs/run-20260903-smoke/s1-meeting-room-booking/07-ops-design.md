# 배포·운영 설계 — RoomBook (사내 회의실 웹 예약)

> 근거 (A7 진입 사전조사, 2026-09-03)
> - **정량**: 4 골든 시그널(지연·트래픽·오류·포화)과 "증상 기반으로 페이지하고 원인은 대시보드로", "모든 페이지는 조치 가능해야 한다", 알람마다 플레이북 항목 — https://sre.google/sre-book/monitoring-distributed-systems/ (확인 2026-09-03). 백업은 "복원이 지루해질 때까지 리허설, 최소 월 1회 복원 테스트" — https://outplane.com/blog/postgresql-backup-guide (확인 2026-09-03)
> - **정성**: "pg_dump는 핵심은 잘 하지만 스케줄·저장·보존·알림·암호화·모니터링은 전부 직접 만들어야 한다" — PostgreSQL 백업 가이드 (outplane.com, 위 URL, 확인 2026-09-03). 그래서 이 문서는 그 "주변"을 전부 명시한다.
> - **사용자 영향**: 장애 시 사용자는 막다른 화면 대신 "지금은 화이트보드에 적어 주세요 — 복구되면 옮겨 드릴게요" 정적 폴백 페이지를 본다(L-06). 메일 지연은 배너로 미리 알린다(EP-04 `mail_degraded`).

## 배포

### 런타임·형상 (DL-005)
- 사내 VM 1대 (권장 2 vCPU / 4 GB / 40 GB SSD, Ubuntu LTS + Docker Engine + Compose v2).
- 컨테이너 4개: `caddy`(TLS 종단 — 사내 CA 발급 인증서 파일 마운트, upstream 장애 시 `fallback.html` 서빙) · `app`(Next.js standalone) · `worker`(같은 이미지, `node worker.js`) · `db`(postgres:16-alpine). 개발용 `mailpit`은 override 파일에만. Caddy·age·gitleaks·Mailpit은 커모디티 도구로 대체 가능(04 근거 절).
- 이미지: 멀티스테이지(deps → build → runner), `node:24-alpine` 고정 태그(Node 24 Active LTS, EOL 2028-04-30 — https://endoflife.date/nodejs, 확인 2026-09-03), non-root(uid 1001), `HEALTHCHECK` 내장, `.dockerignore`에 `.env*`·`node_modules`·`.git`.

```yaml
# docker-compose.yml (프로덕션 스케치)
services:
  caddy:
    image: caddy:2-alpine
    ports: ["443:443"]
    volumes:
      - ./caddy/Caddyfile:/etc/caddy/Caddyfile:ro   # reverse_proxy app:3000; handle_errors → fallback.html; 보안 헤더(CSP·HSTS·X-Frame-Options)
      - ./caddy/certs:/certs:ro                      # 사내 CA 발급 인증서·키
      - ./caddy/fallback.html:/srv/fallback.html:ro
    depends_on: [app]
    restart: unless-stopped
  app:
    image: registry.internal/roombook:${TAG}      # CI가 sha 태그로 푸시
    env_file: .env                                 # 미커밋, 0600
    environment: { ROLE: app }
    # ports 없음 — caddy만 Docker 네트워크로 접근
    depends_on: { db: { condition: service_healthy } }
    restart: unless-stopped
    read_only: true
    tmpfs: [/tmp]
    security_opt: [no-new-privileges:true]
    deploy: { resources: { limits: { cpus: "1.0", memory: 768M } } }
    healthcheck: { test: ["CMD", "wget", "-qO-", "http://localhost:3000/api/health"], interval: 30s, timeout: 3s, retries: 3, start_period: 20s }
    logging: { driver: json-file, options: { max-size: "20m", max-file: "7" } }
  worker:
    image: registry.internal/roombook:${TAG}
    command: ["node", "worker.js"]
    env_file: .env
    environment: { ROLE: worker }
    depends_on: { db: { condition: service_healthy } }
    restart: unless-stopped
    deploy: { resources: { limits: { cpus: "0.5", memory: 256M } } }
    logging: { driver: json-file, options: { max-size: "20m", max-file: "7" } }
  db:
    image: postgres:16-alpine
    env_file: .env.db                              # POSTGRES_PASSWORD 등, 미커밋
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./db/init:/docker-entrypoint-initdb.d      # btree_gist·citext 확장, roombook_app 계정·권한(REVOKE audit UPDATE/DELETE)
    restart: unless-stopped
    healthcheck: { test: ["CMD-SHELL", "pg_isready -U postgres"], interval: 10s, timeout: 3s, retries: 5 }
    # ports 없음 — Docker 네트워크 내부에서만 접근
volumes: { pgdata: {} }
```

### CI/CD 단계
```
PR:       lint(eslint + "문자열 결합 SQL 금지" 규칙) → typecheck → unit(vitest) → integration(Testcontainers PG + Mailpit) → contract(openapi 스키마) → build 이미지(푸시 안 함)
main 병합: 위 전부 → npm audit(high 이상 실패) → 이미지 빌드·푸시(registry.internal/roombook:<sha>) → E2E 3여정(스테이징 compose) → 태그 승격 `release-<date>`
배포(수동 트리거, 평일 22:00 이후만 — SC-007):
  1. VM에서 `TAG=<sha> docker compose pull`
  2. `docker compose run --rm app node migrate.js`   # Drizzle 마이그레이션, 전방 호환(컬럼 삭제 금지, 2단계 배포)
  3. `TAG=<sha> docker compose up -d`                 # app·worker 재생성 (다운 ≤ 30초)
  4. smoke: `GET /api/health` 200 + E2E-1 1회 (스테이징 계정)
  5. 실패 시 롤백: `TAG=<이전 sha> docker compose up -d` (마이그레이션은 전방 호환이므로 스키마 롤백 불필요)
```
- 태그 2개(현재·직전)를 항상 레지스트리와 VM 로컬에 보관 — 즉시 롤백.
- 최초 배포: `.env`에 `BOOTSTRAP_ADMIN_EMAIL` → 앱 기동 시 해당 사용자를 admin으로 upsert(최초 1회, 이후 EP-17로 관리). 일반 직원 계정은 사전 등록 없이 첫 매직링크 요청 때 JIT 생성(FR-001). 회의실 마스터는 admin 화면에서 입력.

### 설정·비밀 (12-factor, 기동 시 zod 검증 — 불량이면 기동 실패)
| 변수 | 예 | 비고 |
|---|---|---|
| `DATABASE_URL` | `postgres://roombook_app:***@db:5432/roombook` | 앱 계정(감사 로그 UPDATE/DELETE 없음) |
| `RETENTION_DATABASE_URL` | `postgres://roombook_retention:***@db:5432/roombook` | Worker의 RetentionJob 전용(파기·익명화 권한, INV-3 예외) |
| `APP_BASE_URL` | `https://roombook.corp.example` | 매직링크 URL 생성 |
| `ALLOWED_EMAIL_DOMAINS` | `corp.example` | 쉼표 구분 |
| `SMTP_URL` · `MAIL_FROM` | `smtp://relay.corp.example:587?requireTLS=true` · `roombook@corp.example` | 사내 릴레이, STARTTLS 필수(평문 25 금지 — 04 STRIDE T). 릴레이가 없어 SES를 쓰면 개인정보 처리위탁 검토가 전제(02 축5) |
| `BOOTSTRAP_ADMIN_EMAIL` | `it-admin@corp.example` | 최초 admin |
| `TZ` | `Asia/Seoul` | 표시 시간대 (저장은 UTC) |
| `LOG_LEVEL` | `info` | |
| `BACKUP_AGE_RECIPIENT` | age 공개키 | 백업 암호화 |
- 비밀은 `.env`·`.env.db`(0600, 미커밋)에만. 저장소는 gitleaks CI 스캔. 폐쇄망 아님(Q5-A)이므로 오프라인 설치 경로는 불필요 — 단, 이미지 `docker save` tar를 배포마다 VM에 함께 보관(레지스트리 장애 대비).

## 관측성

### SLI / SLO-lite (사용자 대면 서비스: 가용성·지연·정확성)
| SLI | 측정 | SLO | 근거 |
|---|---|---|---|
| 가용성 | 근무시간(평일 07~22 KST) 1분 주기 `GET /api/health` 200 비율 | ≥ 99.5% / 월 | SC-007. 100%는 금지(SRE) |
| 예약 확정 지연 | `POST /api/bookings` 서버 처리 시간 p95 (성공·실패 분리) | ≤ 1,000ms | SC-003 |
| 그리드 조회 지연 | `GET /api/bookings` p95 | ≤ 300ms | SC-003 LCP 2s의 서버 몫 |
| 메일 도착 | outbox `sent_at - created_at` p95 (magic_link) | ≤ 60s | SC-006 |
| 정확성 | 겹치는 confirmed 예약 건수 (일 1회 SQL 점검) | 0 | SC-001 |
SLO는 4개뿐. 사용자 기대(화이트보드보다 빠르고, 근무시간에 열려 있음)에서 도출했다.

### 4 골든 시그널 계측
| 시그널 | 지표 | 어디서 |
|---|---|---|
| Latency | 라우트별 처리 시간 히스토그램, 성공/오류 분리 | 앱 미들웨어 → 구조화 로그 필드 `duration_ms` |
| Traffic | 분당 요청 수(라우트별), 활성 세션 수 | 로그 집계, `session` 테이블 카운트 |
| Errors | 5xx 비율, 409/422/429 카운트(정상 동작이므로 오류 아님·대시보드만), outbox failed | 로그 + `/api/health` 필드 |
| Saturation | 컨테이너 CPU/메모리(`docker stats`), 디스크 사용률, DB 연결 수, outbox pending 수·최대 지연 | `alert-check.sh`가 `docker stats --no-stream`·`df` + `/api/health` 필드를 읽음 |

`/api/health`(EP-19)는 인프로세스 5분 롤링 창으로 `p95_5m_ms`·`error_rate_5m`·`disk_pct`·`db_connections`·`outbox_lag_s`·`outbox_failed_1h`·`last_backup_at`을 노출한다 — 단일 인스턴스이므로 별도 지표 저장소 없이 충분하다. 사내에 Prometheus가 있으면 같은 값을 `/api/metrics`로 노출하는 것은 P2.

### 로깅 전략
- **무엇을**: 요청 로그(요청 ID·라우트·상태·`duration_ms`·`user_id`) · 도메인 이벤트(booking.created/updated/cancelled/released, auth.link_sent/verified/failed, outbox.sent/failed, retention.run) · 오류(스택은 로그에만).
- **어디에**: 컨테이너 stdout(JSON) → Docker json-file 드라이버. 중앙 수집은 사내에 Loki/ELK가 있으면 Promtail로 전송(Assumed: 있으면 연결, 없으면 VM 로컬 보관).
- **얼마나**: 로컬 로테이션 20MB × 7파일(≈ 7일) 서비스당. 중앙 전송 시 30일. 감사 이력은 로그가 아니라 `booking_audit`(1년).
- **개인정보 마스킹**: 로그에 이메일·이름 금지(`user_id`만), 매직링크 토큰·세션 ID 절대 기록 금지, 예약 제목 기록 금지.

### 대시보드 (운영자 질문 3개에만 답한다)
1. "지금 예약이 되나?" — 가용성 30일 추이, 예약 확정 p95, 5xx 비율
2. "메일이 나가나?" — outbox pending 수·최대 지연·failed(1h)
3. "디스크·DB가 괜찮나?" — 디스크 %, DB 연결 수, 백업 마지막 성공 시각

## 알림 — 모든 알람은 조치 가능, 알람:런북 = 1:1, 증상 기반
**평가·발송 주체**: 호스트 cron이 1분마다 `alert-check.sh`를 실행 — `/api/health` JSON(위 필드) + `docker inspect --format '{{.State.Health.Status}}'` + `df` + 백업 파일 mtime을 읽어 아래 임계를 평가하고, 상태 파일로 중복 억제(같은 알람은 30분에 1회)한 뒤 사내 메신저 웹훅(Assumed, 02 하류 신규)으로 보낸다. 웹훅이 없으면 이메일 — 단 메일 장애 알람(RB-02)은 그 경로로 못 가므로 IT 담당 전화 목록을 스크립트에 둔다. 근무시간 외(평일 22:00~07:00·주말)는 평가만 하고 발송하지 않는다.

| 조건 | 심각도 | 수신자 | 런북 |
|---|---|---|---|
| `/api/health` 3회 연속 실패(3분) 근무시간 중 | P1 | IT 담당(전화/메신저) | RB-01 서비스 다운 |
| outbox `magic_link` 최대 지연 > 5분 또는 failed ≥ 3건/10분 | P1 | IT 담당 | RB-02 메일 발송 장애 (= 로그인 불가) |
| 예약 확정 p95 > 2s (5분 창) 또는 5xx > 2%/5분 | P2 | IT 담당(메신저) | RB-03 지연·오류 |
| 디스크 사용률 > 80% | P2 | IT 담당 | RB-04 디스크 |
| 백업 24h 이상 성공 없음 | P2 | IT 담당 | RB-05 백업 실패 |
| 겹치는 confirmed 예약 ≥ 1건 (일 1회 점검) | P1 | IT + 개발 | RB-06 정합성 위반(제약 누락 의심) |
| 근무시간 외 다운 | 없음(알람 안 함) | — | 07:00 헬스체크에서 재판정 |
409·422·429 증가, CPU 사용률 같은 원인 지표는 알람하지 않고 대시보드로만 본다.

## 장애·복구

### 시나리오 (상위 5)
| 장애 | 감지 | 영향 | 복구 절차 | RTO / RPO |
|---|---|---|---|---|
| 앱 컨테이너 크래시/무한 재시작 | health 실패, `docker compose ps` restarting | 예약 불가 → 폴백 페이지 | `docker compose logs --tail=200 app` → 직전 태그로 롤백 `TAG=<prev> docker compose up -d app worker` → health 확인 | 30분 / 0 |
| DB 컨테이너 다운·볼륨 손상 | health `db:"error"` | 전면 불가 | `docker compose restart db` → 실패 시 RB-07 복원(최근 백업) | 4h / 24h |
| 메일 릴레이 다운 | outbox 지연 알람 | 신규 로그인 불가(기존 세션은 member 8h·admin 2h 유지), 알림 지연 | `mail_degraded` 배너 자동 표시 → 릴레이 담당 연락 → 급한 사용자는 admin이 EP-18로 링크 수동 발급 → admin 세션까지 만료된 장기 장애면 VM에서 `docker compose exec app node cli.js issue-link <email>`(세션 불필요, 감사 기록) → 복구 후 워커가 자동 재시도 | 즉시 우회 / 0 (outbox 보존) |
| 디스크 포화 | 80% 알람 | 쓰기 실패 → 예약 불가 | `docker system prune -f`, 로그 로테이션 확인, `notification_outbox` 30일 초과분·오래된 백업 삭제, 필요 시 볼륨 확장 | 1h / 0 |
| VM 자체 손실 | health 실패 + SSH 불가 | 전면 불가 | 새 VM에 Docker 설치 → 레지스트리(또는 보관 tar)에서 이미지 → 오프사이트 백업 복원(RB-07) → DNS/프록시 전환 | 4h / 24h |
장애 중 사용자 안내: Caddy가 upstream 연결 실패·5xx 시 `handle_errors`로 정적 `fallback.html`("지금은 화이트보드에 적어 주세요 — 복구되면 옮겨 드릴게요")을 서빙.

### 백업
- **무엇을**: `pg_dump -Fc roombook`(스키마+데이터) + `.env`·`.env.db`·compose 파일(비밀은 별도 금고).
- **주기**: 매일 02:00 KST 호스트 cron. 배포 직전 수동 1회 추가.
- **보관처**: VM 로컬 `/var/backups/roombook/`(7일) + 오프사이트(사내 NAS 또는 오브젝트 스토리지) 30일. age 공개키로 암호화 후 전송.
- **복원 리허설**: 월 1회(근거 위 — outplane.com 권고 채택, 02 백업·DR 행과 일치). 임시 `postgres:16-alpine` 컨테이너에 `pg_restore` → 예약 건수·최신 예약 시각 대조 → 소요 시간 기록(RTO 4h 검증). 결과를 decision-log가 아닌 운영 일지에 남긴다.
- 백업 성공 여부는 cron 끝에 `/api/admin/…`가 아니라 파일 mtime 검사 스크립트로 알람(RB-05).

### 런북 골격 — RB-02 메일 발송 장애 (가장 사용자 영향이 큰 것)
```
메타: 알람 "outbox magic_link 지연 > 5분 / failed ≥ 3건" · 심각도 P1 · 담당 IT
트리거·영향: 신규 로그인 불가(기존 세션 8h 유지), 예약 확인 메일 지연. 예약 기능 자체는 정상.
진단:
  docker compose logs --tail=100 worker | grep outbox            # SMTP 오류 코드 확인
  docker compose exec db psql -U roombook_app -c "select status,count(*),max(now()-created_at) from notification_outbox group by 1"
  nc -zv relay.corp.example 25                                    # 릴레이 연결
해결:
  - 릴레이 장애: 메일 담당 연락. 급한 사용자는 admin 화면 > 사용자 > "로그인 링크 발급"(EP-18) → 메신저로 전달
  - admin 세션(2h)도 만료됨: docker compose exec app node cli.js issue-link <email>   # 세션 불필요, booking_audit 아닌 앱 로그 + 콘솔 출력, VM 셸 접근자만
  - 자격증명 만료: .env SMTP_URL 갱신 → docker compose up -d worker
  - 워커 크래시: docker compose restart worker
에스컬레이션: 30분 내 미복구 → 개발 담당
검증: outbox pending 최대 지연 < 60s, Mailpit/실계정으로 매직링크 1건 수신 확인, 배너 소멸
롤백: 워커 이미지 변경 직후라면 TAG=<prev> docker compose up -d worker
```
RB-01·03~07은 같은 골격(메타→트리거·영향→진단→해결→에스컬레이션→검증→롤백)으로 구현 단계에서 작성. RB-07(복원)은 리허설 절차와 동일 문서.

## 준비도 체크리스트 (ecc:deployment-patterns 축약 — 배포 전 전부 체크)
- [ ] 06 시나리오 전부 GREEN, E2E 3여정 통과 · [ ] `.env` 미커밋·gitleaks 통과 · [ ] 로그에 PII 없음(샘플 100줄 검사)
- [ ] 이미지 태그 고정·non-root·read_only · [ ] 리소스 limits 설정 · [ ] TLS 프록시 + 보안 헤더(CSP·HSTS·X-Frame-Options DENY)
- [ ] `/api/health` 사내 모니터 등록(1분) · [ ] 알람 6종 등록 + RB-01~07 존재 · [ ] 백업 cron 동작 + 첫 복원 리허설 완료
- [ ] 직전 태그 롤백 리허설 1회 · [ ] `fallback.html` 프록시 설정 · [ ] 총무에게 관리자 화면·화이트보드 병행 4주 계획 전달
