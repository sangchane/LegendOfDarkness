# 배포·운영 설계 — 사내 회의실 예약
버전: v1.1 · 기준 03 v1.1

lite: 배포·로그·백업·복구 각 1~2줄 + 착수 자산. 상수는 03 이름으로만 참조.

## 배포
- 런타임·형상: 사내 서버 1대(또는 IP 제한 VPS), Docker Compose 2서비스 — `app`(Next.js standalone, node:22-alpine 멀티스테이지, non-root, `HEALTHCHECK` → `/healthz`) · `db`(postgres:16-alpine, 볼륨 `pgdata`, 포트는 호스트에 노출하지 않음). 앞단 TLS는 Caddy 1개(자동 인증서, 사내 CA면 수동) — 3번째 컨테이너는 이것뿐. 롤백 = 이전 이미지 태그로 `docker compose up -d app`(이미지 태그 = git sha, 직전 2개 보관). 마이그레이션은 추가 전용(컬럼 삭제 금지)이라 앱 롤백과 독립.
- CI/CD(GitHub Actions 또는 사내 Git): lint → typecheck → unit(Vitest) → integration(testcontainers PG) → build image → push → 서버에서 `compose pull && up -d` → smoke(`/healthz` 200 + BK-1 200). E2E(Playwright 2개)는 main 머지 시만.
- 설정·비밀: `.env`(호스트, 0600) → compose `env_file`. 시작 시 zod로 검증해 빠진 키가 있으면 기동 실패. 시크릿은 git에 없음(`.env.example`만 커밋). 폐쇄망이면 이미지를 `docker save`로 반입.

## 관측성
- SLI: 사용자 대면 — 가용성(`/healthz` 200 비율), 지연(BK-1 p95), 오류율(5xx 비율), 정확성(SC-002 겹침 쿼리 0행).
- SLO-lite: 업무시간(BUSINESS_HOURS) 가용성 SLO_AVAILABILITY_PCT/월(장애 시 화이트보드 복귀 가능하므로 더 높게 잡지 않음) · BK-1 p95 ≤ TIMELINE_P95_MS · 5xx < SLO_5XX_MAX_PCT.
- 4 골든 시그널: Latency(라우트별 p50/p95, 성공·실패 분리) · Traffic(라우트별 rpm) · Errors(4xx slug별·5xx) · Saturation(DB 커넥션 사용률, 디스크 여유 %). pino 로그를 호스트 파일로 두고 주간 스크립트로 집계 — 별도 메트릭 서버 없음(1인 운영).
- 로깅: **무엇을** — `auth.login` `auth.failed` `auth.mail_failed` `auth.forbidden` `booking.created` `booking.cancelled` `booking.overlap_rejected` `room.updated` `retention.run` `http.request`(route·status·latency_ms) · **어디에** — 컨테이너 stdout → Docker json-file 드라이버 → 호스트 `/var/lib/docker/containers/...` + logrotate · **얼마나** — LOG_RETENTION_DAYS, 파일당 50MB × 10 · **마스킹** — 이메일·토큰 절대 기록 안 함, actor는 user_id만.

## 알림 (조치 가능한 것만, 알람:런북 1:1)
| 조건 | 심각도 | 수신자 | 런북 |
|---|---|---|---|
| `/healthz` 비200 5분 지속 (호스트 cron curl, 사내 메신저 웹훅) | P1 | 운영자 | RB-1 앱/DB 다운 |
| 5xx ≥ 10건/5분 | P2 | 운영자 | RB-2 오류 급증 |
| `auth.mail_failed` ≥ MAIL_RETRY_MAX건/10분 | P2 | 운영자 | RB-3 SMTP 장애 |
| 백업 파일이 BACKUP_DEADMAN_HOURS 내 갱신 안 됨 (데드맨) | P2 | 운영자 | RB-4 백업 실패 |
| 디스크 여유 < DISK_FREE_MIN_PCT | P2 | 운영자 | RB-5 디스크 |

## 장애·복구
| 장애 | 감지 | 영향 | 복구 절차 | RTO/RPO |
|---|---|---|---|---|
| RB-1 앱 컨테이너 다운 | healthz 알람 | 예약 불가(화이트보드 임시 복귀) | `docker compose ps` → `docker compose logs app --tail 200` → `docker compose up -d app`; 반복되면 직전 태그로 롤백 `APP_TAG=<prev> docker compose up -d app` | 30분 / 0 |
| RB-1b DB 다운·볼륨 손상 | healthz 503 `{db:error}` | 전체 중단 | `docker compose logs db` → 재시작 → 안 되면 아래 백업 복원 | RTO_HOURS / RPO_HOURS |
| RB-2 5xx 급증 | 로그 집계 | 일부 요청 실패 | slug·route별 집계 → 직전 배포면 롤백 → 아니면 DB 커넥션·디스크 확인 | 1시간 / 0 |
| RB-3 SMTP 장애 | mail_failed 알람 | 신규 로그인 불가(기존 세션 정상) | 릴레이 상태 확인 → 급한 사용자는 admin이 USER-3로 링크 발급해 메신저로 전달 → admin 세션도 만료면 `docker compose exec app node scripts/issue-session.js <email>` | 1시간 / 0 |
| RB-4 백업 실패 | 데드맨 알람 | 잠재 데이터 손실 창 확대 | cron 로그 확인 → 수동 `backup.sh` 실행 → 원인(디스크·권한) 수정 | 4시간 / — |
| RB-5 디스크 부족 | 여유율 알람 | DB 쓰기 실패 위험 | `docker system prune` 이미지 정리 → 로그 로테이션 확인 → 볼륨 확장 | 2시간 / 0 |

