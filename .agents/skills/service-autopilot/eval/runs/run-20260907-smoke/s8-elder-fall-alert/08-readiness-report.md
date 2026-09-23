# 준비도 리포트 — 요양시설 낙상 감지·알림 서비스 (FallGuard)
버전: v1.0 · 기준 03 v1.2
작성: 2026-09-07 · 강도 full · 오토파일럿(질문 0, 추천안 5개 `Assumed(무응답)`)

## 판정: **CONCERNS**
GATE 1차 적대적 검토(fresh-context 서브에이전트, fable)는 **FAIL**(CRITICAL 2·HIGH 5·MEDIUM 9·LOW 3)이었다. 전 지적을 타당성 필터(거짓 양성 0)로 통과시켜 03 v1.2·04 v1.2·05 v1.2·06 v1.2·07 v1.1로 **패치 1회** 반영했고 `check_package.py`는 CRITICAL 0·HIGH 0이다. 그러나 평가 런 예산 규칙에 따라 **독립 재검토를 하지 않았으므로** 패치의 정합성은 메인 자기 점검으로만 확인됐다 — 이것이 CONCERNS의 유일한 사유다. 핸드오프 시 아래 "착수 조건"의 첫 항목(패치 범위 재검토)을 SPEC 단계에서 먼저 수행하면 PASS로 전환할 수 있다.

## 절차 이탈 (평가 런 예산 — 명시)
| 항목 | SKILL.md 규정 | 이 런 | 사유 |
|---|---|---|---|
| GATE 검토 인원 | 돈·안전·법 도메인이면 `ecc:santa-method` 독립 리뷰어 2명 | fresh-context 서브에이전트 **1명 1회** (fable) | 운영 제약(평가 런 예산) |
| 재실행·재검토 | FAIL 시 해당 단계 재실행 후 재검토 1회 | 패치 1회, **재검토 없음** → CONCERNS 마감 | 운영 제약 |
| A2 질문 | 최대 5문항 1회 제시 | 배치 생성 후 미제시, 추천안 자동 채택 `Assumed(무응답)` | 오토파일럿 모드 |
| A1 검색 예산 | 서브에이전트 10~15회 | 26회 | 5영역 출처 확보 위해 서브에이전트가 확장 (decision-log A1 결과) |
| 세션 중단 | — | 세션 한도(429)로 1회 중단, 실행 절차 5로 재개(GATE 결과 수신 후, 08 작성 전). 재개 후 웹 검색 불가 → Node 버전은 가정 A-9 | decision-log 재개 기록 |

## check_package.py 출력
**GATE 전 (v1.1)**
```
## CRITICAL (0)
## HIGH (0)
## INFO (3)
- C2 FR 총 27 (P0 17 · P1 7 · P2 3), 05 참조 27
- C3 SC 총 12, 06 참조 12
- C5 축 행 49, 마킹 49, 질문 5
```
**패치 후 (v1.2)**
```
## CRITICAL (0)
## HIGH (0)
## INFO (3)
- C2 FR 총 29 (P0 19 · P1 7 · P2 3), 05 참조 29
- C3 SC 총 12, 06 참조 12
- C5 축 행 49, 마킹 49, 질문 5
```
스크립트는 ID 존재만 센다. 의미적 매핑은 GATE 검토관(1차)과 아래 자기 점검이 본다.

## GATE 1차 검토 — 발견과 처리
검토관 입력: 00~07 + decision-log + check_package 출력. 판정 FAIL. 타당성 필터: 19건 전부 타당(거짓 양성 0). 처리 위치는 문서 버전 v1.2/v1.1.

