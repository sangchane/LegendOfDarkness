# 테스트 설계 — PiCam Hub
버전: v1.2 · 기준 03 v1.3 · 작성 2026-09-07 · v1.2(GATE 반영): 상수 값 재기술 제거(파생 표기), 403 slug, T-015 프록시 필터, T-058 exit(1), T-060~062 추가(degraded/unknown/SC-003 lab), T-044 삭제 · v1.1: A4 검토 반영 — T-002 IP 잠금, T-017 뷰어 정의, T-021 Timeout, T-026 IP 변경 대조, T-038 감사 제외, T-056~059 추가

## 근거 (단계 진입 사전조사 — 추가 검색 0회)
- **정량** — 03 SC 10개 중 실기기 없이 못 재는 것 5개(SC-001·002·005·006·007) → "lab" 레이어를 별도로 두고 CI는 시뮬레이터로 나머지를 돈다.
- **정성** — Frigate 커뮤니티 "Pi 4에서 5대 풀해상도 시 load 20+ 급등"(01) → 부하 시나리오(SC-005)는 카메라 4대·세션 3으로 상한 조건에서 잰다.
- **사용자 영향** — 06의 실패 경로 시나리오가 03 엣지케이스 문구(해요체·CTA)를 그대로 단언해 UX-02(막다른 에러 0)를 코드로 고정.

## 원칙
- AC는 개발 시작 전에 존재한다. 각 시나리오는 처음엔 반드시 실패해야 한다(RED 게이트, ecc:tdd-workflow). RED 확인 후에만 구현.
- 피라미드: unit 다수 → integration(SQLite 임시 파일 + go2rtc 실바이너리 + 카메라 시뮬레이터) → contract(05 OpenAPI 대상) → E2E(Playwright, compose 스택) 최소 → **lab**(실 Pi + 실 카메라, 수동 체크리스트). 목표 비율 ≈ 65/20/10/5(+lab).
- "가능한 한 아래층으로" — 아래층에서 검증한 것을 위층에서 반복하지 않는다(Fowler).
- 커버리지는 **리스크 기반**(ecc:tdd-workflow의 80% 일률 대신): P0 경로·INV 불변식·위협모델 상위 3(A4-I·A1·A5-I) 관련 코드 ≥ 90% 분기, 그 외 ≥ 60% 라인.
- flaky 정책(ecc:e2e-testing): 타임아웃 대기 금지 → SSE/응답 대기, `--repeat-each=10` 통과 후 머지, 격리 시 `test.fixme(이슈 번호)`.

## 테스트 인프라 (시뮬레이터)
| 구성 | 역할 | 비고 |
|---|---|---|
| `sim-camera` 컨테이너 | ONVIF SOAP 스텁(FastAPI): WS-Discovery 응답, GetDeviceInformation/GetProfiles/GetStreamUri/GetSnapshotUri/PTZ ContinuousMove·Stop·Presets, 자격증명 검증, 시리얼·MAC 설정 가능, `ptz_supported` 토글, 지연·타임아웃 주입 | 실카메라 대체. 명령 수신 로그를 테스트가 읽음 |
| `sim-rtsp` | ffmpeg `testsrc` → MediaMTX RTSP 퍼블리시(H.264 720p 1.5Mbps, 오디오 없음/있음 선택) | RTSP DESCRIBE·PLAY 대상. 테스트 중 kill/restart로 끊김 재현 |
| `sim-ntfy` | ntfy 공식 이미지 또는 HTTP 스텁(수신 기록·실패 주입) | 알림 도달·재전송 검증 |
| go2rtc | 실바이너리(버전 핀) | 시그널링 프록시·스트림 등록 검증 |
| 시계 | `freezegun`(단위) / 컨테이너 `faketime`(보존 작업) | 보존·잠금·만료 |

