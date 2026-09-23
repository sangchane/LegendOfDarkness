# PRD — PiCam Watch
버전: v1.2
개정: R1 반영 완료 (REVISIONS.md — A4 독립 검토 16건) · R2 반영 완료 (운영 상수 17개, A7)
스킬: `ecc:product-capability`(역량 진술·불변식·상태 전이 흡수) · `frontend-design-taste`(UI dial) · 참조 `ux-principles-kr.md`

**근거 (진입 사전조사, 검색 3회)**
- 정량 — WebRTC glass-to-glass 200~500ms, LL-HLS 2~5초, HLS 15~30초 (mux.com / videosdk.live / forasoft.com, 2026-09-08). "실시간"의 유일한 1초 이내 후보는 WebRTC.
- 정성 — 사용자 불만: "live view pauses and then 'catches up'", "the security camera lags 10–20 seconds behind" real time; 벤더 지원 문서는 원격 시청에 "switch each camera from 'Clear/Main' to 'Fluent/Sub' stream"을 권한다 (whizz-experts.com 지원 문서, 2025-12-10, 확인 2026-09-08). → 서브스트림 기본값(Q1)과 지연 상수의 근거.
- 사용자 영향 — 운영자는 폰에서 카메라를 누르면 1초 안에 움직이는 화면을 본다. 지연·정지 상태를 화면이 숨기지 않는다(stale 표시). 원칙 L-10(0.4초 피드백)·L-06(막다른 에러 0)·T-08(CTA는 다음 행동 그대로).

## 배경 (RECON 요약)
소상공인 시설의 IP 카메라는 제조사 앱·통신사 결합상품·포트포워딩에 묶여 있고, 원격 시청은 클라우드 릴레이 지연이 크다. 2026년 시점 ONVIF는 Profile T가 표준(Profile S는 2027-03 종료), PTZ는 ONVIF PTZ Service로 표준화돼 벤더 종속 없이 제어할 수 있다. Raspberry Pi 5는 HW H.264 인코더가 없어 **재인코딩 없는 패스스루**(go2rtc)가 설계 축이며, 온보드 RTC로 시계 문제를 HW로 푼다. 국내 개인정보보호법 제25조(안내판·안전조치)와 보존기간 30일 권고(가능성, 원문 재확인 필요)·접속기록 1년 보관(가능성)이 기본 요구사항이다. 상세와 출처: `01-recon.md`.

## 역량 진술 (product-capability CAPABILITY)
소규모 시설 운영자가, 이미 설치된 ONVIF IP 카메라를 라즈베리파이 게이트웨이 1대에 연결하면, 어디서든 스마트폰 브라우저로 **1초 이내 라이브를 보고 PTZ를 움직이고**, 움직임 이벤트 클립을 푸시로 받아 확인하며, **보존기간 파기·접근 기록·안내판 정보가 기본값으로 처리**되어 법 준수를 따로 챙기지 않아도 되는 상태가 된다.

## 제품 목표 — 3개, 직교
| ID | 목표 | 측정 (SC) |
|---|---|---|
| G1 | **원격 실시간 확인·제어** — 브라우저에서 LIVE_LATENCY_P95 안에 보고 PTZ_CMD_LATENCY_P95 안에 움직인다 | SC-001·002·003·011·014 |
| G2 | **무인 운영 신뢰성** — Pi가 사람 손 없이 돌고, 끊기면 알리고, 스스로 복구하고, 업데이트에 실패해도 되돌아온다 | SC-004·008·009·010·013 |
| G3 | **법 준수 내장** — 보존기간 자동 파기·접근 기록·안내판 정보가 기본값 | SC-006·007·012 |

## 유저 스토리 — P1만으로 MVP 성립
| ID | 우선순위 | 스토리 | 독립 테스트 |
|---|---|---|---|
| US-1 | P1 | As a 운영자, I want 폰 브라우저에서 카메라를 눌러 1초 안에 라이브를 보고 싶다, so that 외출 중에도 매장 상황을 확인한다 | SC-001·002 |
| US-2 | P1 | As a 운영자, I want 화면에서 PTZ 카메라를 움직이고 프리셋으로 이동하고 싶다, so that 보고 싶은 곳을 본다 | SC-003 |
| US-3 | P1 | As a 운영자, I want 움직임이 감지되면 푸시를 받고 클립을 바로 보고 싶다, so that 계속 지켜보지 않아도 된다 | SC-005·006 |
| US-4 | P1 | As a 운영자, I want Pi나 카메라가 끊기면 알고 싶다, so that 녹화 공백을 인지하고 조치한다 | SC-004 |
| US-5 | P2 | As a 운영자, I want 직원에게 시청 권한만 주고 누가 언제 봤는지 기록을 보고 싶다, so that 책임을 추적한다 | SC-007 |

