# Decision Log — PiCam Watch

런 시작: 2026-09-08 14:31 (평가 런 run-20260908-trim, 스킬 1.4.1, 세션 모델 Fable 5.1)
평가 런 환경 규칙: 사용자 질문 불가 → A2 배치는 표로만 작성하고 전 문항 `Assumed(무응답)` 채택. GATE 1회, 재검토 없음.

## DL-001 [A0] 강도 = full
- 결정: full. 사용자 지정(`full`) + 신호 표(하드웨어/엣지 · 민감정보(영상) · 외부 연동 2개 이상).
- 대안: lite — 기각(사용자 지정이 우선). spike — 기각(플랫폼·범위·핵심 루프 모두 확정).

## DL-002 [A0] 서비스 유형 = 복합(IoT·엣지 + 관제 + 웹), 프로파일 P1 전부 + P3 일부
- 근거: 00-seed 감지된 제약. P3는 실시간성·알람 폭주·이력 증가·무중단 4항목만 적용(폐쇄망·프로토콜은 P1/외부연동 축이 덮음).

## DL-003 [A0] 해석 8건을 Assumed로 고정 (00-seed "해석한 것")
- 최상위 리스크: ①Pi=엣지 게이트웨이 ②서버 경유 원격 시청. A2 질문 1칸을 여기에 예약한다(질문 프로토콜 3).

## 스킬·모델 사용 기록
- [A0] 스킬 없음 (라우팅 표: 정규화는 모델만으로 충분). 메인, 세션 모델.

## DL-004 [A1] 스택 fit 판정 (01-recon "스택 후보 fit 판정" 표)
- 결정: go2rtc(중계) · Pi 5 4GB · Python+onvif-zeep-async(엣지) · MQTT mosquitto(제어) · WebRTC+coturn(시청) · FastAPI+PostgreSQL 16(서버) · SQLite WAL+USB SSD(엣지 저장) · Pi OS Lite+Overlay FS+log2ram(OS) · 앱 계층 A/B OTA · 웹 푸시.
- 결정 요소: Pi 5 HW 인코더 부재 → 무재인코딩 패스스루가 축. 팀 1~2명 → 언어 1개(Python).
- 기각: MediaMTX(프로토콜 폭 불필요), Pi 4(RTC 없음), Go/Node(언어 2개), RAUC/Mender/balena(규모 대비 과잉·유료·미확인), SD 저장(마모).

## DL-005 [A1] 하드 제약 파생: 카메라 H.264 스트림 1개 필수, Pi 재인코딩 금지, 서버 단일 인스턴스
- 근거: HA ONVIF 문서(H.264 탐색), Pi 5 공식 포럼(HW 인코더 없음).

## DL-006~010 [A2] 질문 5건 전부 추천안 Assumed(무응답) — 평가 런 규칙
- DL-006 Q1 사용 맥락 = 스마트폰 단시간 확인 + 푸시 → 서브스트림 기본, 모바일 우선 UI
- DL-007 Q2 배포 = 중앙 서버 경유(Pi 아웃바운드 MQTT + WebRTC/TURN)
- DL-008 Q3 녹화 = 이벤트 클립, Pi USB SSD, CLIP_RETENTION_DAYS 순환, 서버는 썸네일·메타만
- DL-009 Q4 카메라 = ONVIF S/T + RTSP H.264, PTZ는 capability 자동 노출
- DL-010 Q5 현장 = 유인 → 앱 계층 A/B OTA, OS A/B·UPS non-goal(잔여 리스크 Accept)

## 스킬·모델 사용 기록 (계속)
- [A1] ecc:research-ops + ecc:search-first (sonnet 서브에이전트, general-purpose) — 검색 36회(WebSearch 24 + WebFetch 12), 131k 토큰, 7.4분. 미확인 9건을 명시해 돌려줌. 메인이 WebFetch 5회로 ONVIF 라이브러리 유지보수 근거(HA manifest)·FastAPI·raspi-config Overlay FS를 보강하고 fit 표 작성.
- [A2] ecc:product-lens (메인, 세션 모델) — Mode 1 진단 7문을 register 상단에 흡수. 질문 Impact 판단에 "고통·안티골" 답을 사용.

## DL-011 [A3] 상수 표 확정 (03-prd "상수 표") — 설계 결정 상수 일괄
- LIVE_LATENCY_P95 1.0s(WebRTC 200~500ms의 2배 여유) · LIVE_FIRST_FRAME_P95 3.0s · PTZ_CMD_LATENCY_P95 1.0s · DEVICE_OFFLINE_DETECT_SEC 60(keepalive 20×1.5+여유) · ALERT_DELIVERY_P95 30s · CLIP_PRE/POST 5/15s · PURGE_MAX_DELAY_HOURS 24(법 5일보다 엄격) · VIEWER_MAX 5 · UPLINK_MIN_MBPS 30 · SD_WRITE_MAX_MB_DAY 50 · OTA_ROLLBACK_MAX_MIN 5 등.
- 근거 유형을 넷으로 분리: 출처 URL / 실측 / 설계 결정 DL-# / 미확인. 미확인 2건(TURN_RELAY_COST, KR_UPLOAD_AVG_MBPS)은 SC에 쓰지 않음.
- 법 관련 상수(CLIP_RETENTION_DAYS 30, AUDIT_RETENTION_DAYS 365, AUDIT_REVIEW_INTERVAL_DAYS 30)는 출처 URL을 붙였으나 조문 원문 미인출 → "가능성" 표기. 확인 후 상수 표 한 곳만 수정.