## 수용 기준 → 시나리오 변환표
| ID | SC/FR | Gherkin (Given/When/Then) | 레이어 | 데이터/목킹 |
|---|---|---|---|---|
| T-001 | FR-001 · SC-010 | Given 관리자 없음, When `POST /api/setup` 비밀번호 PASSWORD_MIN_LEN-1자, Then 422 validation-failed; PASSWORD_MIN_LEN자면 201 + 쿠키 `HttpOnly; Secure; SameSite=Strict` | integration | 임시 DB |
| T-002 | FR-001 · SC-010 | Given 관리자 있음, When 같은 IP에서 잘못된 비밀번호 LOGIN_LOCKOUT_FAILS회, Then 다음 시도는 429 auth-locked + `Retry-After`=LOGIN_LOCKOUT_MIN×60, 감사 `login_failed` LOGIN_LOCKOUT_FAILS행; **다른 IP에서는 정상 로그인 가능**(자기 DoS 방지), 실패 응답 지연이 회차마다 증가 | integration | freezegun · X-Forwarded 없이 소켓 IP |
| T-003 | FR-001 | Given 세션 발급 SESSION_TTL_H 전, When 시각을 TTL+1분 뒤로, Then `GET /api/auth/session` 401 session-required | unit | freezegun |
| T-004 | FR-001 · A1-T | Given 유효 세션, When `X-Requested-With` 없이 `DELETE /api/cameras/{id}`, Then 403 csrf-header-required | integration | — |
| T-005 | FR-002 | Given sim-camera 2대 응답, When `POST /api/cameras/discover`, Then 200 목록 2건(ip·mac·model), 소요 ≤ DISCOVERY_TIMEOUT_S+1s | integration | sim-camera ×2 |
| T-006 | FR-002 | Given 1대는 이미 등록, When discover, Then 그 항목 `already_registered=true` | integration | — |
| T-007 | FR-003 · INV-1 | Given 미등록, When 올바른 자격증명으로 `POST /api/cameras`, Then 201 + Location, `profiles` main·sub 2건, `ptz_supported=true`, go2rtc `/api/streams`에 `cam-{id}-sub`·`-main` 존재 | integration | sim-camera·go2rtc |
| T-008 | FR-003 | Given 카메라 A 등록, When 같은 MAC 다른 IP로 등록, Then 409 camera-duplicate, detail="이미 등록된 카메라예요 — 기존 항목의 IP를 바꿀까요?" | integration | — |
| T-009 | FR-003 | Given sim-camera가 자격증명 거부, When 등록, Then 422 camera-auth-rejected, DB 행 0 | integration | sim-camera(auth fail) |
| T-010 | FR-003 | Given MAX_CAMERAS대 등록, When MAX_CAMERAS+1번째 등록, Then 409 camera-limit-reached | integration | — |
| T-011 | FR-003 · INV-3 | Given 등록 완료, When DB `camera.cred_enc`·로그 파일·`GET /api/cameras/{id}` 본문 검사, Then 평문 비밀번호 0회 출현 | integration | grep |
| T-012 | FR-003 · 멱등 | Given 같은 Idempotency-Key로 2회 등록, Then 2번째는 최초 201 본문 재생·DB 행 1; 같은 키 다른 본문은 422 idempotency-key-reused | integration | — |
| T-013 | FR-003 | Given WS-Discovery 무응답 카메라, When IP 직접 입력 등록, Then 201 (ONVIF 인증만으로) | integration | sim-camera(discovery off) |
| T-014 | FR-004 | Given 등록 카메라, When `PATCH name`, Then 200·감사 `camera.update`; When `DELETE`, Then 204, go2rtc 스트림 2건 삭제, 프리셋·헬스·스냅샷 파일 0 | integration | go2rtc |
| T-015 | FR-005 · FR-018 · INV-4 | Given 브라우저 offer SDP에 `m=audio` 포함(또는 MSE 코덱 요청에 mp4a 포함), When E-18 프록시 통과, Then go2rtc로 전달된 SDP·코덱에 오디오 없음; 비디오까지 없으면 400 validation-failed. go2rtc 소스 등록 문자열에 트랜스코딩 옵션 없음 | unit | 프록시 필터·go2rtc 클라 목 |
| T-016 | FR-005 · A1-E | Given 쿠키 없음, When `GET /api/ws?src=cam-x-sub` 업그레이드, Then 401 (프록시 전 차단) | integration | — |
| T-017 | FR-005 | Given 서로 다른 로그인 세션(뷰어) MAX_VIEW_SESSIONS개가 각각 WS를 열고 있음, When 다음 뷰어의 WS, Then 429 view-sessions-exceeded, 화면은 카메라 이름·상태 목록 + "동시 시청 {MAX_VIEW_SESSIONS}명까지예요"; **한 뷰어가 WS MAX_CAMERAS개(그리드)를 열어도 거부되지 않음** | integration + E2E | go2rtc |
| T-018 | FR-005 · FR-006 | Given 카메라 1대 등록(sim-rtsp), When 그리드 진입, Then 타일 `<video>`가 5초 내 `readyState≥2`, 상세 진입 시 main 스트림 이름 사용 | E2E | Playwright chromium |
| T-019 | FR-006 · FR-020 | Given `ptz_supported=false` 카메라, When 상세 진입, Then PTZ 패드 없음 + "이 카메라는 회전을 지원하지 않아요"; `POST ptz/move` 409 ptz-unsupported | integration + E2E | sim-camera(ptz off) |
| T-020 | FR-007 · SC-002 | Given 패드 press, When 150ms 동안 move 3회(50ms 간격), Then sim-camera 수신 ContinuousMove ≤ 2회(PTZ_THROTTLE_MS 병합), 마지막 벡터 값 | integration | sim-camera 로그 |
| T-021 | FR-007 | Given move 후 keepalive 없음, When PTZ_HOLD_TIMEOUT_MS+100ms 경과, Then sim-camera가 Stop 수신; 수신한 ContinuousMove에 `Timeout`=PTZ_HOLD_TIMEOUT_MS(ISO 8601 duration) 포함 | integration | freezegun 불가 → 실시간 2.1s |
| T-022 | FR-007 · FR-016 | Given 두 세션 동시 move(반대 방향), Then 카메라 수신은 마지막 1건, 감사 `ptz.move` 2행(누름 단위) — keepalive 재전송 N회는 감사 0행 | integration | — |
| T-023 | FR-008 | Given PTZ 카메라, When 프리셋 저장·목록·이름 변경·goto·삭제, Then 각각 201/200/200/202/204, sim-camera SetPreset·GotoPreset·RemovePreset 수신, 허브 `ptz_preset` 행 정합 | integration | — |
| T-024 | FR-009 · SC-003 | Given online 카메라, When sim-rtsp·sim-camera 정지, Then 프로브 2회 실패 후 `state=offline`, 경과 ≤ HEALTH_PROBE_INTERVAL_S×OFFLINE_AFTER_FAILURES+PROBE_TIMEOUT_S(≤ OFFLINE_DETECT_MAX_S), `health_sample` 실패 2행 | integration | HEALTH_PROBE_INTERVAL_S 3s·PROBE_TIMEOUT_S 1s로 축소 |
| T-025 | FR-009 | Given 카메라 응답 지연 5s+, When 프로브, Then 타임아웃 후 다른 카메라 프로브가 지연되지 않음(동시 실행) | unit | asyncio 목 |
| T-026 | FR-004 · A4-S | Given 등록 시리얼 S1, When `PATCH ip`로 시리얼 S2인 sim-camera를 가리킴, Then 409 camera-identity-mismatch, ip 미변경; 주기 프로브 요청에 자격증명 헤더 없음 — ONVIF 무인증, RTSP DESCRIBE에 `Authorization` 없음·401 응답도 생존 판정 | integration | sim-camera ×2 |
| T-027 | FR-010 · SC-003 | Given offline 전환, Then `event(camera.offline)` 1행, `alert(sent)` 1행, sim-ntfy 수신 시각 − `event.ts` ≤ ALERT_DELIVERY_MAX_S | integration | sim-ntfy |
| T-028 | FR-010 | Given 같은 카메라 offline→online→offline이 ALERT_DEDUP_WINDOW_S 안, Then offline 알림 1건만, 이벤트는 3행 | unit | freezegun |
| T-029 | FR-010 | Given 4대 동시 offline(같은 프로브 주기), Then 알림 1건 "카메라 4대가 연결이 끊겼어요", 이벤트 4행 | integration | sim ×4 |
| T-030 | FR-011 · SC-009 | Given 스냅샷 요청, Then 201 `sha256`이 파일 해시와 일치, 경로 `/data/snapshots/{cam}/`, `reason=manual`; offline→online 복구 시 `reason=recovery` 자동 1건 | integration | sim-camera snapshot URI |
| T-031 | FR-011 | Given `snapshot_uri=null` 카메라, When 스냅샷, Then go2rtc `frame.jpeg` 폴백 1회 호출, 성공 | integration | go2rtc |
| T-032 | FR-011 · A1-I | Given 서명 URL, When `exp` 지난 뒤·`sig` 변조 요청, Then 401; 정상이면 200 image/jpeg | integration | — |
| T-033 | FR-012 · SC-003 | Given SSE 구독, When 카메라 offline, Then `camera.state` 이벤트 수신 ≤ SSE_STATE_REFLECT_MAX_S, `Last-Event-ID` 재접속 시 누락 없음 | integration | — |
| T-034 | FR-013 | Given ntfy 설정 저장, When 테스트 발송, Then 202·sim-ntfy 수신; sim-ntfy 다운이면 502 notify-channel-failed, detail="알림 서버가 응답하지 않아요" | integration | sim-ntfy |
| T-035 | FR-013 · INV-3 | Given 토큰 저장, When `GET /api/settings/notifications`, Then `token_set=true`·토큰 값 없음 | unit | — |
| T-036 | FR-014 | Given 디스크 잔여 DISK_MIN_FREE_PCT-1(목), Then `hub.disk_low` 이벤트·알림; CPU 5분 CPU_5M_MAX_PCT+1이면 `hub.cpu_high`; 온도 HUB_TEMP_MAX_C+1이면 `hub.temp_high` | unit | psutil 목 |
| T-037 | FR-014 · 엣지 | Given 잔여 DISK_MIN_FREE_PCT 미만, When 스냅샷, Then oldest 1건 삭제 후 생성; 잔여 DISK_STOP_PCT 미만이면 507 storage-exhausted | integration | 디스크 목 |
| T-038 | FR-015 · SC-009 · INV-2 | Given 스냅샷 SNAPSHOT_RETENTION_DAYS+1일 전 1건·−1일 전 1건, When RetentionJob, Then +1일 건 파일·행 삭제, −1일 건 유지; health raw HEALTH_RAW_RETENTION_DAYS+1일 전 행 삭제; 감사 로그는 어떤 나이여도 유지(INV-6) | integration | faketime |
| T-039 | FR-015 · INV-2 | Given 설정으로 보관 31일 시도, Then `PUT /api/settings/privacy` 본문에 보관 필드 없음(읽기 전용) → 무시·상수 유지 | unit | — |
| T-040 | FR-016 · INV-6 | Given 감사 행, When `UPDATE`/`DELETE` 시도, Then SQLite 트리거 ABORT | unit | 임시 DB |
| T-041 | FR-017 | Given sim-ntfy 다운, When 이벤트 ALERT_QUEUE_MAX+20건, Then `alert` queued ALERT_QUEUE_MAX·dropped 20, `alert.dropped` 이벤트 1건(count=20); 복구 후 ALERT_QUEUE_MAX건 순서대로 sent | integration | sim-ntfy 실패 주입 |
| T-042 | FR-018 · SC-009 | Given sim-rtsp에 오디오 트랙 포함, When 등록 후 go2rtc 스트림 정보 조회, Then 오디오 트랙 0 | integration | go2rtc `/api/streams` |
| T-043 | FR-019 | Given 설정 화면, Then 체크리스트 4항목·보관 `snapshot_days`=SNAPSHOT_RETENTION_DAYS·`max_days`=RETENTION_MAX_DAYS·`audio_enabled=false` 표시, 저장 후 재조회 일치 | integration + E2E | — |
| T-045 | SC-001 | Given 실 Pi 5 + 카메라 4대(H.264 sub 720p), When ms 타이머 촬영 20회, Then glass-to-glass p95 ≤ LIVE_LATENCY_P95_MS | lab | 수동 체크리스트 |
| T-046 | SC-002 | Given 실 PTZ 카메라, When 패드 press 20회 화면 녹화, Then 움직임 시작 p95 ≤ PTZ_CMD_P95_MS | lab | — |
| T-047 | SC-004 | Given 카메라 전원 복구, Then 타일 재생 재개 ≤ OFFLINE_DETECT_MAX_S + RECONNECT_BACKOFF_MAX_S (10회) | integration(sim 재시작) + lab | — |
| T-048 | SC-005 | Given 4대·뷰어 3·30분(WebRTC), Then CPU 5분 평균 ≤ CPU_5M_MAX_PCT, 가용 RAM ≥ RAM_MIN_FREE_MB (`vmstat` 로그); 변형: 뷰어 1명 MSE 강제(스트림 4개 릴레이)에서도 동일 기준 | lab | — |
| T-049 | SC-006 | Given 24h 운영, Then rootfs 쓰기 ≤ SD_WRITE_MAX_MB_DAY (`iostat` 누적) | lab | — |
| T-050 | SC-007 | Given 전원 급차단 10회, Then 매번 부팅·`GET /api/health` 200·`integrity_check`=ok | lab | — |
| T-051 | SC-008 | Given 무설명 사용자 5인, Then 등록 결정 지점 ≤ UX_MAX_DECISIONS·≤3분 4/5 이상 | lab(사용성) | 화면 플로우 카운트 |
| T-052 | SC-010 | Given Tailscale 밖 인터넷, When `nmap`, Then 열린 포트 0; 로그·DB grep 평문 자격증명 0 | lab + integration(T-011) | — |
| T-053 | 엣지 · FR-005 | Given 브라우저 H.265 WebRTC 미지원(UA 목) + sim-rtsp H.265 변형(libx265 testsrc), When H.265 상세, Then MSE 폴백 시도 후 실패 시 안내 문구(Chrome 136+ / Safari 18+) | E2E | Playwright firefox · sim-rtsp(h265) |
| T-054 | 엣지 · FR-017 | Given NTP 미동기(시계 2020년) + sim-ntfy 자체 서명 TLS, When ntfy TLS 검증 실패, Then 큐 보관, 시각 동기 후 재전송 | integration | faketime · sim-ntfy(tls) |
| T-055 | A4-E | Given sim-camera가 XXE 페이로드 SOAP 응답, Then zeep 파서가 외부 엔티티를 해석하지 않음(파일 읽기 0) | unit | 악성 XML 픽스처 |
| T-056 | INV-5 · A3-I · SC-010 | Given compose 기동, When 호스트 외부 인터페이스(LAN IP)에서 8554·1984 접속, Then 거부; 8443·8555만 열림; 127.0.0.1:1984는 응답 | integration + lab | `ss -ltn`·nmap |
| T-057 | FR-009 · FR-005 | Given 카메라 2대 등록, When go2rtc 컨테이너 재시작, Then HEALTH_PROBE_INTERVAL_S 내 `/api/streams`에 스트림 4개 재등록, `hub.stream_resynced` 이벤트 1건, 그리드 타일 재생 재개 | integration | go2rtc |
| T-058 | FR-009 · A7-D | Given HealthProber 태스크를 강제 예외로 중단, When PROBE_STALE_FACTOR×HEALTH_PROBE_INTERVAL_S 경과, Then 내부 감시 태스크가 `exit(1)`로 프로세스 종료(종료 직전 `/api/health` 503) → docker restart 정책이 재기동 → 재기동 후 프로브 재개·`/api/health` 200 | integration | 태스크 kill 훅 · compose restart 관찰 |
| T-059 | FR-005 · MSE_MAX_STREAMS | Given MSE 폴백 강제(WebRTC 비활성 UA), When MSE_MAX_STREAMS+1번째 MSE 스트림 WS, Then 429 mse-streams-exceeded + 안내 문구; MSE_MAX_STREAMS개까지는 fMP4 수신 | integration + E2E | Playwright firefox |
| T-060 | FR-009 | Given online 카메라, When sim-camera(ONVIF)만 정지·sim-rtsp 유지, Then OFFLINE_AFTER_FAILURES회 후 `state=degraded`, 타일은 재생 유지 + PTZ·스냅샷 버튼 비활성 안내 | integration | sim-camera 정지 |
| T-061 | FR-009 | Given 등록 직후 첫 프로브 전, Then `state=unknown`, 첫 프로브 성공 시 online, 이벤트 0건(unknown→online은 알림 아님) | unit | — |
| T-062 | SC-003 | Given 실 카메라 전원 차단(스톱워치), Then 타일 offline ≤ OFFLINE_DETECT_MAX_S, 푸시 수신 ≤ OFFLINE_DETECT_MAX_S + ALERT_DELIVERY_MAX_S, 10회 전부 | lab | — |