## 요구사항 풀
| ID | 요구사항 (EARS) | 우선순위 | 출처 |
|---|---|---|---|
| FR-001 | 기기가 처음 켜질 때, 시스템은 이미지의 1회용 클레임 토큰으로 기기 인증서(X.509)를 발급하고 Admin 승인 전까지 `claimed` 상태로 둔다 | P0 | P1 프로비저닝, DL-004 |
| FR-002 | Admin이 카메라 탐색을 요청할 때, 기기는 WS-Discovery로 LAN의 ONVIF 카메라를 나열하고, 자격증명 입력 후 capability(PTZ·프리셋·이벤트·스트림 프로파일)를 저장한다. H.264 스트림이 없으면 등록을 거부하고 카메라 설정 안내를 표시한다 | P0 | US-1, Q4, INV-6 |
| FR-003 | Viewer가 카메라를 선택할 때, 시스템은 WebRTC로 서브스트림을 재생하고 메인스트림 전환을 제공한다 (LIVE_LATENCY_P95, LIVE_FIRST_FRAME_P95). 세션은 LIVE_SESSION_MAX_MIN에 자동 종료되고 "다시 보기" CTA를 보인다 | P0 | US-1, Q1 |
| FR-004 | 라이브·클립 재생을 시작할 때, 시스템은 STREAM_TOKEN_TTL_SEC 유효 서명 토큰을 발급하고 토큰 없는 요청은 거부한다 | P0 | STRIDE I |
| FR-005 | 사이트의 동시 시청 세션이 VIEWER_MAX에 도달했을 때, 시스템은 추가 요청을 429로 거절하고 "다른 시청자가 보는 중"을 안내한다 | P1 | STRIDE D |
| FR-006 | P2P ICE 연결이 실패할 때, 시스템은 TURN 릴레이로 폴백한다. 릴레이 경로에서는 서브스트림으로 고정하고(메인 요청은 강등 + "릴레이 연결" 배지), 릴레이 합계가 TURN_RELAY_MAX_MBPS에 도달하면 새 릴레이 세션을 `viewer-limit`로 거절한다 | P1 | P1 영상 스트림, R1 |
| FR-007 | Viewer가 PTZ 방향 버튼을 누르고 있을 때, 시스템은 ONVIF ContinuousMove를 보내고 떼면 Stop을 보낸다 (PTZ_CMD_LATENCY_P95). 다른 사용자가 PTZ_LOCK_SEC 안에 조작 중이면 잠금 안내를 표시한다 | P0 | US-2, INV-5 |
| FR-008 | Admin이 프리셋을 저장·이동·삭제할 때, 시스템은 ONVIF 프리셋 서비스에 반영하고 목록을 갱신한다 | P1 | US-2 |
| FR-009 | Viewer가 스냅샷을 요청할 때, 시스템은 카메라 스냅샷(JPEG)을 저장하고 다운로드를 제공한다 | P1 | US-2 |
| FR-010 | 클라이언트가 제어 명령을 보낼 때, 시스템은 Idempotency-Key로 중복을 제거하고, 기기가 오프라인이면 큐잉하지 않고 즉시 실패로 응답한다 | P0 | 축 6 |
| FR-011 | 카메라가 ONVIF 모션 이벤트를 보낼 때, 기기는 이벤트를 생성하되 같은 카메라의 EVENT_COOLDOWN_SEC 안 중복은 하나로 합친다 | P0 | US-3 |
| FR-012 | 이벤트가 생성될 때, 기기는 CLIP_PRE_SEC 전부터 CLIP_POST_SEC 후까지 클립을 USB SSD에 저장하고 썸네일·메타를 서버에 올린다 | P0 | US-3, Q3 |
| FR-013 | Viewer가 클립 목록을 열 때, 시스템은 커서 페이지네이션으로 나열하고 선택한 클립을 기기에서 스트리밍 재생·다운로드한다 | P0 | US-3 |
| FR-014 | 클립·썸네일·이벤트가 CLIP_RETENTION_DAYS를 넘길 때, 시스템은 PURGE_MAX_DELAY_HOURS 안에 파기하고 파기 기록을 남긴다 | P0 | G3, INV-1 |
| FR-015 | 클립 디스크 여유가 CLIP_DISK_RESERVE_PCT 미만일 때, 기기는 가장 오래된 클립부터 삭제하고 "보존기간 미달 삭제" 경고를 보낸다 | P1 | P1 SD 마모 |
| FR-016 | Viewer가 수동 녹화를 시작할 때, 시스템은 MANUAL_REC_MAX_MIN까지 녹화하고 클립으로 저장한다 | P1 | US-3 |
| FR-017 | 이벤트·기기 오프라인·디스크 경고가 발생할 때, 시스템은 구독한 브라우저에 웹 푸시(VAPID)를 보낸다 (ALERT_DELIVERY_P95) | P0 | US-3·4 |
| FR-018 | 사이트의 하루 알림이 ALERT_DAILY_MAX를 넘을 때, 시스템은 개별 알림 대신 요약 1건으로 묶는다 | P1 | P3 알람 폭주 |
| FR-019 | 사용자가 이메일 알림을 켰을 때, 시스템은 푸시와 같은 조건으로 이메일을 보낸다 | P2 | 축 5 |
| FR-020 | 기기의 MQTT 연결이 끊길 때, 서버는 LWT로 DEVICE_OFFLINE_DETECT_SEC 안에 `offline`으로 바꾸고 알림한다. 기기는 STATE_REFRESH_SEC마다 CPU·온도·디스크·업링크를 보고한다 | P0 | US-4 |
| FR-021 | 카메라 RTSP/ONVIF 연결이 끊길 때, 기기는 CAM_RECONNECT_BACKOFF로 재접속하고 끊김·복구 이벤트를 만든다 | P0 | US-4 |
| FR-022 | 서버 연결이 없을 때, 기기는 이벤트·상태를 로컬 큐(OFFLINE_QUEUE_MAX)에 쌓고 재접속 후 순서대로 보낸다. 상한 초과 시 가장 오래된 것부터 버린다 | P0 | P1 연결 끊김 |
| FR-023 | Admin이 버전을 지정해 배포할 때, 기기는 서명 검증된 컨테이너 이미지를 받아 교체하고 헬스체크 실패 시 이전 digest로 자동 롤백한다 | P1 | P1 OTA, Q5 |
| FR-024 | Admin이 원격 재시작을 요청할 때, 기기는 에이전트 또는 OS를 재시작하고 결과를 보고한다 | P1 | P1 자가 복구 |
| FR-025 | 사용자가 로그인할 때, 시스템은 이메일+비밀번호(argon2id)를 검증하고 세션 쿠키(HttpOnly, SESSION_TTL_HOURS)를 발급한다. 역할은 Admin/Viewer, 권한은 스코프 `<모듈>:<자원>:<행위>` | P0 | STRIDE S·E |
| FR-026 | 로그인·라이브 시청 시작/종료·PTZ·스냅샷·클립 조회/다운로드/삭제·설정 변경이 일어날 때, 시스템은 감사 로그를 남기고 AUDIT_RETENTION_DAYS 보관하며 Admin이 조회한다 | P0 | G3, INV-2 |
| FR-027 | Admin이 사이트를 설정할 때, 시스템은 안내판 정보(설치 목적·장소·촬영 범위·시간·관리책임자 연락처)와 운영·관리 방침 텍스트를 저장하고 인쇄용으로 출력한다 | P1 | 개인정보보호법 제25조 |
| FR-028 | Admin이 Viewer를 초대할 때, 시스템은 초대 링크를 보내고 사이트 단위 시청 권한만 부여한다 | P2 | US-5 |
| FR-029 | AUDIT_REVIEW_INTERVAL_DAYS가 지날 때, 시스템은 Admin에게 감사 로그 점검 리마인더를 보내고 점검 완료를 기록한다 | P1 | 안전성 확보조치(가능성) |
| FR-030 | 기기 인증서 만료 DEVICE_CERT_RENEW_BEFORE_DAYS 전이 될 때, 기기는 현재 인증서로 갱신을 요청하고 서버 ca 모듈은 DEVICE_CERT_VALID_DAYS 유효 인증서를 재발급한다. 갱신 실패는 Admin 알림이다 | P1 | P1 디바이스 신원, R1 |