| # | 심각도 | 지적(요약) | 처리 |
|---|---|---|---|
| C1 | CRITICAL | 생활실(다인실) 매핑 디바이스 이벤트는 `resident_id`가 없어 보호자 통지가 구조적으로 불가 | E-13 `resident_id`(생활실 이벤트의 `confirmed_fall`에 필수), FR-009·FR-010 개정, EC-C4, TS-061, `family_notice_blocked` 배너 |
| C2 | CRITICAL | 07 알람 임계·SLO·05 레이트리밋·멱등 TTL·SSE 주기·VM 사양·Node 버전이 03 상수 표 밖(출처 없음) | 03 상수 19개 승격(근거 열), 05·07은 이름 참조, VM = 가정 A-8, Node = 가정 A-9 |
| H3 | HIGH | FORCE RLS가 로그인·디바이스 CN·클레임·토큰 조회를 0행으로 만듦; 403 규약은 RLS 아래서 불가 | BYPASSRLS 롤 소유 `SECURITY DEFINER` 함수 4개(05 DDL), 타 시설 단건 404·목록 0행(SC-007·TS-014·TS-064), #21 superseded |
| H4 | HIGH | 근무조 미배정 이벤트는 J-01 CAS 조건 불일치로 level 2 영구 미도달 | 라우팅을 수신 트랜잭션 안으로, J-01 level = 초기+1, `next_escalation_at`(04 시나리오 1·05 J-01·TS-029) |
| H5 | HIGH | 이벤트 없는 관리자 알림(오프라인·채널)을 저장할 행이 없고 채널 미정의 | `notifications.event_id` nullable + `kind` 11종, 용어집 "관리자 알림 = facility_admin 전원 SMS + 콘솔 배너" |
| H6 | HIGH | "시설 전체 오프라인" 감지가 FR·잡·테스트에 없음(디바이스 수만큼 SMS 폭주) | FR-028, J-02 시설 단위, `facilities.status`, TS-059, AL-04 근거 정정 |
| H7 | HIGH | 시설 책임자는 `users` 행이 아니라 ack 불가 → 사이렌 지속·AL-03 오발 | FR-029, `escalation_contact_user_id`(facility_admin), E-32 1회용 ack 링크, `ack_links`, TS-060 |
| M8 | MEDIUM | 06·07에 값 재기입("30일치", "10초") | `PILOT_DURATION`·`CANARY_ROUNDTRIP_MAX`로 치환 |
| M9 | MEDIUM | decision-log #23↔#29 상충, EC-B3 입소자 키 잔존, HMAC 폐기 후 "서명 토큰" 잔존 | #23 superseded, EC-B3 디바이스 키, 03~07 "열람 토큰/링크"로 통일 |
| M10 | MEDIUM | 카운트다운의 `next_escalation_at`이 계약에 없음(클라 계산, L-07 위반) | E-10·E-11·E-14·DDL에 추가, TS-065 |
| M11 | MEDIUM | 목록 엔드포인트는 403이 나올 수 없어 TS-014가 형식적 | 단건 404 / 목록 0행으로 분리 |
| M12 | MEDIUM | J-06 `degraded/down` 임계·FR-022 "연속 실패" 횟수 미정의 | `CHANNEL_DEGRADED_FAIL_RATE`·`CHANNEL_DOWN_STREAK`, FR-022·J-06·TS-048 개정 |
| M13 | MEDIUM | 착수 자산 4건: `retention` DSN 없음, 런북 9개 중 1개, 상수 위치 불일치, 첫 작업 1이 인증 필요 TS 포함 | `.env.example` 키 추가, 런북 착수 시점 명시, `packages/contracts/constants.ts`로 통일, 인증서 오프라인 발급 + AuthModule을 작업 2로 |
| M14 | MEDIUM | 4GB VM에 Prometheus·Grafana·Loki·Vector 동거(컨테이너 9개) — 파일럿 과잉·R4 확대 | 외부 업타임 모니터 + J-09 `ops-alarm` + SQL 뷰로 축소, 모니터링 스택은 다시설 단계 |
| M15 | MEDIUM | 카나리 "자동 false_alarm"은 E-13 409·`created_by NOT NULL`과 충돌, 통계 제외 플래그 없음 | `devices.is_canary` — detections만 저장, 이벤트·알림·집계 제외, E-30 `canary_last_ok_at` |
| M16 | MEDIUM | 미매핑 디바이스 M-01이 `room_id NOT NULL`로 INSERT 실패 → ack 안 됨 → poison 재전달 | EC-A5: detections 저장 + 관리자 알림 `unmapped_device` + ack, 이벤트 없음, TS-062 |
| L17 | LOW | AL-03이 최대 단계 도달 이벤트도 오발 | 조건에 `escalation_level < ESCALATION_LEVELS − 1` |
| L18 | LOW | audit_log 보존이 04 "파기" / 05 "남긴다"로 상충 | "EVIDENCE_RETENTION 후 파기 대상, 구현 P2"로 통일 |
| L19 | LOW | TS-001 "ALERT_LATENCY_P95의 1/2"는 근거 없는 분할 | `INGEST_TO_NOTIFY_MAX` 상수화(근거: 서버 구간 예산) |

검토관이 "잘 된 점"으로 든 것: A4 검토 13건의 ID 단위 전파, 원시/병합 분리 + 같은 tx 잡 예약 + CAS 골격, 시크릿 0·register 49/49·질문 5·운영 검증 SC의 정직한 분리.

