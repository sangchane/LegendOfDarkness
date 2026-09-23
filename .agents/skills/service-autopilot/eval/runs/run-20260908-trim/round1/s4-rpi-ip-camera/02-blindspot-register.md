# 블라인드스팟 레지스터 — PiCam Watch
스캔: 공통 11축 + STRIDE 6범주 + 프로파일 P1(10항목) + P3(4항목 적용) = 31항목 · 2026-09-08
스킬: `ecc:product-lens` Mode 1(제품 진단 7문) — 아래 "제품 진단" 절에 흡수. 질문 형식·1회 배치 규칙은 service-autopilot이 우선.
평가 런 규칙: 사용자 질문 불가 → 배치는 표로만 제시, 전 문항 추천안을 `Assumed(무응답)`로 채택.

## 제품 진단 (product-lens Mode 1)
| # | 질문 | 답 (근거: 01-recon) |
|---|---|---|
| 1 | 누구를 위한 것인가 | 매장·사무실·창고·농장의 소상공인 운영자 1~수 명. 카메라는 이미 있고, 통신사 결합상품이나 제조사 앱에 묶여 있다 |
| 2 | 고통은 무엇인가 | 제조사별 앱이 따로 놀고(멀티벤더), 원격 확인은 포트포워딩·클라우드 유료 구독에 의존. 보존기간·안내판 등 법 준수를 스스로 챙겨야 한다 |
| 3 | 왜 지금인가 | Pi 5(RTC·CPU) + go2rtc 패스스루로 재인코딩 없이 브라우저 WebRTC가 가능해졌고, ONVIF Profile S 종료(2027-03)로 Profile T 전환기 |
| 4 | 10점짜리 버전 | 멀티사이트 AI 객체탐지·클라우드 보관·알림톡·앱 — 전부 non-goal |
| 5 | MVP | 카메라 등록 → 브라우저 라이브(≤1초) → PTZ 제어 → 이벤트 클립 → 푸시 알림 → 기기 상태. P1 스토리만으로 성립 |
| 6 | 안티골 | 24/7 연속녹화, AI 분석, 다중 테넌트 판매, 네이티브 앱, Pi 4 지원, 고가용성 서버 |
| 7 | 작동 판정 | 03 SC 표(pass/fail) — 라이브 지연·PTZ 지연·오프라인 감지·보존 만료 파기 |

