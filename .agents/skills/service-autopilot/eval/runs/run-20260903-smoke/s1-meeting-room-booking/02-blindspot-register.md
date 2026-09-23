# 블라인드스팟 레지스터 — 사내 회의실 웹 예약 (RoomBook)

스캔: 공통 10축 + STRIDE 6범주 + 프로파일 P2 웹 SaaS(6항목) = 22항목 + 도메인 고유 3 + 하류 단계(A3~A7·GATE)에서 생긴 Assumed 6 = 31항목 · 스캔일 2026-09-03 (GATE 패치 2026-09-03)
모드: 오토파일럿(무응답) — Asked 문항은 추천안을 `Assumed(무응답)`로 채택

## 사전 진단 (ecc:product-lens Mode 1 — "왜"의 검증)

| # | 질문 | 답 (근거) |
|---|---|---|
| 1 | 누구를 위한 것인가 | 회의실을 쓰는 전 직원 + 회의실을 관리하는 총무 1~2명 (01-recon 이해관계자) |
| 2 | 고통은 무엇인가 | 화이트보드는 그 앞에 가야 보이고, 중복 기입·지움 분쟁이 생기며, 취소·변경 이력이 없다 [추론]. 예약 후 미사용은 업계 평균 30~45% (myseat.io) |
| 3 | 왜 지금인가 | 사용자 입력에 "지금은 수기" — 규모나 원격/하이브리드 근무로 화이트보드의 물리적 한계에 도달 [추론] |
| 4 | 10점짜리 버전 | 캘린더·회의실 패널·센서 연동, 자동 방 추천, 점유율 분석 (Robin·Google 수준) |
| 5 | MVP | 로그인 → 오늘/주간 회의실 현황 보기 → 빈 칸 클릭 → 예약 확정(중복 원천 차단) → 내 예약 취소. 이 5단계만으로 화이트보드를 대체한다 |
| 6 | 안티골 | 데스크 예약, 방문자 관리, 회의실 패널 하드웨어, 외부 캘린더 양방향 동기화(MVP), 결제 |
| 7 | 작동 판단 지표 | 도입 4주 후 화이트보드 철거 가능 여부 = 주간 예약의 100%가 시스템 경유 + 중복 예약 분쟁 0건 (03-prd SC로 정형화) |

Go/No-go: **Go — 단, 조직이 M365/Google Workspace 캘린더를 이미 쓴다면 No-go(설정으로 해결)**. 이 조건이 Q1이다.

## 레지스터 (전 축 전 항목)

