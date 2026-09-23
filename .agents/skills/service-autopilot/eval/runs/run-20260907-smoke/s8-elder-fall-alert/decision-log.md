# Decision Log — 요양시설 낙상 감지·알림 서비스 (s8-elder-fall-alert)
버전: v1.0
런: run-20260907-smoke / 조건 B (service-autopilot 스킬 사용) / 오토파일럿 모드(질문 금지, 추천안 자동 채택)
시작: 2026-09-07

## 결정 (번호 순)

| # | 단계 | 결정 | 대안·왜 버렸나 | 근거 |
|---|---|---|---|---|
| 1 | A0 | 강도 **full** 판정 | lite — 안전·민감정보·엣지·외부연동 4신호 중 하나라도 있으면 full이므로 불가 | SKILL.md 강도 신호 표 |
| 2 | A0 | 프로파일 P1+P2+P4 합집합 + P3 일부(알람 폭주·실시간성) + 임시 P7(돌봄 안전 알림) | 단일 프로파일 — 감지 디바이스·다시설 SaaS·알림 앱이 모두 있어 합집합 필요 | blindspot-checklists "복합 서비스는 합집합" |
| 3 | A0 | 그린필드 가정 (기존 시스템 감사 절 생략) | — 입력에 기존 시스템 언급 없음 | stage-templates A0 |

## 스킬·서브에이전트 사용 기록

(단계별로 아래에 추가. 형식: `[단계] 스킬명 (모델) — 무엇이 달라졌나`)
- `[A1] ecc:research-ops (메인 세션 fable) — 사실/추론/추천 구분 라벨링·확인일 강제를 조사 지시서에 반영`
- `[A1] ecc:market-research (메인 세션 fable) — 유사 솔루션 비교를 "마케팅 문구 아닌 실제 기능·가격 단서·역증거" 기준으로 지시`
- `[A1] general-purpose 조사 서브에이전트 (sonnet, 검색 12~18회 예산) — 01-recon 초안 작성. ecc:search-first는 단계당 2개 상한으로 미호출(스택 후보의 "사지 말고 만들지 말라" 판단은 evidence-map fit_score로 메인이 수행)`
- `[A1 결과] sonnet 조사 서브에이전트 — 검색 26회(예산 12~18 초과, 5영역 출처 확보 위해 확장), 132,689 tok, 521초. 메인이 fit 판정 절 추가·CCTV 근거법 정정 반영 후 01 저장`
- `[A2] ecc:product-lens (메인 세션 fable) — 7문항 진단을 register 상단에 두고 Q1~Q5 Impact 판단의 근거로 사용 (질문 형식·1회 배치는 이 스킬 규칙 우선)`

| # | 단계 | 결정 | 대안·왜 버렸나 | 근거 |
|---|---|---|---|---|
| 4 | A2 Q1 | 감지 방식 = mmWave 레이더 천장형(비영상) — Assumed(무응답) | 카메라 AI(침실 영상 동의·25조), 웨어러블(착용 거부), 압력센서(이탈만) | 01 LG U+ 98%, 개인정보보호법 25조 |
| 5 | A2 Q2 | 보호자 통지 = 직원 낙상 확정 시에만, 오탐 미통지 — Assumed(무응답) | 감지 즉시 통지(오탐 노출), 일일 요약(P1) | P7 알람 피로 |
| 6 | A2 Q3 | 시설 B2B 침상당 월정액, 보호자 무료, MVP 수동 청구 — Assumed(무응답) | 보호자 유료(PG 복잡), 정부 납품(예산 미확인) | 01 Nobi 시설 월정액 |
| 7 | A2 Q4 | 비의료기기 포지셔닝 + 출시 전 법률 검토 1회 — Assumed(무응답) | 의료기기 인증(비용·기간) | 01 식약처 판정 미확인 |
| 8 | A2 Q5 | 클라우드 멀티테넌트 SaaS, 디바이스 아웃바운드 — Assumed(무응답) | 온프레미스(출동), 하이브리드(P1 후보) | 01 유사 솔루션 전부 클라우드 |
| 9 | A1 fit | 스택 = Mosquitto · NestJS · PostgreSQL 16+ · FCM v1 · SOLAPI · Expo · React 콘솔 | EMQX(BSL), TimescaleDB(볼륨 불필요), Spring/FastAPI(팀 언어) | 01 fit 표 |
| 10 | A2 | 질문 5개 전부 자동 채택(오토파일럿). 사용자 답이 오면 Q1·Q5는 04 재작업 범위 | — | 운영 제약(질문 금지) |
- `[A3] ecc:product-capability (메인 세션 fable) — 요구사항을 EARS 문장으로, 상태 전이(detected→notified→acked→resolved→family_notified)와 불변식(멱등 수신·append-only 감사)을 FR로 승격`
- `[A3] frontend-design-taste (메인 세션 fable) — 직원 앱 dial 4/2/2, 콘솔 8/2/3 고정, 화면 스케치에 빈/로딩/에러/stale 4상태 강제`
- `[A3 사전조사] WebSearch 2회 — long-lie 사망률(정량), 요양보호사 야간 인터뷰(정성)`

