# Decision Log — 요양시설 낙상 감지·알림 서비스 (FallGuard)

런: service-autopilot v1.4.1 평가 런 (run-20260908-trim / s8). 시작 2026-09-08 14:31 (KST).
평가 조건: 사용자 질문 불가(A2 전부 Assumed(무응답)), GATE 검토관 1명(santa 2인 대신 — 예산 이탈, 08에 명시), 재검토 없음.

| # | 단계 | 결정 | 대안·기각 이유 | 근거 |
|---|---|---|---|---|
| DL-1 | A0 | 강도 full (사용자 지정 + 안전·민감정보·법·엣지·외부연동 신호) | spike — 감지 수단 미확정이라 후보였으나 사용자 지정 우선. 리스크는 Q1로 예약 | SKILL.md 강도 표 |
| DL-2 | A0 | 감지 수단 = 비접촉(레이더/카메라 온디바이스) 우선, 웨어러블 보조 | 웨어러블 단독 — 인지저하 입소자 착용 유지 곤란(A1 확인 예정) | 00-seed 해석 1 |
| DL-3 | A0 | 보호자 통보는 직원 확인 후(확인된 낙상만) | 감지 즉시 보호자 통보 — 오탐이 보호자 불안·시설 신뢰 훼손 | 00-seed 해석 4 |
| DL-4 | A0 | 대상 = 국내 장기요양기관, 요양병원 제외 | 병원 포함 — 의료법·EMR 연동으로 범위 2배 | 00-seed 해석 2 |
| DL-5 | A2 | Q1 감지 수단 = mmWave 레이더(비영상) — Assumed(무응답) | 카메라: 화장실 불가·프라이버시 동의 부담 / 웨어러블: 착용 유지 곤란 / 하이브리드: 파이프라인 2개 | 01-recon Vayyar·Milesight, Kepler(카메라) 대조 |
| DL-6 | A2 | Q2 기능 경계 = 감지·알림만, 비의료기기 포지셔닝 — Assumed(무응답) | 예측·활력징후는 디지털의료기기 인허가 가능성 | 01-recon 규제 (c) — 확정 아님, 착수 조건에 식약처 문의 |
| DL-7 | A2 | Q3 보호자 통보 = 직원 확인 후 확인된 낙상만 — Assumed(무응답) | 즉시 자동 통보: 오탐이 보호자에게 감 | 00-seed 해석 4 |
| DL-8 | A2 | Q4 배포 = 온프레미스 게이트웨이 + 클라우드 SaaS — Assumed(무응답) | 완전 온프레미스: 시설별 운영 부담 / 센서 직결 클라우드: 단절 시 감지 불가 | 01-recon 규제 (a) CCTV 의무화 → 네트워크 존재 가능성 |
| DL-9 | A2 | Q5 직원 채널 = 스마트폰 앱 + 관제 화면 — Assumed(무응답) | 너스콜 연동: 시설별 프로토콜 / 전용 단말: 기기 관리 | 01-recon 배치기준 |
| DL-10 | A1 | 스택 = TS 단일 언어(NestJS + React + React Native/Expo) + PostgreSQL 18 + Mosquitto + 완제품 mmWave 센서(LoRaWAN) + Pi 5 GW | FastAPI(ML 툴링 필요 시 2단계) · Flutter(언어 2개) · TI 자체 알고리즘(MVP 범위 초과) · Jetson(추론 불필요) | 01-recon fit 판정 표, 팀 2~4인 Assumed |
| DL-11 | A1 | 감지 알고리즘은 MVP에서 만들지 않고 센서 내장 판정을 쓴다. 정확도는 파일럿 실측 SC로 검증, 센서 어댑터로 벤더 격리 | 자체 모델 — 데이터셋(CC BY-NC-SA) 상용 불가·수집 기간 | search-first 원칙, 01 데이터셋 라이선스 |
| DL-12 | A3 | 지연·가용성 상수: ALERT_LATENCY_P95 15초 / MAX 60초, PILOT_DAYS 30, SLO 99.5% | 01 표본 #11 "AI 30초~2분, 터보 ~10초"는 제품사 블로그 경유라 신뢰도 낮음 → 출처 URL 대신 설계 결정으로 두고 파일럿 실측으로 검증 | LoRaWAN 업링크 + 푸시 경로가 15초 안에 가능(설계상), 60초는 표본 하한 |
| DL-13 | A3 | 정확도 상수: FALL_SENSITIVITY_MIN 0.95, FALSE_ALARM_MAX 1건/센서/주 | 제조사 99%·Kepler 100%는 자체/실증 주장 → 보수적 95%. Kepler 오탐 3개월/1회는 2단계 목표 | 01 표본 #2·#3·#5, 알람 피로 연구 |
| DL-14 | A3 | 대응 상수: ACK 60초 → 에스컬레이션 180초 → 전화 재시도 2회, 바닥 방치 중앙값 5분, resolve 리마인더 30분 | 전통 알람 평균 13분(01 표본 #9) 대비 1/3 이하 목표 | 설계 결정 |
| DL-15 | A3 | 운영 상수: DEDUP 120초, 하트비트 60초, 센서 오프라인 300초, GW 120초, 버퍼 24h, 보존 3년(열람대장 준용), 세션 12h/30d, RPO 24h/RTO 4h | — | P1·P2 기본값, 01 규제 (a) |
| DL-16 | A4 | 모듈형 모놀리스(NestJS 1개: ingest·events·alerting·notify·devices·consent·audit·api·realtime·jobs) | 마이크로서비스 — 이벤트 수십/일·팀 2~4인에 배포·관측 비용만 증가. 모듈 경계는 유지 | ADR 형식, 04 검토한 대안 |
| DL-17 | A4 | 지연 잡·타이머 = pg-boss(PostgreSQL) | BullMQ+Redis — 구성요소 1개 추가, 잡 수 적어 이점 없음 | 04 |
| DL-18 | A4 | 엣지 업링크 = MQTT over mTLS(Mosquitto), 기기별 X.509, ACL f/{facility}/# | HTTPS 배치 POST — QoS·재접속·순서 보장을 직접 구현해야 함 | 04, P1 원격 접근·디바이스 신원 |
| DL-19 | A4 | 테넌시 = PostgreSQL RLS + SET LOCAL app.facility_id | 앱 계층 where — 누락 1회가 건강정보 유출 | 04, INV-4 |
| DL-20 | A4 | 감사로그 = append-only + 행 해시 체인 + DELETE 권한 없는 롤 | WORM/외부 앵커링 — 2단계 | 04, FR-009 |
| DL-21 | A4 | 푸시 = Time-Sensitive 기본, Critical Alert 승인 신청(착수 조건), SMS·관제 보완. 음성 전화 대행은 어댑터 뒤(공급자 미확인) | Critical Alert 전제 — 승인 미확인 | 01 푸시 확정 사실 |
| DL-22 | A5 | 버저닝 = Accept 미디어타입(v1), 에러 = RFC 9457, 페이지네이션 = 커서, 멱등성 = Idempotency-Key 24h | ecc:api-design의 URL 버저닝·자체 {error{code}} 봉투 — stage-templates A5 규약이 우선(skill-routing 규칙 2) | 05 규약 |
| DL-23 | A5 | ID = UUIDv7(시간 정렬), 감사로그만 bigint identity | postgres-patterns의 bigint 권고 — 다시설·클라이언트 멱등 키·외부 노출에 UUID가 맞고, v7이라 인덱스 국소성 유지 | 05 데이터 규칙 |
| DL-24 | A5 | ack 승자 = 조건부 UPDATE(acked_by IS NULL) 영향 행으로 판정, 타인 선점 시 200 winner:false | 409 반환 — 클라이언트 재시도가 "실패"로 보여 직원이 당황(L-06) | 05 E10, INV-5 |
| DL-25 | R1 | FallEvent.resident_id는 resolve 시 확정(다인실 선택, 미상 허용), 통보는 resident 특정 이벤트만(INV-9). 감지 동의 거부자는 센서 미설치 구역 배정(운영 정책) | 센서에 입소자 식별 요구 — 비영상 레이더로 불가 / 침상별 센서 — 비용 4배 | _a4-review #1 CRITICAL |
| DL-26 | R1 | 지연 정책 2단(LATE_EVENT_SEC 표시 / STALE_EVENT_MIN 초과는 직원 알림·에스컬레이션 없음), 센서 오프라인 = 보고 주기 × SENSOR_OFFLINE_MISSED_REPORTS | 단일 임계 — 새벽 시설장 통화 또는 방치 중 미알림 중 하나를 고름 | _a4-review #7·#18 |
| DL-27 | R1 | detected_at = 엣지 RTC(로컬 MQTT 수신), sent_at 오프셋 계산, CLOCK_SKEW_MAX_SEC 초과 clock_suspect, 홉별 지연 예산(PUSH_PROVIDER_LATENCY_BUDGET_SEC) | 센서 시각 — LoRaWAN 센서에 신뢰 가능한 시계 없음 | _a4-review #6·#15 |
| DL-28 | R1 | 클라우드 단절 시 엣지 로컬 경보(릴레이 HAT → 간호실 부저/경광등) FR-021, LTE 백업은 시설 옵션. SLO 99.5%는 로컬 경보가 감지→경보를 유지하므로 MVP 수용 | 엣지 LAN 관제 페이지 — 관제 PC·앱이 같은 회선이라 단절 시 도달 보장 없음 | _a4-review #2·#17 |
| DL-29 | R1 | 이벤트 INSERT + ack-timeout 잡을 같은 트랜잭션(pg-boss 동일 커넥션), 스위퍼 SWEEPER_INTERVAL_SEC, JOB_RETRY_LIMIT → FR-023 | 잡만 신뢰 — 크래시 창에서 타이머 없는 이벤트 | _a4-review #3 |
| DL-30 | R1 | TB1.5 시설 LAN 경계 + GW 관리 UI·엣지 물리·관제 공용 PC 위협, 콜백 타임스탬프 창·체인 검증 주기·연결 코드 TTL/시도 상수화, 시설별 해시 체인, FORCE RLS·롤 분리 | — | _a4-review #8·#11·#12·#13 |
| DL-31 | R1 | 실시간 팬아웃 = PostgreSQL LISTEN/NOTIFY(ID만), APP_INSTANCES_MIN 2 | Redis pub/sub — 구성요소 추가(DL-17과 일관) | _a4-review #14 |
| DL-32 | R1 | 하트비트 = 엣지 1행/EDGE_HEARTBEAT_SEC(센서 목록 포함) + device.last_seen_at 제자리 갱신, 원시 hb만 파티션·BRIN | 센서별 행 — 60대×1/분 = 8.6만 행/일/시설 | _a4-review #14 |
| DL-33 | R1 | 억제 스위치 FR-022 승격(SUPPRESS_MAX_HOURS 만료·배지·감사) | 제거 — 오탐 폭주 시 대응 수단 없음 | _a4-review #16 |
| DL-34 | R1 | fall_event 파티셔닝 제거, PG 18 유지(2025-09-25 출시·5년 지원·pg-boss 호환), 종단 ack(DB 커밋 후 seq ack), (edge_id, seq) 멱등키 | — | _a4-review #4·#5·#19 |

## 스킬·모델 사용 기록
- [A0] 스킬 없음 (라우팅 표: 정규화는 모델만) — 세션 모델(fable)
- [A1] ecc:research-ops (sonnet 서브에이전트, general-purpose) — 도메인·규제·유사 솔루션·스택·정확도 표본 조사, 01-recon.md 직접 저장. ecc:search-first는 호출 안 함(서브에이전트 중첩 스폰 위험·예산). A3~A7 진입 사전조사 후보도 A1에서 함께 수집하도록 지시(메인 검색 절감)
- [A2] ecc:product-lens (Mode 1 진단 7문) — 세션 모델(fable). PRODUCT-BRIEF.md는 따로 만들지 않고 02 register 머리에 흡수(skill-routing 규칙 2)
- [A1 2차] 같은 sonnet 서브에이전트 재개(SendMessage) — 스택·정성·규제 보강 12회 검색, 총 27회. 서버·모바일 프레임워크는 조사 대신 메인 fit 판정(DL-10)
- [A3] ecc:product-capability — CAPABILITY 재진술·INV 불변식 8개·상태 전이를 PRD 절로 흡수. frontend-design-taste — 화면 4종 dial 지정(관제 8/2/3, 직원 알림 4/3/2 등). ux-principles-kr — UX-01~03 발급. 세션 모델(fable). 메인 추가 검색 0회(A1 사전조사 후보 사용)
- [A4] ecc:architecture-decision-records — 검토한 대안 표(ADR 요약)를 04에, 상세는 DL-16~21로 흡수(docs/adr 미생성). ecc:security-review — 체크리스트(비밀·입력 검증·파라미터 쿼리·쿠키·rate limit·로그 마스킹·의존성)를 STRIDE ③에 반영. 세션 모델(fable). 메인 검색 0회
- [A5] ecc:api-design — 상태코드·레이트리밋 티어·체크리스트 반영, 버저닝·에러 봉투는 템플릿 우선(DL-22). ecc:postgres-patterns — 부분 인덱스·BRIN·RLS(SELECT 래핑)·timestamptz·text·파티셔닝 반영. 세션 모델(fable). 메인 검색 0회
- [재개] 2026-09-08 15:0x 세션 한도(429)로 중단 → 2026-09-09 01:52 재개. 00~05·NEXT·decision-log 존재, 재생성 없음. A4 검토관 회신은 코디네이터가 `_a4-review.md`로 보존 — 재실행 없이 필터링 후 R1로 반영
- [A4 검토] fresh-reviewer (fable, fresh) — 판정 FAIL, 지적 20건(CRITICAL 1·HIGH 8·MEDIUM 9·LOW 2). 필터링: 20건 채택(거짓 양성 0, #19 부분). 재검토 없음(예산). 반영 = REVISIONS.md R1, DL-25~34. **04 v1.1 적용 완료, 03·05·02는 배지(적용 대기)** — Bash 명령 길이 한계로 패치 스크립트가 잘려 미적용, 스크립트 파일 실행 방식으로 재개
- [일시 중지] 2026-09-09 02:0x 코디네이터 지시(창 예산) — 06:05 이후 재개. 재개 시 첫 작업 = REVISIONS.md R1의 03·05·02 패치 목록 적용(배지 제거) → check_package C6·C8 확인 → A6