## 패치 후 메인 자기 점검 (<check> 항목별 — 재검토 대체, 한계 명시)
- **커버리지 공백**: FR-028→J-02 시설 단위·E-14 `facility_state`, FR-029→E-25·E-32·`ack_links`, FR-026→E-31·J-07, FR-027→J-08 — 각 엔드포인트/잡이 해당 FR의 동사를 실제로 수행한다. SC-007→TS-014(단건 404·목록 0행)·TS-064(부트스트랩)·TS-057(워커 RLS)로 HTTP 밖 경로까지. SC-009→TS-017·TS-063.
- **모호성**: 03~07에 `미정·TBD·추후·(선택)` 0건(스크립트 C4). 04~07이 쓰는 대문자 상수는 전부 03 상수 표(47개, RPO·RTO 포함)에 존재 — 스크립트 외 정규식 대조로 확인. `PILOT_DURATION`은 07 SLI 절에서 참조.
- **불일치**: 상태값(`detected/notified/acked/resolved/family_notified`), 역할(`facility_admin/staff/family_viewer`), 잡 ID(J-01~J-09), 엔드포인트(E-01~E-32), 토큰 용어(열람 토큰/ack 링크)를 03~07에서 통일. 04 시나리오 1의 라우팅 순서와 05 J-01 규칙, 06 TS-029가 같은 순서(라우팅→INSERT→예약→COMMIT).
- **절대규칙**: 시크릿 값 0(`.env.example`은 키만), register 49/49, 질문 5. 출처 없는 값은 가정 A-8·A-9로 명시(웹 확인 불가 사유 포함).
- **안전 경로**: 소리 없이 멈추는 경로로 지적된 4곳(H1·H2·H4·M16) 전부 DB 제약·같은 tx·수동 ack·EC-A5로 닫힘. 남은 Accept: R1(시설 인터넷 두절)·R3(전 채널 동시 장애)·R4(단일 인스턴스) — 아래 잔여 리스크.
- **중복·과잉**: 모니터링 스택 축소(M14), 하이브리드·RLS 대안은 D1·D8에 기록. 중복 결정은 decision-log에서 superseded 표기(#21·#23).
- **실행가능성**: 07 착수 자산 — 디렉터리 2단계, `.env.example` 21키, 첫 작업 3개가 RED 시나리오 ID를 갖고 US-1~4를 관통. 벤더 샘플 페이로드 1건 확보가 첫 작업 1의 완료 조건.
- **한계**: 위 점검은 패치 작성자 본인이 수행했다. 특히 05 DDL의 `SECURITY DEFINER` 함수 시그니처와 RLS 상호작용, 04 시나리오 1의 트랜잭션 경계(라우팅 조회가 `shifts`를 잠그지 않음)는 구현 시 첫 통합 테스트(TS-029·TS-064)에서 실측이 필요하다.

## 잔여 리스크 (Accept, 파일럿) 및 착수 조건
| 조건 | 왜 먼저인가 |
|---|---|
| **1. 패치 범위 재검토** — SPEC 작성 전에 03 v1.2 개정 항목(FR-009·010·012·022·028·029, 상수 19개)과 05 v1.2(부트스트랩 함수·`notifications.kind`·E-32)를 리뷰어 1명이 독립 검토 | 이 런에서 재검토를 생략한 유일한 CONCERNS 사유 |
| 2. 벤더 레이더 실제 샘플 페이로드 1건 + `device_event_id` 유일성 확인 (A-7, R2) | 첫 작업 1의 완료 조건. 틀리면 dedup·멱등 설계가 소리 없이 드롭 |
| 3. 출시 전 법률 검토 1회 — 비의료기기 문구(FR-023), 인지저하 입소자 대리 동의 절차(A-5), `EVIDENCE_RETENTION` 3년 근거 | Q4·P2 개인정보의 미확인 항목 |
| 4. 시설 LTE 백업 회선 권고를 계약·운영 조건에 포함 (R1), 콘솔 상시 가동 조건 (R3) | Accept한 리스크의 완화가 시설 측 행동에 달림 |
| 5. Node.js Active LTS 메이저 확인 후 Dockerfile 태그 고정 (A-9) | 재개 후 웹 확인 불가로 가정 처리 |

## 핵심 결정 5줄
1. 감지 = mmWave 레이더(비영상) 벤더 디바이스 + 어댑터, 클라우드 SaaS, 시설 B2B 구독 — 전부 A2 추천안 자동 채택.
2. 안전 경로는 DB로 보증: 원시 `fall_detections`(멱등·증거) / 병합 `fall_events`(디바이스당 활성 1) / 같은 트랜잭션 J-01 예약 + CAS / mqtt.js 수동 ack.
3. 알림 전달 = 발송 원장 `notifications` + 앱 수신 보고(E-31) + `CHANNEL_FALLBACK_DELAY` 후 SMS 폴백 + 3단계 에스컬레이션(마지막은 facility_admin + ack 링크).
4. 보호자 통지는 직원 `confirmed_fall`(입소자 확정) 시에만, 알림톡→SMS 폴백 + 열람 링크(앱 없음).
5. 테넌시 = RLS(FORCE) MVP 적용 + 부트스트랩 함수 4개; 관측성은 파일럿에서 외부 업타임 + J-09 + SQL 뷰로 최소화.

## 질문 → 가정 채택 목록 (오토파일럿)
| Q | 채택(추천안) | 답이 바뀌면 재작업 범위 |
|---|---|---|
| Q1 감지 방식 | mmWave 레이더 천장형(비영상) | 04 컴포넌트·05 M-01·개인정보(영상 발생 시 25조) |
| Q2 보호자 통지 시점 | 직원 낙상 확정 시에만, 오탐 미통지 | FR-010·J-04·06 시나리오 |
| Q3 과금 주체 | 시설 B2B 침상당 월정액, 보호자 무료, 수동 청구 | P2 결제·테넌시(변경 작음) |
| Q4 의료기기 경계 | 비의료기기 포지셔닝 + 법률 검토 1회 | FR-023 문구·출시 절차 |
| Q5 배포 형태 | 클라우드 멀티테넌트 SaaS, 디바이스 아웃바운드 | 04 전체(온프레미스/하이브리드 D1) |

## 구현 핸드오프 (service-prompt-workflow SPEC 입력)
/service-prompt-workflow 로 다음을 실행:
<inputs>autopilot/s8-elder-fall-alert/03-prd.md (요구사항 FR-001~029·상수 표 47개·용어집), 05-api-contract.md (E-01~E-32·M-01~03·J-01~J-09·DDL·커버리지), 08-readiness-report.md (착수 조건 5개·첫 작업 3개)</inputs>
<references>04-architecture.md (구현 접근·시나리오 1~3·STRIDE TM-01~28·R1~R4), 06-test-design.md (TS-001~065), 07-ops-design.md (compose·CI/CD·알람 AL-01~09·착수 자산) — 필요할 때만 읽는다</references>
<first_task>SPEC.md 작성 — 위 문서를 진실원으로, 낯선 구현자 실행 가능 수준(≥7/10). 착수 조건 1(패치 범위 재검토)을 SPEC 리뷰에 포함한다</first_task>
<then>superpowers 설치 시 `superpowers:writing-plans` → 07 "첫 작업 3개"(워킹 스켈레톤: ① 벤더 페이로드 계약 + 수신 보장 ② 감지→푸시→ack + 같은 tx 에스컬레이션 + ack 링크 ③ 콘솔 SSE 보드 + 대응 기록→보호자 통지)부터. brainstorming은 생략 — 이 프롬프트를 붙여 넣은 것이 설계 승인이다.</then>
UI 포함 — BUILD·REVIEW에서 frontend-design-taste dial: 직원 앱 DENSITY 4 · MOTION 2 · VARIANCE 2 (다크 기본, ack 버튼 ≥64px 하단 고정), 관리자 콘솔 DENSITY 8 · MOTION 2 · VARIANCE 3 (Cockpit 모드). 문구 해요체(T-09)·FR-023 비의료기기 문구 검사(TS-049).
<model_hints>
opus: FR-003·004(수동 ack·fall_detections/fall_events·부분 유니크·경합 TS-026·052), FR-008(같은 tx J-01·CAS·next_escalation_at), FR-006·026(J-07 전달 확인 폴백), FR-018(RLS FORCE·SECURITY DEFINER 부트스트랩 4개·TS-057·064), FR-019(감사 로그 권한·트리거), FR-029(E-32 ack 링크 토큰), 05 DDL 0001 마이그레이션, 04 R4 복구 절차
sonnet: FR-001·002(디바이스 등록·클레임·매핑), FR-005·015(shifts 라우팅), FR-007·009·013·014·016·017·020·022·027·028 CRUD·SSE·집계, 06 RED 시나리오 작성(TS-001~065), 07 compose·Dockerfile·CI, 직원 앱 화면(4상태)·콘솔 보드, device-sim
haiku: 03 용어집 문구(해요체) 일괄, 로그 필드명·에러 슬러그 목록, README·runbook 골격 RB-01~09 복제, .env.example 정리
</model_hints>
