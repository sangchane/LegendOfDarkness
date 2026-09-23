# PRD — 요양시설 낙상 감지·알림 서비스 (FallGuard)
버전: v1.0
개정 노트 R1 적용 대기 — REVISIONS.md R1 (A4 독립 검토 반영, 03 패치 목록대로 적용 후 v1.1로 올리고 이 줄을 지운다)

## 진입 사전조사 (근거 3줄)
- 정량: 전통적(비AI) 낙상 알람의 병원 응답시간 평균 약 13분(범위 3~21분) — Kepler 블로그가 인용한 미국 4개 병원 연구, 1차 원문 미대조 (01-recon 표본 #9·#10, 확인 2026-09-08). Kepler Night Nurse 실증: 낙상 100% 감지·오탐 99%↓·바닥 방치시간 1/6 미만 (표본 #3).
- 정성: 28년 경력 요양보호사 — "야간 근무를 서고 나면 남은 사람들의 업무 강도는 상상을 초월한다" (여성경제신문, 01-recon A3 정성, 확인 2026-09-08). 병원 간호사 알람 피로가 스트레스 증가·성과 저하와 연관된다는 국내 연구 (PMC13299418).
- 사용자 영향: 야간 요양보호사는 이동 중·소등 환경에서 폰 하나로 알림을 받고 한 손으로 확인해야 한다 → 알림 화면은 결정 1개(L-03), 타깃 ≥ 44pt(L-02), 피드백 ≤ 400ms(L-10). 오탐이 잦으면 알림을 무시하게 되므로(알람 피로) 오탐 상한이 지연만큼 중요하다.

## 배경 (RECON 요약)
- 노인요양시설은 65세+ 낙상 최다 발생 장소(2024년 523건, 소비자원 CISS). 야간 1인이 다수 입소자를 순회 → 무목격 낙상 → 바닥 방치가 핵심 고통. (01-recon 업무 흐름)
- 2023-06-22 장기요양기관 CCTV 설치 의무화(60일 보관·열람대장 3년), 2025년 요양보호사 배치 2.1:1 강화. 법정 CCTV는 사후 확인용이며 실시간 감지는 하지 않는다. (01-recon 규제 (a))
- 상용 비접촉 감지(Vayyar·Kepler·Milesight)가 존재하나 국내 요양시설 워크플로(직원 확인 → 보호자 통보 → 기록)에 맞춘 알림·기록 서비스는 확인되지 않음. (01-recon 유사 솔루션)
- 감지 알고리즘은 센서 내장 판정을 쓰고(DL-11), 서비스는 **알림·확인·에스컬레이션·통보·기록**에 집중한다. 링크: `01-recon.md`, `02-blindspot-register.md`.

## CAPABILITY (product-capability 재진술)
국내 노인요양시설의 야간 근무 직원이, 침실·복도의 비영상 레이더 센서가 낙상을 감지하면 15초 안에 스마트폰과 관제 화면으로 알림을 받아 한 탭으로 확인하고, 아무도 확인하지 않으면 자동으로 에스컬레이션되며, 현장 확인 후 실제 낙상만 보호자에게 기록과 함께 통보되고, 모든 행위가 감사로그로 남는다. 바뀌는 결과: 무목격 낙상의 바닥 방치 시간이 순회 주기(수십 분)에서 수 분으로 줄고, 시설은 낙상 대응 기록을 자동으로 갖는다.

## 제품 목표 (3개, 직교)
- G1 **놓치지 않고 빨리 알린다** — 감지 민감도와 감지→직원 단말 지연 (SC-001·003·004·005)
- G2 **직원을 오탐으로 지치게 하지 않는다** — 센서당 오탐 상한과 병합·억제 (SC-002)
- G3 **확인된 사실을 보호자에게 기록과 함께 전한다** — 동의 기반 통보·감사로그 (SC-006·007·011)

## 유저 스토리 (P1만으로 MVP 성립)
| ID | 우선순위 | 스토리 | 독립 테스트 |
|---|---|---|---|
| US-1 | P1 | As a 야간 요양보호사, I want 낙상 즉시 폰 알림을 받고 한 탭으로 확인하고 싶다, so that 바닥에 방치되는 시간을 줄인다 | 모의 낙상 → 알림 표시 시각·ACK 시각 로그 |
| US-2 | P1 | As a 야간 책임자(간호사), I want 관제 화면에서 구역별 상태와 미확인 알림을 보고, 아무도 확인 안 하면 나에게 전화가 오길 원한다, so that 대응 공백이 없다 | ACK 없는 이벤트 → 재알림·전화 로그 |
| US-3 | P1 | As a 직원, I want 현장 조치(상태·조치·이송)와 실제/오탐을 기록하고 싶다, so that 낙상 보고와 오탐 개선에 쓴다 | resolve 입력 → 이벤트 상태·라벨 저장 |
| US-4 | P1 | As a 보호자, I want 확인된 낙상을 앱(없으면 알림톡·문자)으로 조치 내용과 함께 받고 싶다, so that 시설에 전화해 상황을 안다 | resolve(실제) → 통보 → 도달 콜백 |
| US-5 | P2 | As a 시설 관리자, I want 구역·센서·직원·입소자·보호자를 등록하고 월간 낙상 통계를 내보내고 싶다, so that 운영·공단 평가에 쓴다 | CRUD + CSV 내보내기 |

## 요구사항 풀
| ID | 요구사항 (EARS) | 우선순위 | 출처 |
|---|---|---|---|
| FR-001 | 센서가 낙상 이벤트를 보고하면, 시스템은 FallEvent(detected_at·received_at·zone·device)를 생성하고, 같은 device의 재감지가 DEDUP_WINDOW_SEC 안이면 기존 이벤트에 병합해야 한다 | P0 | US-1, PX 알람 폭주 |
| FR-002 | FallEvent가 생성되면, 시스템은 해당 구역의 당직 직원 앱(Time-Sensitive/Critical 푸시)과 관제 화면에 ALERT_LATENCY_P95 안에 알림을 표시해야 한다. 당직 직원이 0명이면 시설 전 직원과 시설장에게 보낸다 | P0 | US-1, US-2 |
| FR-003 | 직원이 알림을 확인(ack)하면, 시스템은 최초 ack만 유효로 기록하고 다른 직원 단말에는 "○○님이 확인했어요"로 전파해야 한다. 이후 ack는 멱등(200)이다 | P0 | US-1, INV-5 |
| FR-004 | ack 없이 STAFF_ACK_TIMEOUT_SEC이 지나면 시스템은 시설 전 직원에게 재알림하고, ESCALATION_TIMEOUT_SEC이 지나면 야간 책임자·시설장에게 음성 전화(TTS)를 걸어야 한다 | P0 | US-2, PX6 |
| FR-005 | ack 후 직원은 조치 기록(입소자 상태·조치·이송 여부·실제/오탐 라벨)을 입력해 resolve해야 하며, 라벨은 필수다. ack 후 RESOLVE_REMINDER_MIN 내 미입력 시 리마인더를 보낸다 | P1 | US-3, PX8·PX11 |
| FR-006 | 라벨이 "실제 낙상"인 resolve에서 직원이 "보호자에게 알리기"를 실행하면, 시스템은 통보 동의가 유효한 보호자에게만 앱 푸시 → 알림톡 → SMS 순으로 폴백 발송하고 결과를 기록해야 한다. 오탐 라벨이면 실행할 수 없다 | P1 | US-4, INV-3 |
| FR-007 | 시스템은 입소자(또는 법정대리인)의 감지 동의와 보호자의 통보 동의를 일시·범위·철회와 함께 기록해야 하며, 통보 동의가 없거나 철회된 보호자에게는 통보하지 않아야 한다. 감지 동의 미기록 입소자가 있는 구역은 관제에 경고 배지를 표시한다 | P0 | 개인정보보호법, PX5 |
| FR-008 | 센서·게이트웨이는 SENSOR_HEARTBEAT_SEC마다 하트비트를 보내고, SENSOR_OFFLINE_AFTER_SEC(게이트웨이는 GW_OFFLINE_AFTER_SEC) 동안 끊기면 시스템은 관제 화면 배지와 관리자 알림(낙상 알림과 다른 등급·소리)을 내야 한다 | P0 | PX10, P1 연결 끊김 |
| FR-009 | 이벤트 생성·ack·resolve·통보·열람·설정 변경은 append-only 감사로그(행위자·시각·대상·이전/이후)에 해시 체인으로 기록되어야 하며 삭제 API는 없어야 한다 | P0 | STRIDE R/T, 규제 (a) 열람대장 3년 준용 |
| FR-010 | 관제 화면은 구역별 상태(정상/알림/확인중/오프라인/동의 미기록)와 미확인 알림 목록을 실시간(≤ CONSOLE_REFRESH_SEC) 표시하고, 데이터가 STALE_AFTER_SEC 이상 갱신되지 않으면 stale 표시를 해야 한다 | P1 | US-2, frontend-design-taste |
| FR-011 | 직원 앱은 로그인·당직 구역 설정(on/off)·알림 수신·ack·resolve를 제공해야 하며, 알림 화면은 결정 1개(확인)로 구성한다 | P1 | US-1, US-3 |
| FR-012 | 보호자 앱은 연결 코드로 입소자를 연결하고, 통보 목록·조치 내용·시설 연락처를 보여줘야 한다 | P1 | US-4 |
| FR-013 | 관리자는 시설·구역·센서·직원·입소자·보호자를 등록·수정·비활성화하고, 센서를 QR로 구역에 바인딩(프로비저닝)할 수 있어야 한다 | P2 | US-5, P1 프로비저닝 |
| FR-014 | 관리자는 월간 낙상 통계(구역·시간대·실제/오탐·바닥 방치 시간)를 CSV로 내보낼 수 있어야 한다 | P2 | US-5, PX8 |
| FR-015 | 모든 데이터 접근은 facility_id로 격리되어야 하며, 다른 시설의 데이터는 어떤 API로도 조회·변경할 수 없어야 한다 | P0 | P2 테넌시, INV-4 |
| FR-016 | 관리자는 센서별 오탐율(직원 라벨 기준)과 최근 30일 추이를 볼 수 있어야 한다 | P1 | PX11, G2 |
| FR-017 | 게이트웨이는 클라우드 단절 시 EDGE_BUFFER_HOURS 동안 이벤트·하트비트를 로컬에 버퍼하고 복구 시 순서대로 재전송해야 하며, received_at − detected_at이 LATE_EVENT_SEC을 넘는 이벤트는 "지연 도착"으로 표시·알림해야 한다 | P0 | P1 연결 끊김, INV-7 |
| FR-018 | 시스템은 푸시·알림톡·SMS 발송 결과 콜백을 저장하고, 모든 채널이 실패한 알림은 관제 화면에 "미전달"로 표시해야 한다 | P1 | 축 5, P2 이메일/알림 |
| FR-019 | 시스템은 EVENT_RETENTION_YEARS가 지난 이벤트·감사로그를 파기하고, 통보 동의 철회 후 CONSENT_WITHDRAW_PURGE_DAYS가 지나면 보호자 연결 정보를 파기해야 한다 | P2 | P2 개인정보 |
| FR-020 | 직원 앱은 최소 지원 버전 미만이면 사용을 막고(hard gate), 보호자 앱은 안내만(soft gate) 해야 한다 | P2 | P4 강제 업데이트 |

## 제약·불변식 (INV — product-capability CONSTRAINTS)
| ID | 불변식 | 강제 위치 |
|---|---|---|
| INV-1 | FallEvent는 삭제되지 않는다. 상태 전이만 존재한다 | DB(삭제 권한 없음) + API(DELETE 부재) |
| INV-2 | 상태 전이는 `detected → acked → resolved` 순서만 허용. `escalated`는 상태가 아니라 타임라인 플래그다 | DB CHECK + 서비스 계층 |
| INV-3 | 보호자 통보는 `event.state = resolved AND event.label = actual_fall AND consent(notify).status = active`일 때만 발송된다 | 서비스 계층 + 감사로그 |
| INV-4 | 모든 테이블 행은 facility_id를 갖고, 쿼리는 RLS로 시설 범위를 강제한다 | PostgreSQL RLS |
| INV-5 | ack는 이벤트당 최초 1건만 유효(동시 요청은 첫 커밋 승자), 이후 ack는 멱등 | DB 유니크 + 트랜잭션 |
| INV-6 | 알림 발송은 (event_id, recipient, channel, attempt)로 멱등 | notification 유니크 키 |
| INV-7 | 이벤트는 detected_at(센서/게이트웨이 시각)과 received_at(서버 시각)을 둘 다 가지며 둘 다 UTC | 스키마 NOT NULL |
| INV-8 | 원시 센서 데이터(포인트클라우드·영상)는 게이트웨이 밖으로 전송되지 않는다. 클라우드는 이벤트 메타데이터만 갖는다 | 게이트웨이 코드 + 계약 |

## 엣지케이스 (Given/When/Then)
**흐름 A. 감지 → 알림**
- EC-A1 Given 같은 센서의 이벤트가 열려 있음 When DEDUP_WINDOW_SEC 안에 같은 센서가 재감지 Then 새 이벤트를 만들지 않고 기존 이벤트의 `re_detected_count`를 올리고 알림은 다시 보내지 않는다
- EC-A2 Given 게이트웨이가 클라우드와 단절됨 When 단절 중 낙상이 감지되고 EDGE_BUFFER_HOURS 안에 복구 Then 이벤트는 detected_at 원본으로 도착하고, received_at − detected_at > LATE_EVENT_SEC이면 "지연 도착" 등급으로 관제·관리자에게 알리되 직원 낙상 알림 경로는 그대로 탄다
- EC-A3 Given 해당 구역 당직 직원이 0명 When 낙상 감지 Then 시설 전 직원 + 시설장에게 알림하고 관제에 "당직 미설정" 배지를 띄운다
- EC-A4 Given 직원 푸시 토큰이 만료됨 When 알림 발송 Then 푸시 실패를 기록하고 그 직원에게 SMS로 폴백하며, 관제 화면은 영향 없이 표시한다

**흐름 B. 확인 → 에스컬레이션**
- EC-B1 Given 직원 2명이 동시에 ack When 두 요청이 같은 순간 도착 Then 첫 커밋만 acked_by가 되고 두 번째는 200 + "○○님이 먼저 확인했어요"를 받는다
- EC-B2 Given ack 후 직원 앱이 종료됨 When RESOLVE_REMINDER_MIN이 지나도 resolve 없음 Then 그 직원과 야간 책임자에게 리마인더를 보내고 관제에 "확인중(기록 대기)"으로 남긴다
- EC-B3 Given ESCALATION_TIMEOUT_SEC 경과로 전화 발신 When 수신자가 받지 않음 Then ESCALATION_CALL_RETRY회 재시도 후 관제에 "에스컬레이션 미응답" 배지 + 시설장 SMS
- EC-B4 Given 이벤트가 acked When 다른 직원이 resolve 시도 Then 허용한다(누구든 기록 가능), resolved_by는 실제 입력자

**흐름 C. 보호자 통보**
- EC-C1 Given 보호자 앱 미설치 When 통보 실행 Then 푸시를 건너뛰고 알림톡 → 실패 시 SMS로 폴백, 각 결과를 저장
- EC-C2 Given 보호자 통보 동의가 철회됨 When 직원이 통보 실행 Then 발송하지 않고 "동의가 철회되어 보낼 수 없어요"를 표시하며 감사로그에 시도를 남긴다
- EC-C3 Given 보호자가 2명 이상 When 통보 실행 Then 동의 유효한 전원에게 각각 발송하고 결과를 개별 추적한다
- EC-C4 Given 라벨이 오탐 When 통보 버튼 Then 비활성(누를 수 없음), API 호출 시 409

**흐름 D. 센서·게이트웨이 상태**
- EC-D1 Given 정전으로 게이트웨이와 전 센서가 동시에 끊김 When GW_OFFLINE_AFTER_SEC 경과 Then 센서별 알림 N건이 아니라 "게이트웨이 오프라인" 1건으로 그룹해 관리자에게 알린다
- EC-D2 Given 센서 1대만 하트비트 누락 When SENSOR_OFFLINE_AFTER_SEC 경과 Then 관제 구역 배지 + 관리자 알림(낙상 등급 아님), 복구 시 자동 해제
- EC-D3 Given 오프라인 센서가 담당하던 구역 When 그 사이 낙상이 있었어도 감지되지 않음 Then 시스템은 이를 감지할 수 없다 — 잔여 리스크로 시설에 사전 고지(04 ④)

## 성공 기준 (전부 pass/fail, 파일럿 PILOT_DAYS 기준)
| ID | 기준 | 측정 방법 |
|---|---|---|
| SC-001 | 모의 낙상 시험(침실·복도, 주 1회 이상, 총 ≥ 40회)에서 민감도 ≥ FALL_SENSITIVITY_MIN | 시험 일지 ↔ FallEvent 대조 |
| SC-002 | 파일럿 후반 2주 동안 직원 라벨 기준 오탐 ≤ FALSE_ALARM_MAX_PER_SENSOR_WEEK (센서별) | resolve 라벨 집계(FR-016) |
| SC-003 | 감지→직원 단말 표시 지연 p95 ≤ ALERT_LATENCY_P95, 최대 ≤ ALERT_LATENCY_MAX | detected_at ↔ 앱 표시 시각(클라이언트 로그 업로드) |
| SC-004 | ack 없는 이벤트 100%가 STAFF_ACK_TIMEOUT_SEC 후 재알림, ESCALATION_TIMEOUT_SEC 후 전화 발신 | 감사로그 타임라인 |
| SC-005 | 실제 낙상의 바닥 방치 시간(detected_at→ack) 중앙값 ≤ FLOOR_TIME_TARGET_MIN | FallEvent 집계 |
| SC-006 | 통보 실행→첫 채널 도달 콜백 p95 ≤ GUARDIAN_NOTIFY_DELIVERY_P95 | notification 로그 |
| SC-007 | 동의 없는/철회된 보호자에게 발송 0건, 오탐 라벨 이벤트 통보 0건 | 감사로그 전수 + 자동 테스트 |
| SC-008 | 센서·게이트웨이 오프라인이 SENSOR_OFFLINE_AFTER_SEC + 30초 안에 관제에 표시되는 비율 100% | 강제 전원 차단 시험 ≥ 10회 |
| SC-009 | 게이트웨이 단절 EDGE_BUFFER_HOURS 이내 복구 시 이벤트 유실 0건 | 단절 시험 ≥ 5회, 게이트웨이 로그 ↔ 서버 대조 |
| SC-010 | 교차 테넌트 접근 자동 테스트 전 케이스 차단(성공 0건) | 06 계약 테스트 |
| SC-011 | 감사로그 해시 체인 검증 통과, 이벤트·감사로그 삭제 API 부재 | 검증 명령 + 라우트 목록 |
| SC-012 | 월 가용성 ≥ SERVICE_AVAILABILITY_SLO (API + 알림 발송 경로) | 07 SLI |
| SC-013 | UX-01: 직원 앱 알림→확인이 1탭이고, 설명 없이 신규 직원 5인 중 4인 이상이 첫 시도에 완료 (L-03·L-02) | 사용성 테스트 5인 |

## UI 방향 (frontend-design-taste dial + ux-principles-kr)
| 화면 | 프로파일 | DENSITY / MOTION / VARIANCE | 핵심 규칙 |
|---|---|---|---|
| 관제 화면(웹, 간호실 PC 상시) | 관제/대시보드 | 8 / 2 / 3 | Cockpit 모드: 1px 구분선·박스 최소, 숫자 `font-mono`, 상태색 의미쌍(정상/경고/위험/정보), 빈·로딩·에러·**stale** 상태 필수, 강조는 미확인 알림 1곳(L-09) |
| 직원 앱 알림 화면 | 제품/앱 UI (단일 행동) | 4 / 3 / 2 | 화면당 결정 1개 "확인했어요"(L-03·T-08), 타깃 ≥ 44pt(L-02), 피드백 ≤ 400ms(L-10), 해요체(T-09), 진입 팝업 금지(T-04) |
| 직원 앱 조치 기록 | 제품/앱 UI | 5 / 3 / 3 | 필수 4항목만, 자동 채움(시각·구역·확인자, L-07), 에러 시 복구 경로 100%(L-06) |
| 보호자 앱 | 제품/앱 UI | 3 / 3 / 3 | 통보 1건 = 카드 1장(무슨 일·언제·누가 확인·조치·연락처), 능동형 문구(T-10) |
UX-01(L-03·L-02) 알림→확인 1탭 · UX-02(L-10) 액션 피드백 ≤ 400ms · UX-03(L-06) 막다른 에러 0 — 05·07에서 ID로 인용.

## 화면 스케치 — 직원 앱 알림 화면 (핵심 1장)
```
┌──────────────────────────────┐
│ ● 낙상 감지            14:32:07│  ← 상태색: 위험. 시각 font-mono
│                              │
│  302호 (침실)                 │  ← 구역명 크게
│  감지 후 00:12 경과           │  ← 초 단위 갱신
│                              │
│  [        확인했어요        ] │  ← 유일한 CTA, ≥ 44pt, 한 손
│                              │
│  다른 직원 2명에게도 갔어요    │  ← 정보. 강조 아님
└──────────────────────────────┘
상태: 로딩(스켈레톤) · 이미 확인됨("김○○님이 확인했어요" + [조치 기록하기]) · 오프라인(확인은 연결 후 가능해요 — 관제에 알렸어요) · 에러(다시 시도)
```

## 용어집 (08 용어 드리프트 검사 기준)
FallEvent(낙상이벤트) · detect(감지) · ack(확인) · resolve(조치 기록) · notify(통보, 보호자 대상) · alert(알림, 직원 대상) · escalate(에스컬레이션) · resident(입소자) · guardian(보호자) · staff(직원) · facility(시설) · zone(구역: 침실·복도·거실 등) · device(센서 또는 게이트웨이) · consent(동의) · label(actual_fall / false_alarm)

## 범위 밖 (non-goals)
- 낙상 위험 예측·활력징후(호흡·심박) — 의료기기 경계 (Q2, DL-6)
- 요양병원·재가요양 — 규제 체계 상이 (DL-4)
- 법정 CCTV 대체·영상 저장·실시간 영상 — 비영상 레이더, 원시 데이터 미전송 (INV-8)
- 자체 낙상 판정 모델 학습 — 센서 내장 판정 사용 (DL-11)
- 너스콜·EMR·공단 시스템 연동, 119 자동 신고 — 접점(웹훅)만 남기고 MVP 제외
- 결제·구독 모듈 — 계약 기반 수기 청구
- 조치 사진·영상 업로드
- 완전 폐쇄망 배포 (Q4 B)

## 가정 목록
02-blindspot-register.md의 Assumed 전부(51항목) + Asked 5문항의 추천안 채택(Q1 레이더 · Q2 감지·알림만 · Q3 확인 후 통보 · Q4 GW+클라우드 · Q5 스마트폰 앱). 틀리면 비싼 순: Q1 > Q2 > Q4 > Q3 > Q5. 상세는 `02-blindspot-register.md` 반영 기록.

## 상수 표 (단일 출처 — 04~07은 이름으로만 참조)
| 이름 | 값 | 단위 | 근거 |
|---|---|---|---|
| ALERT_LATENCY_P95 | 15 | 초 | 설계 결정 DL-12 |
| ALERT_LATENCY_MAX | 60 | 초 | 설계 결정 DL-12 |
| FALL_SENSITIVITY_MIN | 0.95 | 비율 | 설계 결정 DL-13 |
| FALSE_ALARM_MAX_PER_SENSOR_WEEK | 1 | 건/센서/주 | 설계 결정 DL-13 |
| STAFF_ACK_TIMEOUT_SEC | 60 | 초 | 설계 결정 DL-14 |
| ESCALATION_TIMEOUT_SEC | 180 | 초 | 설계 결정 DL-14 |
| ESCALATION_CALL_RETRY | 2 | 회 | 설계 결정 DL-14 |
| FLOOR_TIME_TARGET_MIN | 5 | 분 | 설계 결정 DL-14 |
| RESOLVE_REMINDER_MIN | 30 | 분 | 설계 결정 DL-14 |
| GUARDIAN_NOTIFY_DELIVERY_P95 | 60 | 초 | 설계 결정 DL-15 |
| DEDUP_WINDOW_SEC | 120 | 초 | 설계 결정 DL-15 |
| SENSOR_HEARTBEAT_SEC | 60 | 초 | 설계 결정 DL-15 |
| SENSOR_OFFLINE_AFTER_SEC | 300 | 초 | 설계 결정 DL-15 |
| GW_OFFLINE_AFTER_SEC | 120 | 초 | 설계 결정 DL-15 |
| LATE_EVENT_SEC | 60 | 초 | 설계 결정 DL-15 |
| EDGE_BUFFER_HOURS | 24 | 시간 | 설계 결정 DL-15 |
| CONSOLE_REFRESH_SEC | 2 | 초 | 설계 결정 DL-15 |
| STALE_AFTER_SEC | 10 | 초 | 설계 결정 DL-15 |
| EVENT_RETENTION_YEARS | 3 | 년 | 출처 URL https://ggscw.or.kr/s5_4/74 (열람대장 3년 준용, 확인 2026-09-08) |
| AUDIT_RETENTION_YEARS | 3 | 년 | 동일 출처 |
| HEARTBEAT_RAW_RETENTION_DAYS | 30 | 일 | 설계 결정 DL-15 |
| CONSENT_WITHDRAW_PURGE_DAYS | 30 | 일 | 설계 결정 DL-15 |
| STAFF_SESSION_HOURS | 12 | 시간 | 설계 결정 DL-15 |
| GUARDIAN_SESSION_DAYS | 30 | 일 | 설계 결정 DL-15 |
| SENSOR_COVERAGE_M2 | 20 | ㎡/대 | 출처 URL https://www.milesight.com/iot/product/lorawan-sensor/vs373 (4m×5m, 확인 2026-09-08) |
| PILOT_MAX_BEDS | 100 | 침상 | 설계 결정 DL-1 |
| PILOT_DAYS | 30 | 일 | 설계 결정 DL-12 |
| SERVICE_AVAILABILITY_SLO | 99.5 | %/월 | 설계 결정 DL-12 |
| RPO_HOURS | 24 | 시간 | 설계 결정 DL-15 |
| RTO_HOURS | 4 | 시간 | 설계 결정 DL-15 |
| ALIMTALK_UNIT_COST | — | 원/건 | 미확인 (NHN Cloud 요금 페이지 미열람, 01-recon) |
| SENSOR_UNIT_COST | — | 원/대 | 미확인 (B2B 견적 필요, 01-recon) |
근거 '미확인' 상수(ALIMTALK_UNIT_COST·SENSOR_UNIT_COST)는 성공 기준에 쓰지 않는다.

## 미결정
0건 — 질문 5문항은 전부 추천안으로 가정 채택(02 반영 기록). 착수 전 확인이 필요한 외부 사실(식약처 의료기기 해당 여부 서면 문의, iOS Critical Alert 승인)은 08 착수 조건으로 넘긴다.