## 불변식 (product-capability CONSTRAINTS)
| ID | 불변식 | 강제 위치 |
|---|---|---|
| INV-1 | 클립·썸네일·이벤트 메타는 생성 후 CLIP_RETENTION_DAYS + PURGE_MAX_DELAY_HOURS를 넘겨 존재하지 않는다. 파기 작업 실패는 알림이다 | 기기 파기 잡 + 서버 대사 |
| INV-2 | 라이브 시청·클립 접근은 감사 로그 INSERT와 같은 트랜잭션에서만 토큰이 발급된다 (로그 실패 = 접근 거부) | 서버 토큰 발급 |
| INV-3 | 기기는 자기 `devices/{device_id}/#` 토픽 밖으로 발행·구독하지 않는다 (사이트 매핑은 서버가 안다 — mosquitto 정적 ACL `pattern readwrite devices/%u/#`) | MQTT ACL |
| INV-4 | Pi는 영상을 재인코딩하지 않는다 — 모든 경로가 패스스루 | go2rtc 설정·코드 리뷰 |
| INV-5 | 카메라당 동시 PTZ 조작자는 1명 (PTZ_LOCK_SEC 잠금) | 서버 잠금 |
| INV-6 | 등록된 카메라는 H.264 스트림 프로파일을 최소 1개 갖는다 | FR-002 등록 검증 |
| INV-7 | 모든 테이블은 `workspace_id`를 갖는다 (MVP는 단일 workspace) — 테넌시 후속 확장 hedge | 05 DDL |

