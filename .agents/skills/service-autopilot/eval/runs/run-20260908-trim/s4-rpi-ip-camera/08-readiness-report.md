# 준비도 리포트 — PiCam Watch
버전: v1.1 · 기준 03 v1.3 (04 v1.3 · 05 v1.2 · 06 v1.1 · 07 v1.1)
검토: 독립 검토관(fable, fresh context) 1회 · 2026-09-09 06:25 · 입력 = 00~07 + decision-log + REVISIONS + 검증 스크립트 출력(CRITICAL 0 · HIGH 0)
강도: full · 재검토 없음(예산 규칙) · 돈·안전·법 도메인이지만 검토관 1명(이탈, 예산)
R4 패치 라운드 1회 (2026-09-09, REVISIONS.md R4 · decision-log DL-020~029) — 검토관 재투입 없이 문서 반영, 검증 스크립트 재실행 CRITICAL 0 · HIGH 0

## 판정: CONCERNS

검토관 사유: 커버리지(FR·SC 매핑)와 개정 전파는 대부분 정합하고, 단계 재실행이 필요한 결함은 없다. 그러나 04 시퀀스의 구 토픽·경로 잔존,
읽기전용 rootfs와 OTA의 충돌, 릴레이 강등 메커니즘 공백을 "미결정 0건"으로 덮은 것, 측정 불가 SC-008, 저장 상수 모순은 이 문서만 들고
구현을 시작하면 첫 스켈레톤에서 되돌아오게 만든다. CRITICAL #1은 규칙 위반이지만 01 fit 행 추가로 닫히므로 FAIL 사유가 아니다.

