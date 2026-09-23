# 테스트 설계 — PiCam Watch
버전: v1.0 · 기준 03 v1.2
개정: R2(운영 상수 추가)는 이 문서에 영향 없음 — 기준 버전만 갱신
스킬: `ecc:tdd-workflow`(RED→GREEN→리팩터, 피라미드) · `ecc:e2e-testing`(POM, 격리, flaky 격리). 충돌: tdd-workflow의 "80% 일률"은 채택하지 않고 **리스크 기반** 목표(말미)를 쓴다.

**근거 (진입 사전조사, 검색 1회)**
- 정량 — Playwright: "By default failing tests are not retried" (`retries` 설정), "'flaky' — tests that failed on the first run, but passed when retried" (playwright.dev/docs/test-retries, 2026-09-08). → 이 스위트는 CI에서 `retries: 1`만 허용하고 flaky로 분류된 테스트는 격리(`test.fixme` + 이슈)한다. 재시도로 통과한 테스트는 통과로 세지 않는다.
- 정성 — WebRTC·MQTT 같은 비동기 경로는 "arbitrary timeout"이 flaky의 주범(e2e-testing 스킬) → 모든 대기는 이벤트·응답 조건(`waitForResponse`, WS 메시지, MQTT resp)으로만.
- 사용자 영향 — SC-001·003(지연)은 목킹으로 증명할 수 없다. **실기(Pi 5 + 실제 ONVIF 카메라 1대 + NAT 뒤)** 계층을 별도 두고, 착수 자산(07)에 그 장비 목록을 포함한다. 운영자가 보는 "1초"는 실기에서만 참이다.

## 레이어 정의
| 레이어 | 대상 | 러너 | 목킹 |
|---|---|---|---|
| 단위 | 순수 로직: 쿨다운 병합, 큐 상한, 잠금, 커서, 상수 로딩, Problem 직렬화 | pytest / vitest | 없음 |
| 통합 | api ↔ PostgreSQL ↔ mosquitto ↔ 가짜 agent · agent ↔ 가짜 ONVIF 카메라 ↔ go2rtc | pytest + docker compose(test 프로파일) | **ONVIF 시뮬레이터**(Python SOAP 스텁: GetCapabilities·PullPoint·PTZ) + **MediaMTX가 테스트 MP4를 RTSP로 송출**(H.264/H.265 프로파일 전환 가능) · 웹 푸시는 VAPID 수신 스텁 |
| 계약 | A5 엔드포인트 표·Problem slug·WS·MQTT 스키마 | schemathesis(OpenAPI) + pytest | 통합 환경 재사용 |
| E2E | 브라우저 여정 (Chromium·Mobile Chrome, Playwright POM) | Playwright | 실 서버 + 가짜 agent(WebRTC는 Pi 없이 go2rtc 컨테이너가 테스트 스트림 송출) |
| 실기 | 지연·복구·SD 쓰기·전원 차단 | 스크립트 + 사람 | Pi 5 + 카메라 1대 + 시험용 NAT 공유기 + LTE 폰 |

## 수용 기준 → 시나리오 변환표
전부 **실패하는 테스트로 먼저 쓴다**(RED). 정상 경로 + 경계값 + 실패 경로(03 엣지케이스 표) 포함.

