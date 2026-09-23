# Decision Log — 사내 회의실 웹 예약 (RoomBook)

형식: 결정 / 이유 / 버린 대안 / 영향 (ecc:architecture-decision-records 형식을 이 파일에 흡수)

## 단계별 스킬 사용 기록
- [A0] 스킬 없음 — 라우팅 표대로 정규화는 모델만으로 수행
- [A1] ecc:research-ops — 증거 경계 4분류([사실]/[입력]/[추론]/[추천])를 01-recon 전 항목에 적용, 확인일 강제
- [A1] ecc:search-first — Adopt/Extend/Build 판정 추가: "M365/Google 보유 시 Adopt(설정)" 조건을 Q1로 승격, MRBS Extend 기각 근거 기록. (ecc:market-research는 단계당 2개 상한으로 미호출 — 경쟁 비교는 research-ops + 검색으로 충당)
- [A2] ecc:product-lens — Mode 1 진단 7문항을 register 상단에 추가, Go/No-go 조건(캘린더 플랫폼 유무)이 Q1 Impact 판단을 보강
- [A3] ecc:product-capability — Capability 재진술·불변식 INV-1~5·상태 전이 표를 PRD에 추가(요구사항 풀만으로는 숨어 있던 "DB 제약 강제·append-only·role은 세션에서만" 제약이 드러남)
- [A3] frontend-design-taste — 제품/앱 UI 프로파일에서 dial(DENSITY 6·MOTION 3·VARIANCE 3) 고정, 하드룰(font-mono 숫자·테마 토큰·상태 4종)을 UI 방향에 명시. UX-01~05 측정 기준 발급
- [A4] ecc:architecture-decision-records — Nygard ADR 형식(결정/이유/버린 대안/영향)을 이 파일 DL-### 항목으로 흡수, 04 "검토한 대안" 표 7건 작성. docs/adr 별도 생성 안 함
- [A4] ecc:security-review — STRIDE 표에 자산/경계 6행 전수 검토, CSRF(SameSite=Lax 선택의 대가)·IDOR·감사 로그 권한 회수·스택트레이스 제거를 대책에 추가
- [A4] ecc:architect (서브에이전트) — 03·04 독립 검토 의뢰(확장성·불변식 위반·YAGNI·다이어그램 정합). 결과는 아래 DL-008에 반영
- [A5] ecc:api-design — 상태코드 표·Idempotency-Key·레이트리밋 헤더·`can_edit` 서버 계산을 엔드포인트 표에 반영. 스킬의 URL 버저닝 권장은 stage-templates(Zalando #115)와 충돌 → 템플릿 우선(DL-007). 스킬의 `{error:{code}}` 봉투 대신 RFC 9457 채택
- [A5] ecc:postgres-patterns — bigint identity·timestamptz·citext·부분 인덱스(outbox pending)·SKIP LOCKED 큐·statement_timeout 설정을 DDL에 반영, 감사 로그 REVOKE로 INV-3 강제
- [A6] ecc:tdd-workflow — RED 게이트(실패 확인 전 프로덕션 코드 금지)·Git 체크포인트(test→fix→refactor)를 원칙에 채택. 스킬의 "80% 일률" 커버리지는 라우팅 충돌 규칙대로 기각, 리스크 기반 표로 대체
- [A6] ecc:e2e-testing — POM 4개·`data-testid` 전용·`waitForResponse`·`retries:2`·`trace:on-first-retry`·fixme 격리 정책을 E2E 절에 반영. 브라우저 프로젝트는 chromium+mobile-chrome으로 축소(사내 표준 브라우저 가정)
- [A7] ecc:deployment-patterns — CI/CD 단계(lint→typecheck→unit→integration→contract→build→audit→E2E)·헬스체크 상세·전방 호환 마이그레이션 2단계 배포·롤백 체크리스트·준비도 체크리스트를 07에 축약 반영. 배포 전략은 롤링/블루그린 대신 "근무시간 외 단일 인스턴스 재생성"(규모 적합)
- [A7] ecc:docker-patterns — compose 스케치에 `127.0.0.1` 포트 바인딩·db 포트 미노출·`read_only`+`tmpfs`·`no-new-privileges`·리소스 limits·json-file 로그 로테이션·`env_file` 비밀 분리·Mailpit은 override 전용을 반영. (ecc:dashboard-builder는 단계당 2개 상한으로 미호출 — 대시보드는 SRE 원칙대로 운영자 질문 3개로 축약)
- [GATE] fresh-context 서브에이전트(general-purpose) 1명 적대적 검토 1회 — CRITICAL 1·HIGH 8·MEDIUM 6 + 참고 6건 → 타당성 필터 후 패치 1회(DL-009). ecc:santa-method는 돈·안전·법 도메인이 아니라 미호출. 예산 제약으로 재검토 루프 없음 → 판정 CONCERNS

## DL-001 — 독립 웹앱 구축, 캘린더 연동은 P2 포트만
- 결정: Q1 무응답 → 추천안 A 채택. 독립 웹앱을 만든다. 캘린더 연동은 P2 착수 시 설계한다 (초안의 "CalendarAdapter 포트만 정의"는 DL-008 #8에서 철회 — 구현 없는 인터페이스는 투기적 설계).
- 이유: 사용자가 "웹에서" 도구를 명시했고 화이트보드 운영은 캘린더 자원 문화 부재를 시사한다. 연동 구현은 플랫폼(M365/Google)에 따라 권한 모델이 달라 지금 결정할 수 없다.
- 버린 대안: (B) M365 room mailbox로 대체 — 조직이 M365면 이게 정답이나 확인 불가. (C) Google 자원 캘린더 — 동일.
- 영향: 04 컨텍스트에 "P2 연동 예정"만 표기(포트 선제 정의는 DL-008에서 제거), 08 잔여 리스크 1순위. **조직이 M365/Google을 쓰면 이 패키지는 폐기하고 설정으로 간다.**

## DL-002 — 인증은 사내 도메인 매직링크, 비밀번호 없음
- 결정: Q2 무응답 → A. 사내 이메일 도메인 화이트리스트 + 매직링크(15분 유효, 1회용). OIDC는 어댑터 P2.
- 이유: 비밀번호를 저장하지 않으면 자격증명 유출·리셋 흐름·해시 정책이 전부 사라진다(Spoofing 위협 자체 축소). 사내 IdP 유무를 모르므로 OIDC 즉시 연동은 선행 과제가 될 위험.
- 버린 대안: (B) OIDC 즉시 — IdP 있으면 최선, 없으면 범위 폭발. (C) 자체 ID/PW — 구현·보안 부담.
- 영향: 05 `/auth/magic-link`·`/auth/verify`, 04 STRIDE-S, 07 SMTP 장애 = 로그인 불가 알림 P1.

## DL-003 — 즉시 확정·전사 공개 (승인 없음)
- 결정: Q3 무응답 → A. 모든 회의실 즉시 확정, 제목·예약자 이름은 로그인한 전 직원에게 공개.
- 이유: 화이트보드와 같은 정보 수준·속도를 유지해야 전환 마찰이 없다. 승인 상태 기계를 없애 모델·화면·알림이 단순해진다.
- 버린 대안: (B) 제한 회의실 승인제 — pending 상태·관리 화면 추가. (C) free/busy만 — "누가 쓰나" 문의 증가.
- 영향: 03 FR-004, 05 Booking 응답 필드, 04 STRIDE-I (제목 민감정보 입력 안내로 완화).

## DL-004 — 노쇼 대책은 조기 반납만, 체크인·자동 해제는 P2
- 결정: Q4 무응답 → A. MVP는 "지금 반납" 버튼(P1). 체크인+자동 해제는 P2, 도입 4주 후 관리자 노쇼 민원 건수로 재결정.
- 이유: 체크인 강제는 화이트보드 대비 마찰 증가. 30~45% 노쇼 통계는 대형 하이브리드 오피스 기준이라 소규모 사내에 그대로 적용하기 어렵다.
- 버린 대안: (B) 체크인+자동해제 P1 — 스케줄러 잡·체크인 화면·알림 추가. (C) 없음.
- 영향: 03 FR-006·SC-005, 05 `POST /bookings/{id}/release`, 04 ReleaseJob 없음(P2).

## DL-005 — 사내 VM 1대 + Docker Compose, HA 없음
- 결정: Q5 무응답 → A. 사내(또는 사내 클라우드 계정) VM 1대, Compose로 caddy+app+worker+db 4컨테이너 (초안 app+db 2개 → A4에서 Worker 분리, GATE 패치에서 TLS 종단 Caddy 확정). 규모 가정 직원 ≤300·회의실 ≤30.
- 이유: 직원 개인정보를 사내에 보관, 운영 단순. 장애 시 화이트보드 폴백이 가능한 드문 도메인이라 HA 투자 가치가 낮다.
- 버린 대안: (B) Vercel+관리형 DB — 개인정보 외부 저장·위탁 검토. (C) 폐쇄망 — 매직링크 불가로 Q2 재결정 필요.
- 영향: 07 Compose 스케치·백업 경로·RTO 4h, 04 Context & Scope.

## DL-007 — API 경로에 버전 없음 (`/api/*` + `API-Version` 헤더)
- 결정: URL 버저닝을 쓰지 않는다. 응답 헤더 `API-Version: 1`, 스펙 파일 semver, 파괴적 변경은 미디어타입으로만.
- 이유: stage-templates(Zalando #115)가 URL 버저닝 회피를 요구하고, 내부 도구·단일 클라이언트라 버전 분기 자체가 YAGNI. ecc:api-design의 `/api/v1/` 권장과 충돌 → 라우팅 규칙 2(템플릿 우선) 적용.
- 버린 대안: `/api/v1/` 경로 버저닝 — 명시적이나 두 문서(04·05)가 다른 경로를 쓰게 되어 용어 드리프트 발생.
- 영향: 04의 `/api/v1/*` 전부를 `/api/*`로 수정. 05 규약 섹션.

## DL-008 — A4 독립 검토(ecc:architect) 8건 반영
- 결정: 지적 8건 중 7건 반영, 1건(#6 SC-007) 부분 반영.
  - #1 HIGH 23P01 후 같은 트랜잭션 SELECT는 25P02 → ROLLBACK 후 새 트랜잭션 탐색 + 후보 FR-011 재검증 + `suggestion:null` 문구 (04 구현 접근·시나리오 2, 03 FR-011·EC-B10, 05 EP-07, 06 TS-032·033·055)
  - #2 HIGH 주기 실행 주체 없음 → Worker에 RetentionJob 노드 (04)
  - #3 MED FR-008/014/015 소유 컴포넌트 없음 → AdminService 추가, AUD·OUT 엣지 (04)
  - #4 MED 레이트리밋이 인증 경로만 → RL을 전 라우트 앞단 미들웨어로, 인증/일반 버킷 분리 (04)
  - #5 MED outbox 우선순위·클레임·배너 계약 없음 → `priority`·`claimed_at`·SKIP LOCKED 클레임, `GET /api/me.mail_degraded` (04·05, 06 TS-047·056·057)
  - #6 MED SC-007 99.5% vs 단일 VM → 배포 창 근무시간 외·자동 재기동·RTO 분리(앱 30분/DB 복원 4h)로 SC 유지, DB 전체 복원은 그 달 SC 미달로 리뷰 (03 SC-007 주석, 04 Context, 07)
  - #7 LOW 다이어그램 정합 → AUTH→OUT 엣지, 시퀀스에 AUD/OUT participant, 메일 릴레이 이름 통일 (04)
  - #8 LOW PRD 밖 정책·이중 선택·투기적 포트 → "미래 예약 ≤20건"을 FR-011(h)로 승격(03·05·06), ORM은 Drizzle 단일 확정, CalendarAdapter 포트 제거 (03·04·05·02)
- 이유: 거짓 양성 필터링 결과 8건 모두 문서 근거가 있는 실제 결함이었다. #6만은 SC를 낮추는 대신 운영 제약으로 달성하는 쪽을 택했다(사용자 기대 기준의 SLO 원칙).
- 버린 대안: 지적 무시(GATE에서 재발), SC-007 하향(사용자 기대 기준 위반).
- 영향: 02·03·04·05·06 수정. 07은 이 결정을 전제로 작성.

## DL-009 — GATE 적대적 검토 지적 반영 (패치 1회, 재생성 루프 없음)
- 결정: 검토관 판정 FAIL(CRITICAL 1 + HIGH 8 + MEDIUM 6)에 대해 타당성 필터 후 **패치 1회**로 대응하고, 재검토 없이 08에 CONCERNS로 기록한다(스모크 런 예산 제약).
- 반영(전부 타당):
  - CRITICAL 근거 없는 기술 채택 → Drizzle(custom migration 문서 + EXCLUDE 미지원 이슈 URL), Node 24 LTS(endoflife.date), k6 단일 확정, Caddy·age·gitleaks·Mailpit은 "커모디티, 근거 생략" 명시, OWASP 인용은 "블로그 2건 교차"로 정정 (04·07)
  - HIGH 감사 로그 파기 vs REVOKE 모순 → `roombook_retention` 롤 + `RETENTION_DATABASE_URL`, INV-3 재기술 (03·04·05·06 TS-031·07)
  - HIGH 계정 생성 경로 없음 → FR-001 JIT 생성 + `PATCH /api/me {name}` (03·04 시퀀스1·05·06 TS-010·07)
  - HIGH 반납 후 재예약 vs 15분 경계 → EC-C3/TS-019/E2E-3을 14:15~15:00으로 정정 (03·06)
  - HIGH 대안 슬롯 창 "오늘" vs "24h" → "요청 start_at 이후 당일 운영시간 종료까지 전방 탐색"으로 통일 (03·04·05)
  - HIGH admin 2h 세션이 메일 장애 우회 무력화 → 세션 불필요 CLI `issue-link` + FR-001에 admin 2h 명시 (03·04·06 TS-011·07)
  - HIGH 에러 slug 드리프트 → TS-053 두 단언 분리, EC-C6에 slug 병기 (03·06)
  - HIGH 알람 평가 주체·TLS 종단 미결정 → `alert-check.sh` 1분 cron + `/api/health` 확장 필드, Caddy 컨테이너 확정 (07·02)
  - HIGH 하류 Assumed 미등재 → 02에 7행 추가(GDPR·브라우저·중앙 로그·TLS·알림 채널·관리자 감사 범위·JIT)
  - MEDIUM 관리자 감사 범위 과장 → "예약 관련만, admin_audit P2"로 축소 (04·02) · STRIDE 이유 없는 해당없음 5셀·미대응 위협(메일 변조)·스캐너 선소비 → 이유 기재, STARTTLS Transfer, verify 2단계(GET 확인 페이지 → POST 소비) (03 EC-A5·04·05 EP-02·06 TS-059) · 스테일 결정 기록 → DL-001·DL-005 정정, 02 축2·이메일 행 갱신 · SES 폴백 vs 사내 보관 → 처리위탁 검토 전제조건 명시 (02·04·07) · 퇴사자 미래 예약 → FR-015 admin_cancel "퇴사 처리" + EC-C7/TS-058 (03·05·06) · 참고 항목 중 PolicyValidator→DB 엣지, EP-02 공개 목록, audit→push 순서, 복원 리허설 월 1회도 반영
- 부분 반영(YAGNI MEDIUM): 커서 페이지네이션은 EP-16·17로 축소, 스코프 체계는 "문서 표기용, 저장은 role 2값"으로 축소. **Idempotency-Key와 미디어타입 버저닝은 유지** — stage-templates 규약이며, 재시도로 인한 이중 예약은 409로만 막으면 사용자에게 "내 예약과 충돌"이라는 혼란을 준다 [추론]. 잔여 지적으로 08에 기록.
- 미반영(LOW): EC 번호 순서(B9·B10이 B8 앞), DL-006 위치, 03 yarooms "검색 요약 인용" 표기 — 정보 손실 없어 보류.
- 영향: 02·03·04·05·06·07·decision-log 패치. 08은 패치 후 상태 기준으로 작성하되 재검토 미실시를 명시.

## DL-006 — 반복 예약 MVP 제외
- 결정: 단발 예약만 P1. 주간 반복은 P2.
- 이유: 반복 예약이 고스트 미팅의 최대 원인(deskbird)이고, 예외일·RRULE 처리가 초기 복잡도를 키운다.
- 버린 대안: 주간 반복 P1.
- 영향: 05 Booking에 recurrence 컬럼 없음(P2 시 별도 테이블), 06 시나리오 없음.
