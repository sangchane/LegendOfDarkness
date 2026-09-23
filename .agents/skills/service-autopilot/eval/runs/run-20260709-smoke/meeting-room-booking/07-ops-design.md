# Ops Design - 사내 회의실 웹 예약

## 배포

- 배포 형태: 사내 서버 또는 컨테이너 배포. 폐쇄망에서도 동작하도록 외부 CDN/외부 API 의존을 MVP에서 제거한다.
- CI/CD 단계: lint -> unit test -> integration test -> build -> contract test -> deploy -> smoke.
- 설정/비밀: DB URL, JWT/세션 secret, SMTP 정보는 환경 변수 또는 사내 secret store에서 주입한다.
- 마이그레이션: room/booking/policy/audit 테이블은 backward-compatible DDL부터 적용하고, 배포 후 feature flag로 메뉴를 노출한다.

## 관측성

### SLI/SLO-lite

| SLI | SLO |
|---|---|
| 예약 목록 조회 성공률 | 업무시간 월간 99.5% 이상 |
| 예약 생성 성공 요청 p95 latency | 1.5초 이하 |
| 가용성 조회 p95 latency | 800ms 이하 |
| 자동 해제 지연 | 대상 예약 95%가 정책 시간+1분 이내 처리 |
| 중복 예약 성공 건수 | 0건 |

### 4 Golden Signals

| Signal | 측정 |
|---|---|
| Latency | `/availability`, `/bookings` 생성/수정/조회 p50/p95/p99 |
| Traffic | 분당 API 요청 수, 예약 생성 수, 조회 수 |
| Errors | 4xx/5xx, 409 conflict, policy violation, notification failure |
| Saturation | DB connection pool, slow query count, release job queue lag |

### 로그

- 애플리케이션 로그: 요청 ID, actorId, endpoint, status, latency, error code.
- 감사 로그: 예약 생성/변경/취소/체크인/자동해제/정책 변경 전후값.
- 보안 로그: 인증 실패, 권한 없는 관리자 API 접근, rate limit.
- 개인정보 마스킹: 제목/참석자/이메일은 일반 로그에 남기지 않고 audit에는 권한 있는 저장소에만 보관한다.
- 보관: 일반 로그 30일, 감사 로그 1년(조직 정책에 따라 조정), 백업 암호화.

## 알림

| 알림 | 조건 | 심각도 | 수신자 | 연결 절차 |
|---|---|---|---|---|
| 예약 생성 5xx 증가 | 5분간 5xx 비율 2% 초과 | High | IT 운영 | API 로그와 DB 상태 확인 |
| 중복 예약 검증 실패 | DB 검증 쿼리에서 overlap 발견 | Critical | IT+총무 | 예약 동결, 충돌 예약 수동 조정 |
| 자동 해제 job 지연 | lag 5분 초과 | Medium | IT 운영 | job 재시작, 실패 예약 재처리 |
| 백업 실패 | 일일 백업 실패 | High | IT 운영 | 수동 백업 실행, 원인 확인 |
| 알림 발송 실패 | 10분간 실패 20건 초과 | Low | IT 운영 | SMTP/알림 큐 확인 |

## 백업·복구

- 백업: DB 전체 일 1회, 트랜잭션 로그 또는 증분 백업은 운영 중요도에 따라 15분~1시간.
- 복구 리허설: 분기 1회 staging에서 백업 복원 검증.
- RPO: 1시간 이하. RTO: 4시간 이하.

| 장애 | 감지 방법 | 영향 | 복구 절차 | RTO/RPO |
|---|---|---|---|---|
| DB 장애 | health check, DB connection error | 예약/조회 중단 | standby 또는 백업 복원, 앱 재기동 | RTO 4h / RPO 1h |
| 잘못된 정책 배포 | 예약 실패 급증, audit diff | 신규 예약 차단 | 정책 rollback, 실패 예약 재시도 안내 | RTO 1h |
| 자동 해제 오작동 | auto_released 급증 | 실제 회의 예약 해제 | job 중지, audit 기준 상태 복구, 사용자 공지 | RTO 1h |
| 알림 장애 | notification failure metric | 예약 변경 인지 지연 | 큐 재처리, 앱 내 알림으로 대체 | RTO 4h |
| 애플리케이션 배포 실패 | smoke test 실패 | 서비스 접근 불가 | 이전 버전 rollback | RTO 30m |

## 운영 점검

- 일일: 에러율, conflict율, 자동 해제 건수, 백업 성공 여부 확인.
- 주간: 회의실 이용률, 노쇼율, 정책 위반 예약 시도 검토.
- 월간: 관리자/권한 계정 리뷰, 감사 로그 샘플 검토.
