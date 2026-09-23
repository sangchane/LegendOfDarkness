# 준비도 리포트 — 사내 회의실 예약 (meeting-room-booking)
버전: v1.0 · 기준 03 v1.0 · 강도 lite — GATE는 서브에이전트 없이 메인이 `scripts/check_package.py` + 자기 점검으로 수행 (SKILL.md 강도 표)

## 1. check_package.py 출력 (2026-09-07, 04 Drizzle 확정 후 재실행)
```
# check_package — eval\runs\run-20260907-smoke\s1-meeting-room-booking

## CRITICAL (0)

## HIGH (0)

## INFO (3)
- C2 FR 총 10 (P0 4 · P1 4 · P2 2), 05 참조 10
- C3 SC 총 5, 06 참조 5
- C5 축 행 22, 마킹 22, 질문 0
exit=0
```
1차 실행(04 수정 전)도 CRITICAL 0 · HIGH 0이었다. 수정은 자기 점검 발견 #1에 따른 것.

## 2. 자기 점검 발견 목록 (심각도순, 적대적 모드 — 반영 전 타당성 필터링)
| # | 심각도 | 문서:섹션 | 발견 | 처리 |
|---|---|---|---|---|
| 1 | HIGH | 04:구현 접근 | "Drizzle 또는 Kysely 중 하나 — 구현 시 채택"은 위장된 미결정 (check_package의 어휘 스캔을 비껴감) | 반영: Drizzle로 확정, decision-log #15 |
| 2 | MEDIUM | 07:알림·장애 | 알람 임계(5분·5%·10분 창)와 RB-1/2/4의 RTO(30분·1h·4h)가 03 상수 표에 없는 숫자 | Accept(사유): 운영 임계는 도입 후 실측으로 조정되는 값이라 03 도메인 상수와 분리. 첫 조정 시 03 v1.1 상수로 승격하고 07 버전을 함께 올린다 |
| 3 | MEDIUM | 05:규약 | Idempotency-Key 보관 "24h"가 03 상수가 아님 | Accept(사유): stage-templates가 명시한 Stripe 관례 값(≥24h). 도메인 상수 아님 |
| 4 | LOW | 02 vs 03 | register(02)에 세션 30일·발송 5회/시간·자동해제 15분 등 값이 직접 적혀 있음 | 필터링(거짓 양성): 규칙 9는 04~07의 하류 전파 규칙. 02는 03 상수 표의 상류 입력이며 값이 일치함 |
| 5 | LOW | 03:FR-002 vs 05:EP-04 | 05가 조회 가능 날짜 범위(오늘−1 ~ 오늘+`MAX_ADVANCE_DAYS`)를 03보다 구체화 | 필터링: 계약이 요구사항을 좁히지 않고 정밀화한 것. 03 개정 불필요 |
- 커버리지 공백: 없음 (C2·C3 스크립트 확인 + 05 역방향 매핑 "매핑 없는 EP 없음").
- 모호성: 03의 질적 표현은 전부 상수·UX-NN으로 변환됨(G2 "1초" → `GRID_P95_MS`, "3클릭" → SC-002).
- 불일치: 03 E8(200 멱등) ↔ 05 EP-06(200) ↔ 06 FR-004 E8 시나리오 일치. 04 컴포넌트 이름(BookingService·AuthService·Mailer·AuditLog)이 06·07 디렉터리 구조와 일치. 04 시퀀스의 23P01→409는 05 EP-05·06 SC-001과 일치.
- 절대규칙: 추천 전부 출처 있음(01·03·04 근거 블록). register 22축 전부 마킹, 질문 0(lite 상한).
- 중복·과잉: 실시간 푸시·로그 스택·k8s를 YAGNI로 배제(decision-log #9·#13·#14). 상충 기술 없음.

## 3. 판정
**PASS** — CRITICAL 0 · HIGH 0(발견 #1 반영 완료) · MEDIUM 2건은 사유와 함께 Accept. lite 강도 한계: 서브에이전트 적대 검토 없음, 위협모델 한 단락. 아래 착수 조건이 어긋나면 해당 단계부터 재실행한다.

### 착수 조건 (핸드오프 전 사람이 확인)
1. **그룹웨어 자원 캘린더 부재 확인** — 회사가 M365 또는 Google Workspace를 쓰고 회의실을 자원으로 등록할 수 있으면 **이 패키지를 폐기하고** room mailbox/Calendar 자원을 설정한다 (01 유사 솔루션, decision-log #2). 확인자: IT 운영.
2. **팀이 TypeScript/Next.js 가능** — 아니면 스택 S2(FastAPI + HTMX)로 교체. 03·05·06 산출물은 그대로, 04 컴포넌트 이름과 07 Dockerfile만 바뀐다 (decision-log #3).
3. 사내 SMTP(또는 발송 대행) 계정 1개와 VM 1대(Docker 가능) 확보.

### 핵심 결정 5줄
1. Build(최소) — 그룹웨어 없다는 가정 아래 엔드포인트 12개 + 배치 1개.
2. 중복 차단은 PostgreSQL `EXCLUDE (room_id WITH =, period WITH &&) WHERE status='active'` — 코드가 아니라 스키마가 보장.
3. 인증은 매직링크 + 사내 도메인 화이트리스트, 비밀번호 없음, 세션 `SESSION_DAYS`.
4. MVP = 그리드 조회·예약·취소(P0 4건). 반복 예약·자동 해제·통계는 non-goal/P2.
5. 운영은 VM 1대 compose 2컨테이너, 일 1회 pg_dump, 알람 4개 = 런북 4개.

### 질문 대신 가정으로 채택된 항목 (전부 02 register)
그룹웨어 부재 · 회의실 ≤`ROOMS_MAX`·직원 ≤`USERS_MAX` · TypeScript · 사내망 전용 · SMTP 1개 · 단일 `TZ` · 관리자 1인 · 예산 0 · 운영시간 `OPEN_HOUR`~`CLOSE_HOUR` · 슬롯 `SLOT_MINUTES` · 보존 `BOOKING_RETENTION_DAYS` · 백업 `RPO_HOURS`/`RTO_HOURS`.

## 구현 핸드오프 (service-prompt-workflow SPEC 입력)
/service-prompt-workflow 로 다음을 실행:
<inputs>eval/runs/run-20260907-smoke/s1-meeting-room-booking/03-prd.md (요구사항·불변식·상수 표), 05-api-contract.md (엔드포인트·ERD·DDL 스케치), 08-readiness-report.md (착수 조건·첫 작업 3개)</inputs>
<references>04-architecture.md, 06-test-design.md, 07-ops-design.md — 필요할 때만 읽는다</references>
<first_task>SPEC.md 작성 — 위 문서를 진실원으로, 낯선 구현자 실행 가능 수준(≥7/10). 착수 조건 1·2를 SPEC 머리에 체크박스로 둔다</first_task>
<then>superpowers 설치 시 `superpowers:writing-plans` → 07 "첫 작업 3개"(① EXCLUDE 경합 테스트 RED→GREEN ② 매직링크→세션→GET /api/grid ③ 그리드 화면+예약/취소 E2E 2개)부터. brainstorming은 생략 — 이 프롬프트를 붙여 넣은 것이 설계 승인이다.</then>
UI 있음 → BUILD·REVIEW에서 frontend-design-taste dial = DENSITY 6 / MOTION 2 / VARIANCE 3 (03 UI 방향), 빈/로딩/에러/409 상태 필수.
<model_hints>
opus: FR-003(EXCLUDE 제약·23P01→409·Idempotency-Key), FR-001(매직링크 토큰·세션·레이트리밋), 05 DDL 마이그레이션, INV-1~5 검증 계층
sonnet: FR-002 그리드 페이지, FR-004~007 CRUD·목록·감사 로그, 06 RED 시나리오 작성, 07 compose·Dockerfile·backup.sh
haiku: 해요체 문구 일괄, RFC 9457 type slug·로그 필드명, README·.env.example 주석
</model_hints>  (분류 기준: references/model-routing.md 하단 표)
