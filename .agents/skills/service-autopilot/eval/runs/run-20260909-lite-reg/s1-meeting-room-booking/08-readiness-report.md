# 준비도 리포트 — 사내 회의실 예약 (lite)
판정일: 2026-09-09 · 검토 방식: lite — 메인 자기 점검(서브에이전트 없음) + `scripts/check_package.py` · 기준 03 v1.1

## check_package 출력 (패치 R1 후, 원문)
```
# check_package — …/s1-meeting-room-booking

## CRITICAL (0)

## HIGH (0)

## INFO (4)
- C2 FR 총 10 (P0 5 · P1 3 · P2 2), 05 참조 10
- C3 SC 총 5, 06 참조 5
- C5 축 행 27, 마킹 27, 질문 0
- C7 근거 미확인 상수 없음
```
(패치 전 1차 실행도 CRITICAL 0 · HIGH 0 — 아래 HIGH 2건은 스크립트가 아니라 자기 점검이 찾은 것)

## 자기 점검 발견 목록 (심각도순)
| # | 심각도 | 위치 | 내용 | 처리 |
|---|---|---|---|---|
| F1 | HIGH | 04·05·06·07 | 규칙 9 위반 — API 레이트(60/분)·메일 재시도(3)·타임라인 범위(7일)·멱등 TTL(24h)·토큰 길이(32B)·부하 행수(5만)·SLO 4개가 03 상수 표 밖에 값으로 산재 | **패치 R1**: 03 v1.1 상수 10개 추가, 하류 이름 참조로 교체, 04~07 `기준 03 v1.1` 전파 (DL-22). 닫힘 |
| F2 | HIGH | 06 계약 테스트 | 수량 주장 "17개 엔드포인트" — 05는 HTTP 15개 + JOB-1 | **패치 R1**: "HTTP 15개(JOB-1 제외)"로 정정. 닫힘 |
| F3 | MEDIUM | 06 | 용어 드리프트 "방" vs 03 용어 "회의실" | 패치 R1에서 "회의실"로 통일. 닫힘 |
| F4 | MEDIUM | 05 USER-1·USER-2 | FR에 직접 매핑되지 않는 엔드포인트 | 수용 — 03 상태 모델(user active→disabled)과 FR-009 익명화의 전제. 커버리지 표는 FR→엔드포인트 방향만 요구 |
| F5 | MEDIUM | 01·03 | 개인정보보호법 원문 URL 미확인(WebSearch 예산 소진) — RETENTION_DAYS 근거가 설계 결정뿐 | 선행 조건 P2로 이월. SC에는 쓰지 않았으므로 C7 통과 |
| F6 | MEDIUM | 07 .env.example | `SESSION_SECRET` 설명의 "32바이트 이상" 리터럴 | 수용 — 세션 서명 비밀의 최소 길이이며 TOKEN_BYTES(매직링크 토큰)와 다른 값. 구현 시 consts로 승격 여부 판단 |
| F7 | LOW | 03 배경 | "무설명 UX"는 질적 표현 | 요구사항이 아니라 구축 근거 서술. 요구사항 쪽은 SC-003(결정 지점 ≤3)으로 이미 변환됨 |

확인한 것(문제없음의 근거): FR-001~010 전부 05 커버리지에 있음(스크립트 C2) · SC-001~005 전부 06 시나리오(C3) · 03 미결정 0건, 03~07에 미정·TBD 표기 없음(C4) · register 23항목 + 승격 검토 4행 전부 마킹, 질문 0(C5) · 04~07 머리 `기준 03 v1.1`(C6) · 미확인 근거 상수 없음(C7) · 개정 배지 없음(C8) · 04 시퀀스의 컴포넌트명(Route Handler·booking 모듈·auth 모듈)이 04 컴포넌트도와 07 디렉터리(src/modules)에 일치 · 05 slug 21개가 03 엣지케이스 slug와 06 계약 테스트에서 동일 · 스택 추천(DL-5)에 PostgreSQL 문서 URL 근거, "만들지 말고 사라" 대안(MRBS·LibreBooking)을 숨기지 않음(DL-4).

## 판정: **PASS** (선행 조건 3, 모두 MEDIUM 이하)
HIGH 2건은 패치 R1로 닫혔고 스크립트 CRITICAL·HIGH 0. 잔여는 구현을 막지 않는다.

선행 조건 (첫 작업 전):
- P1. **MRBS 데모 30분** (https://github.com/meeting-room-booking-system/mrbs-code) — 그래도 자체 구축이면 진행. 이유 DL-4: 0원 대안이 요구를 이미 덮는다.
- P2. 개인정보보호법(국가법령정보센터) 원문에서 임직원 개인정보 보존·파기 조항 확인 → RETENTION_DAYS 근거를 `출처 URL`로 교체 (03 v1.2, 04~07 전파).
- P3. 팀 언어가 TypeScript가 아니면 DL-5 스택 B(Django)로 교체 — 05 계약·DDL은 그대로, 04 컴포넌트 이름만 바뀐다.

## 가정으로 채택된 항목 (질문 0 — 전부 Assumed)
규모 단일 사이트·회의실 ≤10·임직원 ≤50 (DL-2) · 인증 매직링크, SSO non-goal (DL-7) · 배포 사내 서버 1대 compose, 백업 오프호스트 (DL-8) · 보존기간 365일 (DL-9) · 팀 언어 TS (DL-5) · PC+폰 반응형 (00 해석 4) · 예약 규칙 상수 (DL-11).

## 구현 핸드오프 (service-prompt-workflow SPEC 입력)
/service-prompt-workflow 로 다음을 실행:
<inputs>s1-meeting-room-booking/03-prd.md (요구사항 FR-001~010 · INV-1~5 · 상수 표 27개), 05-api-contract.md (엔드포인트 15 + JOB-1 · DDL · slug 21), 08-readiness-report.md (선행 조건 P1~P3 · 첫 작업 3개)</inputs>
<references>04-architecture.md, 06-test-design.md, 07-ops-design.md — 필요할 때만 읽는다</references>
<first_task>SPEC.md 작성 — 위 문서를 진실원으로, 낯선 구현자 실행 가능 수준(≥7/10)</first_task>
<then>superpowers 설치 시 `superpowers:writing-plans` → 첫 작업 3개(워킹 스켈레톤, 07 착수 자산)부터: (1) 0001_init.sql + 동시 100요청 통합 테스트 RED→GREEN (2) validateBookingWindow → POST/GET /api/bookings → 타임라인 화면 1장 (3) 매직링크 인증 + compose 기동 + seed-week. brainstorming은 생략 — 이 프롬프트를 붙여 넣은 것이 설계 승인이다.</then>
UI 있음 → BUILD·REVIEW에서 frontend-design-taste dial = 밀도 중간 · 모션 낮음 · 강조색 1 · 내부 도구 프로파일 (03 UI 방향, 원칙 L-03·L-06·L-07·L-10·T-04·T-08·T-09)
<model_hints>
opus: FR-003(EXCLUDE 제약·23P01→409·Idempotency-Key), FR-001(매직링크·세션·Origin 검사·requireRole), 05 DDL 0001_init.sql, INV-2 validateBookingWindow 경계값
sonnet: FR-002 타임라인 화면·BK-1, FR-004/005/006/007 CRUD·cancel, 06 RED 시나리오 작성, 07 compose·Caddyfile·backup.sh, FR-009 retention 잡, FR-010 healthz
haiku: 해요체 문구 일괄, 로그 event 필드명, .env.example 주석, README
</model_hints>  (분류 기준: references/model-routing.md 하단 표)