## DL-012 [A3] 목표 3·스토리 5(P1 4개)·FR 29(P0 16·P1 11·P2 2)·SC 14·INV 7
- INV-2(감사 로그와 토큰 발급 같은 트랜잭션, fail-closed)와 INV-7(workspace_id 전 테이블)은 product-capability의 "숨은 제약을 드러내라" 규칙에서 나옴.
- FR-010: 오프라인 기기에 PTZ 명령 큐잉 금지 — 지연 실행되는 PTZ는 사용자 의도와 어긋나 위험. 즉시 실패.

## 스킬·모델 사용 기록 (계속)
- [A3] ecc:product-capability (메인) — 역량 진술·불변식 INV-1~7·상태 전이 4종을 PRD에 흡수. frontend-design-taste (메인) — dial DENSITY 5/MOTION 3/VARIANCE 3(라이브 화면 7), 상태 4종 강제. 사전조사 검색 3회(WebSearch 1 + WebFetch 2, 1회 403).

## DL-013 [A4] ADR-1~7 (04-architecture "검토한 대안" 표 — ADR 형식 흡수, docs/adr 별도 파일 없음)
- ADR-2 MQTT 5 단일 채널(시그널링 포함) — 브로커 메시지 상한 64KB. ADR-3 클립 온디맨드 업로드 + 서버 캐시(STREAM_TOKEN_TTL_SEC 재사용, 새 상수 없음). ADR-5 coturn 자체 호스팅. ADR-6 웹 푸시 PWA(iOS는 홈 화면 추가 필요). ADR-7 서버 단일 인스턴스.
- 잔여 리스크 R-1(go2rtc NAT 뒤 TURN 동작 — README는 8555 포트 개방 안내) → 워킹 스켈레톤 첫 작업. R-2 법 상수 원문 확인 → 핸드오프 선행 조건. R-3 OS 벽돌·R-4 LAN 물리 보안 Accept.

## DL-014 [A4] 위협모델 — 경계 6개(B1~B6) × STRIDE 6, 대응 25건 (Mitigate 21 · Eliminate 2 · Transfer 1 · Accept 1)

## 스킬·모델 사용 기록 (계속)
- [A4] ecc:architecture-decision-records (메인) — ADR 표 형식(결정·대안·왜·결과). ecc:security-review (메인) — 체크리스트를 ③ 대응 표와 말미 적용 문장에 반영. 사전조사 WebFetch 1회(go2rtc README).
- [A4 검토] fresh-reviewer (fable, fresh) — 04 독립 검토 1회 (결과는 DL-015에 기록).

## 재개 — 2026-09-09 01:52 (2026-09-08 15:00경 세션 한도 429로 중단. 00~05·NEXT·decision-log 존재, 다시 만들지 않음. A4 검토관 회신은 코디네이터가 `_a4-review.md`로 보존)