### 성공 기준 (SC — 03 v1.1, 15건 전부)
| SC/FR ID | Gherkin 시나리오 (Given/When/Then) | 레이어 | 데이터/목킹 |
|---|---|---|---|
| SC-001 | Given Pi 5 + 카메라(sub 프로파일) + NAT 뒤, 카메라 앞 밀리초 시계 / When Wi-Fi 15회·LTE 15회 라이브 시청 중 화면 캡처 / Then 화면 시계와 실제 시계 차의 p95 ≤ LIVE_LATENCY_P95 | 실기 | 밀리초 LED 시계, 캡처 스크립트 |
| SC-002 | Given 같은 환경 / When 카메라 선택 후 `performance.now` 첫 프레임(`loadeddata`)까지 30회(STUN 차단으로 TURN 폴백 10회 포함) / Then p95 ≤ LIVE_FIRST_FRAME_P95 | 실기 + E2E 계측 | 브라우저 성능 로그 |
| SC-003 | Given PTZ 카메라 / When 화면 녹화 중 방향 버튼 30회 / Then 버튼 시각↔영상 변화 시작 p95 ≤ PTZ_CMD_LATENCY_P95 | 실기 | 화면 녹화 프레임 분석 |
| SC-004 | Given 기기 online / When 전원 차단 10회 / Then 각 회 DEVICE_OFFLINE_DETECT_SEC 안에 E13 status=offline + 푸시 수신 (10/10) | 실기 + 통합(LWT는 가짜 agent 강제 종료로 재현) | 푸시 수신 스텁 타임스탬프 |
| SC-005 | Given 카메라 앞 사람 이동 30회 / When 모션 이벤트 / Then 푸시 도착 p95 ≤ ALERT_DELIVERY_P95 | 실기 + 통합(ONVIF 시뮬레이터가 MotionAlarm 발화) | 발화 시각 vs 푸시 시각 |
| SC-006 | Given 이벤트 3건의 started_at을 CLIP_RETENTION_DAYS + 1일 과거로 조작 / When purge-job 1회 + Pi 파기 잡 1회 / Then PURGE_MAX_DELAY_HOURS 안에 행 DELETE·썸네일·클립 파일 삭제, AUDIT_LOG action=purge 3행, `purge.report` 대사 일치 100% | 통합 | 시계 조작(freezegun), 파일 시스템 검사 |
| SC-007 | Given Viewer 세션 / When 라이브 시작·종료, PTZ 10회, 스냅샷, 클립 조회·다운로드 = 50행위 스크립트 / Then AUDIT_LOG에 50행 존재, action·target 일치 100% | 통합 | 행위 로그 vs DB 대조 |
| SC-008 | Given agent 실행 중 / When `kill -9` 10회 / Then 60초 안에 라이브 복귀(첫 프레임). Given 커널 hang(`echo c > /proc/sysrq-trigger`) 3회 / Then WATCHDOG_TIMEOUT_SEC + 부팅 시간 안에 state online | 실기 | HW watchdog 활성 이미지 |
| SC-009 | Given 기기 online / When 방화벽으로 서버 차단 1시간 동안 시뮬레이터가 이벤트 100건 발화 / Then 차단 해제 후 100건이 ULID 순서로 EVENT에 존재, 중복 0, 유실 0 (3회) | 통합(가짜 agent + 실 outbox 코드) + 실기 1회 | iptables 스크립트 |
| SC-010 | Given 헬스체크가 실패하는 결함 이미지 release / When E17 배포 3회 / Then 각 회 OTA_ROLLBACK_MAX_MIN 안에 이전 digest로 복귀, DEPLOYMENT.status=rolled_back, 라이브 복귀, 재시도는 OTA_RETRY_MAX 이하 | 실기 + 통합(updater 단위: digest 교체·롤백 상태기계) | 결함 이미지 태그 |
| SC-011 | Given 사이트에 VIEWER_MAX 세션 활성 / When 1개 추가 요청 / Then 429 `viewer-limit`, 기존 세션 프레임 드롭 0(WebRTC stats `framesDropped` 차분) | 통합 + E2E | 동시 접속 스크립트 |
| SC-012 | Given 유효 세션 없음 / When 스트림 시그널링 URL·클립 URL·썸네일 URL 접근 / Then 401/403 100%. Given 기기 A 인증서 / When `devices/B/state` 발행 / Then 브로커 거부(ACL) 100% | 통합 | 인증서 2세트 |
| SC-013 | Given Pi 24시간 운영(이벤트 EVENTS_PER_CAM_DAY_MAX의 10% 발화) / When `/proc/diskstats` SD 섹터 차분 / Then 쓰기 ≤ SD_WRITE_MAX_MB_DAY | 실기 | 24h 스크립트 |
| SC-014 | Given 로그인 상태 / When 카메라 선택 → 라이브 / Then 결정 지점 ≤ 2(플로우 카운트, 화면 전환 이벤트 로그), 모든 클릭 후 시각 피드백 ≤ UI_FEEDBACK_MS(Performance API mark) | E2E | Playwright 트레이스 |
| SC-015 | Given STUN 차단으로 릴레이 강제 / When 릴레이 합계가 TURN_RELAY_MAX_MBPS에 도달한 뒤 새 릴레이 세션 요청 / Then 429 `viewer-limit` reason=relay-cap, 기존 세션 프레임 드롭 0, 모든 릴레이 세션 stream_effective=sub | 통합(coturn 컨테이너 + 대역 카운터 목킹) + 실기 1회 | 대역 스텁, WebRTC stats |