R4 패치 후: 위 사유의 구 토픽·경로 잔존(#8·#9), rootfs↔OTA 충돌(#3), 릴레이 강등 공백(#4), SC-008(#5), 저장 상수 모순(#7)은 전부 문서에 반영됐다(발견 목록의 `→ 반영(R4)`). 판정은 재검토 없이 CONCERNS를 유지한다(예산 규칙) — 남은 것은 아래 선행 조건 2개뿐이다.

## 검토 결과 요약

| 심각도 | 건수 | 이 턴에 반영 | 핸드오프 선행 조건으로 이관 |
|---|---|---|---|
| CRITICAL | 1 | 1 — #1 웹 프론트엔드·Caddy fit 근거 (01 행 추가, DL-019, R3) | 0 |
| HIGH | 6 | 6 — #2 (R3, 04 v1.2) · #3~#7 (R4, DL-020~023) | 0 |
| MEDIUM | 15 | 15 — #8~#22 (R4; 정책 결정 #14·#15·#18·#19·#20·#22는 DL-021·024~027로 단순한 쪽 채택) | 0 |
| LOW | 8 | 7 — #23~#27·#29·#30 (R4, DL-028·029); #28은 정보로 유지 | 0 |

타당성 필터링: 30건 전부 타당(거짓 양성 0). #28(TURN_RELAY_MAX_MBPS 여유)은 가드 상수의 의도된 여유라 정보로 분류하고 유지. R4(2026-09-09): R3 미반영 28건 중 27건 반영 · 이관 0건 — 구 선행 조건 1~5 전부 닫힘.

## 핸드오프 선행 조건 (SPEC 작성 전에 닫는다)

R4에서 닫힌 것: 구 선행 조건 1~5 전부(#3~#27·#29·#30 — 발견 목록 `→ 반영(R4)`, REVISIONS.md R4). 남은 것 2개:

1. **R-2 법 상수 원문 1회 확인** — CLIP_RETENTION_DAYS·AUDIT_RETENTION_DAYS·AUDIT_REVIEW_INTERVAL_DAYS(03 상수 표 "가능성" 3건). law.go.kr 조문 확인 후 값이 다르면 03 상수 표 한 곳만 수정 → R5 + 버전 전파 + 검증 스크립트 재실행. (04 R-2)
2. **R4 자율 결정의 사용자 확인** — 사용자 부재 중 단순한 쪽으로 정한 6건: EVENTS_PER_CAM_DAY_MAX 500→120(DL-023) · 백업에서 이벤트 메타 제외(DL-025) · Pi 재설치 = SSD 인증서 재사용(DL-024) · 릴레이 판정을 클라 `ice_state` 보고에 의존, main 승격은 신규 세션(DL-021) · 이벤트 capability 없는 카메라 등록 허용(DL-026) · E44·Event 파티션·password_enc 제거, INV-7 유지(DL-027). 뒤집으면 해당 DL의 "대안" 쪽으로 R5.

## 검토관 발견 목록 (원문, 심각도순)

**CRITICAL (규칙 위반)**

1. **웹 프론트엔드 스택(React + Tailwind + Zustand)·caddy에 근거가 없다** — 04:97, 07:16,133. 01 fit 표(01:160-172)는 11행 중 웹 UI 행이 없고, DL-004도 프론트엔드를 열거하지 않는다. 검색·fit 요소·기각 대안 없이 04 구조도에 처음 등장한다. 규칙상 "근거 없는 추천". 파급은 작다(상수·SC에 영향 없음) — fit 행 1개 + DL 추가로 닫힌다. → **이 턴에 반영(R3)**

**HIGH**

2. **R1 #15 전파 누락 — 04 시퀀스가 구 토픽 프리픽스를 쓴다** — 04:134 `sites/{s}/devices/{d}/cmd/webrtc.offer` vs 05:119 `devices/{device_id}/cmd/{name}` 및 03 INV-3. 다이어그램↔계약 불일치이자 R1 반영 미완. → **이 턴에 반영(R3, 04 v1.2)**
3. **읽기전용 rootfs(Overlay FS)와 앱 계층 OTA가 충돌한다** — 07:15,21-22. firstrun은 "compose pull → Overlay FS 활성 → 재부팅"인데, 이후 `cmd/deploy`의 digest pull은 `/var/lib/docker`(rootfs = RAM 상위 레이어)에 쓰인다 → 재부팅 시 롤백/배포 상태 소실, RAM 소모. 쓰기 구역 목록에 Docker data-root가 없다. FR-023·SC-010·ADR-4가 운영에서 성립하지 않는다. log2ram 동기화 대상 경로도 SSD로 bind되지 않으면 RAM에 떨어진다. → **반영(R4)**
4. **릴레이 강등(FR-006·엣지 17)의 메커니즘이 미설계인데 03은 "미결정 0건"** — 03:41,110,257 · 04:55,129-143 · 05:79,115. 스트림은 offer 시점(`src=cam_{id}_sub|main`)에 고정되고 ICE 결과는 그 뒤에 결정된다. main→sub 강등은 재협상이 필요하지만 시퀀스에 없고, 서버가 "릴레이로 붙었다"를 아는 경로도 없다(WS 클라→서버는 `ping`만). relay-cap 429도 E27 시점엔 릴레이 필요 여부를 모른다. FR-003의 "메인스트림 전환"도 세션 중 전환 엔드포인트가 없다. → **반영(R4)**
5. **SC-008이 측정 불가** — 03:123 · 06:33. "WATCHDOG_TIMEOUT_SEC + 부팅 시간"에서 부팅 시간이 상수가 아니라 pass/fail 상한이 없다. 같은 행의 "60초 안에 라이브 복귀"는 상수 표 밖 숫자. → **반영(R4)**
6. **상수 표 밖 숫자 직접 기입(규칙 9)** — 03:98 엣지 9 "30초 안에 모션 5회"(= EVENT_COOLDOWN_SEC), 03:15,27 "1초"(= LIVE_LATENCY_P95), 03:123 "60초"; 04:55 "250Mbps × 2"·"약 5%"(R1 #10 부분 반영); 06:55 "30초"; 07:37 healthcheck `interval: 30s, retries: 3`(= HEALTHCHECK_*), 07:85 "1시간 창", 07:112 "30일 순환"(= CLIP_RETENTION_DAYS), 07:119 "30분". → **반영(R4)**
7. **CLIP_STORAGE_MIN_GB와 EVENTS_PER_CAM_DAY_MAX가 서로 모순** — 03:203,213. 저장은 50이벤트/일로 산정(≈75GB×3=256GB)했는데 선언된 상한은 500이벤트/일 → 4캠×500×12.5MB×30일 ≈ 750GB. 상한 근처에서 FR-015 "보존기간 미달 삭제"가 상시 발동해 G3(보존 보장)이 깨진다. 썸네일 최악치(≈120GB, 04:209)도 07에 VPS 디스크 상수·볼륨 크기가 없다. → **반영(R4)**

**MEDIUM**

8. R1 #5 전파 부분 누락 — 04:308 프라이버시 절이 클립 캐시 수명을 STREAM_TOKEN_TTL_SEC로 쓴다(ADR-3·05 E42는 CLIP_CACHE_TTL_MIN). 03:39 FR-004도 "클립 재생 = STREAM_TOKEN_TTL_SEC 토큰"이라 서명 URL TTL과 캐시 TTL이 문서마다 뒤섞인다. DL-013 "새 상수 없음"도 정정되지 않았다. → **반영(R4)**
9. 다이어그램↔계약 경로 불일치 — 04:199 `PUT /devices/{d}/events/{e}/thumbnail` vs 05:109 D01 `PUT /device/events/{event_id}/thumbnail`. 04 시퀀스 2의 `device_offline`·`ptz_locked`도 05 slug(`device-offline`·`ptz-locked`)와 표기가 다르다. → **반영(R4)**
10. 07 SLO 측정이 05에 없는 메시지에 의존 — 07:62 "브라우저가 WS로 first_frame_ms 보고" vs 05:115 클라→서버 `ping`만. 계약 추가 없이는 SLO_LIVE_SUCCESS_PCT 산출 불가. → **반영(R4)**
11. 릴리스 등록 경로 부재 — 07:48 "릴리스 등록(E19 RELEASE 행)"인데 E19는 GET(05:69). CI가 RELEASE를 쓰는 엔드포인트·인증 수단이 없다. → **반영(R4)**
12. 이메일 발송 인프라 부재 — 07:85,88,89 P1 알람이 이메일을 쓰고 FR-019가 이메일 알림을 요구하지만 compose·`.env.example`에 SMTP 관련 항목이 없다. `.env.example`에는 firstrun 클레임 토큰 키, TURN realm/external-ip도 없다. → **반영(R4)**
13. D03 기기 설정 상수 목록 불완전 — 05:112 "기기는 D03으로 자기 몫만"인데 CAM_RECONNECT_BACKOFF·MANUAL_REC_MAX_MIN·RINGBUF_SEGMENT_SEC·DEVICE_CERT_RENEW_BEFORE_DAYS·DEVICE_TURN_CRED_TTL_HOURS가 빠져 있다. → **반영(R4)**
14. 런북 RB(Pi 부팅 불능) 자가모순 — 07:101 "기존 USB SSD 그대로 연결(인증서 보존)"인데 "자동 재클레임 → Admin 승인". 재클레임은 새 device_id를 만들고 `device_one_per_site`(05:262)에 걸린다. 인증서 재사용인지 재클레임(+구 기기 retire)인지 결정 필요. → **반영(R4)**
15. INV-1이 백업에서 깨진다 — 07:109-111. pg_dump(EVENT 메타 포함)를 BACKUP_RETENTION_DAYS 보관하면 이벤트 메타가 최대 CLIP_RETENTION_DAYS + BACKUP_RETENTION_DAYS 존재. thumbs "백업도 파기 잡 대상"은 스냅샷 백업에선 실행 불가 — 미러(rsync --delete)인지 명시해야 한다. → **반영(R4)**
16. 파기 보고 경로 이중화 — 05:123 `events` type에 `clip_ready`·`purged`(상태이지 유형이 아님, DDL CHECK 05:229와 불일치) + 05:124 `cmd/purge.report` — FR-014 매핑(05:297)이 둘 다 지목하고 "파티션 DROP"(R1 #1로 폐기)도 남아 있다. → **반영(R4)**
17. FR-018 매핑 실체 없음 — 05:301 "07 설정"이라 했으나 07에 ALERT_DAILY_MAX 언급 0건. → **반영(R4)**
18. 이벤트 capability 없는 카메라 미처리 — FR-002가 capability.events를 저장하지만 events=false(또는 MotionAlarm 토픽 없음)일 때 US-3(P1)이 성립하지 않는다. 엣지케이스·등록 경고 없음(03:37,84-107). → **반영(R4)**
19. go2rtc ICE 포트 loopback 바인딩 vs STUN 직결 — 07:45 "8555는 localhost 바인딩"이면 srflx 후보 수집이 막혀 전 세션이 릴레이가 된다(가능성). 04 R-1 실증 항목(04:299)에 포함되어 있지 않다. → **반영(R4)**
20. "프레임 드롭 0" 기준의 flaky 위험 — 03:126,130 · 06:36,40. WebRTC `framesDropped`는 정상 상태에서도 0이 아니어서 06의 flaky 0 정책(06:98)과 충돌한다. 허용치 상수가 필요하다. → **반영(R4)**
21. 용어 드리프트 "운영자" — 03은 시설 운영자(사용자 페르소나, 03:27), 07은 알람 수신 운영 담당(07:84-91,120). 같은 단어가 두 역할. → **반영(R4)**
22. YAGNI — INV-7 전 테이블 `workspace_id`(03:76)는 seed 7 non-goal(다중 테넌트)에 대한 hedge; Event 월 파티션(05:238)은 R1 이후 "지역성용"만 남아 일 1회 행 DELETE와 중복; CAMERA.password_enc(05:209) 서버 컬럼은 항상 null; E44·E26(05:77,99)은 FR 없는 엔드포인트. → **반영(R4)**

**LOW**

23. 02 집계 불일치 — 02:2,54 "31항목 / Asked 5 · Assumed 24"인데 표는 33행, Asked 마킹 행은 6(1·3·5·11·OTA·원격접근). → **반영(R4)**
24. 06 절 제목 "SC — 03 v1.1"(06:23) vs 머리 "기준 03 v1.2". → **반영(R4)**
25. 03 목표 표(03:20-22)에 SC-005·SC-015가 어느 목표에도 매핑되지 않음. → **반영(R4)**
26. 권한 문구 불일치 — FR-028 "시청 권한만"(03:63) vs Viewer 스코프에 ptz/record/snapshot(05:17); FR-008 "Admin이 프리셋 이동"(03:43) vs E34 goto는 Viewer 스코프. → **반영(R4)**
27. E11 `/device/claim`이 공개인데 규약(05:16)은 `/api/device/*` = mTLS 전용 — 예외를 명시해야 프록시 설정에서 막히지 않는다. → **반영(R4)**
28. TURN_RELAY_MAX_MBPS 100 > 설계 최대 릴레이 51.2Mbps(SITES_MAX×VIEWER_MAX×sub×2) — 설계 한도 내에서는 SC-015 경로가 도달 불가(가드로는 유효). → 정보로 분류, 유지
29. 07 알람 표에 큐 길이 알람이 없다 — 03 ALARM_WARN_PCT 근거는 "TURN·디스크·큐 공통"(03:240). caddy(TLS 종단)가 04 구조도에 없다. → **반영(R4)**
30. DL-014 "대응 25건"(decision-log:54)은 R1 #9 행 추가 후 26행. → **반영(R4)**

**문제없음 확인 (검토관 근거)**

- FR→엔드포인트: FR-001~030 전부 05 매핑 존재, P0 16건은 구체 엔드포인트/토픽/DDL 지목. FR-018만 실체 없음(#17).
- SC→시나리오: SC-001~015 전부 06에 Given/When/Then·레이어·데이터 지정, 실기 계층 분리·릴리스 게이트 명시. P0/P1 FR 28건 = 03 P0 16 + P1 12와 일치.
- 미확인 상수 격리: TURN_RELAY_COST·KR_UPLOAD_AVG_MBPS는 03 상수 표·04 ADR-5 결과열·DL-011에만 등장, SC/FR/06/07 미사용.
- register 마킹: 33행 전부 상태·처리·근거 기재, Asked→Q1~Q5→DL-006~010 반영 기록 존재.
- R1 전파 14/16 확인(부분: #10 04:55 숫자, #15 04:134 → R3에서 정정). R2 전파: 운영 상수 17개 03에 존재, 07이 전부 이름으로 참조. 버전 줄이 REVISIONS 기재와 일치.
- 상수 파생 검산: RINGBUF_TMPFS_MB ≈23MB ✓, 릴레이 최악 ≈5% ✓, TURN_CRED_TTL_MIN 90 > LIVE_SESSION_MAX_MIN 60 ✓, CMD_TIMEOUT_SEC 10 < DEVICE_OFFLINE_DETECT_SEC 60 ✓, CLIP_CACHE_TTL_MIN 30 > MANUAL_REC_MAX_MIN 5 ✓, DEVICE_OFFLINE_DETECT_SEC 60 ≥ keepalive 20×1.5 ✓.
- 위협모델: 6경계×STRIDE 36셀 기입, 해당없음 사유 있음, 대응표 Accept 1건에 사유.
- 06 계약 테스트 ↔ 05: 멱등 10개·페이지네이션 6개·레이트리밋 3개·WS 5+7종이 05 표와 1:1.
- 07 착수 자산: 디렉터리 2단계·`.env.example` 키 이름만(값 없음)·첫 작업 3개가 04 R-1→G3 파이프라인→INV-2 순이고 성공 조건이 SC/FR ID로 측정 가능, 실기 장비 목록 존재.
- 시크릿: 어느 산출물에도 자격증명 값 없음.


## 구현 핸드오프 (service-prompt-workflow SPEC 입력)

/service-prompt-workflow 로 다음을 실행:
<inputs>s4-rpi-ip-camera/03-prd.md (요구사항·상수 표 v1.3), 05-api-contract.md (계약 v1.2), 08-readiness-report.md (선행 조건 2개 · 첫 작업 3개)</inputs>
<references>04-architecture.md, 06-test-design.md, 07-ops-design.md — 필요할 때만 읽는다</references>
<preconditions>위 선행 조건 1~2를 SPEC 작성 전에 닫는다 (법 상수가 바뀌면 03 상수 표 → R5, 버전 전파, 검증 스크립트 재실행)</preconditions>
<first_task>SPEC.md 작성 — 위 문서를 진실원으로, 낯선 구현자 실행 가능 수준(≥7/10)</first_task>
<then>superpowers 설치 시 `superpowers:writing-plans` → 07 착수 자산의 첫 작업 3개(① R-1 실증: NAT 뒤 Pi → 브라우저 첫 프레임 ② 이벤트 파이프라인 얇게 끝까지 ③ 프로비저닝 + 감사 fail-closed)부터. ①에 R-1 실증 ⑤⑥(ice_state 판정 신뢰성·go2rtc 8555 LAN 바인딩)을 포함한다. brainstorming은 생략 — 이 프롬프트를 붙여 넣은 것이 설계 승인이다.</then>
UI 있음: BUILD·REVIEW에서 frontend-design-taste 적용 — dial) · 참조 `ux-principles-kr.md`
<model_hints>
opus: 판단 집약 — INV-1~7 불변식(파기·감사 fail-closed·토픽 격리·패스스루), TURN 자격증명 2계층과 릴레이 판정 `ice_state`·main 승격 세션(FR-005·006), 기기 인증서 발급·갱신(FR-001·030), 온디맨드 클립 singleflight·캐시(FR-013·016), OTA A/B 롤백(FR-023)
sonnet: 패턴 반복 — 엔드포인트 CRUD(E11~E49), 화면(live/clips/devices/admin), RED 테스트 작성(06 시나리오·계약 테스트), compose·Caddyfile·mosquitto.conf·turnserver.conf, alembic 마이그레이션(05 DDL)
haiku: 기계적 — constants.yaml 로더, problems.yaml slug 표, .env.example, 문구·리네임·포맷
</model_hints>