## 레지스터
| 축/항목 | 상태 | 처리 | 근거·출처 |
|---|---|---|---|
| 1. 기능 범위·행동 | Partial → Asked | **Asked → Q3** (녹화 범위·저장). 나머지 유스케이스·non-goal은 Assumed: 라이브·PTZ·프리셋·스냅샷·이벤트 클립·푸시·기기 상태 / non-goal = 진단 6번 | 00-seed 해석 3·6, 01-recon 유사 솔루션 기능 범위 |
| 2. 도메인·데이터 모델 | Missing → Assumed | Assumed: 엔티티 Workspace·User·Site·Device·Camera·Preset·Event·Clip·Command·AuditLog. 식별자 ULID, 시각 UTC ISO8601, 볼륨 = 상수 표(EVENTS_PER_CAM_DAY_MAX) | 01-recon 유사 솔루션(Frigate 이벤트/클립 모델) · Zalando 가이드 |
| 3. 상호작용·UX 플로우 | Missing → Asked | **Asked → Q1** (사용 맥락이 레이아웃·스트림 기본값을 결정). 권한별 화면: Admin(설정·사용자·감사로그)/Viewer(라이브·클립). 에러 시 복구 경로 제시 100%(L-06) | ux-principles-kr B표 |
| 4. 비기능 품질 | Missing → Assumed | Assumed: 실시간 = LIVE_LATENCY_P95(WebRTC 근거), PTZ_CMD_LATENCY_P95, DEVICE_OFFLINE_DETECT_SEC, ALERT_DELIVERY_P95 — 값은 03 상수 표에만. 관측성·보안은 A7·A4 | 01-recon 정량 근거(WebRTC 200~500ms) |
| 5. 통합·외부 의존성 | Partial → Asked | **Asked → Q4** (기존 카메라의 ONVIF/PTZ/H.264 현황). 그 외 Assumed: NTP(chrony), 웹 푸시(VAPID), coturn. 외부 장애 시: 카메라 끊김 → 지수 백오프 재접속(CAM_RECONNECT_BACKOFF), 서버 끊김 → 로컬 큐 | 01-recon fit 표, P1 연결 끊김 |
| 6. 엣지케이스·실패 처리 | Missing → Assumed | Assumed: 동시 PTZ 명령은 카메라당 잠금(PTZ_LOCK_SEC) + 마지막 승리, 명령 중복은 Idempotency-Key, 시청자 상한 초과는 429 거절, 클립 디스크 만료 순환(오래된 것부터), 빈 사이트/카메라 0대 상태 화면 | 03 엣지케이스 표로 전파 |
| 7. 제약·트레이드오프 | Partial → Assumed | Assumed: 팀 1~2명, 서버 VPS 1대 + coturn, 기한 미상 → P1만 MVP, Pi 5 4GB 고정, 폐쇄망 아님(인터넷 필수) | 00-seed 해석 4·7 |
| 8. 용어·일관성 | Missing → Assumed | Assumed: 용어집을 03에 둔다 — Site(사이트)·Device(Pi 게이트웨이)·Camera·Stream(main/sub)·Preset·Event·Clip·Command·Admin/Viewer. "기기"=Device, "카메라"=Camera로 고정 | — |
| 9. 완료 신호 | Missing → Assumed | Assumed: 03 SC 표 전부 pass/fail, 06이 시나리오로 변환 | stage-templates A3 |
| 10. 비용·라이선스 | Partial → Assumed | Assumed: 채택 스택 전부 MIT/BSD/EPL(go2rtc MIT·coturn BSD-3·mosquitto EPL/EDL·FastAPI MIT·PostgreSQL). GPL 계열(ZoneMinder·MotionEye) 미채택. 런타임 비용 = VPS 1대 + TURN 대역(**미확인** → SC 목표로 쓰지 않음) | 01-recon 유사 솔루션·스택 표 |
| 11. 사용 맥락 | Missing → Asked | **Asked → Q1** | 질문 프로토콜 3 (환경 질문 전에 맥락 먼저) |
| S. Spoofing | Missing → Assumed | Assumed: 기기 = 기기별 X.509 mTLS(내부 CA), 사용자 = 비밀번호 argon2id + 세션 쿠키(HttpOnly), 스트림 = 단기 서명 토큰. A4 경계별 전수 검토 | P1 디바이스 신원(IoT Lens IOTSEC 1·2) |
| T. Tampering | Missing → Assumed | Assumed: 전 구간 TLS, 컨테이너 이미지 서명 검증, 클립 파일 SHA-256 기록 | P1 OTA 서명 검증 |
| R. Repudiation | Missing → Assumed | Assumed: AuditLog(누가·언제·어느 카메라를 봤나/움직였나/클립 조회·삭제) 보관 AUDIT_RETENTION_DAYS | 01-recon 접속기록 1년(가능성) |
| I. Information Disclosure | Missing → Assumed | Assumed: RBAC(Admin/Viewer), 스트림 토큰 STREAM_TOKEN_TTL_SEC, 클립 URL 서명, 로그에 영상·스냅샷 미기록, 오류 응답에 스택 미노출 | 개인정보보호법 제25조 안전조치 |
| D. Denial of Service | Missing → Assumed | Assumed: 사이트당 동시 시청 VIEWER_MAX, API 레이트리밋, Pi 컨테이너 CPU/메모리 상한, TURN 사용자별 쿼터 | P1 영상 스트림(기기 과부하) |
| E. Elevation of Privilege | Missing → Assumed | Assumed: 권한 스코프 `<모듈>:<자원>:<행위>`, 기기 MQTT ACL은 자기 토픽만, 관리자 승격은 Admin만 | A5 규약 |
| P1. SD카드 마모 | Missing → Assumed | Assumed: Overlay FS(read-only rootfs) + log2ram + 이벤트 DB·클립은 USB SSD + 고내구 microSD | 01-recon h·데이터 공급 실측, dzombak |
| P1. 전원 차단 | Missing → Assumed | Assumed: read-only rootfs + USB SSD ext4 저널링 + SQLite WAL. UPS 없음(Accept — 유인 현장) | Mender Pi 체크리스트 |
| P1. 자가 복구 | Missing → Assumed | Assumed: HW watchdog 활성(WATCHDOG_TIMEOUT_SEC ≤15) + systemd Restart=always + 컨테이너 헬스체크 → 실패 시 재시작 | blindspot P1(Pi watchdog 15초 상한) |
| P1. OTA 업데이트 | Partial → Asked | **Asked → Q5** (현장 인력에 따라 OS A/B 필요 여부가 갈림). 추천 = 앱 계층 A/B | 01-recon fit OTA 행 |
| P1. 시계 드리프트 | Missing → Assumed | Assumed: Pi 5 온보드 RTC 배터리 + chrony NTP. 부팅 시 NTP 동기 전 TLS 시도 금지(chrony-wait) | raspberrypi.com RTC 배터리(확정) |
| P1. 디바이스 신원 | Missing → Assumed | Assumed: 기기별 X.509(내부 CA 발급, 1년 만료·자동 갱신) + MQTT ACL 자기 토픽만 | IoT Lens IOTSEC |
| P1. 연결 끊김 | Missing → Assumed | Assumed: 이벤트·상태를 로컬 SQLite 큐(OFFLINE_QUEUE_MAX)에 쌓고 재접속 시 순서 전송, 상한 초과 시 오래된 것부터 폐기. 라이브는 버퍼 없음(끊김 = 재접속) | P1 기본값 |
| P1. 영상 스트림 | Partial → Assumed | Assumed: WebRTC(go2rtc) 서브스트림 기본·메인스트림 선택, 사이트당 VIEWER_MAX, 업링크는 프로비저닝 시 실측 저장(UPLINK_MIN_MBPS 미만이면 경고) | 01-recon 비트레이트·지연 근거 |
| P1. 원격 접근 | Partial → Asked | **Asked → Q2** (A0 최상위 해석 — 서버 경유 vs LAN vs VPN) | 00-seed 해석 2 |
| P1. 프로비저닝 | Missing → Assumed | Assumed: 이미지에 1회용 클레임 토큰 → 첫 접속 시 기기 인증서 발급 → Admin이 사이트에 승인. 카메라는 ONVIF WS-Discovery 자동 탐색 + 자격증명 입력 | IoT Lens IOTOPS 3 |
| P3. 실시간성 | Missing → Assumed | Assumed: "실시간" = LIVE_LATENCY_P95 + 기기 상태 반영 STATE_REFRESH_SEC. 알람 경로 우선 처리 | 01-recon WebRTC 지연 |
| P3. 알람 폭주 | Missing → Assumed | Assumed: 카메라별 이벤트 쿨다운 EVENT_COOLDOWN_SEC + 알림 그룹핑 + 일일 알림 상한 ALERT_DAILY_MAX 초과 시 요약 1건 | SRE "조치 가능한 알람만" |
| P3. 이력 증가 | Missing → Assumed | Assumed: 클립·썸네일·이벤트 메타 = CLIP_RETENTION_DAYS 후 파기(법 준수), AuditLog = AUDIT_RETENTION_DAYS. 연간량 = 상수 표에서 계산 | 표준지침 30일(가능성) |
| P3. 무중단 | Missing → Assumed | Assumed: 서버 재시작 중 Pi는 로컬 녹화·큐잉 계속(엣지 자율), MQTT 자동 재접속. 서버 단일 인스턴스(HA non-goal, Accept) | P1 연결 끊김 |
| P3. 폐쇄망 | Clear(해당없음) | — 인터넷 필수 전제(축 7) | 00-seed |
| P3. 프로토콜 | Clear(해당없음) | — ONVIF/RTSP 표준으로 확정, 축 5·Q4가 덮음 | 01-recon ONVIF |