### P0·P1 요구사항 (FR — P2인 FR-019·028 제외 28건 전부)
| SC/FR ID | Gherkin 시나리오 (Given/When/Then) | 레이어 | 데이터/목킹 |
|---|---|---|---|
| FR-001 | Given 이미지의 클레임 토큰 / When E11 claim + CSR / Then 201 cert_pem, DEVICE.status=claimed; Given 같은 토큰 재사용 / Then 409 `claim-token-used` + Admin 알림(엣지 16); Given Admin E14 승인 / Then approved, 이후 MQTT 접속 시 online | 통합 | 내부 CA 테스트 키 |
| FR-002 | Given ONVIF 시뮬레이터 2대(H.264+H.265 / H.265만) / When E20 탐색 → E22 등록 / Then 1대 201(capabilities에 streams·ptz 저장), 1대 422 `no-h264-stream` + 안내 문구; Given 잘못된 자격증명 / Then 422 `camera-auth-failed` | 통합 | ONVIF 시뮬레이터 프로파일 전환 |
| FR-003 | Given 카메라 connected / When E27 stream=sub → E29 offer / Then answer 수신, 미디어 재생(E2E: `<video>` readyState≥2); Given LIVE_SESSION_MAX_MIN 경과(시계 조작) / Then 세션 종료 + "다시 보기" CTA(엣지 18) | 통합 + E2E | go2rtc 컨테이너 테스트 스트림 |
| FR-004 | Given E27 토큰 / When STREAM_TOKEN_TTL_SEC 경과 후 시그널링 / Then 401; Given 토큰 없음 / Then 401 (SC-012 공유) | 통합 | 시계 조작 |
| FR-005 | SC-011과 동일 + 응답 본문 `current_viewers` = VIEWER_MAX | 통합 | — |
| FR-006 | Given STUN 차단 / When 라이브 / Then ICE 후보 relay, LIVE_SESSION.relayed=true, "릴레이 연결" 배지; Given 릴레이 중 main 요청 / Then stream_effective=sub 통지 + 배지(엣지 17); 상한은 SC-015 | 통합 + E2E | coturn 컨테이너, iptables |
| FR-007 | Given PTZ 카메라 / When E30 move → E31 stop / Then 시뮬레이터가 ContinuousMove·Stop 수신 순서대로; Given Viewer A 조작 중 / When B가 PTZ_LOCK_SEC 안 E30 / Then 423 `ptz-locked` holder=A(엣지 5); Given PTZ 없는 카메라 / Then 422 `ptz-unsupported`, E2E: 컨트롤 비노출(엣지 6) | 통합 + E2E | 시뮬레이터 호출 기록 |
| FR-008 | Given PTZ 카메라 / When E33 저장 → E32 목록 → E34 goto → E35 삭제 / Then 시뮬레이터 프리셋 상태 일치, 삭제 후 목록에서 제거 | 통합 | — |
| FR-009 | When E36 / Then 202 → E21 done, result.url로 JPEG 다운로드 200, EVENT.type=snapshot, AUDIT_LOG snapshot | 통합 | 시뮬레이터 스냅샷 JPEG |
| FR-010 | Given 같은 Idempotency-Key로 E30 2회 / Then 응답 동일, 시뮬레이터 ContinuousMove 1회(엣지 8); Given 같은 키·다른 본문 / Then 422; Given 기기 offline / When E30 / Then 409 `device-offline`, COMMAND 행 없음(큐잉 안 함, 엣지 7) | 통합 | 가짜 agent 끊기 |
| FR-011 | Given 30초 안 MotionAlarm 5회 / Then EVENT 1건, ended_at = 마지막 모션 + CLIP_POST_SEC(엣지 9); Given EVENT_COOLDOWN_SEC 넘어 2회 / Then 2건 | 단위(병합 로직) + 통합 | 시뮬레이터 발화 스크립트 |
| FR-012 | Given 링버퍼에 CLIP_PRE_SEC 이상 세그먼트 / When 이벤트 / Then 클립 길이 = CLIP_PRE_SEC + CLIP_POST_SEC ± RINGBUF_SEGMENT_SEC, `ffprobe` 코덱 copy(재인코딩 없음, INV-4), D01 썸네일 ≤ THUMB_MAX_KB, `events` clip_ready에 sha256; Given USB SSD 미마운트 / Then clip_failed + 디스크 경고 1회(엣지 10) | 통합(agent + go2rtc + MediaMTX) | 마운트 해제 스크립트 |
| FR-013 | When E39 커서 2페이지 / Then 중복·누락 0; When E42 첫 요청 / Then 202 clip-uploading, 동시 요청 5개 → `clip.upload` 명령 1회(singleflight); 업로드 완료 후 E42 → 200, E43 Range 206; Given 재생 중 파기(엣지 11) / Then 재생 세션 EOF, 새 요청 410 `clip-purged` | 통합 + 계약 | 가짜 agent 업로드 지연 주입 |
| FR-014 | SC-006과 동일 + INV-1: 어떤 시각에도 `started_at < now - CLIP_RETENTION_DAYS - PURGE_MAX_DELAY_HOURS`인 미파기 행 0 (속성 테스트, 시계 전진 100회) | 단위(속성) + 통합 | hypothesis |
| FR-015 | Given 클립 디스크 여유 < CLIP_DISK_RESERVE_PCT / When 새 이벤트 / Then 가장 오래된 클립부터 삭제, `disk_low` 이벤트 + 경고 푸시 1회, 반복 억제 | 통합(agent, tmpfs 작은 디스크) | 작은 루프 디바이스 |
| FR-016 | When E37 duration=MANUAL_REC_MAX_MIN×60 / Then 202, EVENT.type=manual, 길이 상한 준수; duration 초과 → 422; E38로 조기 종료 | 통합 | — |
| FR-017 | Given 푸시 구독(E45) / When 이벤트·offline·disk_low / Then VAPID 수신 스텁에 각 1건, 본문에 영상·썸네일·비밀번호 없음(프라이버시) | 통합 | 푸시 스텁 |
| FR-018 | Given 하루 알림 ALERT_DAILY_MAX건 발송됨 / When 추가 이벤트 / Then 개별 푸시 대신 요약 1건, 다음 날 리셋 | 단위(카운터) + 통합 | 시계 조작 |
| FR-020 | Given 가짜 agent 연결 / When 강제 종료 / Then LWT로 DEVICE_OFFLINE_DETECT_SEC 안에 offline, WS `device.state`; When STATE_REFRESH_SEC마다 state 발행 / Then E13 metrics 갱신 | 통합 | mosquitto 컨테이너 |
| FR-021 | Given 카메라 connected / When MediaMTX 중단 / Then CAM_RECONNECT_BACKOFF 간격(1→2→4…, 상한) 재접속 시도 로그, `camera_disconnected` 이벤트 1건, WS `camera.state`; When 복구 / Then `camera_reconnected` | 통합(agent) | MediaMTX 정지/재개 |
| FR-022 | SC-009 + 경계: OFFLINE_QUEUE_MAX + 1건 적재 시 가장 오래된 1건 폐기, 나머지 순서 유지 | 단위(outbox) + 통합 | — |
| FR-023 | SC-010 + 정상 경로: 서명 유효 이미지 → healthy, previous_digest 기록; 서명 불일치 → failed, 교체 없음 | 통합(updater) | cosign 테스트 키 |
| FR-024 | When E16 target=agent / Then `cmd/restart` 수신 → agent 재시작 → state online 재발행; target=os는 실기 | 통합 + 실기 | — |
| FR-025 | When E01 올바른/틀린 비밀번호 / Then 200 Set-Cookie(HttpOnly·Secure·SameSite=Strict) / 401; Viewer가 E09·E49 호출 / Then 403; 비밀번호 < PASSWORD_MIN_LEN / Then 422 | 계약 + 통합 | — |
| FR-026 | SC-007 + INV-2: AUDIT_LOG INSERT 실패 주입 시 E27 토큰 미발급(500, LIVE_SESSION 행 없음) | 통합(장애 주입) | DB 트리거로 실패 유도 |
| FR-027 | When E09 signage 저장 → E10 / Then PDF에 목적·장소·범위·시간·연락처 5항목 텍스트 존재 | 통합 | PDF 텍스트 추출 |
| FR-029 | Given 마지막 AUDIT_REVIEW + AUDIT_REVIEW_INTERVAL_DAYS 경과 / When 스케줄 실행 / Then Admin 리마인더 푸시 1건; E50 기록 후 재발송 없음 | 통합 | 시계 조작 |
| FR-030 | Given 인증서 만료 DEVICE_CERT_RENEW_BEFORE_DAYS 전 / When D04 / Then 200 새 cert(DEVICE_CERT_VALID_DAYS), cert_fingerprint 교체, 구 인증서로 MQTT 접속 거부; Given 만료 인증서 / When D04 / Then 401 + Admin 알림 | 통합 | CA 테스트 키, 시계 조작 |

