# 테스트 설계 — PiCam Watch

> 근거 (A6 진입 사전조사, 검색 1회, 2026-09-03)
> - **정량**: 하드웨어 없이 ONVIF Device/Media/PTZ/Events(PullPoint) + RTSP H.264를 제공하는 mock PTZ 카메라가 존재(ContinuousMove/Stop/Preset 지원, Docker) ([10bedicu/mock-ptz-camera](https://github.com/10bedicu/mock-ptz-camera) · [bugrauluyurt/onvif-devices](https://github.com/bugrauluyurt/onvif-devices) macvlan 디스커버리) → 통합·계약·E2E의 80%는 mock 카메라로 CI에서, 실카메라는 주 1회 HW 리그에서만.
> - **정성**: "PTZ 버튼은 보이는데 카메라가 안 움직인다"류 보고([Frigate #11087](https://github.com/blakeblackshear/frigate/discussions/11087))는 mock으로 재현 불가 → **실카메라 회귀 스위트**를 별도 레이어로 둔다.
> - **사용자 영향**: 시청자가 겪는 실패는 "재생이 안 됨"·"카메라가 안 멈춤"·"클립이 없음" 3가지 — 이 세 여정만 E2E로 고정한다(L-06 막다른 에러 0).

스킬: `ecc:tdd-workflow`(RED 게이트·독립 테스트·AAA 구조·목킹 패턴 적용. **"80%+ 일률 커버리지"는 리스크 기반 목표로 대체** — decision-log #18) · `ecc:e2e-testing`(POM·auto-wait 로케이터·`--repeat-each`로 flaky 판별·quarantine 규칙·아티팩트 보관 적용)
개정: v1.1 — PRD v1.1 반영. **v1.2 (2026-09-03, GATE 반영)** — SC-009·SC-014·E-15·E-16·FR-027/028/029 시나리오 추가, SC-001/004 HW rig 행 명시, 계수 재검산(P0 17·P1 9), 엔드포인트 ID를 05 v1.2 접두사로, E-2 차단 방법을 OS 방화벽으로, WS Origin 검사, 오디오 부재 검사.

## 원칙
- AC는 개발 시작 전에 존재한다. 각 시나리오는 처음엔 **반드시 실패(RED)** 해야 한다 — 컴파일/실행되어 의도한 이유로 실패한 것만 RED로 인정.
- 피라미드 ≈ unit 70 / integration+contract 20 / E2E 10. 하위에서 검증된 것은 상위에서 반복하지 않는다.
- 카메라 의존 테스트는 **mock 카메라(CI)** 와 **실카메라 리그(주 1회)** 로 분리, 실카메라 결과는 "검증된 카메라 목록"에 기록.
- 하드웨어 시간·전원 테스트(SC-011)는 리허설 체크리스트로 수행하고 결과를 07 운영 문서에 기록.
- MVP 게이트 = SC-002(P2)·SC-014 실카메라(P1 게이트)를 제외한 SC 전부 pass.

## 레이어 정의
| 레이어 | 도구 | 대상 | 목킹 |
|---|---|---|---|
| unit | pytest | 스코프 검사, 세션 한도 계산, 클립 연장 판정, 보존·E-11 보정 계산, ULID 커서, Problem 변환, 링 세그먼트 선택, 암호화, 해시 체인, PTZ 범위 판정 | 전부 목 |
| integration | pytest + SQLite 임시 파일 + mock 카메라 컨테이너 | EventIngest·ClipRecorder·RecorderSupervisor·RetentionJob·BootReconcile·PtzController·StreamRegistrar·StreamHealth·BackupJob | go2rtc 실행, ffmpeg 실행, 카메라 mock |
| contract | schemathesis(OpenAPI 1.2.0) + pytest | 05 엔드포인트 표 전부 | mock 카메라 |
| E2E | Playwright(chromium + mobile-chrome) | 3 여정 | mock 카메라, 실 브라우저 WebRTC |
| HW rig | 수동 체크리스트 + 스크립트 | SC-001/001b/002/003/004/011/014, 실카메라 PTZ | 없음 |

## 수용 기준 → 시나리오 변환표
| SC/FR | Gherkin 시나리오 | 레이어 | 데이터/목킹 |
|---|---|---|---|
| FR-001 | Given mock 카메라 2대가 같은 세그먼트(macvlan)에 있고 host 네트워크 / When `POST /camera-discoveries` / Then 10s 내 200, data 길이 2, 각각 xaddr 포함 | integration | onvif-devices |
| FR-002 | Given 유효한 ONVIF 주소·자격증명(main H.264 4Mbps GOP 2s, sub H.264) / When `POST /cameras` / Then 201, `ptz_supported=true`, `record_profile=main`, `max_rtsp_sessions` 양수, warnings 빈 배열, Location 헤더 | contract | mock-ptz-camera |
| FR-002 (실패) | Given 잘못된 비밀번호 / When `POST /cameras` / Then 502 `camera-unreachable`, DB에 행 없음 | contract | mock 401 |
| FR-002 / E-12 / INV-1 | Given 카메라가 H.265 프로파일만 제공 / When 등록 / Then 415 `codec-unsupported`, detail에 "H.264 서브스트림" 안내, DB 행 0, 트랜스코딩 프로세스 0개 | integration | mock H.265 only |
| FR-002 / E-13 | Given main H.265 + sub H.264 / When 등록 / Then 201, `record_profile=sub`, warnings에 `main-h265-not-playable` | contract | mock |
| FR-002 / E-14 | Given main GOP 8s / When 등록 / Then 201, warnings에 `gop-over-4s` | contract | mock GOP 설정 |
| FR-002 (비트레이트) | Given main 6Mbps / When 등록 / Then `record_profile=sub`, warnings에 `bitrate-over-4mbps` | contract | mock |
| FR-003 / INV-5 | Given 카메라 등록됨 / When `GET /cameras/{id}`, 로그 전체 grep, `curl 127.0.0.1:1984/api/streams`(인증 없음), `/data/config/go2rtc.yaml` grep, 백업 아카이브 `age -d` 없이 strings / Then 비밀번호 문자열 0회, go2rtc 무인증 401, yaml에 `rtsp://` 0회, DB `password_enc` ≠ 평문 | unit + integration | 고정 비밀번호 `Zz9!test` |
| FR-003 (재등록) | Given go2rtc 컨테이너 재시작 / When 30s 경과 / Then StreamRegistrar가 `{cam}_sub/_main/_rec` 재등록, 라이브 복구 | integration | — |
| FR-004 / SC-001 | Given 로그인 Viewer / When 그리드 진입 / Then 각 타일 `POST /whep` 201 + Location, 5s 내 `video.readyState≥2`, `getStats().framesDecoded` 증가 | E2E | mock testsrc |
| FR-004 / E-2 / SC-001b | Given 테스트 호스트 nftables로 8555/udp DROP / When 그리드 진입 / Then 5s 내 `DELETE /whep-sessions` 후 MSE 배지, 재생 진행 | E2E | OS 방화벽(page.route는 HTTP만이라 사용 안 함) |
| FR-005 | Given 그리드 / When 타일 클릭 / Then `profile=main` WHEP 1회, 단일 뷰 렌더; main H.265 카메라면 415 후 sub로 재요청 + 표시 | E2E | — |
| FR-006 / SC-007 / E-1 | Given 카메라 4대 online / When mock 1대 정지 / Then 10s 내 HLT-2 status=offline(StreamHealth), 타일 offline, 나머지 3대 유지; mock 재기동 후 60s 내 online | integration + E2E | mock stop/start |
| FR-007 / SC-004 | Given Owner / When `POST /ptz/moves {pan:0.5}` 100회 / Then 202 ≤500ms(p95), mock이 GetStatus + ContinuousMove(Timeout=PT1S) 수신 | contract + integration | mock 수신 로그 |
| FR-007 (홀드) | Given PTZ 패드를 3s 누름 / When 갱신 루프 / Then moves 요청 4회(750ms ±100ms), mock 수신 간격 ≤1s, keyup 후 Stop 요청 ≤100ms | E2E + integration | mock 타임스탬프 |
| FR-007 / SC-004 (실카메라) | HW rig: Stop 후 실제 정지 ≤ 1.0s(영상 확인), 3초 홀드 시 연속 이동 끊김 0 | HW rig | 실카메라 |
| FR-024 / E-5 | Given mock이 Stop에 무응답 / When stop / Then 1.5s 내 Stop 3회 재전송, `ptz_fault=1`, 감사 `ptz.fault`, 이후 moves 409 `ptz-fault` | integration | mock fault 모드 |
| FR-008 | Given PTZ 카메라 / When 프리셋 생성·목록·이동·삭제 / Then 201/200/202/204, mock의 SetPreset/GotoPreset/RemovePreset 수신 | contract | — |
| FR-008 / E-7 | Given 프리셋이 카메라에서 삭제됨 / When recalls / Then 404 `preset-not-found` | contract | mock 프리셋 제거 |
| FR-009 / SC-010 / E-6 / E-16 | Given Viewer 세션 / When Owner 전용 엔드포인트 17종(CAM-1/3/5/6, PTZ-1/2/4/5/6, CLIP-3/4, SET-2/3, AUD-1/2, USR-1/2/3) 전부 호출 / Then 100% 403 Problem JSON, 감사 `authz.denied` 17건 | contract | 파라미터화 |
| FR-010 / SC-005 | Given Owner / When PTZ moves 100회 / Then `audit_log` 100건, `GET /audit-logs/verification` ok=true, `picam audit verify` exit 0, `UPDATE/DELETE audit_log` → ABORT | integration + unit | — |
| FR-010 (열람·반출) | Given Viewer / When WHEP 생성→DELETE, `CLIP-2 intent=view` / Then 감사 `stream.open`·`stream.close(duration)`·`clip.view`; Given Owner / When `CLIP-3` / Then `clip.download` | contract | — |
| FR-010 (체인 변조) | Given 감사 행 50건 / When 파일 직접 편집으로 25번째 행 변경 / Then verification ok=false, first_broken_id = 25번째 | unit | SQLite 파일 조작 |
| FR-011 | Given purpose·coverage_area·operating_hours 중 하나 누락 / When `POST /cameras` / Then 422 errors[].field 해당 필드 | contract | 파라미터화 3건 |
| FR-021 (P2) | Given FR-011 5항목 입력 / When `GET /cameras/{id}/signage` / Then 문구에 목적·장소·촬영범위·촬영시간·관리책임자 성명·연락처 전부 포함 | contract | — |
| FR-012 / SC-006 / INV-4 | Given retention 1일 클립, 시계 +25h / When 파기 잡 / Then 파일 없음, status=purged, 감사 `clip.auto-purge`(system), `GET /clips/{id}` 410 | integration | freezegun + tmp 디렉터리 |
| FR-012 / E-10 | Given retention 30→7 변경 / When 저장 후 파기 잡 / Then 7일 초과 클립만 파기 | integration | — |
| FR-013 / E-8 | Given mock이 t=0, 15, 25s에 Motion 발행 / When 폴링 / Then EVENT 1건, end_at = t+45s, 클립 duration 53~57s | integration | mock 이벤트 주입 |
| FR-013 (상한) | Given 모션 150s 연속 / Then end_at = start+120s, 클립 ≤ 130s, 두 번째 이벤트가 이어서 생성 | integration | — |
| FR-013 (파싱 실패) | Given PullMessages 응답 Topic=None / When 폴링 / Then 해당 카메라만 재구독, 다른 카메라 정상 | unit | XML 픽스처 |
| FR-014 / SC-008 | Given 링버퍼(TS 10s×4) / When 모션 100회(각 60s 간격) / Then 클립 ≥99개, duration 28~32s, sha256 = 파일 해시 | integration | ffmpeg testsrc GOP 2s |
| FR-014 (전 10초) | Given 이벤트 시각 T / When 클립 생성 / Then 첫 프레임 PTS ≤ T−8s, 진행 중 세그먼트 포함, DTS 불연속 0(ffprobe) | integration | GOP 2s testsrc |
| FR-014 (링 포화·크기 상한) | Given 6Mbps 소스 강제 / When 60s 가동 / Then tmpfs ≤ 128MB, HLT-2 `ring_buffer.bytes` 보고; 200MB 도달 클립은 종료 | integration | — |
| FR-015 | Given 이벤트 3건 / When `GET /events?camera_id&limit=2` / Then 2건 + next_cursor, 두 번째 페이지 1건 | contract | — |
| FR-015 | Given 클립 / When `CLIP-2` Range: bytes=0-1023 / Then 206, 1024B; Viewer `CLIP-3` → 403 | contract | — |
| FR-016 / E-9 | Given 볼륨 90% / When 새 클립 저장 / Then 최구 클립 purged, 신규 stored, 경고 플래그 HLT-2 | integration | loop 디바이스(100MB) |
| FR-017 | Given Owner 계정 / When 로그인 성공·실패 / Then 쿠키 HttpOnly·Secure·SameSite=Lax, 실패 401·감사 `auth.login.fail`; `X-Requested-With` 없는 PATCH → 403; WS `Origin: https://evil` → 403 `origin-not-allowed` | contract | — |
| FR-018 | Given 빈 DB, setup-token 파일 / When 잘못된 토큰 5회 → 423, 올바른 토큰 → 201 + `recovery_key`, 재호출 → 409 | contract | — |
| FR-019 / SC-013 | Given 등록 카메라 / When go2rtc `/api/streams`(API 인증) 조회, ffmpeg 인자, ffprobe(링·클립), OpenAPI 스키마 grep / Then 스트림 URL에 `#media=video`, 인자에 `-an`, 오디오 트랙 0, OpenAPI에 `audio` 문자열 0회 | integration + contract | ffprobe |
| FR-022 | When `GET /health` 무인증 / Then 200 `{status}`만; `HLT-2`·`HLT-3` Viewer → 403; `HLT-2.backup.last_ok_at` 존재 | contract | — |
| FR-023 | When PTZ moves 6회/1s / Then 6번째 429 + Retry-After; 750ms 루프(4회/3s)는 429 없음 | contract | — |
| FR-025 | Given 재생 중 / When mock 스트림 3s 정지 / Then 타일 stale 배지 ≤ 4s | E2E | mock pause |
| FR-026 | Given `recording` 이벤트 1건 + 클립 파일 1바이트 변조 / When 서비스 재시작 / Then 이벤트 `failed`, 감사 `boot.reconcile`·`clip.hash-mismatch`, HLT-2 `boot_reconcile{1,1}` | integration | 파일 조작 |
| FR-027 / E-15 / SC-014 | Given ptz_limits pan ±30° / When 범위 밖으로 홀드 이동 20회 / Then 경계 도달 갱신에서 Stop + 409 `ptz-out-of-range`, 감사 `ptz.denied-range` 20건, mock 위치 초과 ≤ 5° | integration + contract | mock GetStatus |
| FR-027 (위치 미지원) | Given `position_status=false` 카메라, home_preset 설정 / When 5분 유휴 / Then GotoPreset(home) 1회, 감사 `preset.recall`(system) | integration | mock 캡 제거 |
| FR-027 / SC-014 (실카메라) | HW rig: 실카메라 1종에서 동일 절차, 초과 각도 ≤ 5° | HW rig | 실카메라 |
| FR-028 (P2) | Given webhook_url 설정 / When SET-3, 카메라 offline 5분, 5분 경과 / Then 수신처에 test·camera.offline·heartbeat POST 각 1건(https만 허용, http URL은 422) | integration | 로컬 HTTPS 수신 mock |
| FR-029 | Given Owner / When USR-2 viewer 생성, USR-4 비밀번호 변경, 마지막 Owner USR-3 / Then 201, 204, 409 `last-owner` | contract | — |
| E-3 / INV-8 | Given 사용자 A 세션 스트림 4개 / When 5번째 / Then 409; Given 전역 16 / When 17번째(다른 사용자) / Then 409, 기존 유지; 탭 강제 종료 후 10s 내 카운트 감소; SET-2로 16 초과 설정 시 422 | contract + integration | go2rtc 소비자 동기화 |
| E-4 | Given Owner A 이동 중 / When Owner B 반대 명령 / Then mock 마지막 벡터 = B, 감사 2건 | integration | — |
| E-11 | Given NTP 미동기(시계 1970) / When 이벤트 / Then `time_unsynced=1`·`mono_offset_s` 기록, 파기 잡 건너뜀; NTP 동기 시 `created_at` 보정·`expires_at` 재계산; 단조 90일 경과 시 파기 | unit + integration | chrony 상태 목, freezegun |
| SC-001 / SC-001b | HW rig: LAN WebRTC 30회 p95 ≤ 1.0s; 8555/udp 차단 MSE 30회 p95 ≤ 3.0s | HW rig | 실카메라 |
| SC-002 (P2 게이트) | HW rig: LTE 기기 + Tailscale DERP 강제 30회 p95 ≤ 2.5s, ICE 후보에 100.x 포함 | HW rig | 실카메라 |
| SC-003 | HW rig: 4대 × 시청자 4명(16세션) → CPU 5분 평균 ≤60%, go2rtc 드롭 ≤1% | HW rig | 실카메라 |
| SC-009 | 24h 소크: mock 4대 + 시청 세션 8개 상시, HLT-3 롤업 가용성 ≥ 99.5%, 재접속·좀비 재시작 이벤트 기록; 운영 30일 ≥ 99%는 07 SLI로 측정 | integration(소크) + 운영 | mock |
| SC-011 | HW rig: PTZ 1회 직후 전원 차단 ×10 → 부팅 10/10, `integrity_check`=ok, 직전 `ptz.move` 감사 행 보존 10/10, 고아 recording 0, 링 비어 있음 | HW rig | — |
| SC-012 / UX-02 | 5인 사용성: 설명 없이 라이브→클립 재생 ≥4/5 | 수동 | 프로토타입 |
| 백업(07) | Given USB 마운트 / When BackupJob / Then `/mnt/backup/*.age` 생성, `age -d`로 복원 후 DB `integrity_check` ok, 매니페스트 `row_hash` 일치, 감사 `backup.ok`; USB 없음 → `backup.fail` + HLT-2 경고 | integration | tmp 마운트 |

누락 확인(ID 전수 대조): SC-001, 001b, 002, 003, 004, 005, 006, 007, 008, 009, 010, 011, 012, 013, 014 = 15/15 · P0 FR 17건(001~014, 017, 019, 024) 17/17 · P1 FR 9건(015, 016, 018, 022, 023, 025, 026, 027, 029) 9/9 · P2 FR 3건(020은 SC-002 HW rig, 021, 028) 3/3 · E-1~E-16 16/16. **누락 0.**

## 계약 테스트 (A5 엔드포인트 표 기준)
- schemathesis로 OpenAPI 전 경로 퍼징: 스키마 위반 응답 0, 5xx 0(카메라 오류는 502/503으로 매핑).
- 에러 포맷: 모든 4xx/5xx가 `application/problem+json` + `type/title/status`, `detail`에 스택·경로·SQL 문자열 0회.
- 멱등성: `POST /cameras` 같은 `Idempotency-Key` 2회 → 두 번째 응답 동일, DB 행 1개; 키 재사용 + 다른 본문 → 422 `idempotency-mismatch`.
- 커서: 100건 생성 후 `limit=20` 5페이지 순회 → 100건 정확히 1회씩, 중간 삽입 후에도 중복 0.
- 인가 매트릭스: (Viewer, Owner, 무세션) × 전 엔드포인트 → 기대 코드 표와 일치. CSRF: `X-Requested-With` 누락 시 REST 상태 변경·WHEP 403; WS는 `Origin` 검사.
- go2rtc 경계: API가 go2rtc 응답 본문을 그대로 전달하지 않음(SDP만), `/api/streams` 원문이 어떤 응답에도 미포함.
- 오디오 부재: OpenAPI·설정 스키마·DB 스키마에 `audio` 키 0(SC-013).

## E2E 후보 (Playwright POM: LoginPage · GridPage · CameraViewPage · EventsPage)
1. **라이브 여정**: 로그인 → 그리드 4타일 재생 → 타일 클릭 → 단일 뷰 → 1대 mock 정지 → offline 표시 → 재기동 → 복구 → 탭 닫기 → 감사 `stream.close`. (FR-004/005/006/010/025)
2. **제어 여정**: Owner 로그인 → 단일 뷰 → PTZ 패드 3s 홀드/뗌 → mock 수신 확인 → 범위 경계까지 이동 → 409 토스트 → 프리셋 저장·이동 → 설정 > 감사로그에 move·denied-range·stop·preset 항목 + 체인 검증 ok. Viewer 로그인 → PTZ 패드 미표시 + API 직접 호출 403. (FR-007/008/009/010/027)
3. **보존 여정**: mock 모션 발생 → 이벤트 화면에 새 항목 ≤ 35s → 클립 재생(감사 clip.view) → Viewer는 다운로드 버튼 없음 → Owner 다운로드(감사) → Owner 삭제 → 410. (FR-013/014/015, CLIP-3/4)
- flaky 전략: WebRTC 재생 판정은 `video.readyState`·`getStats().framesDecoded` 증가로(`waitForTimeout` 금지), 신규 E2E는 `--repeat-each=10` 통과 후 편입, 실패 시 `test.fixme(이슈#)` 격리, trace/video는 실패 시만 보관, CI 재시도 2회.

## 리스크 기반 커버리지 목표
| 영역 | 근거 | 목표 |
|---|---|---|
| 인가·스코프·감사로그·해시 체인 (FR-009/010, 위협 1위) | 법 제25조⑤·⑦ 직결 | 라인 100%, 인가 매트릭스 전수 |
| PTZ 갱신·범위·워치독·fault (FR-007/024/027, 위협 3위) | 목적 외 촬영 우려 | 분기 100% (무응답·부분응답·범위 경계·예외) |
| 보존·파기·시간 보정·부팅 정합성 (FR-012/026, E-10/11) | 법정 보존 | 분기 100% + 시계 조작 테스트 |
| 자격증명 암호화·로그 마스킹·go2rtc 경계·백업 암호화 (FR-003, 위협 2위) | 시크릿 | 100% + 로그·yaml·아카이브 grep 게이트 |
| 오디오 부재 (FR-019, INV-6) | 녹음 금지 | 스키마·설정·ffprobe 게이트 |
| 스트림 프록시·세션 한도 (STR-1/3, INV-8) | 가용성 | 라인 80% |
| 링버퍼·클립 조립·연장 (FR-013/014) | 데이터 정확성 | 통합 시나리오 5종 + 전 10초·DTS 연속 검증 |
| UI 컴포넌트 | 사용성 | 4 상태(빈/로딩/에러/stale) 스냅샷만, 라인 목표 없음 |