| 축/항목 | 상태 | 처리 | 근거·출처 |
|---|---|---|---|
| 1. 기능 범위·행동 | Partial | Assumed: MVP = 현황 조회·예약·변경·취소·회의실 관리. non-goal = 데스크·방문자·패널·결제 | seed + product-lens #5·#6 |
| 2. 도메인·데이터 모델 | Partial | Assumed: 엔티티 Room·Booking·User·AuditLog. 예약 단위 15분, 최대 길이 4시간, 선예약 창 60일, 사용자당 미래 confirmed 예약 ≤ 20건, 시간대 Asia/Seoul 단일, 저장 UTC. 사용자 계정은 허용 도메인 이메일 첫 요청 시 JIT 생성(사전 등록 없음) | M365 정책 항목(BookingWindowInDays·MaximumDurationInMinutes)이 동일 개념을 정책화 — learn.microsoft.com room-mailboxes. 값 자체는 사내 기본값 가정 |
| 3. 상호작용·UX 플로우 | Partial | Assumed: 주 여정 = 주간 타임라인 그리드에서 빈 칸 클릭 → 3필드(제목·시작·종료) 입력 → 확정. 역할 2개(member/admin). 에러는 복구 경로 동반 (L-06) | ux-principles-kr.md L-03·L-06·L-10; 화이트보드의 "주간 그리드" 멘탈모델 유지 (L-01 제이콥) |
| 4. 비기능 품질 | Missing | Assumed: 가용성 99.5%/월(근무시간), 예약 확정 p95 ≤ 1s, 현황 화면 첫 표시 ≤ 2s, 동시 사용자 50, 관측성=구조화 로그+헬스체크 | 사내 도구 관행; 도허티 임계 L-10; 상세는 03-prd SC·07-ops SLO |
| 5. 통합·외부 의존성 | Missing | **Asked → Q1**(캘린더 플랫폼), **Q2**(인증). 이메일 발송은 Assumed(사내 SMTP 릴레이 + STARTTLS, 실패 시 재시도·화면 알림 폴백). 사내 릴레이가 없어 SES 등 외부 발송을 쓰면 직원 이메일이 외부로 나가므로 **개인정보 처리위탁 검토가 전제조건** | Impact: 구축 불필요 가능성 / Uncertainty: 조직 환경 |
| 6. 엣지케이스·실패 처리 | Partial | Assumed: 동시 예약 → DB EXCLUDE 제약으로 한 쪽 409; 과거 시각 예약 불가; 종료≤시작 거부; 운영시간 밖 거부; 회의실 비활성화 시 기존 예약 유지+관리자 알림 | PostgreSQL rangetypes 문서; 03-prd 엣지케이스 표 |
| 7. 제약·트레이드오프 | Missing | **Asked → Q5**(배포 환경·규모). 팀 역량은 Assumed(TypeScript 가능 1명 이상, Docker 운영 가능) | Impact: IaC·백업·접근 방식 / Uncertainty: 조직 인프라 |
| 8. 용어·일관성 | Partial | Assumed: 용어집 — 회의실(Room)·예약(Booking)·예약자(Owner)·관리자(Admin)·반납(Release)·운영시간(Business hours). "회의" ≠ "예약"으로 구분 | 문서 전반 동일 용어 사용, 05 ERD 이름과 일치 |
| 9. 완료 신호 | Partial | Assumed: SC-001~006 pass/fail (03-prd). 인수 = 4주 파일럿 후 화이트보드 철거 | product-lens #7 |
| 10. 비용·라이선스 | Clear | 라이선스 비용 0 (Next.js MIT·PostgreSQL License·Docker CE). 런타임 = VM 1대. 상용 대안 Skedda $99/월~ 은 비용 결정자 참고 | 01-recon 유사 솔루션 표 |
| S. Spoofing | Partial | Assumed: 비밀번호 미보유(매직링크/OIDC), 세션 쿠키 HttpOnly·SameSite=Lax·8h 만료, 사내 이메일 도메인 화이트리스트 | A4 위협모델 ②에서 경계별 재검토 |
| T. Tampering | Partial | Assumed: 예약 소유자·관리자만 변경, 서버측 권한 검사, 중복 방지는 DB 제약(앱 우회 불가) | A4 |
| R. Repudiation | Partial | Assumed: booking_audit 테이블에 생성·변경·취소·반납 이벤트 + actor + 시각 기록(1년 보존) | 화이트보드의 "누가 지웠나" 분쟁이 원 고통 → 감사 로그가 핵심 가치 |
| I. Information Disclosure | Partial | **Q3와 결합**: 예약 제목·예약자는 전사 공개(화이트보드와 동일 정보 수준). 이메일 주소는 예약자 본인·관리자만. 로그에 PII 최소화 | 개인정보 최소 수집(privacy.go.kr) |
| D. Denial of Service | Partial | Assumed: 사내망/로그인 뒤. 매직링크 요청 레이트리밋(이메일당 5회/10분), 예약 API 사용자당 60회/분 | A4·A7 |
| E. Elevation of Privilege | Partial | Assumed: 역할은 서버 세션에서만 결정, 클라이언트 파라미터로 role 전달 금지, admin 경로 미들웨어 검사 | A4 |
| P2. 테넌시 | Clear | 해당없음 — 단일 회사·단일 인스턴스. tenant_id 없음 | seed(사내 도구) |
| P2. 인증 | Missing | **Asked → Q2** | Impact: 인증 재작업 / Uncertainty: 사내 IdP 유무 |
| P2. 결제·구독 | Clear | 해당없음 — 내부 도구, 과금 없음 | 01-recon 이해관계자 |
| P2. 백업·DR | Missing | Assumed: pg_dump 일 1회 02:00 KST, 보관 30일, 오프사이트(사내 NAS 또는 오브젝트 스토리지) 1부, 복원 리허설 월 1회(outplane.com 가이드 권고, 07 근거). RPO 24h·RTO 4h(그 사이 화이트보드 폴백) | blindspot-checklists P2 기본값; 화이트보드 폴백은 이 도메인 고유 장점 |
| P2. 개인정보 | Partial | Assumed: 수집=이름·사내 이메일·예약 이력. 퇴사 시 계정 비활성화 + 90일 후 이름 익명화, 예약 이력 1년 보존 후 파기. 처리방침 사내 공지 | 개인정보보호법 최소수집·파기 원칙 (privacy.go.kr, 확인 2026-09-03) |
| P2. 이메일/알림 | Partial | Assumed: 매직링크·예약 확정·취소·회의실 비활성화 알림 4종만. 사내 SMTP 릴레이(없으면 SES — 위탁 검토 전제). 큐 + 실패 재시도 3회(지수 백오프), 최종 실패는 화면 배너로 대체 | blindspot-checklists P2 기본값 |
| 도메인 고유. 노쇼·자동 해제 | Missing | **Asked → Q4** | Impact: 데이터 모델·잡·화면 / Uncertainty: 조직 노쇼 실태 |
| 도메인 고유. 반복 예약 | Partial | Assumed: MVP 제외(P2). 반복 예약이 고스트 미팅 최대 원인(deskbird)이므로 초기엔 단발만 | https://www.deskbird.com/blog/ghost-meetings (확인 2026-09-03) |
| 도메인 고유. 언어·시간대 | Clear | 한국어 단일, Asia/Seoul 단일 오피스 | seed(사내) |
| 하류 신규(A1). GDPR | Clear | 해당없음 — EU 거주 직원 없음 Assumed | 01-recon 규제 절 |
| 하류 신규(A6). 브라우저 | Partial | Assumed: 사내 표준 브라우저 Chrome/Edge → E2E는 chromium+mobile-chrome만 | Impact 낮음(Playwright 프로젝트 추가로 해결) |
| 하류 신규(A7). 중앙 로그 수집 | Partial | Assumed: 사내 Loki/ELK 있으면 Promtail 연결, 없으면 VM 로컬 7일 보관 | 07 로깅 전략 |
| 하류 신규(A7). TLS·인증서 | Partial | Assumed: 사내 CA가 서버 인증서를 발급해 줌 → Caddy 컨테이너가 TLS 종단·폴백 페이지 담당 | 07 런타임. 없으면 사내 리버스 프록시에 위임 |
| 하류 신규(A7). 알림 채널 | Partial | Assumed: 웹훅 수신 가능한 사내 메신저 존재 → 호스트 cron `alert-check.sh`가 평가·발송. 없으면 이메일(메일 장애 알람은 그 경로로 못 감 → 전화 목록) | 07 알림 |
| 하류 신규(GATE). 관리자 감사 범위 | Partial | Assumed: MVP 감사 로그는 예약 관련 행위만. 회의실·설정·사용자 변경 감사(admin_audit)는 P2 | 04 STRIDE 관리자 행 |

