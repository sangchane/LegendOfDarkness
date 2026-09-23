# 배포·운영 설계 — 사내 회의실 예약 (meeting-room-booking)
버전: v1.0 · 기준 03 v1.0 · 강도 lite (배포·로그·백업·복구 각 1~2줄 + 착수 자산)

## 배포
- 런타임·형상: 사내 VM 1대, Docker Compose 2서비스 — `web`(Next.js standalone 이미지, non-root, `read_only` + `/tmp` tmpfs, `restart: always`, 메모리 512M 제한) + `db`(`postgres:16-alpine`, named volume `pgdata`, 포트는 compose 네트워크 안에서만). 개발 compose에는 `mailpit`을 추가해 매직링크를 캡처한다(06 E2E). 단일 VM compose는 오케스트레이션 없는 운영이지만 사내 도구·1인 운영에서는 수용한다(decision-log #13).
- CI/CD 단계: `lint → typecheck → unit → integration(실 PG 서비스 컨테이너) → contract → build image(태그 = git sha) → deploy(VM에서 `docker compose pull && up -d`) → smoke(`GET /healthz` 200 + 그리드 200)`. E2E는 main 머지 시에만. 롤백 = 직전 sha 태그로 `up -d`(마이그레이션은 추가 전용·역호환만 허용).
- 설정·비밀: 전부 환경변수(`.env`는 gitignore, 아래 `.env.example`). 기동 시 zod로 검증해 빠진 값이면 즉시 실패. `ADMIN_EMAILS`로 Admin 지정. 사내망 TLS는 VM 앞 리버스 프록시(사내 표준 nginx/Caddy)가 담당하고 앱은 `Secure` 쿠키만 요구.

## 관측성
- SLI: 사용자 대면 → 가용성(`/healthz` + 그리드 200 비율)·지연(EP-04 p95)·정확성(409 비율 — 경합은 정상이므로 "급증"만 본다).
- SLO (적게): 업무시간(`OPEN_HOUR`~`CLOSE_HOUR`, 평일) 가용성 99% / EP-04 p95 ≤ `GRID_P95_MS`. 100% 금지.
- 골든 시그널: Latency(성공/실패 분리, 경로별) · Traffic(요청/분) · Errors(5xx 비율, 503 `mail-unavailable` 건수) · Saturation(DB 커넥션 사용률, 컨테이너 메모리).
- 로깅 — **무엇을**: 요청 로그(경로·상태·ms·user_id)와 이벤트 `booking.created/cancelled/conflict`, `auth.link_requested/consumed/failed/rejected`, `job.purge.done/failed`. **어디에**: 구조화 JSON을 stdout → Docker json-file 드라이버(VM 로컬). **얼마나**: `max-size 20m × max-file 10`(≈`LOG_RETENTION_DAYS` 분량), 그 이상은 보관하지 않는다. **개인정보**: 이메일·토큰·세션 id는 로그 금지, user_id만.
- 지표 수집: 별도 스택 없이 `docker logs`를 주 1회 스크립트로 집계(요청 수·p95·409 비율·5xx). 대시보드가 필요해지면 그때 Loki/Grafana 추가(YAGNI).

## 알림 (모든 알람은 조치 가능해야 한다)
| 조건 | 심각도 | 수신자 | 연결 런북 |
|---|---|---|---|
| `/healthz` 비200이 5분 지속 (VM cron 1분 폴링) | 높음 | IT 운영 1인 (사내 메신저) | RB-1 서비스 다운 |
| 5xx 비율 > 5% (10분 창) | 중간 | IT 운영 | RB-2 앱 오류 |
| `job.purge.failed` 또는 백업 파일 미생성(데드맨: 매일 04:00 검사) | 중간 | IT 운영 | RB-3 배치·백업 실패 |
| 503 `mail-unavailable` > 3건/10분 | 낮음 | IT 운영 | RB-4 메일 발송 불가 |

## 장애·복구
| 장애 | 감지 | 영향 | 복구 절차 | RTO/RPO |
|---|---|---|---|---|
| RB-1 web 컨테이너 다운/무한 재시작 | healthz 알람 | 예약 전면 불가 | `docker compose ps` → `docker compose logs --tail=200 web` → 설정 오류면 `.env` 수정 후 `up -d`; 이미지 문제면 직전 sha로 `up -d`(롤백) | 30분 / 0 |
| RB-2 DB 다운 또는 디스크 풀 | healthz `db` 실패, 5xx | 예약 전면 불가 | `df -h` → 로그·오래된 이미지 정리(`docker system prune`) → `docker compose restart db` → healthz 확인 | 1h / 0 |
| RB-3 DB 볼륨 손상·VM 유실 | 5xx 지속, 백업 데드맨 | 데이터 손실 위험 | 새 VM에 compose 배포 → 최신 `pg_dump`를 `psql`로 복원 → 앱 기동 → 그리드 조회 확인 → 직원 공지(복원 시점 이후 예약 재입력). 이 절차가 SC-005 리허설 대본 | `RTO_HOURS` / `RPO_HOURS` |
| RB-4 SMTP 장애 | 503 알람 | 신규 로그인 불가(기존 세션 정상) | 메일 서버 담당자에게 전달, 앱은 조치 없음. 장기화 시 Admin이 임시 세션 링크를 DB에서 발급하는 스크립트 실행 | 4h / 0 |
- 백업 — **무엇을**: `pg_dump -Fc` 전체 DB. **주기**: 일 1회 03:30 `TZ`(VM cron, purge 배치 뒤). **보관처**: VM 로컬 `/backup` + 사내 NAS 동기화(rsync), `BACKUP_RETENTION_DAYS` 지나면 삭제. **복원 리허설**: 분기 1회 RB-3 절차를 스테이징 컨테이너에서 실행하고 소요 시간 기록(SC-005).
- 런북 골격(각 RB): 메타(알람 연결) → 트리거·영향 → 진단 명령 → 해결 → 에스컬레이션(IT 운영 → VM 담당 → 메일 담당) → 검증(`/healthz` + 그리드 200) → 롤백.
- SC-004 주간 체크리스트(Admin, 4주): ① 시스템 밖에서 이뤄진 예약이 있었나 ② 중복·충돌 민원이 있었나 — 둘 다 "없음"이면 통과.

## 착수 자산
디렉터리 구조 (최상위 2단계):
```
meeting-room-booking/
├─ app/                 Next.js App Router 페이지·Route Handler (grid, me/bookings, admin/rooms, auth)
├─ src/
│  ├─ services/         BookingService · AuthService · RoomService · AuditLog · Mailer
│  ├─ db/               쿼리 계층, 마이그레이션(raw SQL: btree_gist·EXCLUDE), seed(rooms·admin)
│  ├─ validation/       zod 스키마 + 슬롯·운영시간 검증(INV-2, clock 주입)
│  └─ lib/              config(env zod), logger(JSON), problem(RFC 9457 응답 헬퍼), clock
├─ tests/
│  ├─ unit/  ├─ integration/  ├─ contract/  └─ e2e/ (Playwright POM)
├─ ops/                 docker-compose.yml · docker-compose.prod.yml · Dockerfile · backup.sh · restore.sh · purge cron
└─ docs/                03·05·08 사본(진실원 링크)
```
`.env.example` (키와 설명만 — 값은 어떤 산출물에도 쓰지 않는다):
```
DATABASE_URL=            # postgres 접속 문자열 (compose 내부 호스트명 db)
SESSION_SECRET=          # 세션 쿠키 서명 키, 32바이트 이상 랜덤
ALLOWED_EMAIL_DOMAIN=    # 매직링크 허용 도메인 (03 상수)
ADMIN_EMAILS=            # 쉼표 구분 Admin 이메일 목록
SMTP_URL=                # smtp://host:port (개발은 mailpit)
MAIL_FROM=               # 발신 주소
APP_BASE_URL=            # 매직링크 절대 URL 생성용
TZ=                      # 03 상수 TZ
LOG_LEVEL=               # info | debug
```
첫 작업 3개 (워킹 스켈레톤 — Impact×Uncertainty 큰 것부터):
1. **DB 스키마 + EXCLUDE 경합 테스트 RED→GREEN**: 마이그레이션(btree_gist, bookings, rooms, users) → 06 SC-001 동시 100건 integration 테스트 작성(RED) → BookingService.create + 23P01→409 변환으로 GREEN. 이 하나로 G1이 증명된다.
2. **매직링크 로그인 → 세션 쿠키 → GET /api/grid**: AuthService + Mailer(mailpit) + EP-01/02/04. 06 FR-001 시나리오 4개 RED→GREEN. 이 시점에 브라우저로 그리드가 보인다.
3. **그리드 화면 + 예약 폼 + 취소(EP-05/06/07) E2E 2개 통과**: frontend-design-taste dial(03) 적용, 빈/로딩/에러/409 상태 구현. 통과하면 화이트보드를 뗄 수 있는 MVP.