## 계약 테스트 (A5 엔드포인트 표 기준)
- **스키마**: OpenAPI 스케치를 schemathesis로 퍼징 — E01~E50·D01~D04 전부 응답이 스키마·`application/vnd.picam.v1+json`에 맞는지, 오류는 `application/problem+json` + slug 표의 type URI만.
- **에러 포맷**: 모든 4xx/5xx에 `type·title·status·detail·instance` 존재, `detail`에 스택·SQL·경로 없음(정규식), 500은 고정 문구.
- **멱등성**: E16·E17·E20·E27·E30·E33·E34·E36·E37·E42 — 같은 키 재시도 = 바이트 동일 응답, 키 없음 = 422, SESSION_TTL_HOURS 경과 후 같은 키 = 새 요청.
- **페이지네이션**: E06·E12·E18·E23·E39·E49 — `limit` 경계(0·1·100·101), 커서 변조 → 422.
- **레이트리밋**: E01·E27·E30에 `RateLimit-*` 헤더, 초과 시 429 + `Retry-After`.
- **WS**: 시그널링 메시지 타입 5종·상태 채널 7종의 JSON 스키마, 미인증 연결 즉시 종료.
- **MQTT**: `state`·`events`·`cmd/*`·`resp/*` 페이로드 JSON 스키마, MQTT_MSG_MAX_KB 초과 발행 → 브로커 연결 종료, ACL `devices/%u/#` 밖 발행·구독 거부.

