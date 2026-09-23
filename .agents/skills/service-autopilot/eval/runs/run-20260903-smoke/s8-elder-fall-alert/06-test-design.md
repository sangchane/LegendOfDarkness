# 테스트 설계 — FallGuard-Care (요양시설 낙상 감지·알림)

> 근거 (A6 진입 사전조사, 검색 0회 — 01-recon 재사용)
> - **정량** — 실환경 2년 실증(아파트 10곳, 레이더·웹캠·깊이카메라)에서도 "오경보 없는 견고한 낙상 감지는 여전히 큰 도전" ([Skubic 보고서](https://c2ship.missouri.edu/wp-content/uploads/2024/01/Skubic-Fall-detection-Final.pdf), 2026-09-03) → 감지 정확도는 **실측 프로토콜**(연기 낙상 50회 + 일상 동작 200회)로만 판정하고 단위 테스트로 대체하지 않는다
> - **정성** — 요양 현장은 "야간 1인이 수십 명 담당" ([유스연합](https://www.youthassembly.kr/news/909740)) → 오탐이 잦으면 알림을 끈다는 가정을 테스트 대상으로 승격: 재알림 상한·병합창이 실제로 알림 수를 제한하는지 검증
> - **사용자 영향** — 직원은 테스트 결과를 보지 않는다. 테스트가 지키는 것은 "울려야 할 때 울리고, 조용해야 할 때 조용한" 두 가지뿐 (L-06 막다른 에러 0, FR-016 4상태)

스킬 적용 기록: `ecc:tdd-workflow` — "테스트가 코드보다 먼저·RED 확인 후 구현·AAA 구조·독립 테스트" 채택, **일률 80% 커버리지는 채택하지 않음**(리스크 기반 목표가 우선, skill-routing 충돌 규칙). `ecc:e2e-testing` — POM·자동 대기 로케이터·`waitForResponse`·격리(quarantine)·재현(`--repeat-each=10`)·아티팩트(trace/video) 채택. 직원 앱은 네이티브 Android라 Playwright 대상이 아님 → **Maestro**(모바일 UI 플로우)로 같은 원칙 적용 (decision-log #D-023).

## 원칙

- 수용 기준(SC·FR·INV)은 개발 시작 전에 시나리오로 존재한다. 각 시나리오는 처음 실행 시 **반드시 실패**해야 한다(RED 게이트). 실패 이유가 "미구현"이 아니라 설정 오류면 RED로 인정하지 않는다.
- 피라미드: unit(상태기계·병합·타이머·해시체인) 다수 → integration(게이트웨이↔앱 LAN, 게이트웨이↔클라우드 MQTT, API↔DB RLS) → contract(05 엔드포인트) → E2E 최소(3 여정). 비율 목표 ≈ 65/20/10/5.
- "가능한 한 아래층으로": 상태 전이 규칙은 unit에서만, E2E는 전이 규칙을 다시 검증하지 않고 **사람이 보는 결과**(단말이 울림·알림톡 도달)만 본다.
- 시간은 주입한다(가짜 시계). 60초/180초/15분/5분 타이머는 실시간으로 기다리지 않는다.
- 외부 채널(FCM·알림톡·음성)은 integration 이하에서 목킹, E2E에서만 샌드박스 실계정.

## 수용 기준 → 시나리오 변환표

| SC/FR/INV | Gherkin 시나리오 | 레이어 | 데이터/목킹 |
|---|---|---|---|
| FR-001 / A-1 | **Given** 302호 센서 2대 **When** 20초 간격 낙상 신호 2건 **Then** FallEvent 1건, `signal_ids` 2개, 알림 1회 | unit(EventEngine) | 가짜 시계, DetectionSignal 픽스처 |
| FR-001 경계 | **Given** 같은 구역 **When** 31초 간격 신호 2건 **Then** FallEvent 2건 | unit | 병합창 30s 경계값 |
| FR-001 경계 | **Given** 다른 구역 2곳 **When** 동시 신호 **Then** FallEvent 2건, 각각 담당 직원 라우팅 | unit | roster 픽스처 |
| FR-002 / SC-001 | **Given** 302 담당 직원 2명 근무 **When** DETECTED **Then** WS `alert` 2건 + FCM 2건, 단말 표시 지연 p95 ≤ 10초 (100회 반복) | integration(GW↔앱 에뮬레이터) | FCM 목 서버, 로컬 WS |
| FR-002 중복제거 | **Given** 같은 event_id가 WS·FCM으로 도착 **When** 앱 수신 **Then** 알림 UI 1회, 소리 1회 | unit(앱) | Maestro + 로그 단언 |
| FR-002 / NFR-001 Doze | **Given** 공용 단말 24시간 유휴(화면 꺼짐·충전 거치, 일부는 비충전) **When** DETECTED **Then** 표시 ≤ 10초, 포그라운드 서비스 생존, WS 재연결 로그 상한 30초 | HIL(단말 실기) | 실기 단말 3종 |
| INV-04 오프라인 자격증명 | **Given** 클라우드 단절 13시간, 클라우드 토큰 만료 **When** 앱이 LAN WS 접속·ack **Then** 기기 자격증명으로 인증 성공, ack 반영 / **Given** 폐기 목록에 있는 단말 **Then** 401 | integration(GW) | 가짜 시계 |
| FR-002 클라우드 경유 ACK | **Given** LAN 불가·인터넷 가능 **When** E-12 **Then** `transition-proposal` → 게이트웨이 전이 → 200 / **Given** 게이트웨이 오프라인 **Then** 202, 앱 LAN 재시도 큐 | integration(cloud↔GW) | Mosquitto 테스트 브로커 |
| FR-003 / B-1 / INV-02 | **Given** DETECTED **When** A·B가 1초 차 ack **Then** 상태 ACKNOWLEDGED, acked_by=A, B 응답 200 `already-acked-by=A` | unit + integration | 동시 요청 |
| FR-003 경계 | **Given** 이미 CONFIRMED **When** ack **Then** 409 `invalid-transition` | contract | — |
| FR-004 / SC-007 / A-2 | **Given** 근무 직원 1명, 미확인 **When** t=60s **Then** 전 직원 재알림, 30초마다 ×3 / **When** t=180s **Then** `ESCALATED_PHONE` 전이 + 음성 API 호출 1회(간호사·시설장) | unit(타이머) + integration | 가짜 시계, 음성 목 |
| FR-004 근무 공백 | **Given** 근무 직원 0명 **When** DETECTED **Then** 즉시 `ESCALATED_PHONE`, 관리자 `roster-gap` 경고 | unit | 빈 roster |
| FR-004 상한 | **Given** 미확인 **When** 재알림 4번째 시점 **Then** 재알림 없음(최대 3회) | unit | 알림 피로 가정 검증 |
| FR-005 / B-2 | **Given** ACKNOWLEDGED **When** 15분 무입력 **Then** 리마인더 1회, 상태 유지, 관리 웹 미결 목록 포함 | unit + integration | 가짜 시계 |
| FR-005 / INV-02 | **Given** DETECTED(ACK 전) **When** resolve **Then** 409 | contract | — |
| FR-006 / SC-005 / INV-01 | **Given** 보호자 2명 동의 **When** CONFIRMED **Then** 알림톡 2건 queued→sent, 영수증 저장, 10분 내 | integration(NotificationService) | 알림톡 목(Solapi 샌드박스 계약) |
| INV-01 부정 | **Given** FALSE_POSITIVE로 종결(낙상 통보 발송 이력 없음) **Then** 낙상·정정 통보 0건 / **Given** DETECTED·ACKNOWLEDGED **Then** 통보 0건 / **Given** CONFIRMED 통보 발송 전(큐 대기) 정정 **Then** 낙상·정정 둘 다 0건 | unit | 상태별 파라미터화 |
| FR-005 resident 필수 | **Given** 다인실(수급자 2명) **When** resolve CONFIRMED without resident_id **Then** 422 `resident-required` / **Given** 1인실 **Then** 자동 채움 후 200 / **Given** 지정 **Then** 해당 수급자 보호자에게만 통보 | unit + contract | — |
| FR-004 phone_targets | **Given** 근무 중 간호사 2명·시설관리자 1명 **When** 180초 미확인 **Then** 간호사 2명 동시 발신 → 30초 후 미ACK면 시설관리자 발신 → ACK 시 중단 / **Given** 간호사 `phone_enc` 없음 **Then** E-30 저장 시 422 | unit + contract | 음성 목 |
| FR-020 수동 등록 | **Given** 센서 미감지 낙상 발견 **When** E-18 **Then** FallEvent origin=manual, status=ACKNOWLEDGED, acked_by=요청자 → resolve CONFIRMED → 통보·PDF 동일 | integration | — |
| FR-021 / SC-001 displayed | **Given** 알림 표시 **When** 앱이 `displayed` 전송(LAN 실패 시 E-17) **Then** transitions.payload.displayed[]에 device_id·displayed_at·received_at 기록, E-41 p95_display_s 산출 | integration | — |
| INV-04 단절 + 미확인 | **Given** 클라우드 단절 **When** 180초 미확인 **Then** 전 단말 siren 모드, ESCALATED_PHONE은 outbox에 큐잉, 복구 시 전화 발신(이미 ACK면 발신 안 함) | integration | toxiproxy + 가짜 시계 |
| FR-004 roster 0 + 단절 | **Given** 근무 직원 0명·클라우드 단절 **When** DETECTED **Then** 즉시 siren 모드(등록 단말 전부) + 관리자 큐, 복구 시 전화 | integration | — |
| FR-006 / C-2 | **Given** 알림톡 5xx **When** 발송 **Then** 10초 간격 3회 재시도 → SMS 폴백 → 둘 다 실패 시 관리자 알림 `manual-contact-required` | integration | 실패 주입 목 |
| FR-006 / C-1 | **Given** 보호자 0명 **When** CONFIRMED **Then** 통보 생략, 관리 웹 "통보 불가", 감사 사유 기록 | integration | — |
| FR-006 / C-3 | **Given** 보호자 3명 중 1명 동의 철회 **When** CONFIRMED **Then** 2명만 발송, 철회자 감사 "동의 없음" | unit | — |
| FR-006 / C-4 | **Given** 같은 수급자 1시간 내 2번째 CONFIRMED **Then** 통보 2건(억제 없음) | unit | — |
| FR-006 / B-4 / INV-02 | **Given** CONFIRMED 3분 경과·낙상 통보 발송됨 **When** amend FALSE_POSITIVE **Then** 정정 알림톡 발송, 전이 2건 보존 / **Given** FALSE_POSITIVE 3분 경과 **When** amend CONFIRMED(resident 지정) **Then** 낙상 통보 발송 / **Given** 6분 경과 **Then** 409 `amend-window-closed` / **Given** 이미 1회 정정 **Then** 409 `amend-already-used` | unit + contract | 가짜 시계 |
| FR-007 / SC-006 / A-3 / INV-04 | **Given** 클라우드 MQTT 차단 **When** 30분간 감지 20건·ack 20건 **Then** LAN 알림 20/20, outbox 40건 / **When** 복구 **Then** 순서대로 재생, 클라우드 전이 40건, 유실 0, CONFIRMED 통보는 복구 후 발송 | integration(도커 네트워크 차단) | toxiproxy |
| FR-007 stale | **Given** 단절 15분 경과 **Then** 관리 웹 stale 배지 / 14분 **Then** 배지 없음 | integration + E2E(웹) | 가짜 시계 |
| FR-007 / B-3 | **Given** 앱 LAN·인터넷 모두 끊김 **When** ack 탭 **Then** 로컬 큐 "전송 대기", 재연결 시 같은 Idempotency-Key로 전송, **ack_at = 게이트웨이 RTC 수신 시각**, payload.device_at = 단말 탭 시각(보조) | integration(앱) | Maestro + 네트워크 토글 |
| FR-007 outbox 상한 / INV-03 | **Given** outbox 10만 건 **When** 신규 전이 **Then** 가장 오래된 동기화 완료 행 삭제, 미동기화 행 보존, **`transitions` 테이블 행 수 불변** | unit(LocalStore) | — |
| FR-006 재생 억제 | **Given** 단절 중 CONFIRMED 후 3분 뒤 amend(FALSE_POSITIVE) **When** 복구 재생 **Then** 낙상 알림톡 0건·정정 알림톡 0건, 전이 2건 미러, `outage_deferred=true` / **Given** 단절 중 CONFIRMED만 **Then** 재생 후 10분 내 통보 1건 | integration | toxiproxy |
| FR-009 감지 중단 | **Given** 게이트웨이 텔레메트리 180초 미수신 **Then** 근무 중 전 직원 FCM "낙상 감지 중단" + 관리자 알림 + 관리 웹 stale / **Given** 170초 **Then** 없음 | integration(cloud) | 가짜 시계 |
| FR-008 체인 분리 | **Given** 게이트웨이 전이 재생과 클라우드 관리자 행위가 동시 발생 **When** 각 체인 verify **Then** 둘 다 통과(체인 분기 없음) / **Given** 게이트웨이 seq 건너뜀 **Then** 감사 경고 | integration | — |
| FR-008 / SC-008 / INV-03 | **Given** 전이 10건 **When** verify **Then** 체인 통과 / **When** 중간 1건 payload 변조 **Then** `broken_at`=해당 seq | unit(AuditLog) + contract(E-61) | — |
| FR-008 append-only | **When** `UPDATE event_transitions` 시도 **Then** 권한 오류 | integration(DB) | 앱 롤로 접속 |
| FR-008 RTC | **Given** NTP 차단 24h **Then** 게이트웨이 시각 오차 ≤ 2초 | HIL(하드웨어) | RTC 모듈 실기 |
| FR-009 / A-4 | **Given** 센서 heartbeat 5분 미수신 **Then** 장비 알람(관리자 FCM channel=device), 직원 단말 무음 | unit + integration | 가짜 시계 |
| FR-010 | **Given** 근무표 22:00~06:00 A(301~305) **When** 03:00 DETECTED 302 **Then** A에게만 1차 알림 / **When** 07:00 **Then** 주간 근무자에게 | unit(라우팅) | shifts 픽스처 |
| FR-011 / SC-002 / SC-004 | **Given** 30일 이벤트 픽스처 **When** E-41 **Then** median_ack_s·p95_display_s·false_positive_rate가 손계산과 일치 | integration(API) | 시드 데이터 |
| FR-012 | **Given** CONFIRMED **When** E-16 **Then** PDF에 발생 시각·구역·수급자·확인자·조치·통보 시각 6항목 존재 / **Given** DETECTED **Then** 409 | contract | PDF 텍스트 추출 |
| FR-013 | **Given** 퇴소 처리 **When** purge_after 경과 배치 **Then** 수급자·보호자 파기, fall_events는 resident_id NULL 처리·보존 | integration(배치) | 가짜 시계 |
| FR-014 / INV-06 | **Given** 시설 A 토큰 **When** 시설 B 이벤트 GET **Then** 404 / **When** 요청 본문 role=admin **Then** 무시 | contract | RLS 세션 변수 |
| FR-014 부정 | **Given** 요양보호사 토큰 **When** PATCH facilities **Then** 403 | contract | 스코프 표 파라미터화 |
| FR-015 | **Given** 클레임 토큰 **When** E-50 **Then** 201 인증서, 재사용 시 409 / **Given** 서명 불일치 OTA **Then** 롤백, fw_version 유지 | integration + HIL | Mender 데모 서버 |
| FR-016 | **Given** 관리 웹 이벤트 목록 **When** 0건 / 로딩 / 500 / WS 끊김 **Then** 각각 빈·로딩·에러(다음 행동 버튼)·stale 표시 | E2E(웹, Playwright) | 목 API 4상태 |
| SC-003 / NFR-003 미탐 | 실측 프로토콜(아래) 연기 낙상 50회 **Then** 미탐 ≤ 2 | HIL(현장) | 실기 센서 |
| SC-004 / NFR-003 오탐 | 일상 동작 200회(앉기·눕기·물건 줍기·이불 털기·휠체어 이동) **Then** 오탐 ≤ 10회(5%) 실험실 / 파일럿 4주차 주간 ≤ 30% | HIL + 운영 지표 | — |
| SC-009 / UX-01·04 | 신규 직원 5인 × 2회 무설명 **Then** 완료 ≥ 9/10, 알림→확인 탭 1회 | 사용성 테스트 | 프로토타입 |
| SC-010 | 식약처 질의 접수번호·동의 서식 법률 검토서 존재 | 문서 검사 | — |
| INV-05 | **When** FallEvent·Transition·Notification 스키마 검사 **Then** 영상·음성·신체 필드 0 | contract(스키마) | JSON Schema |
| INV-07 | **When** 앱·웹·알림톡 템플릿 문자열 grep("진단","치료","위험도") **Then** 0건 | 정적 검사(CI) | 문자열 스캔 |
| NFR-006 | 주 CTA 높이 ≥ 64dp, 글자 ≥ 18sp | 정적 검사(레이아웃 린트) | — |
| NFR-007 | 전원 차단 100회 **Then** 부팅 100%, 24h 쓰기 ≤ 100MB | HIL | 전원 릴레이 자동화 |

누락 검사: SC-001~010 → 10/10, P0/P1 FR-001~016·020·021 → 18/18, INV-01~07 → 7/7, PRD 엣지케이스 A1~4·B1~4·C1~4 → 12/12, GATE 추가(단절+미확인 siren, roster 0+단절, resident 필수, phone_targets, displayed). **누락 0.**

## 실측 프로토콜 (SC-003·SC-004 — 단위 테스트로 대체 불가)

- 장소: 파일럿 시설 빈 침실 2·화장실 1·복도 1, 센서 설치 높이 표준(제조사 권장) ± 20cm 변형 포함.
- 연기 낙상 50회: 훈련된 성인 연기자(낙상 매트 위), 유형 = 전방/측방/후방 넘어짐, 침대에서 미끄러짐, 변기 옆 주저앉기, 느린 낙상(벽 잡고 미끄러짐) 각 ≥ 8회. 낙상 후 바닥 정지 ≥ 10초.
- 일상 동작 200회: 앉기/눕기/일어서기, 바닥 물건 줍기, 이불 정리, 휠체어 이동, 직원 2인 동시 이동, 커튼·문 개폐.
- 기록: 회차·유형·감지 여부·감지 지연(초)·오탐 여부를 표로. 결과는 **평가셋으로 동결**(P5 평가셋 가정) — 펌웨어·임계 변경 시 재실행.
- 판정: 미탐 ≤ 2/50, 실험실 오탐 ≤ 10/200. 실패 시 임계 조정 후 전체 재실행(부분 재실행 금지).

## 계약 테스트 (05 엔드포인트 표 기준)

- 스키마: 전 엔드포인트 요청/응답이 `openapi.yaml`과 일치(Prism/Dredd 또는 NestJS DTO 스냅샷).
- 에러: 4xx/5xx 전부 `application/problem+json` + `type`·`title`·`status`, 스택트레이스 문자열 0.
- 멱등성: E-12/13/14/21/50 — 같은 키+같은 본문 2회 → 동일 상태코드·본문, 같은 키+다른 본문 → 409, 73시간 후 같은 키 → 신규 처리.
- 버저닝: `Accept` 없는 요청 → 기본 v1, `version=2` → 406.
- 페이지네이션: `limit=201` → 400, `next_cursor` 왕복 시 중복·누락 0.
- 레이트리밋: 로그인 11회/min → 429 + `Retry-After`.
- 테넌시: 스코프 표 × 역할 4종 × 엔드포인트 전수 매트릭스(허용/403/404).
- MQTT: 타 시설 토픽 발행 → 브로커 거부, `seq` 중복 → 클라우드 무시(멱등), 스키마 위반 페이로드 → DLQ + 알람.

## E2E 후보 (돈·안전·법 — 3개만)

| # | 여정 | 도구 | 판정 |
|---|---|---|---|
| E2E-1 안전 | 실기 센서 낙상 주입 → 직원 단말이 울림 → "지금 갈게요" 탭 → 다른 단말 "확인됨" → "실제 낙상이에요" → 보호자 테스트 번호에 알림톡 도달 → 관리 웹 이력·PDF | HIL + Maestro(앱) + Playwright(웹) + 알림톡 샌드박스 | 전 구간 타임스탬프 로그 + 스크린샷 |
| E2E-2 안전(단절) | 클라우드 링크 차단 → 낙상 주입 → 단말 울림 → 확인·확정 → 링크 복구 → 알림톡 발송 → 이력 정합 | toxiproxy + Maestro | 유실 0, 순서 보존 |
| E2E-3 안전(에스컬레이션) | 낙상 주입 → 단말 무응답 방치 → 60초 전 직원 알림 → 180초 시설장 테스트 전화 수신(음성 API 샌드박스) | 실시간(가짜 시계 불가, 4분 소요) | 통화 로그 + 전이 기록 |

flaky 대책: 자동 대기 로케이터, `waitForResponse`, 임의 `sleep` 금지, 실패 시 trace·video 보관, 10회 반복 안정성 확인 후 CI 편입, 불안정 테스트는 `fixme` 격리 + 이슈 번호.

## 리스크 기반 커버리지 목표

| 영역 | 목표 | 근거 |
|---|---|---|
| EventEngine(상태기계·병합·타이머·라우팅) | 분기 **100%** | INV-01·02·04, P0 경로 전부 |
| AuditLog 해시체인·LocalStore outbox | 분기 **100%** | INV-03, G3 법적 증거 |
| NotificationService(폴백·통보 조건) | 분기 **95%** | INV-01, 위협모델 TB4 |
| API 인가·RLS | 매트릭스 전수 | INV-06, 위협모델 E |
| 관리 웹 UI | 주요 화면 4상태 + 리포트, 라인 60% | P1, 안전 경로 아님 |
| 직원 앱 | 알림 수신·중복제거·오프라인 큐 분기 90%, 나머지 60% | P0 경로 |
| 게이트웨이 OS 형상(rootfs·watchdog·RTC) | HIL 체크리스트 100% 통과 | NFR-007, P1 프로파일 |
| 리포트 PDF·CSV | 스냅샷 1종 | P1, 낮은 리스크 |