누락 검사: SC-001~010 전부 T-045~052·T-062·T-024·027·030·033·038·042·056에 매핑, FR-001~020 전부 ≥1행(FR-020은 T-019), INV-1~6 각각 T-008·038·011·015·056·040, 상태값 4종 T-024·060·061. P2 FR-021~026은 P2 착수 시 추가.

## 계약 테스트 (05 엔드포인트 표 기준)
- **스키마**: `schemathesis run openapi.yaml --checks all` — 전 엔드포인트 응답이 05 스키마·상태코드와 일치, 에러는 `application/problem+json` + `type` slug가 05 문제 유형 표 안에 있음.
- **에러 포맷**: 500 경로(DB 파일 잠금 주입)에서 본문에 스택트레이스·예외 클래스명 0 (Zalando #176).
- **멱등성 재시도**: E-06·E-14·E-23 — 같은 키 3회 → 부작용 1회·응답 동일(본문·상태·Location).
- **인증 경계**: 표의 "hub" 스코프 전 엔드포인트 쿠키 없이 401, `E-20`은 200, `E-25`는 서명 검증.
- **리스너 경계**: 외부 인터페이스에서 go2rtc 8554(비활성)·1984(localhost) 닫힘, 8443·8555만 열림 (T-056).
- **페이지네이션**: E-24·E-26·E-27·E-33 `limit=201` → 422, 커서 순회로 전량 도달·중복 0.

## E2E 후보 (Playwright, compose 스택 + 시뮬레이터 — 핵심 여정 3)
1. **첫 설치 → 등록 → 라이브**: setup → login → 카메라 찾기 → 등록 → 그리드 타일 재생(T-018) → 스냅샷 → 서명 URL 이미지 로드. 상태 4종(빈/로딩/에러/stale) 스크린샷 아티팩트.
2. **PTZ**: 상세 진입 → 패드 press/release → 프리셋 저장·이동 → 감사 목록에 행 (T-019·T-023 UI 경로).
3. **끊김 → 알림 → 복구**: sim-rtsp 정지 → 타일 stale 배지 + 카운트다운 → SSE 반영 → sim-ntfy 수신 → sim 재시작 → 타일 복귀 + 복구 스냅샷 (T-024·027·033·047).
설정: `retries: 2`(CI), `trace: on-first-retry`, `video: retain-on-failure`, chromium 필수·firefox는 T-053만.

## 리스크 기반 커버리지 목표
| 영역 | 목표 | 왜 |
|---|---|---|
| AuthModule·세션·잠금·CSRF·서명 URL | 분기 ≥ 95% | 위협모델 A1(단일 실패점) |
| 자격증명 암복호·로그 마스킹(INV-3) | 분기 100% + T-011 grep | A4-I·A5-I 잔여 리스크의 유일한 완화 |
| HealthProber 상태 기계·EventAlertRouter dedup/큐 | 분기 ≥ 90% | P0 G3, 알람 폭주·유실 |
| RetentionJob(INV-2) | 분기 100% | 법(30일) |
| PTZ 스로틀·hold 타이머 | 분기 ≥ 90% | 카메라가 멈추지 않는 물리 리스크 |
| 화면 템플릿·htmx 배선 | E2E 3여정 + 라인 ≥ 60% | 낮은 리스크, 위층에서 확인 |
| go2rtc 클라이언트 | 통합 T-007·014·015·042 | 외부 바이너리 계약 |