## E2E 후보 (돈·안전·법이 걸린 여정만)
1. **프로비저닝→승인→라이브** (G1·G2, 안전): 클레임 → Admin 승인 → 카메라 탐색·등록 → 라이브 첫 프레임 → PTZ → 종료. 감사 로그 5행 확인.
2. **이벤트→푸시→클립→파기** (G3, 법): 모션 → 푸시 → 클립 재생(202→200) → 시계 전진 → 파기 확인 → 410.
3. **권한·프라이버시** (법): Viewer 로그인 → 관리 API 403 → 서명 없는 클립 URL 403 → 로그아웃 후 라이브 401.
프로젝트: `chromium` + `mobile-chrome`(Q1). `retries: 1`(CI), flaky는 `test.fixme` + 이슈로 격리, `--repeat-each=10`으로 승격 전 검증.

## 리스크 기반 커버리지 목표
| 영역 | 목표 | 왜 |
|---|---|---|
| P0 경로(FR-001~004·007·010~014·017·020~022·025·026) 분기 | ≥ 90% | MVP 성립 조건 |
| 위협모델 상위 리스크 R-1(TURN 릴레이)·R-2(법 상수 파기·감사) 코드 | 100% 시나리오 + 실기 1회 | 틀리면 서비스 불가/법 위반 |
| INV-1~7 | 속성 테스트(hypothesis) 각 1개 이상 | 불변식은 예시가 아니라 속성으로 증명 |
| P1 경로 | ≥ 70% | — |
| UI 컴포넌트·P2 | 최소(스모크) | 리스크 낮음 |
| 실기 계층 | SC-001·002·003·004·008·013 + SC-009·010·015 각 1회 = 릴리스 게이트 | 목킹으로 증명 불가 |
| flaky 허용 | 0 (격리 후 수정 전 병합 금지) | 비동기 경로가 핵심 |