미마킹 축: **0** (게이트 조건 충족)

## 질문 배치 (5문항, 1회) — 제시 2026-09-03 · 오토파일럿: 무응답

### Q1. 회사가 이미 쓰는 캘린더 플랫폼이 있나? (있으면 구축 대신 설정이 정답일 수 있다)
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | 없음 또는 미상 → 독립 웹앱으로 구축. 캘린더 연동은 P2(착수 시 설계, 지금 인터페이스를 만들지 않음) | 화이트보드를 쓰는 조직은 캘린더 자원 문화가 없을 가능성이 높고, 사용자가 "웹 도구"를 명시함. 리스크: M365/Google 보유 시 과잉 구축 |
| B | Microsoft 365 보유 → room mailbox 설정으로 대체, 구축 중단 | 추가 비용 0, Outlook/Teams 통합. 단, 웹 전용 현황판이 필요하면 Graph API 읽기 전용 화면만 별도 |
| C | Google Workspace 보유 → 자원 캘린더 설정으로 대체, 구축 중단 | 추가 비용 0, 자동 방 추천 포함 |
→ 답: 무응답 → **A Assumed(무응답)**. 잔여 리스크로 08-readiness에 기록.

### Q2. 로그인은 어떻게 하나?
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | 사내 이메일 도메인 제한 매직링크(비밀번호 없음) + OIDC 어댑터를 P2로 설계 | 비밀번호 저장 자체를 없애 유출 리스크 제거(Spoofing 완화). 리스크: 메일 발송 장애 = 로그인 불가 → 07에서 알림·폴백 |
| B | 사내 IdP(Entra ID/Google/Keycloak) OIDC 즉시 연동 | IdP가 있으면 최선. 없으면 IdP 구축이 선행 과제가 되어 범위 폭발 |
| C | 자체 ID/비밀번호 | 비밀번호 리셋·해시·정책 구현 부담, 유출 리스크 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q3. 예약 정보 공개 범위와 승인 정책은?
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | 전 회의실 즉시 확정(승인 없음), 제목·예약자 이름 전사 공개 | 화이트보드와 동일한 정보 수준·속도. 승인 상태 기계 없음 → 모델 단순. 리스크: 민감 회의 제목 노출 → 입력란에 "제목은 전사에 보여요" 안내 |
| B | 제한 회의실(임원실·대형)만 관리자 승인 | 희소 자원 통제. pending 상태·알림·관리 화면 추가 |
| C | 제목 비공개, free/busy만 표시 | 프라이버시 최대. 화이트보드보다 정보가 줄어 "누가 쓰나" 문의 증가 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q4. 노쇼(예약 후 미사용) 대책을 MVP에 넣나?
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | MVP는 "조기 반납" 버튼만. 체크인·자동 해제는 P2 — 도입 4주 후 관리자 노쇼 민원 건수로 판단 | 체크인 강제는 화이트보드보다 마찰이 커 초기 정착을 해침. 소규모 조직은 노쇼 비용이 낮음 [추론]. 업계 노쇼율 30~45%는 대형 하이브리드 오피스 기준(myseat.io) |
| B | 체크인 + 유예 15분 자동 해제 P1 | 고스트 미팅 즉시 차단. 체크인 화면(QR/버튼)·스케줄러 잡·알림 추가 |
| C | 없음 | 노쇼 방치 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q5. 어디에 배포하고 규모는 얼마나 되나?
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | 사내 VM(또는 사내 클라우드 계정 VM) 1대, Docker Compose. 직원 ≤300·회의실 ≤30 가정 | 데이터 사내 보관, 운영 단순. HA 없음 → 장애 시 화이트보드 폴백 |
| B | 퍼블릭 관리형(Vercel + 관리형 Postgres) | 운영 최소. 직원 개인정보가 외부 SaaS에 저장 → 처리 위탁 검토 필요 |
| C | 폐쇄망 온프레미스 | 이메일 발송·외부 폰트·CDN 전부 차단 → 매직링크 불가, Q2를 B/C로 바꿔야 함 |
→ 답: 무응답 → **A Assumed(무응답)**

## 반영 기록
- Q1 A → 04-architecture 컨텍스트에 "P2 연동 예정"만 표기(포트 선제 정의 안 함, DL-008), 08에 "M365/Google 보유 시 No-go" 잔여 리스크 (decision-log #1)
- Q2 A → 04 인증 컴포넌트=매직링크, 05 `/auth/magic-link` 엔드포인트, 07 SMTP 장애 알림 (decision-log #2)
- Q3 A → 03 FR-004 공개 범위, 05 Booking 응답에 title·owner_name 포함, 04 STRIDE-I 검토 (decision-log #3)
- Q4 A → 03 FR-006 조기 반납 P1, 체크인/자동 해제 P2. 05 `POST /bookings/{id}/release` (decision-log #4)
- Q5 A → 07 Compose 스케치·백업 경로, 04 Context (decision-log #5)