| # | 단계 | 결정 | 대안·왜 버렸나 | 근거 |
|---|---|---|---|---|
| 11 | A3 | 상수 25개를 03 상수 표에 단일화(ALERT_LATENCY_P95=10s, ACK_TIMEOUT=3m, ESCALATION_LEVELS=3, DEDUP_WINDOW=120s, EVIDENCE_RETENTION=3y 등) | 문서마다 값 반복(S4 미전파 사고) | SKILL 규칙 9 |
| 12 | A3 | 보호자 채널 = 알림톡→SMS 폴백 + 서명 링크, 앱 없음 | 보호자 앱(설치·심사 부담, P4 프로파일 항목 증가) | 01 fit |
| 13 | A3 | 미탐 목표를 0이 아닌 DETECTION_RECALL_MIN=90%로 | 0건(SLO 100% 금지 원칙 위반, 측정 불가) | Google SRE |
| 14 | A3 | 근무조 미배정 시 시설 전체 staff 발송 + 관리자 경고 (EC-A2) | 발송 보류(안전 위험) | 안전 우선 |
- `[A4] ecc:architecture-decision-records (메인 세션 fable) — "검토한 대안" D1~D8을 Nygard 형식(대안·트레이드오프·왜 아닌가)으로 기록, 별도 docs/adr 대신 이 로그에 흡수`
- `[A4] ecc:security-review (메인 세션 fable) — 시크릿·입력 검증·인가·레이트리밋·로그 마스킹 체크리스트를 STRIDE 대책 TM-07~TM-27에 반영`
- `[A4 사전조사] WebSearch 2회 — Mosquitto 인증서 CN ACL 패턴, NestJS MQTT EventPattern 함정`

| # | 단계 | 결정 | 대안·왜 버렸나 | 근거 |
|---|---|---|---|---|
| 15 | A4 | 모듈형 모놀리스(NestJS) + PostgreSQL 단일 저장소 + pg-boss 지연 잡 | 마이크로서비스(D5), Redis/BullMQ(D2) — 파일럿 규모에 운영 대상만 증가 | 04 검토한 대안 |
| 16 | A4 | 디바이스 인증 = 기기별 X.509 + Mosquitto CN ACL 패턴 (TM-01·06 Eliminate) | 공유 비밀번호(1대 탈취 = 전체 위장) | mosquitto.conf(5) |
| 17 | A4 | 콘솔 실시간 = SSE + 폴링 폴백 | WebSocket(양방향 불필요) | D4 |
| 18 | A4 | 감사 로그 append-only를 DB 권한+트리거로 강제 (TM-21 Eliminate) | 앱 코드 규약만(우회 가능) | SC-011 |
| 19 | A4 | 인터넷 두절 리스크 R1은 파일럿에서 Accept + LTE 권고, 하이브리드 D1은 P1 | MVP부터 하이브리드(두 경로 개발) | Q5 |
- `[A5] ecc:api-design (메인 세션 fable) — 상태코드 매트릭스(201+Location, 409 상태 충돌, 429+Retry-After)·레이트리밋 계층 채택. URL 버저닝 권고는 stage-templates(Zalando #115)와 충돌해 미채택`
- `[A5] ecc:postgres-patterns (메인 세션 fable) — 부분 유니크 인덱스(활성 이벤트 1/입소자, 현재 판정 1/이벤트, 침상 점유 1), 복합 인덱스 순서(등호→범위), timestamptz·text 타입 규칙 반영`
- `[A5 사전조사] WebSearch 1회 — Idempotency-Key IETF 초안 -07 상태`
- `[A4 검토] ecc:architect 서브에이전트 (fable, fresh) — 03·04 독립 검토 실행 중, 결과는 A5 완료 후 반영`