## 상태·전이
- **Device**: `claimed` →(Admin 승인) `approved` → `online` ⇄(LWT) `offline` →(Admin) `retired`
- **Camera**: `discovered` →(자격증명·capability 검증) `registered` → `connected` ⇄(RTSP 끊김) `disconnected` →(Admin) `removed`
- **Event**: `detected` →(클립 저장) `clip_ready` →(보존 만료) `purged`; 클립 저장 실패 시 `clip_failed`(썸네일만)
- **Command**: `accepted` → `executing` → `done` | `failed`(기기 오프라인·ONVIF 오류·잠금)

## 엣지케이스 (Given/When/Then)
**라이브**
1. Given 카메라가 H.265 메인·H.264 서브만 제공 / When Viewer가 메인 전환 / Then "이 카메라의 고화질은 브라우저에서 재생할 수 없어요" 안내 + 서브 유지 (L-06)
2. Given P2P ICE 실패 / When TURN 폴백 / Then LIVE_FIRST_FRAME_P95 안에 재생, 화면에 "릴레이 연결" 배지
3. Given 라이브 중 프레임이 STALE_AFTER_SEC 동안 멈춤 / When 감지 / Then 마지막 프레임 위에 stale 배지 + 자동 재연결, 실패 시 "카메라 연결이 끊겼어요 — 다시 시도" 버튼
4. Given VIEWER_MAX 도달 / When 새 Viewer 요청 / Then 429 + 현재 시청자 수 표시, 기존 세션 영향 없음

**PTZ**
5. Given Viewer A가 조작 중 / When Viewer B가 PTZ_LOCK_SEC 안에 조작 / Then B에게 "A가 조작 중 (N초)" 표시, 명령 거절
6. Given 카메라가 PTZ capability 없음 / When 라이브 화면 / Then PTZ 컨트롤 비노출 (빈 컨트롤 표시 금지)
7. Given 기기 오프라인 / When PTZ 명령 / Then 즉시 `failed`(device_offline), 큐잉 없음
8. Given 같은 Idempotency-Key로 재요청 / When 처리 / Then 최초 응답 재생, 카메라에 명령 중복 전송 없음

**이벤트·클립**
9. Given 30초 안에 모션 5회 / When 이벤트 생성 / Then 이벤트 1건, 클립은 마지막 모션 + CLIP_POST_SEC까지 연장
10. Given USB SSD 미장착 또는 마운트 실패 / When 이벤트 / Then 클립 `clip_failed`, 썸네일만 저장, Admin에게 디스크 경고 1회(반복 억제)
11. Given 보존 만료 클립이 재생 중 / When 파기 잡 실행 / Then 파기는 진행, 재생 세션은 EOF로 종료
12. Given 기기 시계가 NTP 동기 전 / When 이벤트 발생 / Then 이벤트에 `clock_synced=false`, 서버 수신 시각으로 보정 표시

**기기·서버**
13. Given 서버 1시간 다운 / When 복구 / Then 기기 큐의 이벤트가 순서대로 도착, 중복 없음(이벤트 ULID 기준)
14. Given 새 이미지가 헬스체크 실패 / When 롤백 / Then 이전 digest로 복귀 ≤ OTA_ROLLBACK_MAX_MIN, 배포 상태 `rolled_back` + 알림
15. Given 사이트에 카메라 0대 / When 라이브 탭 / Then 빈 상태 화면 "카메라를 찾아 등록하기" CTA (T-08)
16. Given 클레임 토큰 재사용 / When 두 번째 기기가 접속 / Then 거부 + Admin 알림 (STRIDE S)

**라이브 (R1 추가)**
17. Given ICE가 TURN 릴레이로 붙음 / When Viewer가 메인스트림 요청 / Then 서브스트림으로 강등 + "릴레이 연결 — 기본 화질" 배지, 오류 아님
18. Given 라이브 세션이 LIVE_SESSION_MAX_MIN 도달 / When 만료 / Then 스트림 종료 + "다시 보기" CTA, 감사 로그 종료 기록; 릴레이 합계가 TURN_RELAY_MAX_MBPS면 새 릴레이 세션은 429 `viewer-limit`(직결 세션은 허용)