Asked 5 · Assumed 24 · Clear 2 = 31.

## 질문 배치 (최대 5) — 2026-09-08 (평가 런: AskUserQuestion 미사용, 표로만 제시)

### Q1. 주로 누가, 어떤 기기로, 한 번에 얼마나 보나요? (축 11 · 축 3)
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | 운영자가 **스마트폰**으로 하루 몇 번 1~3분 확인 + 이벤트 푸시로 호출됨 | 소상공인 시장(영상보안의 35%, 통신사 결합상품)의 사용 패턴. 서브스트림 기본·1카메라 전체화면 우선·그리드는 썸네일. 되돌릴 때 비용: 낮음(레이아웃 dial·기본 스트림만 변경) |
| B | **PC 관제석**에서 상시 4분할 시청 | 메인스트림 4동시 → Pi 업링크·CPU 상한과 VIEWER_MAX를 상향, 장시간 세션. 되돌릴 때 비용: 중(상수·대역 재산정) |
| C | 둘 다 동등 | 두 레이아웃 + 상수 상향. 되돌릴 때 비용: 중 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q2. 어디서 보나요 — 배포 형태 (A0 최상위 해석 · P1 원격 접근)
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | **중앙 서버(VPS 1대) 경유** — Pi는 아웃바운드 연결만, 어디서든 브라우저 | 포트포워딩 없이 원격 시청·제어. 서버 운영·TURN 대역 비용 발생. 되돌릴 때 비용: 높음(서버 컴포넌트·인증 제거) |
| B | **LAN 전용** — 서버 없이 Pi 웹 UI, 외부에서는 못 봄 | 가장 단순·비용 0. 원격 요구가 생기면 A 전부 추가. 되돌릴 때 비용: 높음 |
| C | **Pi에 VPN**(Tailscale류) — 서버 없이 원격 | 시청자마다 VPN 앱 설치·계정 필요, 푸시 알림 서버는 결국 필요. 되돌릴 때 비용: 중 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q3. 녹화는 무엇을 어디에 얼마나 남기나요? (축 1 · P1 SD 마모 · 규제)
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | **이벤트 클립**(모션·수동, 전후 CLIP_PRE_SEC/CLIP_POST_SEC) → Pi **USB SSD**에 CLIP_RETENTION_DAYS 순환, 서버에는 썸네일·메타만 | 표준 개인정보 보호지침 30일 권고(가능성)와 정합, SD 마모 회피, 업링크 절약. Pi 오프라인이면 클립 열람 불가(라이브도 불가라 동일 조건). 되돌릴 때 비용: 중 |
| B | **24/7 연속 녹화** Pi USB SSD | 4카메라×5Mbps×30일 ≈ 6.5TB → 대용량 SSD, NVR 영역. 되돌릴 때 비용: 높음 |
| C | 클립을 **서버 오브젝트 스토리지**로 업로드 | Pi 오프라인에도 열람 가능. 서버 저장 비용·업링크 사용·개인정보 서버 집중. 되돌릴 때 비용: 중 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q4. 지금 갖고 있는 카메라는 어떤 종류인가요? (축 5 · 해석 3 "제어")
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | **ONVIF(Profile S 또는 T) + RTSP H.264** 카메라, PTZ는 일부만 | 서비스가 ONVIF capability로 PTZ 유무를 자동 노출, H.264 스트림 1개 필수(H.265 전용이면 카메라 설정 안내). HA ONVIF 통합과 같은 요구사항. 되돌릴 때 비용: 낮음 |
| B | **ONVIF 미지원**(제조사 전용 앱만) | 제조사별 어댑터 → 범위 폭발. 현실적 답은 카메라 교체 권고. 되돌릴 때 비용: 높음 |
| C | 아직 **미구매** — 권장 모델을 지정해 달라 | Profile T + PTZ 지원 모델 목록을 제공. 되돌릴 때 비용: 낮음 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q5. 현장에 사람이 있나요 — 복구·업데이트 깊이 (P1 OTA · 자가 복구 · 전원)
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | **유인 현장**(영업 중 사람 있음) | 원격 복구 실패 시 "전원 재투입·SD 교체" 안내 가능 → 앱 계층 A/B(컨테이너 digest 롤백) + OS 보안 업데이트만, OS A/B 파티션·UPS는 non-goal. 되돌릴 때 비용: 중(RAUC 도입 = 이미지 파이프라인 신설) |
| B | **무인 원격지**(농장·창고, 출동 2시간 이상) | OS 계층 A/B(RAUC + tryboot) + 이중 SD + UPS 필수. 되돌릴 때 비용: 높음 |
→ 답: 무응답 → **A Assumed(무응답)**

## 반영 기록
- Q1 A → 03 UI 방향(모바일 우선, 1카메라 전체화면, 그리드=썸네일) · 상수 VIEWER_MAX·기본 스트림 sub (decision-log DL-006)
- Q2 A → 04 시스템 컨텍스트(서버 경유·MQTT·TURN) · 00-seed 해석 2 확정 (DL-007)
- Q3 A → 03 FR 녹화·상수 CLIP_RETENTION_DAYS/CLIP_PRE_SEC/CLIP_POST_SEC · 07 백업 범위 (DL-008)
- Q4 A → 03 FR 카메라 온보딩(ONVIF 탐색·capability) · 가정 "H.264 스트림 1개 필수" (DL-009)
- Q5 A → 07 OTA(앱 계층 A/B) · 04 위협모델 Accept(OS 벽돌·정전) (DL-010)