| # | 단계 | 결정 | 대안·왜 버렸나 | 근거 |
|---|---|---|---|---|
| 20 | A5 | 버저닝 = 미디어타입 파라미터(`Accept: …; version=1`), URL `/v1` 회피 | ecc:api-design의 URL 버저닝 — 템플릿(Zalando #115) 우선 규칙 | skill-routing 규칙 2 |
| 21 | A5 | 타 시설 자원 접근은 404가 아닌 403 통일 | 404(열거 방지) — 시설 내부 사용자라 오류 명확성 우선 | 규약 |
| 22 | A5 | 외부 ID uuid, audit_log만 bigint identity | 전부 bigint(열거 노출) / 전부 uuid(감사 로그 순서·용량) | postgres-patterns 타입 표 + 볼륨 |
| 23 | A5 | DEDUP_WINDOW 병합을 앱 로직 + 부분 유니크 인덱스(활성 이벤트 1/입소자)로 이중 보증 | 앱 로직만(경합 시 중복 이벤트) | TM-26 동형 |
| 24 | A5 | 보호자 링크 토큰은 해시만 저장(`token_hash`), 원문은 메시지에만 | 원문 저장(DB 유출 시 링크 재사용) | TM-13 |
- `[A6] ecc:tdd-workflow (메인 세션 fable) — RED 게이트·AAA·독립 테스트 원칙 채택. 일률 80% 커버리지는 리스크 기반 표로 대체(충돌 우선순위)`
- `[A6] ecc:e2e-testing (메인 세션 fable) — POM·data-testid·waitForResponse·retries/trace 설정을 E2E-3와 flaky 전략에 반영`
- `[A6 사전조사] WebSearch 1회 — pg-boss 지연 잡 테스트 함정(가짜 시계 무효, singleton 중복)`

| # | 단계 | 결정 | 대안·왜 버렸나 | 근거 |
|---|---|---|---|---|
| 25 | A6 | 시각 의존 로직은 앱 계층 Clock 포트 주입(단위) + 축소 상수(통합), sleep 금지 | pg-boss에 가짜 시계 — DB 시각을 써서 무효(이슈 #210) | 06 근거 |
| 26 | A6 | 파일럿 실데이터로만 판정 가능한 SC(001·004·005·009·012)는 "운영 검증" 레이어로 분리하고 07 대시보드 쿼리·리허설에 연결 | 자동 테스트로 위장 | 측정 가능성 |
| 27 | A6 | 동시성 시나리오(TS-026 dedup 경합, TS-031 동시 ack)를 P0 통합 테스트로 | 단위 테스트만(DB 제약 미검증) | TM-26, EC-B1 |
- `[A4 검토 결과] ecc:architect (fable, fresh, 03+04만 입력) — 판정 FAIL, HIGH 4·MEDIUM 6·LOW 3 + 불일치 10. 50,734 tok, 264초. 타당성 필터: 거짓 양성 0건 → 03 v1.1·04 v1.1·05 v1.1·06 v1.1로 전파 (규칙 9). A4 재실행 1회로 계산(실행 절차 4), 재검토 없음`

| # | 단계 | 결정 | 대안·왜 버렸나 | 근거 |
|---|---|---|---|---|
| 28 | A4 v1.1 | MQTT 수신 = mqtt.js 직접 구독 + 수동 ack(DB 커밋 후), QoS1·영속 세션·브로커 persistence | NestJS `@EventPattern` 트랜스포트(자동 ack → 커밋 전 크래시 소실) | A4 검토 H2, D9 |
| 29 | A4 v1.1 | 원시 감지 `fall_detections`(항상 저장) / 병합 `fall_events`(디바이스당 활성 1개) 분리, dedup 키 = 디바이스 | 입소자 키(생활실 디바이스 판별 불가), 병합 시 행 없음(상태 미정의) | H3·M7·M8, D10 |
| 30 | A4 v1.1 | 첫 에스컬레이션 잡을 이벤트 INSERT와 같은 트랜잭션에 예약, 실행은 CAS(`status`·`escalation_level`), ack는 잡 취소에 의존 안 함, J-02 스테일 안전망 | FCM 후 예약 + cancel 의존 (크래시 시 감시자 0, active 잡 취소 불가) | H1·M6 |
| 31 | A4 v1.1 | 푸시 전달 확인 = 앱 수신 보고 E-31 + J-07(`CHANNEL_FALLBACK_DELAY`) → SMS 폴백. FR-026 신설 | 발송사 message_id를 전달로 간주 | H4 |
| 32 | A4 v1.1 | RLS(FORCE)를 MVP부터, 컨텍스트 SET LOCAL, SSE 시설별 채널 — D8 역전 | 리포지토리 필터만(워커·SSE·raw SQL 누출 경로) | M9 |
| 33 | A4 v1.1 | R4 자체 SPOF를 파일럿 Accept로 명시, SC-009를 MQTT→DB 카나리 왕복으로 개정 | HTTP 헬스체크만(안전 경로를 재지 않음) | M5 |
| 34 | A4 v1.1 | 보호자 링크 토큰 = CSPRNG 256-bit, 해시 저장, 행 바인딩(폐기 가능); HMAC 파생 폐기 | HMAC(event‖guardian‖exp) — 폐기 불가·공식 불일치 | L12 |
| 35 | A4 v1.1 | `family_notified` 이후 판정 변경 거부(409), 정정 통지 기능은 non-goal | 정정 통지 발송(FR 추가·문구·법적 검토 확대) | L13, 단순함 우선 |
- `[A7] ecc:deployment-patterns (메인 세션 fable) — 파이프라인 단계(lint→typecheck→unit→integration→build→staging→smoke→prod), 헬스체크 3종, 롤백 체크리스트, 프로덕션 준비도 체크리스트를 07에 반영`
- `[A7] ecc:docker-patterns (메인 세션 fable) — compose 스케치(볼륨·네트워크 분리·no-new-privileges·read_only·env_file), 멀티스테이지 Dockerfile, .dockerignore 반영. ecc:dashboard-builder는 단계당 2개 상한으로 미호출(운영자 질문 4개는 SRE 골든 시그널로 대체)`
- `[A7 사전조사] WebSearch 1회 — Mosquitto persistence/autosave_interval·공유 구독 2.0.19 수정·docker 볼륨`

| # | 단계 | 결정 | 대안·왜 버렸나 | 근거 |
|---|---|---|---|---|
| 36 | A7 | 파일럿 = VM 1대 + Docker Compose(edge/api/worker/mosquitto/postgres), api·worker를 같은 이미지·다른 ROLE로 분리 | k8s(2~3인 팀 과함), 단일 프로세스(워커 재시작이 API를 끊음) | ecc:deployment-patterns, R4 |
| 37 | A7 | 스모크 = SC-009 카나리 스크립트 재사용(M-01 발행→fall_detections 왕복), 관리형 PostgreSQL 권고 | HTTP 헬스체크만(안전 경로 미측정) | A4 검토 M5 |
| 38 | A7 | 알람 9개 전부 증상 기반 + 런북 1:1, SLO 위반은 주간 리뷰 | 원인 지표 알람(알람 피로) | Google SRE |
| 39 | A7 | 백업 = pg_dump 일 1회 + WAL 연속 + mq_data + 인증서 볼륨, 국내 오브젝트 스토리지 암호화, 리허설 BACKUP_RESTORE_DRILL | DB만 백업(브로커 큐·인증서 소실 시 디바이스 재발급 출동) | P1·P2 프로파일 |
| 40 | A7 | 첫 작업 1 = 벤더 페이로드 계약 스파이크(완료 조건: 실제 샘플 1건) | UI부터(불확실성 최대 항목을 뒤로 미룸) | Impact×Uncertainty, R2 |

## 재개 기록
- 2026-09-07 23:28 — 세션 한도(429)로 중단 후 재개. 00~07·decision-log 존재 확인, GATE 1차 검토 결과는 중단 직전에 이미 수신(아래 기록). 실행 절차 5에 따라 없는 첫 파일(08)부터: 패치 1회 → check_package → 08 → s8-b.md. 웹 검색은 재개 후 사용 불가(한도) — Node 버전은 가정 A-9로 처리.

- `[GATE] general-purpose 적대적 검토관 (fable, fresh context, 00~07 + decision-log + check_package 출력 입력) — 판정 FAIL: CRITICAL 2·HIGH 5·MEDIUM 9·LOW 3. 171,369 tok, 443초, 도구 12회. 타당성 필터: 거짓 양성 0건. 예산 규칙(평가 런)에 따라 패치 1회, 재검토 없음 → 08은 CONCERNS로 마감`

| # | 단계 | 결정 | 대안·왜 버렸나 | 근거 |
|---|---|---|---|---|
| 41 | GATE 패치 | 생활실 매핑 이벤트는 E-13 `resident_id`로 직원이 입소자를 지정, 미지정 시 통지 보류 + 배너 (FR-009·010, EC-C4) | 다인실 디바이스 금지(설치 비용↑), 전 입소자 통지(오통지) | GATE C1 |
| 42 | GATE 패치 | 05·07의 운영 임계 19개를 03 상수 표로 승격, VM·Node 버전은 가정 A-8·A-9 | 07에 값 상주(규칙 9 위반) | GATE C2 |
| 43 | GATE 패치 | RLS 부트스트랩 = BYPASSRLS 롤 소유 SECURITY DEFINER 함수 4개(단일 행), 타 시설 단건 404·목록 0행 — #21(403 통일) superseded | 부트스트랩 테이블만 RLS 제외(users 전체 노출) | GATE H3 |
| 44 | GATE 패치 | 라우팅을 수신 트랜잭션 안으로, J-01 level = 초기 level+1, `next_escalation_at` 저장 | 커밋 후 라우팅(미배정 시 level 2 영구 미도달) | GATE H4·M10 |
| 45 | GATE 패치 | `notifications.event_id` nullable + `kind` 11종, 관리자 알림 = facility_admin 전원 SMS + 콘솔 배너(용어집) | 관리자 알림 별도 테이블(원장 분산) | GATE H5 |
| 46 | GATE 패치 | FR-028 시설 전체 오프라인(집계 1건, 디바이스별 억제) 신설 | 디바이스별 알림만(SMS 폭주) | GATE H6 |
| 47 | GATE 패치 | FR-029 시설 책임자 = facility_admin 사용자 + 1회용 ack 링크 E-32 (`ACK_LINK_TTL`) | 책임자는 SMS만(ack 불가 → 사이렌 지속·AL-03 오발) | GATE H7 |
| 48 | GATE 패치 | 파일럿 관측성 = 외부 업타임 모니터 + J-09 `ops-alarm` + SQL 뷰; Prometheus/Grafana/Loki는 다시설 단계 | 4GB VM에 모니터링 스택 동거(R4 SPOF 확대) | GATE M14 |
| 49 | GATE 패치 | 카나리 = `devices.is_canary` — detections만 저장, 이벤트·알림·집계 제외 | 시스템 사용자로 자동 false_alarm(E-13 409·created_by 문제) | GATE M15 |
| 50 | GATE 패치 | 미매핑 디바이스 감지 = detections 저장 + 관리자 알림 + ack, 이벤트 없음 (EC-A5) | room_id NOT NULL INSERT 실패 → poison 재전달 | GATE M16 |
| 51 | GATE 패치 | 용어 통일: "서명 토큰/링크" → "열람 토큰/링크"(03~07), #23(입소자 키 부분 유니크)은 #29로 superseded | — | GATE M9 |
| 52 | GATE 패치 | 이벤트·알림·감사 기록 보존 = EVIDENCE_RETENTION 후 파기 대상, 구현 P2 (04·05 통일) | 04 파기 / 05 영구 보존 상충 | GATE L18 |
| 53 | GATE | 재검토 없음(예산) → 08 판정은 CONCERNS: 패치의 독립 검증 부재가 사유. 상수 총계는 #11의 25개 → v1.2 47개(RPO·RTO 포함) | 재검토 1회(예산 초과) | 운영 제약 |

## 비용 기록
- 강도: **full** (안전·민감정보·엣지·외부연동 4신호)
- 검색: 서브에이전트 26회(A1) + 메인 7회 완료(A3 2·A4 2·A5 1·A6 1·A7 1) + 1회 실패(Node LTS, 세션 한도) = 34회
- 서브에이전트 3: A1 조사(sonnet) 132,689 tok · A4 독립 검토(ecc:architect, fable) 50,734 tok · GATE(fresh, fable) 171,369 tok = 354,792 tok
- 메인 세션(fable): 약 410,000 tok (세션 카운터 15,000,000 → 14,593,848 기준, 스킬 본문 13회 로드 포함). 합계 약 765,000 tok — full 상한 80만 이내
- 스킬 호출 13회(A1 2·A2 1·A3 2·A4 2·A5 2·A6 2·A7 2), 단계당 ≤2 준수
- 소요 시간: 00-seed 생성부터 비용 기록까지 약 303분(세션 중단 대기 포함), 서브에이전트 합계 20.5분
- 산출물: 00~08 + decision-log, 총 198KB(08 제외 시점)