## 성공 기준
| ID | 기준 (pass/fail) | 측정 방법 |
|---|---|---|
| SC-001 | 라이브 glass-to-glass 지연 p95 ≤ LIVE_LATENCY_P95 | 카메라 앞 밀리초 시계를 촬영, 화면 캡처와 대조 30회 (Wi-Fi·LTE 각 15회) |
| SC-002 | 카메라 선택 → 첫 프레임 p95 ≤ LIVE_FIRST_FRAME_P95 | 브라우저 performance.now 로그 30회 (TURN 폴백 포함 10회) |
| SC-003 | PTZ 버튼 → 카메라 움직임 시작 p95 ≤ PTZ_CMD_LATENCY_P95 | 화면 녹화로 버튼 시각↔영상 변화 30회 |
| SC-004 | Pi 전원 차단 후 DEVICE_OFFLINE_DETECT_SEC 안에 `offline` + 푸시 도착 10/10회 | 전원 차단 테스트 10회 |
| SC-005 | 모션 발생 → 푸시 도착 p95 ≤ ALERT_DELIVERY_P95 | 사람이 카메라 앞 이동 30회, 푸시 수신 시각 기록 |
| SC-006 | 만료 클립·썸네일·이벤트가 PURGE_MAX_DELAY_HOURS 안에 삭제되고 파기 로그 존재 100% | 시계 앞당김 테스트, 파일·DB 대조 |
| SC-007 | 라이브·PTZ·스냅샷·클립 행위 100%가 감사 로그에 존재 | E2E 스크립트 50행위 → 로그 대조 |
| SC-008 | 에이전트 프로세스 kill 후 60초 안에 라이브 복귀; 커널 hang 시뮬 후 WATCHDOG_TIMEOUT_SEC + 부팅 시간 안에 복귀 | kill -9 10회, `echo c > /proc/sysrq-trigger` 3회 |
| SC-009 | 네트워크 차단 1시간 동안 이벤트 100건 → 복구 후 100건 순서대로 도착, 유실 0 | 방화벽 차단 테스트 3회 |
| SC-010 | 고의 결함 이미지 배포 → 이전 digest 복귀 ≤ OTA_ROLLBACK_MAX_MIN, 라이브 복귀 | 결함 이미지 배포 3회 |
| SC-011 | VIEWER_MAX+1번째 요청 429, 기존 세션 프레임 드롭 0 | 동시 접속 스크립트 |
| SC-012 | 토큰 없는 스트림·클립 URL 401/403 100%; 기기 A 인증서로 기기 B 토픽 발행 거부 100% | 보안 테스트 스위트 |
| SC-013 | 24시간 운영 중 SD 카드 쓰기 ≤ SD_WRITE_MAX_MB_DAY | `/proc/diskstats` 섹터 차분 |
| SC-014 | 카메라 선택 → 라이브까지 사용자 결정 지점 ≤ 2, 모든 액션 시각 피드백 ≤ UI_FEEDBACK_MS | 플로우 다이어그램 카운트, 브라우저 성능 로그 |
| SC-015 | 릴레이 세션은 전부 서브스트림; 릴레이 합계가 TURN_RELAY_MAX_MBPS에 도달하면 새 릴레이 세션 429, 기존 세션 프레임 드롭 0 | coturn 릴레이 지표 + 릴레이 강제(STUN 차단) 동시 접속 스크립트 |

## UI 방향
- **dial (frontend-design-taste)**: 제품/앱 UI 프로파일을 모바일 우선으로 — VISUAL_DENSITY 5 · MOTION_INTENSITY 3 · DESIGN_VARIANCE 3. 라이브 화면만 DENSITY 7(영상이 화면을 차지, 컨트롤은 오버레이).
- 모바일 우선(Q1): 1카메라 전체화면이 기본, 사이트 그리드는 썸네일(STATE_REFRESH_SEC 갱신). PC는 같은 컴포넌트를 2열 그리드로.
- 상태 4종 필수: 빈(카메라 0) / 로딩(ICE 연결 중, 스켈레톤) / 에러(복구 버튼 100%, L-06) / stale(프레임 정지 배지).
- 문구: 해요체·능동형·CTA는 다음 행동 그대로 (T-08·T-09·T-10). 숫자는 `font-mono`.
- 측정 기준: UX-01 결정 지점 ≤ 2 (L-03), UX-02 피드백 ≤ UI_FEEDBACK_MS (L-10), UX-03 PTZ 타깃 ≥ 44pt (L-02 · 터치).