- 백업: **무엇을** `pg_dump -Fc` 전체 DB · **주기** 일 1회 03:00 (RPO_HOURS) · **보관처** 호스트 `/backup` + 다른 호스트/오브젝트 저장소로 rclone 1벌, BACKUP_RETENTION_DAYS 후 삭제 · **복원 리허설** 분기 1회 = SC-005 (아래 절차를 스테이징 컨테이너에서 실행, 소요 시간과 `count(*)` 기록).
- 복원 절차(RB-1b): `docker compose stop app` → `docker compose exec -T db pg_restore -U app -d booking --clean --if-exists < /backup/booking-<date>.dump` → `SELECT count(*) FROM bookings` 대조 → `docker compose start app` → smoke.
- 런북 골격(공통): 메타(연결 알람) → 트리거·영향 → 진단 명령 → 해결 → 에스컬레이션(1인 운영: 사내 IT 담당) → 검증(`/healthz` + BK-1) → 롤백.
- SC-001 운영 체크리스트: 도입 4주째 월~금 매일 화이트보드 사진 1장 + `SELECT count(*) FROM bookings WHERE status='active' AND during && tstzrange(<오늘>)` 기록. SC-002b: 주 1회 06의 겹침 쿼리 실행, 0행 기록.

## 착수 자산
디렉터리 구조 (최상위 2단계):
```
meeting-room-booking/
├─ src/app/            # Next.js App Router: (login|timeline|my-bookings|admin) 화면 + api/ Route Handlers
├─ src/modules/        # auth · booking · room · user · mailer · logger — 모듈 = 디렉터리, 04 컴포넌트 그대로
├─ src/db/             # drizzle 스키마 · migrations/ (0001_init.sql = 05 DDL) · seed.ts
├─ src/lib/            # problem.ts(RFC 9457) · consts.ts(03 상수를 env에서 로드) · time.ts(calendarDayToUtcRange)
├─ tests/              # unit/ · integration/(testcontainers) · e2e/(playwright) · load/(k6)
├─ deploy/             # compose.yml · Caddyfile · backup.sh · restore.sh · cron.example
├─ scripts/            # issue-session.js(RB-3) · seed-week.ts(화이트보드 이번 주 옮기기)
└─ .env.example · Dockerfile · package.json
```
`.env.example` (키와 설명만 — 값은 어떤 산출물에도 쓰지 않는다):
```
DATABASE_URL            # postgres 접속 문자열 (compose 내부 host=db)
SESSION_SECRET          # 세션 토큰 서명용, 32바이트 이상 랜덤
APP_ORIGIN              # https://booking.<사내 도메인> — Origin 검사·매직링크 URL 생성
ALLOWED_EMAIL_DOMAINS   # 쉼표 구분, 03 상수
SMTP_URL                # smtps://user:pass@relay:465 — 테스트는 file 트랜스포트
MAIL_TRANSPORT          # smtp | file (E2E용)
SLOT_MIN BOOKING_MAX_MIN LEAD_MAX_DAYS BUSINESS_HOURS MAX_ACTIVE_PER_USER   # 03 상수 (기본값은 consts.ts)
MAGIC_LINK_TTL_MIN MAGIC_LINK_RATE SESSION_DAYS RETENTION_DAYS ROOM_MAX     # 03 상수
LOG_LEVEL               # info
APP_TAG                 # 배포 이미지 태그 (git sha) — 롤백 시 직전 값
```
첫 작업 3개 = 워킹 스켈레톤 (Impact×Uncertainty 큰 순):
1. **DDL + INV-1 증명**: `0001_init.sql`(05 DDL) → testcontainers 통합 테스트 "동시 100요청 → active 1행"과 "제약 존재" 테스트를 RED로 먼저 쓰고 GREEN. 이것이 G2의 전부.
2. **예약 경로 끝까지 얇게**: `validateBookingWindow` 단위 테스트(경계값 8개) → `POST /api/bookings`(Idempotency-Key·Problem JSON·23P01→409) → `GET /api/bookings` → 타임라인 화면 1장(스켈레톤·빈 상태·에러 배너). 인증은 이 단계에서 고정 테스트 세션으로 대체.
3. **매직링크 인증 + compose 기동**: AUTH-1·2·3·4 + 세션 미들웨어 + requireRole → `deploy/compose.yml`로 app·db 기동 → `/healthz` smoke → `scripts/seed-week.ts`로 이번 주 화이트보드 내용 입력.
