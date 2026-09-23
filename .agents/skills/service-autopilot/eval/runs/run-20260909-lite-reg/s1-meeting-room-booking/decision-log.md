# Decision Log — 사내 회의실 예약

런 시작: 2026-09-09 13:17 (KST) · 강도: lite (사용자 지정) · 평가 런(회귀 확인, 사용자 무응답 전제)

| # | 단계 | 결정 | 이유 | 버린 대안 · 이유 |
|---|---|---|---|---|
| DL-1 | A0 | 강도 lite 확정 | 사용자 지정 + full 신호 0개 (00-seed) | full: 신호 없음 / spike: 플랫폼·범위·루프 확정됨 |
| DL-2 | A0 | 규모를 단일 사이트·회의실 ≤10·임직원 ≤50으로 축소 해석 | 화이트보드 1장이 감당하던 규모. 확장 해석은 A2 질문 1칸 대상이나 lite는 질문 0 → Assumed | 멀티사이트·수백 명: 시드에 근거 없음, 확장 해석 위 설계는 범위 절제 감점 |
| DL-3 | A0 | 프로파일 P2(웹 SaaS)를 단일 테넌트로 축소 적용 | 사내 도구라 테넌시·결제 축은 해당없음 | P3 관제: 해당 없음 |

## 스킬·모델 사용 기록
- [A0] 스킬 없음 (라우팅 표: 정규화는 모델만으로 충분)
| DL-4 | A1 | 자체 구축 근거 = 무설명 UX(한 화면·매직링크)로 도입 마찰 제거. 단 핸드오프 선행 조건으로 "MRBS 데모 30분 사용 후 구축 결정" | MRBS·LibreBooking이 요구를 0원으로 이미 덮음(search-first). 사용자 원문은 "웹에서 하고 싶다"뿐 | 바로 MRBS 도입: 사용자가 설계 패키지를 요청했으므로 설계는 완성하되 선행 조건으로 남김 |
| DL-5 | A1 | 스택 A: Next.js + PostgreSQL 16(btree_gist EXCLUDE) + Drizzle + Docker Compose | 이중예약 방지를 DB 제약으로 보장(PG 문서), 1인 운영·단일 언어 | B Django+admin: 언어 2개 / SQLite: EXCLUDE 없음 → 최상위 리스크를 앱 코드에 떠넘김 |
| DL-6 | A1 | 개인정보보호법 원문 URL은 미확인으로 표기, 보존기간은 설계 결정으로만 두고 SC에 쓰지 않음 | WebSearch 세션 예산 소진(200/200)으로 법령 조회 불가. 지어내지 않음(규칙 1) | 학습 지식으로 조문 인용: 규칙 1 위반 |
- [A1] ecc:research-ops (메인, 세션 모델 Fable 5.1 — lite라 서브에이전트 없음) — 증거 유형 4분류([출처]/[사용자]/[추론]/[미확인]) 표기를 01에 적용. WebSearch 1회 성공 후 세션 예산 소진 → WebFetch 4회(1차 소스 직접 조회)로 대체. 총 웹 호출 5회(lite 상한 5)
| DL-7 | A2 | 인증 = 사내 이메일 도메인 매직링크 + 서버 세션. SSO non-goal, 인증 모듈 분리 | 비밀번호 저장 회피(리셋 흐름·해시 관리 제거), 50명 규모에 충분. 외부 IdP 연동은 full 신호 | SSO 우선: 사용자 환경 미상, 외부 연동 |
| DL-8 | A2 | 배포 = 사내 서버 1대 Docker Compose, 백업은 pg_dump 일 1회 오프호스트 | 1인 운영·0원. 최대 손실 = 예약 2주치(화이트보드 복귀 가능) | 매니지드 DB: 비용, 사내망 요구와 충돌 가능 |
| DL-9 | A2 | 개인정보 보존기간 = 취소·종료 예약 365일 후 하드삭제, 퇴사자 비활성화 후 365일 뒤 익명화 (설계 결정, 법령 미확인) | 분쟁 기록 목적의 1년이면 충분 [추론]. 법령 원문 미확인이라 SC에 쓰지 않음 | 무기한 보존: 최소 수집·파기 원칙 위반 |
| DL-10 | A2 | 질문 0문항 — 후보 4개 모두 Assumed | lite 1문항 조건(데이터 손실·법)에 미달. 평가 런이라 응답자도 없음 | 규모·인증 질문: full이면 승격됐을 것 |
- [A2] ecc:product-lens (메인) — Mode 1 진단 7문항을 register 머리에 흡수(별도 PRODUCT-BRIEF 안 만듦). go/no-go = go + DL-4 선행 조건. 질문 후보의 Impact 판정에 "10점짜리 vs MVP" 구분을 사용
| DL-11 | A3 | 예약 규칙 상수: SLOT 15분 · 최대 240분 · 선예약 14일 · 업무시간 08-20 · 1인 활성 10건 | 화이트보드가 주 단위였으므로 2주면 여유. 15분은 30분 회의의 절반 격자. 상한은 독점(hoarding) 방지 [추론] | 30분 격자: 45분 회의 표현 불가 / 상한 없음: 축 D 독점 |
| DL-12 | A3 | 타임라인 p95 1000ms, 로그 보존 90일 | L-10 400ms 피드백은 스켈레톤으로 충족, 데이터 도착은 1초 이내(LAN·5만 행) [추론]. 로그 90일 = 분쟁 조회 창 | p95 400ms: 폰 3G에서 비현실 |
- [A3] ecc:product-capability (메인) — CAPABILITY 한 문단·INV 5개·상태 전이 3개를 03에 흡수. frontend-design-taste는 호출하지 않고 ux-principles-kr로 dial 지정(lite 단계당 스킬 1개)
| DL-13 | A4 | 세션 쿠키 SameSite=Lax + 상태 변경은 POST/PATCH + Origin 검사 | 매직링크 클릭(top-level GET)이 Strict에서는 쿠키 없이 도착. security-review 체크리스트의 Strict 권고는 이 흐름과 충돌 → Lax + Origin 검사로 CSRF 방어 | Strict: 링크 클릭 후 로그인 상태가 안 붙음 |
| DL-14 | A4 | STRIDE는 trust boundary 1개(브라우저→앱)만 6범주 검토 | lite 규칙: full 신호 없음. DB·SMTP는 사내망 같은 신뢰 구역 | 전수 STRIDE: 비용 대비 발견 없음 |
- [A4] ecc:security-review (메인) — 체크리스트 중 쿠키·CSRF·레이트리밋·스택 노출·입력 스키마·CSP를 STRIDE 대응에 반영. SameSite=Strict 권고는 매직링크 흐름과 충돌해 Lax+Origin으로 조정(DL-13). ecc:architecture-decision-records는 호출하지 않음(lite 단계당 1개, ADR은 decision-log에 흡수)
| DL-15 | A5 | 페이지네이션 없음 — 모든 목록 상한이 03 상수(ROOM_MAX·USER_MAX·MAX_ACTIVE_PER_USER·타임라인 ≤7일) | 상한이 상수로 묶여 있어 커서 구현은 YAGNI. 상한 해제 시 커서 추가 | 커서 기본 탑재: 1인 운영에 코드만 늘림 |
| DL-16 | A5 | POST /api/bookings에 Idempotency-Key 필수 | 더블탭·재전송 시 사용자에게 201을 재생. EXCLUDE만으로는 두 번째가 409가 되어 "이미 잡혔어요"로 보임 | 없음: UX 오류(자기 예약을 겹침으로 오인) |
| DL-17 | A5 | INV-2(격자·길이·선예약·업무시간)는 앱 검증, INV-1만 DB 제약 | 상수가 .env로 바뀌므로 DB CHECK 고정은 마이그레이션 부담. 이중예약만이 되돌릴 수 없는 손해 | 전부 DB CHECK: 상수 변경마다 마이그레이션 |
- [A5] ecc:api-design (메인) — 상태코드 표(201+Location·409/422 구분·429+Retry-After)·레이트리밋 계층·zod 검증 패턴 채택. 충돌 2건은 stage-templates 우선: URL 버저닝(/api/v1) 대신 미디어타입 자리 예약, `{error:{code}}` 대신 RFC 9457 Problem JSON. ecc:postgres-patterns는 호출하지 않음(lite 단계당 1개)
| DL-18 | A6 | 통합 테스트는 testcontainers 실 PostgreSQL, DB 목 금지 | INV-1(EXCLUDE·23P01)과 부분 제약은 목으로 검증 불가. 통합 시나리오의 절반이 DB 제약 동작 | 목 DB: 최상위 리스크를 테스트 못 함 |
| DL-19 | A6 | 라인 커버리지 목표 없음, 리스크 기반(순수 함수 100%·booking/auth 통합 전부·화면은 E2E 2개) | skill-routing 충돌 규칙: tdd-workflow 80% 일률 대신 A6 리스크 기반 | 80% 일률: 화면 단위 테스트에 시간 소모 |
- [A6] ecc:tdd-workflow (메인) — 단위/통합/E2E 3층·경계값·에러 경로·테스트 격리 원칙 채택, RED 게이트는 핸드오프 BUILD로 넘김. 80% 일률 커버리지는 채택 안 함(DL-19). ecc:e2e-testing은 호출하지 않음(lite 단계당 1개)
| DL-20 | A7 | 메트릭 서버 없음 — pino 로그 파일 + 주간 집계 스크립트 + cron curl 알람 | 1인 운영·50명. Prometheus/Grafana는 컨테이너 +2와 유지 비용. SLO-lite 99%는 화이트보드 복귀가 가능해서 | 관측 스택 도입: 운영 대상이 운영자보다 커짐 |
| DL-21 | A7 | 롤백 = 이전 이미지 태그 재기동, 마이그레이션은 추가 전용 | deployment-patterns 롤백 체크리스트(이전 아티팩트 보관·역호환 마이그레이션). 블루그린·카나리는 단일 호스트에 과잉 | 블루그린: 2배 인프라 |
- [A7] ecc:deployment-patterns (메인) — 멀티스테이지 Dockerfile·non-root·HEALTHCHECK·env zod 검증·롤백 체크리스트·준비도 체크리스트를 07에 반영. k8s probe·블루그린·카나리는 단일 호스트라 미채택. ecc:docker-patterns·dashboard-builder는 호출하지 않음(lite 단계당 1개)
| DL-22 | GATE R1 | 04~07에 값으로 흩어진 상수 10개(API 레이트·메일 재시도·타임라인 범위·멱등 TTL·토큰 길이·부하 행수·SLO 4개)를 03 상수 표로 이관, 03 v1.1 → 04~07 `기준 03 v1.1` 전파. 06의 "17개 엔드포인트"를 "HTTP 15개(JOB-1 제외)"로 정정, "방"→"회의실" 용어 통일 | 자기 점검 HIGH 2건(규칙 9 상수 산재, 수량 주장 불일치). 규칙 실행 절차 2: HIGH 이상은 패치 1회 후 08 | 선행 조건으로 넘기기: HIGH는 넘길 수 없음(MEDIUM 이하만) |
- [GATE] 서브에이전트 없음 (lite) — 메인 자기 점검 + check_package 2회(패치 전·후). HIGH 2건 패치 R1로 닫힘, 판정 PASS + 선행 조건 3(MEDIUM 이하)

## 비용 기록 (런 종료 2026-09-09 13:36 KST)
- 강도: lite (사용자 지정, full 신호 0) · 질문 0문항 (후보 4개 전부 Assumed)
- 웹 호출: 5회 = WebSearch 1(성공) + WebFetch 4. WebSearch 추가 2회는 세션 예산(200/200) 소진으로 미실행. lite 상한 5 준수
- 서브에이전트: 0
- 스킬 호출: 8 (service-autopilot 1 + 단계 스킬 7: research-ops · product-lens · product-capability · security-review · api-design · tdd-workflow · deployment-patterns — 단계당 1개)
- 도구 호출: 약 40 (Bash 15 · Write 7 · Skill 8 · Web 7 · ToolSearch 1 · 실패 1 포함)
- 소요 시간: 약 19분 (13:17 → 13:36)
- 토큰: 세션 카운터 기준 약 20만 (skill 본문 7종 로드 포함) — lite 실측 20만·상한 25만 범위 안
- 패치 라운드: 1 (HIGH 2 → 0)
