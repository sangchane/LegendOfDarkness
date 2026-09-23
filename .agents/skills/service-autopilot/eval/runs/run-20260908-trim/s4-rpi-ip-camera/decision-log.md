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
- ADR-2 MQTT 5 단일 채널(시그널링 포함) — 브로커 메시지 상한 64KB. ADR-3 클립 온디맨드 업로드 + 서버 캐시(초안은 STREAM_TOKEN_TTL_SEC 재사용 — R1 #5에서 CLIP_CACHE_TTL_MIN 신설로 정정, DL-016; R4 #8). ADR-5 coturn 자체 호스팅. ADR-6 웹 푸시 PWA(iOS는 홈 화면 추가 필요). ADR-7 서버 단일 인스턴스.
- 잔여 리스크 R-1(go2rtc NAT 뒤 TURN 동작 — README는 8555 포트 개방 안내) → 워킹 스켈레톤 첫 작업. R-2 법 상수 원문 확인 → 핸드오프 선행 조건. R-3 OS 벽돌·R-4 LAN 물리 보안 Accept.

## DL-014 [A4] 위협모델 — 경계 6개(B1~B6) × STRIDE 6, 대응 26건 (Mitigate 22 · Eliminate 2 · Transfer 1 · Accept 1) — R1 #9 행 추가 후 재집계(R4 #30)

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

## DL-020 [R4 #3] Docker data-root·log2ram 동기화 대상 = USB SSD
- 결정: firstrun이 `/etc/docker/daemon.json` data-root=/mnt/data/docker, log2ram HDD_LOG=/mnt/data/log을 설정한 뒤 Overlay FS를 켠다. 쓰기 구역에 둘을 명시(07).
- 왜: rootfs 읽기전용이면 `/var/lib/docker`가 RAM 상위 레이어에 떨어져 OTA 배포·롤백 상태가 재부팅에 소실(FR-023·SC-010 불성립). 대안 — Overlay FS 포기: SD 마모 대책(SC-013) 상실이라 기각.

## DL-021 [R4 #4·#10·#19] 릴레이 판정 — 모든 세션 sub 시작, 클라 `ice_state`·`first_frame` 보고, main = 직결 확인 후 신규 세션
- 결정: 스트림은 offer 시점에 고정되므로 세션 중 강등·재협상을 두지 않는다. ① 모든 E27 세션은 sub로 시작 ② 브라우저가 getStats selected pair로 `ice_state{relayed}`를, 첫 프레임 시 `first_frame{first_frame_ms}`를 E29 WS로 보고 ③ main은 relayed=false로 보고된 활성 세션을 `direct_session_id`로 참조하는 신규 세션(승격 중 2세션 공존, VIEWER_MAX 포함) ④ relayed=true인 main 세션은 `main-requires-direct`, 합계가 TURN_RELAY_MAX_MBPS를 넘기는 릴레이 세션은 `viewer-limit`(relay-cap)로 WS error 후 종료 ⑤ `stream_effective` 메시지 제거 ⑥ go2rtc 8555는 LAN 바인딩(localhost면 srflx 수집 불가 → 전 세션 릴레이).
- 왜: 가장 단순한 결정 — go2rtc 스트림 전환 API·재협상이 필요 없고, 서버가 릴레이 여부를 아는 유일한 경로(클라 보고)를 SLO 지표(#10)와 겸한다. 대안 — 서버가 coturn 세션 로그로 판정: 세션↔TURN 할당 상관이 불명확해 기각. relay-cap의 E27 429는 판정 불가라 폐기(SC-015·엣지 18 정정).
- 영향: 03 FR-003·006·SC-015·엣지 17·18, 04 난점 표·시퀀스 1·B4·R-1 ⑤⑥, 05 slug·E27·E29·WS·FR 매핑, 06 FR-003·006·SC-015, 07 SLO·첫 작업 1.

## DL-022 [R4 #5·#6·#20] 상수 5개 추가 + 상수 표 밖 숫자 제거
- 추가: PI_BOOT_MAX_SEC 120 · AGENT_RECOVER_MAX_SEC 60 · FRAME_DROP_MAX_PCT 1 · ALARM_EVAL_WINDOW_MIN 60 · OFFLINE_ESCALATE_MIN 30 (전부 설계 결정).
- 제거: 03 "1초"(역량 진술·US-1·근거) → LIVE_LATENCY_P95, 엣지 9 "30초" → EVENT_COOLDOWN_SEC, SC-008 "60초·부팅 시간" → AGENT_RECOVER_MAX_SEC·PI_BOOT_MAX_SEC; 04 "250Mbps × 2·약 5%" → 상수 산식·TURN_RELAY_MAX_MBPS 비교; 06 "30초" → EVENT_COOLDOWN_SEC; 07 healthcheck 30s/3 → HEALTHCHECK_*, "1시간 창" → ALARM_EVAL_WINDOW_MIN, "30일 순환" → CLIP_RETENTION_DAYS, "30분" → OFFLINE_ESCALATE_MIN. SC-011·015 "프레임 드롭 0" → ≤ FRAME_DROP_MAX_PCT(정상 상태에도 0이 아니라 flaky 0 정책과 충돌).

## DL-023 [R4 #7] EVENTS_PER_CAM_DAY_MAX 500 → 120, VPS_DISK_MIN_GB 80 추가
- 결정: 상한을 낮춰 CLIP_STORAGE_MIN_GB 256과 정합(최악 ≈ 180GB ≤ 256 × 0.9). 대안 — SSD를 1TB로: 원가 상승, 그리고 120건/카메라/일도 소상공인 시설 모션 빈도로 충분히 큰 상한이라 기각. 상한 초과는 FR-015(예비 미달 삭제)가 처리. VPS 디스크는 thumbs 최악치(≈29GB) + clipcache + pgdata 산식으로 80GB.
- 영향: 03 상수 표(2행 + CLIP_STORAGE_MIN_GB 근거), 07 서버 형상.

## DL-024 [R4 #14] Pi 재설치 = SSD 인증서 재사용, 재클레임은 SSD 손상·인증서 만료 시만
- 결정: firstrun이 `/mnt/data/certs/device.crt`를 발견하면 클레임을 건너뛰고 같은 device_id로 재접속(Admin 승인 불필요). 대안 — 항상 재클레임 + 구 기기 retire: 새 device_id·`device_one_per_site` 충돌·카메라 재등록이 따라와 기각.
- 영향: 07 firstrun·장애 표·백업 표, 03 RTO_DEVICE_MIN 근거.

## DL-025 [R4 #15] 백업과 INV-1 — pg_dump에서 event(·idempotency_key) 데이터 제외, thumbs는 rsync --delete 미러
- 결정: 백업에 CLIP_RETENTION_DAYS를 넘긴 이벤트 메타·썸네일이 남지 않게 한다(INV-1 문구 유지). 미러는 purge-job 직후 같은 크론에서 실행. 비용: 서버 복원 후 이벤트 목록은 새 이벤트부터(클립 원본은 Pi SSD에 그대로, Pi 파기 잡 독립 동작).
- 대안 — INV-1 범위를 "운영 저장소"로 좁히고 백업은 BACKUP_RETENTION_DAYS 상한: 법 해석 리스크(백업도 보유)라 기각. 이벤트 재동기 명령 추가: 범위 확장이라 기각.
- 영향: 07 백업·장애 표, 03 RPO_SERVER_HOURS·BACKUP_RETENTION_DAYS 근거.

## DL-026 [R4 #18] 이벤트 capability 없는 카메라 — 등록 허용 + `no-motion-events` 경고 + 이벤트 UI 비노출
- 결정: E22 201 응답에 warnings[], 03 엣지케이스 19, 06 FR-002 시나리오. US-3은 그 카메라에 적용되지 않음을 화면 문구로 알린다. 대안 — 등록 거부: 라이브·PTZ만으로도 가치가 있어 기각.

## DL-027 [R4 #22] YAGNI 4건 — 3건 제거, INV-7은 유지
- 제거: Event 월 파티션(일 1회 DELETE와 중복), CAMERA.password_enc 서버 컬럼(항상 null — 기기 SQLite에만), E44 DELETE /events(FR·스토리 없음; AUDIT_LOG `event.delete`·04 B6-R 문구도 제거). E26 DELETE /cameras는 Camera `removed` 전이(FR-002 역방향)로 FR-002 매핑에 편입.
- 유지: INV-7 workspace_id — 제거하면 ERD·DDL·B1 대응·스코프 규약 전면 수정이라 비용 > 이득. 단일 workspace 고정은 그대로.

## DL-028 [R4 #11·#12·#17·#21·#26·#27] 계약·형상 공백 메우기
- E51 POST /releases(CI `Authorization: Bearer` CI_RELEASE_TOKEN, Idempotency-Key) — CI 마지막 단계가 RELEASE 행을 쓴다. `.env.example`에 SMTP_URL·SMTP_FROM·CI_RELEASE_TOKEN·TURN_REALM·TURN_EXTERNAL_IP·PICAM_CLAIM_TOKEN·DOCKER_DATA_ROOT·LOG2RAM_HDD_PATH 추가; 이메일은 외부 SMTP 릴레이(컨테이너 없음).
- FR-018 요약 규칙을 07 알림 절에 명문화(사이트별 일 카운터, 요약 1건/일, offline 알림 제외). 용어: 07의 알람 수신자는 "운영 담당"(03 용어집 추가)으로 사용자 페르소나 "운영자"와 분리.
- 권한 문구: FR-008(프리셋 이동은 Viewer), FR-028·US-5(Viewer 스코프 전체) — 05 스코프 표 기준으로 03 정정. E11 `/device/claim`은 mTLS 예외로 05 규약·07 caddy·04 caddy 노드에 명시.

## DL-029 [R4 #8·#9·#13·#16·#23·#24·#25·#29·#30] 전파·정정(기계적)
- 04 프라이버시 절 캐시 TTL → CLIP_CACHE_TTL_MIN, DL-013 문구 정정 · 04 시퀀스 D01 경로·slug kebab-case · D03 상수 목록 12개 보강 · `events` 페이로드에 status 필드(type에서 clip_ready·purged 제거, 파기 보고는 `cmd/purge.report` 응답만) · 02 집계(33행, Asked 6행/질문 5) · 06 절 제목 03 v1.3 · G1←SC-015, G2←SC-005 · 07 큐 길이 알람 행(OFFLINE_QUEUE_MAX × ALARM_WARN_PCT) + 04 구조도 caddy 노드 · DL-014 26건.

## [GATE 패치] R4 — 반영 27건 / 이관 0건 (#28은 정보로 유지) · 2026-09-09
- 남은 선행 조건: R-2 법 상수 원문 확인 1회, R4 자율 결정(DL-020~028)의 사용자 확인. 재검토 없음(예산 규칙). check_package: CRITICAL 0 · HIGH 0.

## 비용 기록
- 강도 full · 서브에이전트: A1 조사(sonnet, 기록은 위 스킬 사용 기록), A4 독립 검토관(fable), GATE 검토관(fable) — 검토관 토큰 GATE 검토관 15.7만 · A4 검토관 6.4만
- 소요: 2026-09-08 14:31 시작 → 2026-09-09 06:25 마감. 한도 중단 2회(09-08 15:00, 09-09 06:00), 재개 2회, 재개 시 컨텍스트 압축 1회(대화 58만 토큰 > 20만 한도).
- 검색 횟수·메인 토큰: 미상 — 에이전트가 보고 전에 종료. 다음 런은 단계마다 decision-log에 누적 기록하도록.