## 화면 스케치 — 라이브 (모바일)
```
┌──────────────────────────────┐
│ ‹ 매장 앞      ● 온라인  ⋯   │  ← 헤더: 뒤로, 카메라명, 기기 상태
│                              │
│                              │
│        [ 영상 16:9 ]         │  ← 로딩: 스켈레톤 + "연결하는 중…"
│                              │     stale: 우상단 "3초 전 화면" 배지
│    ⟲ 릴레이 연결   HD ▢      │     에러: "카메라 연결이 끊겼어요" + [다시 연결]
│                              │
├──────────────────────────────┤
│   ▲        [프리셋 ▾]  📷   │  ← PTZ 없는 카메라: 이 줄 비노출
│ ◀  ●  ▶     (스냅샷)        │
│   ▼      ● 녹화  ⏺          │
├──────────────────────────────┤
│ 최근 이벤트                   │
│ ▣ 14:02 움직임  ▣ 13:47 …    │  ← 빈 상태: "아직 감지된 움직임이 없어요"
└──────────────────────────────┘
```

## 범위 밖 (non-goals)
24/7 연속 녹화 · AI 객체 탐지(사람/차량 분류) · 다중 테넌트 판매·과금 · 네이티브 앱 · Raspberry Pi 4 지원 · 서버 고가용성 · 카카오 알림톡/SMS · 카메라 전원(릴레이) 제어 · Pi Camera Module(CSI) 입력 · 폐쇄망 운영 · 양방향 오디오.

## 가정 목록 (register Assumed 요약 — `02-blindspot-register.md`)
- 사용 맥락: 스마트폰 단시간 확인 + 푸시 (Q1) · 배포: 중앙 서버 경유 (Q2) · 녹화: 이벤트 클립, Pi USB SSD (Q3) · 카메라: ONVIF S/T + H.264 (Q4) · 현장: 유인, OS A/B 없음 (Q5)
- 규모: SITES_MAX·CAMERAS_PER_SITE_MAX·VIEWER_MAX (해석 4) · 팀 1~2명, 언어 Python 1개 · 인터넷 필수
- 법: 보존 30일 권고와 접속기록 1년은 **가능성**(원문 재확인 필요) — 값을 상수로 두어 확인 후 한 곳만 바꾼다
- 엣지: Pi 5 4GB · Overlay FS · log2ram · HW watchdog · 기기별 X.509(DEVICE_CERT_VALID_DAYS, 자동 갱신) · 클립은 USB SSD
- 서버: VPS 1대 회선 VPS_LINE_MBPS 가정, 릴레이는 서브스트림 고정 + TURN_RELAY_MAX_MBPS 상한 (R1) · 컨테이너 레지스트리는 외부(GHCR) + cosign

## 용어집
| 용어 | 정의 |
|---|---|
| Site(사이트) | 카메라들이 있는 물리 장소 1곳. Device 1대가 속한다 |
| Device(기기) | 라즈베리파이 게이트웨이. "Pi"와 동의어 |
| Camera(카메라) | ONVIF IP 카메라 1대. Device의 LAN에 있다 |
| Stream | 카메라의 RTSP 프로파일. `main`(고화질) / `sub`(저화질) |
| Preset | ONVIF PTZ 프리셋 위치 |
| Event | 모션·수동·끊김 등 카메라/기기에서 생긴 사건 |
| Clip | Event에 붙는 영상 파일 (Device의 USB SSD) |
| Command | 서버→Device 제어 요청 (PTZ·스냅샷·재시작·배포) |
| Admin / Viewer | 설정·사용자·감사 로그 권한 / 라이브·클립·PTZ 권한 |
| Workspace | 계정 단위(테넌트). MVP는 1개 |

