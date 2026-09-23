# PRD — PiCam Hub (라즈베리파이 IP 카메라 제어·실시간 모니터링 허브)
버전: v1.3 — 개정하면 올리고, 04~07 머리의 `기준 03 v`를 같은 턴에 갱신 (SKILL.md 규칙 9) · 작성 2026-09-07 · v1.1(A5): 카메라 자격증명 거부 401→422 `camera-auth-rejected`, PTZ 미지원 405→409 `ptz-unsupported` (허브 세션 401과 의미 분리, Zalando #150) · v1.2(A4 검토 반영): 시청 세션=뷰어 정의·MSE 상한, 프로브 병렬·PROBE_TIMEOUT_S·주기 25s, IP 기준 잠금, 감사 무기한, healthcheck 상수, 온도 상수 · v1.3(GATE 반영): 자가복구=내부 감시 exit(1), 상태값 4종 정의, RTSP 프로브 무인증, ALERT_DELIVERY_MAX_S 기산점, 감사 단위, FR-018 강제 지점, 운영 기본값 상수 편입, 검증 대기 가정 명시

## 근거 (단계 진입 사전조사 — 추가 검색 0회, 01-recon 재사용)
- **정량** — WebRTC glass-to-glass 200~500ms, HLS 15~30s (forasoft·mux, 2026-09-07 확인, 01 수치 표) → "실시간"을 p95 1,000ms로 계량.
- **정성** — "대시보드에 카메라를 붙였더니 지연이 누적된다" (Home Assistant 커뮤니티 camera-lag-on-dashboard, 01 이해관계자) → 그리드는 sub-stream + WebRTC로 고정.
- **사용자 영향** — 타일 클릭·PTZ 버튼의 시각 피드백 ≤400ms(L-10 도허티), 카메라가 끊겨도 막다른 화면 없음(L-06 피크엔드).

## 배경 (RECON 요약)
- 소규모 사용자는 카메라 벤더 앱(카메라마다 다름, 클라우드 경유)이나 NAS·NVR(수십만 원)로 여러 대를 본다. ONVIF(Profile S/T)와 RTSP가 사실상 표준이라 벤더 무관 허브가 가능하다 (01 도메인 흐름).
- 스트리밍 변환은 해결된 문제다: go2rtc(MIT, RTSP→WebRTC/MSE, ONVIF 소스 지원)가 Pi에서 저부하로 돈다. 객체 감지 NVR은 Frigate가 이미 있다 (01 유사 솔루션).
- 한국 개인정보보호법 §25·§25조의2와 표준 개인정보 보호지침(보관 최대 30일)이 공개 장소 촬영에 적용된다 (01 규제).
- 결정(02): **Extend** — go2rtc를 스트림 엔진으로 채택하고 "등록→라이브→제어→상태→알림"의 얇은 층만 만든다. 하드웨어 기준 Pi 5 4GB + USB SSD, 카메라 ≤ MAX_CAMERAS, 원격 접근 Tailscale.

## 제품 목표 (3개, 직교)
- **G1 본다** — 벤더가 달라도 카메라 ≤4대를 브라우저 한 화면에서 LIVE_LATENCY_P95_MS 이내 지연으로 본다.
- **G2 움직인다** — PTZ 카메라를 같은 화면에서 PTZ_CMD_P95_MS 이내 반응으로 조작하고 프리셋으로 되돌린다.
- **G3 안다** — 카메라·허브가 죽거나 되살아나면 감지(이벤트 기록) 후 ALERT_DELIVERY_MAX_S 이내에 푸시로 알고, 무슨 일인지 스냅샷으로 확인한다.

## 유저 스토리 (P1만으로 MVP 성립)
| ID | 우선순위 | 스토리 | 독립 테스트 |
|---|---|---|---|
| US-1 | P1 | As a 운영자, I want LAN에서 자동 발견된 카메라를 자격증명만 넣어 등록하고 싶다, so that RTSP URL을 손으로 찾지 않는다 | 카메라 1대 전원 → 발견 목록 노출 → 등록 → 그리드에 타일 생김 |
| US-2 | P1 | As a 운영자, I want 등록한 카메라 전부를 한 화면 그리드로 실시간으로 보고 싶다, so that 앱을 오가지 않는다 | 4대 등록 → 2×2 그리드 전부 재생, 지연 측정 |
| US-3 | P1 | As a 운영자, I want 카메라를 상하좌우·줌으로 움직이고 자주 보는 위치를 프리셋으로 저장하고 싶다, so that 원하는 곳을 즉시 본다 | PTZ 패드 → 카메라 회전, 프리셋 저장 → 이동 복귀 |
| US-4 | P1 | As a 운영자, I want 카메라가 끊기거나 허브 디스크가 차면 휴대폰 푸시로 알고 싶다, so that 몰래 죽어 있는 카메라가 없다 | 카메라 전원 차단 → OFFLINE_DETECT_MAX_S + ALERT_DELIVERY_MAX_S 내 푸시 수신, 복구 → 복구 푸시 |
| US-5 | P2 | As a 운영자, I want 카메라 모션 이벤트 전후 영상을 클립으로 남기고 싶다, so that 알림을 받았을 때 무슨 일인지 본다 | 모션 발생 → 이벤트 목록 → 클립 재생 |

## 용어집
| 용어 | 정의 |
|---|---|
| 카메라 | ONVIF 장치 1대 (MAC로 유일) |
| 스트림 프로파일 | 카메라의 RTSP main(고화질)/sub(저화질) 출력 |
| 프리셋 | 카메라에 저장된 PTZ 위치(카메라 측 토큰 + 허브 측 이름) |
| 헬스 샘플 | HEALTH_PROBE_INTERVAL_S마다 카메라·허브를 프로브한 결과 1행 |
| 이벤트 | 허브가 기록한 사실(카메라 offline/online, 디스크 하한, 모션(P2)) |
| 알림 | 이벤트가 알림 채널(ntfy)로 전달된 기록 |
| 스냅샷 | 특정 시각의 JPEG 1장 |
| 시청 세션 | 활성 스트림(WS)을 하나 이상 가진 로그인 세션 = 뷰어 1명. WS 수가 아니다 |
| 클립(P2) | 이벤트 전후 CLIP_PRE_S~CLIP_POST_S 구간의 MP4 파일 |

## 요구사항 풀
| ID | 요구사항 (EARS) | 우선순위 | 출처 |
|---|---|---|---|
| FR-001 | 첫 접속 시 시스템은 관리자 비밀번호(PASSWORD_MIN_LEN 이상) 설정을 요구하고, 이후 로그인·세션(SESSION_TTL_H)·로그아웃을 제공해야 한다. 같은 출발 IP에서 LOGIN_LOCKOUT_FAILS회 실패하면 그 IP를 LOGIN_LOCKOUT_MIN분 잠그고(계정 잠금 아님 — 내부자의 자기 DoS 방지) 실패마다 응답을 지수 지연한다 | P0 | 02 S · 위협모델 |
| FR-002 | 운영자가 "카메라 찾기"를 누르면 시스템은 WS-Discovery로 DISCOVERY_TIMEOUT_S 내 응답한 장치의 IP·제조사·모델·MAC을 목록으로 보여야 한다 | P0 | US-1 |
| FR-003 | 운영자가 발견 목록(또는 IP 직접 입력)에서 자격증명을 넣어 등록하면 시스템은 ONVIF로 인증·프로파일을 조회해 main/sub RTSP URL·PTZ 지원 여부를 저장하고, 같은 MAC의 중복 등록을 거부해야 한다 | P0 | US-1 · INV-1 |
| FR-004 | 운영자는 카메라 이름·자격증명을 수정하고 카메라를 삭제할 수 있어야 하며, 삭제 시 스트림·헬스 프로브·프리셋이 함께 정리돼야 한다 | P0 | US-1 |
| FR-005 | 시스템은 등록 카메라 전부를 2×2 그리드에 sub-stream·WebRTC(폴백 MSE)로 재생해야 하며, 동시 시청 세션(뷰어)은 MAX_VIEW_SESSIONS까지, 뷰어당 스트림은 MAX_CAMERAS까지다. MSE 폴백 스트림은 허브를 경유하므로 허브 전체 MSE_MAX_STREAMS까지만 허용한다 | P0 | US-2 · G1 |
| FR-006 | 운영자가 타일을 열면 시스템은 그 카메라의 main-stream 단일 뷰와 PTZ 패드·프리셋 목록을 한 화면에 보여야 한다 | P0 | US-2 · US-3 |
| FR-007 | 운영자가 PTZ 패드를 누르고 있는 동안 시스템은 ONVIF ContinuousMove를 보내고 떼면 Stop을 보내야 하며, 명령은 PTZ_THROTTLE_MS로 스로틀하고 마지막 명령이 이긴다 | P0 | US-3 · G2 |
| FR-008 | 운영자는 프리셋을 조회·이동·저장·이름 변경·삭제할 수 있어야 하며, 프리셋은 카메라에 저장되고 이름은 허브에 저장된다 | P0 | US-3 |
| FR-009 | 시스템은 HEALTH_PROBE_INTERVAL_S마다 각 카메라에 프로브 2종을 병렬(각 PROBE_TIMEOUT_S)로 보내고 — ONVIF GetSystemDateAndTime(무인증), RTSP DESCRIBE(**자격증명 없이**, 401·200 모두 생존) — 허브(CPU·RAM·디스크·온도·go2rtc 생존·스트림 목록 대조→불일치 시 재등록)를 프로브해 헬스 샘플을 기록하고 상태를 판정해야 한다: **unknown**=등록 후 첫 프로브 전 · **online**=두 프로브 성공 · **degraded**=RTSP 성공이나 ONVIF가 OFFLINE_AFTER_FAILURES회 연속 실패(라이브 가능, PTZ·스냅샷 불가) · **offline**=RTSP가 OFFLINE_AFTER_FAILURES회 연속 실패(라이브 불가) | P0 | US-4 · G3 |
| FR-010 | 카메라 상태가 바뀌면 시스템은 이벤트를 기록하고 알림 채널로 보내야 하며, 같은 카메라·같은 유형은 ALERT_DEDUP_WINDOW_S 안에 1건만, 전체 카메라 동시 offline은 1건으로 묶어야 한다 | P0 | US-4 · P3 알람 폭주 |
| FR-011 | 운영자가 스냅샷 버튼을 누르거나 offline→online 복구 이벤트가 나면 시스템은 JPEG를 SSD에 저장하고 SHA-256을 기록해야 한다 | P0 | US-4 · G3 |
| FR-012 | 카메라·허브 상태가 바뀌면 시스템은 SSE로 열린 화면에 SSE_STATE_REFLECT_MAX_S 내 반영해야 한다 (배지·타일 상태) | P0 | US-2 · P3 실시간성 |
| FR-013 | 운영자는 알림 채널(ntfy 서버 URL·토픽·토큰)을 설정하고 "테스트 발송"으로 확인할 수 있어야 한다 | P1 | US-4 |
| FR-014 | 허브 디스크 잔여가 DISK_MIN_FREE_PCT 미만이거나 CPU 5분 평균이 CPU_5M_MAX_PCT를 넘거나 SoC 온도가 HUB_TEMP_MAX_C를 넘으면 시스템은 이벤트·알림을 내야 한다 | P1 | US-4 · P1 SD |
| FR-015 | 시스템은 하루 1회 보존 정리를 실행해 SNAPSHOT_RETENTION_DAYS·HEALTH_RAW_RETENTION_DAYS·EVENT_RETENTION_DAYS를 넘은 데이터를 파기해야 한다(감사 로그는 파기하지 않는다 — INV-6; 헬스 시간 요약은 P2 FR-021과 함께). 어떤 영상·스냅샷도 RETENTION_MAX_DAYS를 넘겨 보관할 수 없다 | P1 | 01 규제 · INV-2 |
| FR-016 | 시스템은 로그인(성공·실패)·PTZ 명령(누름·뗌·프리셋 단위 — keepalive 재전송은 제외)·설정 변경·삭제를 append-only 감사 로그(누가·언제·무엇)에 기록해야 한다 | P1 | 02 R |
| FR-017 | 알림 채널이 실패하면 시스템은 알림을 로컬 큐(ALERT_QUEUE_MAX)에 보관했다가 순서대로 재전송하고, 넘치면 오래된 것을 폐기하며 폐기 수를 기록해야 한다 | P1 | P1 연결 끊김 |
| FR-018 | 시스템은 라이브·클립에 오디오 트랙을 포함하지 않아야 한다 (기본 off, MVP에서 켜는 수단 없음). 강제 지점: E-18 프록시가 WebRTC offer SDP의 `m=audio`와 MSE 코덱 요청의 오디오 코덱을 제거·거부하고, 플레이어는 `media=video`로 요청한다 — go2rtc 소스 옵션에 의존하지 않는다 | P1 | 01 규제(녹음 금지) · Q1 |
| FR-019 | 설정 화면은 개인정보 운영 체크리스트(안내판 설치·운영방침·보관 기간 표시·관리책임자)를 보여야 하며, 보관 기간 값은 상수 표의 값을 읽어 표시한다 | P1 | 01 규제 |
| FR-020 | PTZ를 지원하지 않는 카메라에는 시스템이 PTZ 패드·프리셋 UI를 숨기고 "이 카메라는 회전을 지원하지 않아요"를 보여야 한다 | P1 | US-3 · 엣지 |
| FR-021 | 운영자는 카메라별 최근 24h 가용률과 헬스 이력 차트를 볼 수 있어야 한다 (헬스 원본 → 시간 요약 롤업, HEALTH_ROLLUP_RETENTION_DAYS) | P2 | US-4 |
| FR-022 | 시스템은 ONVIF Events(PullPoint)로 모션 이벤트를 구독해 이벤트를 기록해야 한다 | P2 | US-5 |
| FR-023 | 모션 이벤트가 나면 시스템은 CLIP_PRE_S 전부터 CLIP_POST_S 후까지 main-stream을 재인코딩 없이 MP4로 저장하고 CLIP_RETENTION_DAYS 후 파기해야 한다 | P2 | US-5 · INV-2 |
| FR-024 | 운영자는 이벤트 목록을 시간순으로 보고 클립을 재생·삭제할 수 있어야 한다 | P2 | US-5 |
| FR-025 | 운영자는 두 번째 알림 채널로 Telegram Bot을 추가할 수 있어야 한다 | P2 | 01 fit |
| FR-026 | 운영자는 카메라 상세에서 현재 프로파일(해상도·코덱·비트레이트)을 읽기 전용으로 볼 수 있어야 한다 | P2 | Q5 |

### 불변식·제약 (ecc:product-capability — 구현 전에 참이어야 하는 것)
| ID | 불변식 | 강제 지점 |
|---|---|---|
| INV-1 | 카메라 MAC은 허브 안에서 유일하다 | DB UNIQUE + 등록 API 409 |
| INV-2 | 어떤 스냅샷·클립도 생성 후 RETENTION_MAX_DAYS를 넘겨 존재하지 않는다 | 정리 작업 + 설정 UI는 상한 초과 값을 받지 않음 |
| INV-3 | 카메라 자격증명은 저장·로그·API 응답 어디에도 평문으로 나타나지 않는다 | AES-GCM 저장, 응답 DTO에서 제외, 로그 마스킹 |
| INV-4 | 허브는 라이브·클립 영상을 트랜스코딩하지 않는다 (패스스루). 유일한 예외: 카메라가 스냅샷 URI를 주지 않을 때 단일 키프레임 JPEG 추출(카메라당 ≤1회/초) | go2rtc 설정에 ffmpeg 트랜스코드 소스 금지, 스냅샷 폴백만 허용 |
| INV-5 | 허브 HTTP 포트는 LAN·Tailscale 인터페이스에만 바인딩된다; go2rtc API는 localhost에만 | compose 네트워크·바인딩 |
| INV-6 | 감사 로그는 갱신·삭제되지 않는다 | 트리거로 UPDATE/DELETE 거부 |

## 엣지케이스 (Given/When/Then)
**등록**
- Given 발견 목록에 카메라 A, When 잘못된 비밀번호로 등록, Then 422 `camera-auth-rejected`("카메라가 자격증명을 거부했어요")와 재입력 CTA, 저장 없음.
- Given 카메라 A(MAC X) 등록됨, When 같은 MAC를 다른 IP로 다시 등록, Then 409 + "이미 등록된 카메라예요 — 기존 항목의 IP를 바꿀까요?" CTA.
- Given 카메라가 WS-Discovery에 응답하지 않음, When IP 직접 입력으로 등록, Then ONVIF 인증만으로 등록 진행.
**라이브**
- Given 그리드 재생 중, When 카메라 B RTSP가 끊김, Then B 타일만 마지막 프레임 정지 + "연결 끊김 · 다시 시도 중(12초)" stale 배지, 나머지 타일 유지, 재연결 백오프 1→RECONNECT_BACKOFF_MAX_S.
- Given 서로 다른 로그인 세션(뷰어) MAX_VIEW_SESSIONS개가 스트림을 열고 있음, When 다음 뷰어가 그리드 접속, Then 그리드 대신 카메라 이름·상태 목록(라이브 없음) + "동시 시청 {MAX_VIEW_SESSIONS}명까지예요" 안내, 상태 배지는 정상 반영.
- Given 브라우저가 WebRTC H.265 미지원, When H.265 카메라 상세 뷰, Then MSE 폴백 시도, 실패 시 "이 브라우저는 이 카메라 화질을 재생할 수 없어요 — Chrome 136+ 또는 Safari 18+" 안내.
**PTZ**
- Given PTZ 패드 누름 중, When 브라우저 탭이 닫히거나 네트워크 끊김, Then 허브가 서버측 타임아웃(PTZ_HOLD_TIMEOUT_MS)으로 Stop을 보낸다; 허브 자체가 죽어도 카메라가 멈추도록 ContinuousMove의 Timeout을 PTZ_HOLD_TIMEOUT_MS로 함께 보낸다.
- Given 두 탭에서 동시에 다른 방향 명령, When 두 명령이 PTZ_THROTTLE_MS 안에 도착, Then 마지막 명령만 카메라에 전달되고 감사 로그에는 두 누름이 모두 남는다(keepalive는 제외).
- Given PTZ 미지원 카메라, When 상세 뷰 진입, Then FR-020 문구, API는 409 `ptz-unsupported`.
**헬스·알림**
- Given 카메라 4대 모두 offline 전환(스위치 다운), When 판정 시각이 같은 프로브 주기, Then 알림 1건 "카메라 4대가 연결이 끊겼어요" + 이벤트는 4건.
- Given 알림 채널 다운, When 이벤트 120건 발생, Then 큐 100건 보관·20건 폐기·폐기 카운트 이벤트 1건, 채널 복구 시 순서대로 재전송.
- Given 디스크 잔여 8%, When 스냅샷 생성 요청, Then 가장 오래된 스냅샷을 먼저 지우고 생성, 잔여 5% 미만이면 생성 거부 + 알림.
- Given NTP 미동기 부팅 직후, When ntfy.sh TLS 실패, Then 큐 보관 후 시각 동기 뒤 재전송.

## 성공 기준 (전부 pass/fail, 기술 중립)
| ID | 기준 | 측정 방법 |
|---|---|---|
| SC-001 | 카메라 4대 그리드에서 glass-to-glass 지연 p95 ≤ LIVE_LATENCY_P95_MS | 카메라 앞 ms 타이머를 촬영, 화면 캡처와 비교 20회 |
| SC-002 | PTZ 패드 누름 → 영상에서 움직임 시작 p95 ≤ PTZ_CMD_P95_MS | 화면 녹화 프레임 분석 20회 |
| SC-003 | 카메라 전원 차단 후 타일 offline 표시 ≤ OFFLINE_DETECT_MAX_S; 이벤트 기록 시각(event.ts) 기산 푸시 수신 ≤ ALERT_DELIVERY_MAX_S (전원 차단 기준 합계 ≤ OFFLINE_DETECT_MAX_S + ALERT_DELIVERY_MAX_S) | 스톱워치 10회 전부 통과 (lab T-062) + 시뮬레이터 T-024·T-027 |
| SC-004 | 카메라 전원 복구 후 타일 재생 재개 ≤ OFFLINE_DETECT_MAX_S + RECONNECT_BACKOFF_MAX_S | 스톱워치 10회 |
| SC-005 | 4대 30분 연속 시청(세션 3) 중 Pi CPU 5분 평균 ≤ CPU_5M_MAX_PCT, 가용 RAM ≥ RAM_MIN_FREE_MB | `vmstat`·`/proc/meminfo` 로그 |
| SC-006 | 24h 정상 운영 중 SD(rootfs) 쓰기 ≤ SD_WRITE_MAX_MB_DAY | `iostat -d` 누적 kB_wrtn |
| SC-007 | 전원 급차단 10회 후 매번 부팅·서비스 자동 복귀·`PRAGMA integrity_check`=ok | 10/10 |
| SC-008 | 무설명 신규 사용자 5인이 카메라 1대 등록을 결정 지점 ≤ UX_MAX_DECISIONS, ≤ 3분에 완료 | 사용성 테스트 5인 4/5 이상 |
| SC-009 | 정리 작업 후 SNAPSHOT_RETENTION_DAYS 초과 스냅샷 0건, 라이브·클립 오디오 트랙 0 | DB 쿼리 + `ffprobe` |
| SC-010 | Tailscale 밖 인터넷에서 허브 열린 포트 0, 로그인 LOGIN_LOCKOUT_FAILS회 실패 후 잠금, 로그·DB에 평문 자격증명 0건 | 외부 `nmap`, 로그인 시도, `grep` |

## UI 방향 (frontend-design-taste)
- 프로파일 **관제/대시보드**: VISUAL_DENSITY 8 · MOTION_INTENSITY 2 · DESIGN_VARIANCE 3. Cockpit 모드 — 카드 박스 없이 1px 선·`divide-y`, 숫자 `font-mono`, 세리프 금지, 순수 #000 금지, 테마 토큰(CSS 변수)만.
- 폰트는 Pi에서 로컬 서빙(외부 CDN 0): 시스템 산세리프 스택 + 로컬 번들 모노(JetBrains Mono).
- 상태 4종 필수: 빈(카메라 0대 → "카메라 찾기" CTA) / 로딩(스켈레톤 타일) / 에러(원인 + CTA) / stale(재연결 카운트다운).
- 측정 가능 UX 기준: UX-01(L-10) 모든 액션 시각 피드백 ≤ UX_FEEDBACK_MAX_MS · UX-02(L-06) 복구 CTA 없는 에러 0 · UX-03(L-03) 화면당 주 결정 1개 · UX-04(T-08) 버튼 라벨은 일어날 행동("프리셋 저장") · UX-05(T-09) 해요체.

## 화면 스케치 — ① 라이브 그리드 (핵심 화면)
```
┌ PiCam Hub ───────────────────────────── 허브 CPU 41% · 디스크 63% · ● 알림 연결됨 ┐
│ [카메라 찾기]                                                     [설정]           │
├──────────────────────────────┬──────────────────────────────────────────────────┤
│ 입구  ● online  1.2 Mbps     │ 매장 안쪽  ◐ 연결 끊김 · 다시 시도 중 (12초)    │
│ ┌──────────────────────────┐ │ ┌──────────────────────────────────────────────┐ │
│ │      [WebRTC video]      │ │ │  마지막 프레임 정지 10:41:07          [stale] │ │
│ │                          │ │ │                                              │ │
│ └──────────────────────────┘ │ └──────────────────────────────────────────────┘ │
│ [스냅샷] [크게 보기 →]       │ [지금 다시 연결]                                 │
├──────────────────────────────┼──────────────────────────────────────────────────┤
│ 창고  ● online  0.9 Mbps     │ (빈 슬롯)                                        │
│ ┌──────────────────────────┐ │                                                  │
│ │      [WebRTC video]      │ │      카메라를 추가할 수 있어요                   │
│ │                          │ │      [카메라 찾기]                               │
│ └──────────────────────────┘ │                                                  │
│ [스냅샷] [크게 보기 →]       │                                                  │
└──────────────────────────────┴──────────────────────────────────────────────────┘
빈 상태: 카메라 0대 → 그리드 대신 "아직 카메라가 없어요 · [카메라 찾기]" 1개 CTA
로딩: 타일 자리 스켈레톤 + "연결 중…" / 에러: 원인 문장 + CTA 1개 / stale: 마지막 프레임 정지 + 카운트다운
```
② 카메라 상세 = main-stream 1개 + 우측 PTZ 패드(상하좌우·줌 ±, 누르는 동안 이동) + 프리셋 목록(이동·저장·이름·삭제) + 프로파일 정보(P2).
③ 설정 = 카메라 목록(수정·삭제) / 알림 채널(ntfy URL·토픽·토큰·테스트 발송) / 개인정보 체크리스트(FR-019) / 허브 상태.

## 범위 밖 (non-goals)
- AI 객체 감지(사람·차량) — 목적이면 Frigate 채택이 정답 (02 제품 렌즈)
- 24/7 연속 녹화, 클라우드 저장, 다중 Pi·다중 사이트, 다중 사용자·역할, 모바일 네이티브 앱
- 카메라 펌웨어·해상도·비트레이트 설정 변경(읽기만 P2), 오디오(양방향 포함), 카메라 TLS(RTSPS)
- A/B 파티션 OTA, read-only rootfs, UPS, RTC 모듈 (02 P1 축소 채택 항목)

## 가정 목록 (02 register Assumed 26 + 무응답 채택 5 요약)
장소=소규모 사업장 기준 내장 · HW=Pi 5 4GB+SSD · 녹화=P1 스냅샷/P2 클립 · 원격=Tailscale · 카메라 ≤4 H.264 sub-stream · 1인 운영 · 인터넷 가능(NTP) · 알림=ntfy · 브라우저=Chrome/Safari 최신. 상세는 `02-blindspot-register.md`.

**검증 대기 가정** (값은 결정됨 — 미결정이 아니라 착수 첫 작업의 검증 항목; 실패 시 03 개정 → 하류 전파):
1. onvif-zeep-async 라이선스 = MIT 추정, ntfy = Apache-2.0/GPL-2.0 이중 추정 → `pip show`·저장소 LICENSE로 확인 (02 축 10)
2. 오디오 제외는 E-18 프록시 필터로 강제(go2rtc 소스 옵션 미의존) → T-042 ffprobe로 실효 확인
3. MAX_VIEW_SESSIONS=3 · MSE_MAX_STREAMS=4 → lab T-048로 검증(Pi 업링크·CPU)
4. hub-api↔go2rtc host 네트워크 localhost 공유 → T-056

## 상수 표 (단일 출처 — 04~07은 이름으로만 참조)
| 이름 | 값 | 단위 | 근거 |
|---|---|---|---|
| MAX_CAMERAS | 4 | 대 | Q5 · go2rtc 커뮤니티 실측(10, 단일 출처)의 절반 이하 |
| MAX_VIEW_SESSIONS | 3 | 뷰어(로그인 세션) | 4타일×3뷰어 = 최대 12 스트림·18Mbps 이하 Pi 업링크 여유 (01 대역 수치). WS 상한은 파생값 MAX_VIEW_SESSIONS×MAX_CAMERAS |
| MSE_MAX_STREAMS | 4 | 스트림 | MSE 폴백은 hub-api가 fMP4를 릴레이(04 ADR-5) — 뷰어 1명 그리드 분량만, 초과는 WebRTC 필요 안내. 07 lab에서 실측 후 조정 |
| LIVE_LATENCY_P95_MS | 1000 | ms | WebRTC 200~500ms 실측 문헌(01) + 그리드 4타일 여유 |
| PTZ_CMD_P95_MS | 500 | ms | UX L-10 400ms + ONVIF 왕복 여유 |
| PTZ_THROTTLE_MS | 100 | ms | 초당 10명령 — ONVIF ContinuousMove 갱신에 충분(추론) |
| PTZ_HOLD_TIMEOUT_MS | 2000 | ms | 클라이언트 keepalive 끊김 시 서버측 Stop |
| HEALTH_PROBE_INTERVAL_S | 25 | s | INTERVAL×FAILURES + PROBE_TIMEOUT_S = 55 ≤ OFFLINE_DETECT_MAX_S (A4 검토 #6) |
| PROBE_TIMEOUT_S | 5 | s | ONVIF·RTSP 프로브 각각, 병렬 실행 |
| OFFLINE_AFTER_FAILURES | 2 | 회 | 일시 패킷 손실 오탐 방지 |
| OFFLINE_DETECT_MAX_S | 60 | s | 목표 상한. 최악 = HEALTH_PROBE_INTERVAL_S × OFFLINE_AFTER_FAILURES + PROBE_TIMEOUT_S = 55 |
| RECONNECT_BACKOFF_MAX_S | 60 | s | P1 연결 끊김 기본값 |
| ALERT_DELIVERY_MAX_S | 60 | s | 02 비기능 |
| ALERT_DEDUP_WINDOW_S | 300 | s | P3 알람 폭주 기본값 |
| ALERT_QUEUE_MAX | 100 | 건 | P1 연결 끊김 기본값 |
| SSE_STATE_REFLECT_MAX_S | 3 | s | P3 실시간성 기본값 |
| RETENTION_MAX_DAYS | 30 | 일 | 표준 개인정보 보호지침 (01 규제) — 하드 상한 |
| SNAPSHOT_RETENTION_DAYS | 30 | 일 | = RETENTION_MAX_DAYS |
| CLIP_RETENTION_DAYS | 7 | 일 | P2 · SSD 256GB 여유 |
| HEALTH_RAW_RETENTION_DAYS | 7 | 일 | P3 이력 증가 |
| HEALTH_ROLLUP_RETENTION_DAYS | 90 | 일 | P2 FR-021 전용 (MVP 미사용) |
| EVENT_RETENTION_DAYS | 90 | 일 | 영상 없는 메타데이터 |
| (감사 로그 보존) | 무기한 | — | INV-6 삭제 금지와 모순되는 보존 상한을 두지 않는다 (A4 검토 #8). 영상 없는 행위 기록 |
| DISK_MIN_FREE_PCT | 10 | % | 02 D · 엣지케이스 |
| DISK_STOP_PCT | 5 | % | 생성 거부 하한 |
| CLIP_PRE_S / CLIP_POST_S | 5 / 15 | s | P2 클립 구간 |
| SUB_STREAM_MAX | 720p · 1.5 Mbps | — | 01 sub-stream 권장. **권장값(강제 아님)** — 카메라 설정 변경은 non-goal. 등록 시 sub 프로파일이 이를 넘으면 등록 화면에 경고 문구, E-08 profiles로 확인 |
| CPU_5M_MAX_PCT | 70 | % | 02 비기능 |
| HUB_TEMP_MAX_C | 80 | °C | Pi 5 스로틀링 시작 온도 근방(추론) — 초과 시 hub.temp_high 이벤트 |
| RAM_MIN_FREE_MB | 1024 | MB | Pi 5 4GB 중 여유 |
| SD_WRITE_MAX_MB_DAY | 50 | MB/일 | log2ram 1h 동기화 × 소량 |
| HW_WATCHDOG_S | 15 | s | Pi watchdog 상한 (blindspot P1) |
| HEALTHCHECK_INTERVAL_S | 30 | s | docker healthcheck 주기(**관측용** — docker는 unhealthy를 표시만 하고 재시작하지 않는다, GATE #1). 자가 복구는 hub-api **내부 감시 태스크**가 마지막 프로브 경과 > PROBE_STALE_FACTOR×HEALTH_PROBE_INTERVAL_S면 로그 후 `exit(1)` → `restart: unless-stopped`가 되살린다 |
| PROBE_STALE_FACTOR | 2 | 배 | 프로브 정체 판정 = HEALTH_PROBE_INTERVAL_S × PROBE_STALE_FACTOR |
| SESSION_TTL_H | 24 | h | 1인 운영 편의 vs 위험 |
| PASSWORD_MIN_LEN | 12 | 자 | 위협모델 S |
| LOGIN_LOCKOUT_FAILS / LOGIN_LOCKOUT_MIN | 5 / 15 | 회 / 분 | 위협모델 S — 출발 IP 기준 |
| LOGIN_RATE_PER_MIN | 10 | 회/분/IP | 위협모델 A1-D |
| DISCOVERY_TIMEOUT_S | 5 | s | WS-Discovery 관행(추론) |
| UX_FEEDBACK_MAX_MS | 400 | ms | L-10 도허티 |
| UX_MAX_DECISIONS | 3 | 개 | quality-decomposition UX-01 |
| AVAILABILITY_SLO_MONTHLY_PCT | 99 | % | 1인 운영 현실 · SRE 100% 금지 |
| MVP_TARGET_WEEKS | 4~6 | 주 | 계획 가정(근거 없음, 02 축 7) |

### 운영 기본값 (근거 약함 — 운영 관행, 착수 후 조정 가능. 05·07은 이 이름으로만 참조)
| 이름 | 값 | 단위 | 근거 |
|---|---|---|---|
| PAGE_LIMIT_DEFAULT / PAGE_LIMIT_MAX | 50 / 200 | 건 | API 관행(근거 없음) |
| IDEMPOTENCY_TTL_H | 24 | h | Stripe 관행(01 stage-templates) |
| API_RATE_PER_MIN | 120 | 회/분/세션 | 관행(근거 없음) |
| ALERT_RETRY_INTERVAL_S | 30 | s | 첫 발송 즉시, 실패 시 재시도 주기 |
| RETENTION_JOB_TIME | 03:30 | 로컬 시각 | 야간 저부하(근거 없음) |
| HUB_API_MEM_LIMIT_MB | 512 | MB | Pi 5 4GB 중 hub-api 상한(추론, 07 lab 실측 후 조정) |
| HEALTHCHECK_RETRIES / HEALTHCHECK_START_S | 3 / 20 | 회 / s | docker 관행 |
| LOG_FILE_MB / LOG_FILE_COUNT | 10 / 5 | MB / 개 | json-file 롤링(SSD) |
| BACKUP_TIME / BACKUP_LOCAL_GENERATIONS | 04:00 / 7 | 로컬 시각 / 세대 | 관행(근거 없음) |
| BACKUP_OFFSITE_INTERVAL / RESTORE_DRILL_INTERVAL | 주 1회 / 분기 1회 | — | blindspot P2 백업·DR 기본값 |
| RPO_H / RTO_H | 24 / 1 | h | 1인 운영 현실(근거 없음) |
| DEADMAN_TIME | 09:00 | 로컬 시각 | 운영자 근무 시작(가정) |
| SLO_LIVE_PCT / SLO_ALERT_PCT / SLO_PROBE_PCT | 97 / 95 / 95 | % | SRE "100% 금지", 초기 목표(근거 없음, 운영 1개월 후 재설정) |

## 미결정 — 0건
- 0건 (register 33항목 전부 마킹, 무응답 5건은 추천안 채택. 값이 정해졌으나 실측·확인이 남은 것은 위 '검증 대기 가정' 4건으로 관리 — 결정은 끝났고 검증만 남았다)