## DL-015 [A4 검토 반영] fresh-reviewer(fable) 16건 — 타당성 필터링 결과 전부 채택, 거짓 양성 0 → REVISIONS.md R1
- HIGH 4: #1 Event 월 파티션 DROP이 INV-1 위반 → 행 DELETE 일 1회. #2 TURN 대역 미대조 → VPS_LINE_MBPS·TURN_RELAY_MAX_MBPS + 릴레이 sub 고정(FR-006·SC-015·엣지 17·18). #3 FR-021 미커버 → agent 카메라 연결 감시 책임. #4 CA·인증서 갱신 부재 → ca 모듈 + FR-030 + D04.
- MEDIUM 7: 클립 singleflight/원자적 rename/CLIP_CACHE_TTL_MIN(#5) · CMD_TIMEOUT_SEC·webrtc.close·session_id 멱등·trickle(#6) · TURN 자격증명 2계층 + LIVE_SESSION_MAX_MIN(#7) · RINGBUF_TMPFS_MB(#8) · STRIDE ③ B6-R/B6-E/B5-D 추가(#9) · 04 숫자 제거(#10) · 참가자 이름 일치(#11).
- LOW 5: THUMB_MAX_KB(#12) · 골든 시그널 문구(#13) · clock_synced 통일(#14) · MQTT 토픽 `devices/{device_id}/` + ACL %u(#15) · 레지스트리 외부화 GHCR(#16).
- 판정 CONCERNS → 보강 완료. 재검토 없음(예산). 검토 결과 원문: `_a4-review.md`.

## DL-016 [R1] 상수 14개 추가 (03 v1.1 상수 표): VPS_LINE_MBPS 1000 · TURN_RELAY_MAX_MBPS 100 · LIVE_SESSION_MAX_MIN 60 · TURN_CRED_TTL_MIN 90 · DEVICE_TURN_CRED_TTL_HOURS 24 · CMD_TIMEOUT_SEC 10 · CLIP_CACHE_TTL_MIN 30 · DEVICE_CERT_VALID_DAYS 365 · DEVICE_CERT_RENEW_BEFORE_DAYS 30 · RINGBUF_SEGMENT_SEC 2 · RINGBUF_TMPFS_MB 256 · MQTT_MSG_MAX_KB 64 · OTA_RETRY_MAX 1 · THUMB_MAX_KB 200. 전부 설계 결정.
- 버전 전파: 03 v1.0→v1.1, 04 v1.1(기준 03 v1.1), 05 v1.1(기준 03 v1.1). 같은 턴에 반영, 적용 대기 배지 없음.

## 스킬·모델 사용 기록 (계속)
- [A5] ecc:api-design + ecc:postgres-patterns (메인) — 레이트리밋 헤더·Problem slug 표·커서·인덱스/파티션/타입 관례. 충돌 처리: URL 버저닝→미디어타입, 자체 에러 봉투→RFC 9457, offset→커서, bigint ID 권고→ULID text(시간순, 기기 생성 멱등 필요). 사전조사 WebFetch 1회(IETF Idempotency-Key draft-07).

## DL-017 [A6] 레이어 5종(단위·통합·계약·E2E·실기) — SC 15건 + P0/P1 FR 28건 전부 시나리오, INV는 속성 테스트, flaky 0 정책(Playwright retries 1, 재시도 통과는 통과 아님)
- 실기 계층을 명시: SC-001·002·003·004·008·013은 목킹으로 증명 불가 → 릴리스 게이트. 장비 목록은 07 착수 자산.
- 충돌 처리: tdd-workflow 80% 일률 → 리스크 기반(P0 ≥90 · R-1/R-2 100% · P1 ≥70 · UI/P2 스모크).

## DL-018 [A7] 운영 상수 17개 (R2, 03 v1.2) + 배포·관측·알림·복구 설계
- 엣지 A/B는 앱 계층(ADR-4) — updater가 Docker 소켓을 쓰는 유일한 컨테이너라 agent와 분리. 골든 이미지는 pi-gen, 클레임 토큰은 Imager 커스터마이즈로 주입.
- docker-patterns "compose 프로덕션 금지"는 규모 근거로 미채택(ADR-7). 알람 8종 ↔ 런북 RB-1~9 1:1. 클립·기기 SQLite는 백업 없음(설계: Q3 30일 순환, SSD 보존이 복구 수단).

## 스킬·모델 사용 기록 (계속)
- [A6] ecc:tdd-workflow + ecc:e2e-testing (메인) — RED 우선·POM·flaky 격리 관례. 사전조사 WebFetch 1회(Playwright retries).
- [A7] ecc:deployment-patterns + ecc:docker-patterns (메인) — CI/CD 단계·헬스체크·compose 보안 옵션·.env 검증. 사전조사 WebFetch 1회(log2ram README).

## DL-019 [GATE] 독립 검토관(fable, fresh context) 1회 — 판정 CONCERNS (CRITICAL 1 · HIGH 6 · MEDIUM 15 · LOW 8, 거짓 양성 0)
- 이 턴 반영: #1 웹 프론트엔드·Caddy fit 행(01) — 결정 요소는 팀 스택·PWA 푸시·자동 TLS. #2 04 시퀀스 토픽 프리픽스를 05 규약으로 정정 → 04 v1.2 (R3).
- 이관: HIGH #3~#7(rootfs↔OTA 충돌, 릴레이 강등 메커니즘, SC-008 측정 불가, 상수 직접 기입, 저장 상수 모순)과 MEDIUM·LOW 전부를 08 핸드오프 선행 조건 5개로. 재검토 없음(예산 규칙).
- [재개] 2026-09-09 06:25 — 생성 에이전트가 06:00 한도(429)로 종료(07까지 저장). check_package(CRITICAL 0 · HIGH 0)·GATE·08은 메인 세션이 수행.

## 비용 기록
- 강도 full · 서브에이전트: A1 조사(sonnet, 기록은 위 스킬 사용 기록), A4 독립 검토관(fable), GATE 검토관(fable) — 검토관 토큰 GATE 검토관 15.7만 · A4 검토관 6.4만
- 소요: 2026-09-08 14:31 시작 → 2026-09-09 06:25 마감. 한도 중단 2회(09-08 15:00, 09-09 06:00), 재개 2회, 재개 시 컨텍스트 압축 1회(대화 58만 토큰 > 20만 한도).
- 검색 횟수·메인 토큰: 미상 — 에이전트가 보고 전에 종료. 다음 런은 단계마다 decision-log에 누적 기록하도록.