## 상수 표 (단일 출처 — 04~07은 이름으로만 참조)
| 이름 | 값 | 단위 | 근거 |
|---|---|---|---|
| LIVE_LATENCY_P95 | 1.0 | 초 | 출처 URL https://www.mux.com/articles/low-latency-live-streaming-developers-guide-ll-hls-webrtc-cmaf (WebRTC 200~500ms) + 설계 결정 DL-011 (2배 여유) |
| LIVE_FIRST_FRAME_P95 | 3.0 | 초 | 설계 결정 DL-011 (ICE 수집 + TURN 폴백 포함) |
| STALE_AFTER_SEC | 3 | 초 | 설계 결정 DL-011 |
| PTZ_CMD_LATENCY_P95 | 1.0 | 초 | 설계 결정 DL-011 |
| PTZ_LOCK_SEC | 5 | 초 | 설계 결정 DL-011 |
| MQTT_KEEPALIVE_SEC | 20 | 초 | 설계 결정 DL-011 |
| DEVICE_OFFLINE_DETECT_SEC | 60 | 초 | 출처 URL https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/os/mqtt-v3.1.1-os.html (keep alive × 1.5 규칙) + DL-011 여유 |
| STATE_REFRESH_SEC | 5 | 초 | 설계 결정 DL-011 |
| ALERT_DELIVERY_P95 | 30 | 초 | 설계 결정 DL-011 |
| EVENT_COOLDOWN_SEC | 30 | 초 | 설계 결정 DL-011 |
| ALERT_DAILY_MAX | 50 | 건/사이트/일 | 설계 결정 DL-011 |
| CLIP_PRE_SEC | 5 | 초 | 설계 결정 DL-011 |
| CLIP_POST_SEC | 15 | 초 | 설계 결정 DL-011 |
| MANUAL_REC_MAX_MIN | 5 | 분 | 설계 결정 DL-011 |
| CLIP_RETENTION_DAYS | 30 | 일 | 출처 URL https://www.privacy.go.kr/front/bbs/bbsView.do?bbsNo=BBSMSTR_000000000049&bbscttNo=20779 (표준 개인정보 보호지침 30일 권고 — 원문 재확인 필요, 가능성) |
| PURGE_MAX_DELAY_HOURS | 24 | 시간 | 설계 결정 DL-011 (법의 "지체없이(5일)"보다 엄격) |
| CLIP_DISK_RESERVE_PCT | 10 | % | 설계 결정 DL-011 |
| CLIP_STORAGE_MIN_GB | 256 | GB | 설계 결정 DL-011 (4카메라 × 50이벤트/일 × 20초 × 5Mbps × 30일 ≈ 75GB의 3배) |
| AUDIT_RETENTION_DAYS | 365 | 일 | 출처 URL https://itwiki.kr/w/개인정보처리시스템_접속기록 (안전성 확보조치 기준 1년 — 원문 재확인 필요, 가능성) |
| AUDIT_REVIEW_INTERVAL_DAYS | 30 | 일 | 출처 URL https://itwiki.kr/w/개인정보처리시스템_접속기록 (월 1회 점검 — 가능성) |
| VIEWER_MAX | 5 | 세션/사이트 | 설계 결정 DL-006 (Q1) |
| CAMERAS_PER_SITE_MAX | 4 | 대 | 설계 결정 DL-003 (해석 4) |
| SITES_MAX | 10 | 곳 | 설계 결정 DL-003 (해석 4) |
| MAIN_STREAM_BITRATE_MBPS | 5 | Mbps | 출처 URL https://www.hikvision.com/content/dam/hikvision/ca/faq-document/H.2645-&-H.2645-Recommended-Bit-Rate-at-General-Resolutions.pdf (1080p 2~5Mbps 상한) |
| SUB_STREAM_BITRATE_KBPS | 512 | kbps | 출처 URL https://www.unifore.net/ip-video-surveillance/simple-guide-of-ip-camera-bitrate-setting.html (단일 벤더 블로그, 가능성) |
| UPLINK_MIN_MBPS | 30 | Mbps | 설계 결정 DL-011 (VIEWER_MAX × MAIN_STREAM_BITRATE_MBPS = 25 + 여유) |
| OFFLINE_QUEUE_MAX | 10000 | 건 | 설계 결정 DL-011 |
| EVENTS_PER_CAM_DAY_MAX | 500 | 건 | 설계 결정 DL-011 (볼륨 상한 가정) |
| STREAM_TOKEN_TTL_SEC | 300 | 초 | 설계 결정 DL-011 |
| SESSION_TTL_HOURS | 24 | 시간 | 설계 결정 DL-011 |
| PASSWORD_MIN_LEN | 12 | 자 | 설계 결정 DL-011 |
| WATCHDOG_TIMEOUT_SEC | 15 | 초 | 출처 URL https://mender.io/blog/raspberry-pi-in-production (Pi HW watchdog 상한 15초) |
| CAM_RECONNECT_BACKOFF | 1→60 | 초 (지수, 무한) | 설계 결정 DL-011 |
| OTA_ROLLBACK_MAX_MIN | 5 | 분 | 설계 결정 DL-011 |
| SD_WRITE_MAX_MB_DAY | 50 | MB/일 | 설계 결정 DL-011 (log2ram 기본 동기화 시 로그만 남는 수준) |
| UI_FEEDBACK_MS | 400 | ms | 출처 ux-principles-kr L-10 (도허티 임계) |
| VPS_LINE_MBPS | 1000 | Mbps | 설계 결정 DL-016 (VPS 플랜 1Gbps 포트 가정 — 계약 시 확인, 미달이면 이 값만 수정) |
| TURN_RELAY_MAX_MBPS | 100 | Mbps | 설계 결정 DL-016 (VPS_LINE_MBPS의 10%; 릴레이는 수신+송신이라 회선 점유 2배) |
| LIVE_SESSION_MAX_MIN | 60 | 분 | 설계 결정 DL-016 (Q1 단시간 확인 맥락, 세션 자동 종료) |
| TURN_CRED_TTL_MIN | 90 | 분 | 설계 결정 DL-016 (LIVE_SESSION_MAX_MIN + 여유 — coturn use-auth-secret 만료 후 refresh 실패 방지) |
| DEVICE_TURN_CRED_TTL_HOURS | 24 | 시간 | 설계 결정 DL-016 (Pi go2rtc 설정 파일 회전 주기) |
| CMD_TIMEOUT_SEC | 10 | 초 | 설계 결정 DL-016 (기기 명령 응답 대기 — DEVICE_OFFLINE_DETECT_SEC보다 짧게) |
| CLIP_CACHE_TTL_MIN | 30 | 분 | 설계 결정 DL-016 (온디맨드 클립 서버 캐시, 활성 토큰 있으면 연장; MANUAL_REC_MAX_MIN보다 김) |
| DEVICE_CERT_VALID_DAYS | 365 | 일 | 설계 결정 DL-016 |
| DEVICE_CERT_RENEW_BEFORE_DAYS | 30 | 일 | 설계 결정 DL-016 |
| RINGBUF_SEGMENT_SEC | 2 | 초 | 설계 결정 DL-016 |
| RINGBUF_TMPFS_MB | 256 | MB | 설계 결정 DL-016 (CAMERAS_PER_SITE_MAX × MAIN_STREAM_BITRATE_MBPS × (CLIP_PRE_SEC + 2×RINGBUF_SEGMENT_SEC) ≈ 23MB의 약 10배 여유) |
| MQTT_MSG_MAX_KB | 64 | KB | 설계 결정 DL-016 (SDP 수 KB + 여유) |
| OTA_RETRY_MAX | 1 | 회 | 설계 결정 DL-016 (같은 digest 재시도 상한, 롤백 루프 방지) |
| THUMB_MAX_KB | 200 | KB | 설계 결정 DL-016 (썸네일 상한; 최악 디스크 = EVENTS_PER_CAM_DAY_MAX × CAMERAS_PER_SITE_MAX × SITES_MAX × CLIP_RETENTION_DAYS × THUMB_MAX_KB) |
| SLO_WINDOW_DAYS | 30 | 일 | 설계 결정 DL-018 (R2) |
| SLO_LIVE_SUCCESS_PCT | 99 | % | 설계 결정 DL-018 (E27 201 → LIVE_FIRST_FRAME_P95 안 첫 프레임 도달 비율) |
| SLO_DEVICE_ONLINE_PCT | 99.5 | % | 설계 결정 DL-018 (사이트별, 인터넷 장애 포함) |
| SLO_PUSH_SUCCESS_PCT | 99 | % | 설계 결정 DL-018 |
| ALARM_WARN_PCT | 80 | % | 설계 결정 DL-018 (상한 대비 경고 임계 — TURN·디스크·큐 공통) |
| HEALTHCHECK_INTERVAL_SEC | 30 | 초 | 설계 결정 DL-018 |
| HEALTHCHECK_FAIL_MAX | 3 | 회 | 설계 결정 DL-018 |
| PI_LOG_RAM_MB | 128 | MB | 출처 URL https://github.com/azlux/log2ram (기본 SIZE 128M) |
| JOURNAL_MAX_MB | 20 | MB | 출처 URL https://github.com/azlux/log2ram (README 권고 SystemMaxUse=20M) |
| LOG_RETENTION_DAYS | 14 | 일 | 설계 결정 DL-018 (서버 앱 로그; 감사 로그는 AUDIT_RETENTION_DAYS) |
| BACKUP_INTERVAL_HOURS | 24 | 시간 | 설계 결정 DL-018 |
| BACKUP_RETENTION_DAYS | 30 | 일 | 설계 결정 DL-018 (썸네일 백업은 CLIP_RETENTION_DAYS 파기 대상) |
| RESTORE_DRILL_INTERVAL_DAYS | 90 | 일 | 출처 blindspot-checklists P2 백업·DR (복원 리허설 분기 1회) |
| RTO_SERVER_MIN | 120 | 분 | 설계 결정 DL-018 (팀 1~2명, 새 VPS 재설치 + 복원) |
| RPO_SERVER_HOURS | 24 | 시간 | 설계 결정 DL-018 (= BACKUP_INTERVAL_HOURS; 이벤트는 기기 outbox 재전송으로 0에 근접) |
| RTO_DEVICE_MIN | 60 | 분 | 설계 결정 DL-018 (유인 현장 SD 재굽기 + 재클레임) |
| ALERT_OPS_DEDUP_MIN | 30 | 분 | 설계 결정 DL-018 (운영 알람 중복 억제) |
| TURN_RELAY_COST | — | 원/GB | 미확인 (01-recon 미확인 6) — SC에 쓰지 않는다 |
| KR_UPLOAD_AVG_MBPS | 211 | Mbps | 미확인 (검색 스니펫, 1차 통계 미열람) — SC에 쓰지 않는다 |

## 미결정
0건.
