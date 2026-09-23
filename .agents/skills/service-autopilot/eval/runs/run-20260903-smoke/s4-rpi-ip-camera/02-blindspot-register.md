# 블라인드스팟 레지스터 — PiCam Watch

스캔: 공통 10축 + STRIDE 6범주 + 프로파일 P1 IoT·엣지(10) + P3 관제 부분(3) + P2 웹 SaaS 부분(4) + 시드 특이 항목(3) = **36항목, 전수 마킹**
작성: 2026-09-03 · 모드: 오토파일럿(질문 배치 1회 제시, 무응답 → 추천안 `Assumed(무응답)`) · v1.2 GATE 반영(값 동기화·가정 2건 추가)
스킬: `ecc:product-lens` Mode 1(제품 진단 7문) — 질문 후보의 Impact 판단 보강에 사용

## 제품 진단 (product-lens Mode 1 — 요약)

| # | 질문 | 답 (근거: 01-recon) |
|---|---|---|
| 1 | 누구를 위한 것인가 | 카메라 1~4대를 둔 집주인·소상공인 1인 운영자 + 가족·직원 시청자 소수 |
| 2 | 어떤 고통인가 | 벤더 앱은 구독·클라우드 종속, 벤더 혼합 시 앱이 여러 개, PTZ·보존정책이 벤더마다 다름. 셀프호스팅 NVR(Frigate 등)은 설정 부담·가속기 비용 |
| 3 | 왜 지금인가 | go2rtc·MediaMTX가 트랜스코딩 없는 WebRTC 중계를 Pi급에서 실용화(14k/20k★, MIT). Pi 5 + NVMe 보급 |
| 4 | 10점짜리 버전 | 어떤 벤더 카메라든 꽂으면 자동 등록, 1초 미만 라이브, 프리셋 순찰, 법정 보존정책 자동, 어디서나 안전 접속 |
| 5 | MVP | ONVIF 카메라 등록 → WebRTC 라이브 그리드 → PTZ/프리셋 → 이벤트 클립 + 보존 파기 → Tailscale 원격 |
| 6 | 안티골 | 객체 인식(AI), 클라우드 SaaS·다중 테넌트, 벤더 전용 프로토콜 역공학, 모바일 네이티브 앱 |
| 7 | 작동 판정 지표 | 라이브 지연 p95, 스트림 가동률, PTZ 명령 성공률, 만료 클립 파기 정확도 (→ PRD SC-###) |

판정: **Go** — 단, "제어"는 개인정보보호법 제25조⑤(임의조작 금지)와 충돌 가능성이 있어 감사로그·역할분리를 P0로 올린다.

## 레지스터

| 축/항목 | 상태 | 처리 | 근거·출처 |
|---|---|---|---|
| **공통 1. 기능 범위·행동** | Partial | Assumed: MVP = 등록·라이브·PTZ·이벤트클립·보존·원격. non-goal = AI 감지·클라우드·모바일 앱 | seed + product-lens #5·#6 |
| 공통 2. 도메인·데이터 모델 | Partial | Assumed: 엔티티 Camera/Preset/Event/Clip/User/AuditLog, 클립은 파일+메타, 시각 UTC ISO8601, 식별자 ULID | 01-recon 업무 흐름 |
| 공통 3. 상호작용·UX 플로우 | Partial | Assumed: 역할 2개(Owner/Viewer), 화면 4개(라이브 그리드·단일뷰+PTZ·이벤트·설정). 에러 시 복구 경로 제시(L-06) | ux-principles-kr L-03·L-06 |
| 공통 4. 비기능 품질 | Missing | Assumed: 라이브 지연 p95 ≤ 1.0s(WebRTC, LAN), 스트림 가동률 ≥ 99%/월, PTZ 명령 응답 p95 ≤ 500ms, 동시 세션 사용자당 ≤ 4·전역 ≤ 16(INV-8, 설정 불가) | go2rtc WebRTC 특성 [01-recon], 도허티 임계 L-10 |
| 공통 5. 통합·외부 의존성 | Partial | **Asked → Q2** (카메라 프로토콜) · Assumed: 카메라 오프라인 시 30s 백오프 재접속, 타일에 "연결 끊김" 상태 표시 | ONVIF/RTSP [01-recon] |
| 공통 6. 엣지케이스·실패 처리 | Missing | Assumed: PTZ 동시 명령 = 최후 명령 우선(ONVIF non-blocking 특성), 잠금 없음; 디스크 90% 시 최구 클립부터 삭제; 카메라 자격증명 오류 3회 후 비활성 | ONVIF PTZ Spec [01-recon] |
| 공통 7. 제약·트레이드오프 | Partial | Assumed: 1인 개발, 예산 = Pi 5 8GB + NVMe 256GB, 폐쇄망 아님(NTP·Tailscale 가능) | seed(라즈베리파이 고정) |
| 공통 8. 용어·일관성 | Missing | Assumed: 용어집 — Camera(카메라), Stream(스트림), Preset(프리셋), Event(이벤트), Clip(클립), Retention(보존기간), Owner/Viewer | 이 문서가 원본 |
| 공통 9. 완료 신호 | Missing | Assumed: PRD MVP 게이트 SC 전부 pass(SC-002는 P2 게이트, SC-014 실카메라는 P1 게이트) + 06 시나리오 전부 green + 07 장애 시나리오 3건 리허설 | stage-templates A3·A6 |
| 공통 10. 비용·라이선스 | Clear | go2rtc MIT·MediaMTX MIT·python-onvif-zeep MIT·FastAPI MIT. 런타임 비용 0(Tailscale 개인 무료 티어) | 01-recon 유사 솔루션 표 |
| **STRIDE S (Spoofing)** | Partial | Assumed: 사용자 인증 = 로컬 계정(argon2id) + 세션 쿠키; 카메라 자격증명은 서버 측 암호화 저장; go2rtc는 localhost 바인딩 | A4에서 경계별 확정 |
| STRIDE T (Tampering) | Partial | Assumed: 클립 파일 SHA-256 기록, 설정 변경은 AuditLog | A4 |
| STRIDE R (Repudiation) | Missing | Assumed: **PTZ·설정·클립 삭제·로그인 전부 감사로그(append-only)** — 법 제25조⑤ 대응 | 01-recon 규제 |
| STRIDE I (Info Disclosure) | Partial | Assumed: 스트림은 세션 쿠키 인증 후 API 프록시로만(별도 토큰 없음), 카메라 비밀번호는 API·go2rtc API·설정 파일·백업 평문 미포함, 클립 경로 직접 노출 금지 | A4 |
| STRIDE D (DoS) | Partial | Assumed: 동시 세션 사용자당 4·전역 16, PTZ 5/s, 로그인 시도 제한 | A4 |
| STRIDE E (Elevation) | Partial | Assumed: 스코프 기반 인가(`cam:ptz:write`는 Owner만), 서버 측 검사 | A4 |
| **P1. SD카드 마모** | Missing | Assumed: rootfs overlay(read-only) + log2ram + noatime + swap off; SQLite·클립은 **NVMe** | Mender Pi 체크리스트·dzombak (checklists 출처) |
| P1. 전원 차단 | Missing | Assumed: read-only rootfs + 쓰기 구역(NVMe) ext4 저널링; UPS 미채택(설치 환경 미상, 상용 전원 전제) | 위와 동일 |
| P1. 자가 복구 | Missing | Assumed: HW watchdog 10s(15s 상한 이내), systemd Restart=always, 호스트 헬스 타이머(30s)가 3회 연속 실패(90s) 시 api 컨테이너 재시작(Docker healthcheck는 재시작하지 않음) | checklists P1(Pi watchdog 15s 상한) |
| P1. OTA 업데이트 | Missing | Assumed: MVP는 **컨테이너 이미지 태그 교체 + 이전 태그 보관(수동 롤백)**, A/B 파티션은 P2. 무결성은 레지스트리 다이제스트 고정 | AWS IoT Lens 표준 대비 축소(단일 기기·현장 접근 가능) |
| P1. 시계 드리프트 | Missing | Assumed: NTP + Pi 5 내장 RTC에 배터리 장착(BOM); 미동기 시 E-11 단조시계 보정 규칙, 동기 전 TLS 의존 작업 대기 | checklists P1 |
| P1. 디바이스 신원 | Clear(해당없음) | 단일 Pi가 서버 자체. 기기→클라우드 인증 구조 없음. 원격 접근 신원은 Tailscale 노드 키 | seed(단일 기기) |
| P1. 연결 끊김 | Partial | Assumed: 카메라↔Pi 끊김 = 재접속 백오프 + 이벤트 camera.offline; Pi↔시청자 끊김 = 플레이어 자동 재협상. 이벤트 큐 상한 1,000건 | go2rtc 재접속 동작 [01-recon] |
| P1. 영상 스트림 | Partial | Assumed: WebRTC 우선, MSE 폴백, **트랜스코딩 금지**(Pi 5 HW 인코더 없음), 카메라 서브스트림(≤720p)을 그리드에, 메인스트림을 단일뷰에; 링버퍼는 메인(≤4Mbps, 초과 시 서브) tmpfs 128MB | 01-recon 스택 표 |
| P1. 원격 접근 | Missing | **Asked → Q3** | 사용자 요구 의존 |
| P1. 프로비저닝 | Partial | Assumed: 첫 부팅 시 웹 설정 마법사(Owner 계정 생성) + ONVIF 디스커버리로 카메라 자동 탐색 | 01-recon 업무 흐름 1 |
| **P3. 실시간성** | Missing | Assumed: "실시간" = 카메라→화면 glass-to-glass p95 ≤ 1.0s(LAN)/≤ 2.5s(Tailscale DERP 릴레이) | RaspberryPi-WebRTC 100~200ms 사례 [01-recon] + 릴레이 지연 여유 |
| P3. 알람 폭주 | Partial | Assumed: 진행 중 클립이 있으면 새 이벤트 대신 종료 시각 연장(마지막 모션+20s, 상한 120s) — 사각지대 0 | SRE "조치 가능한 알람" |
| P3. 이력 증가 | Missing | **Asked → Q4** (녹화 정책이 용량 결정) · Assumed: 이벤트 메타는 보존기간 + 90일, 클립은 보존기간(기본 30일) 후 파기 | 법 제25조⑦·가이드라인 30일 [01-recon] |
| **P2. 테넌시** | Clear(해당없음) | 단일 운영자·단일 Pi. tenant_id 없음 | seed |
| P2. 인증 | Partial | Assumed: 로컬 계정(ID/비밀번호, argon2id), SSO 없음 | 오프라인 동작 우선 |
| P2. 백업·DR | Missing | Assumed: 설정+SQLite 일 1회 **age 암호화** 스냅샷을 **외부 USB(BOM 필수)** 에 저장 + NVMe 부본 7세대; 클립은 백업 대상 아님(RPO 24h, USB 없으면 복구 불능으로 정직 기재) | A7에서 리허설 주기 확정 |
| P2. 개인정보 | Partial | **Asked → Q1** (적용 범위) · Assumed: 안내판 5항목(촬영범위·촬영시간 포함) 필수 입력, 보존기간 설정 필수(상한 90일은 Assumed), 오디오 기능 부재(Eliminate), 클립 다운로드 Owner 전용, PTZ 허용 범위(FR-027) | 법 제25조 [01-recon] |
| **시드 특이. 이벤트 감지 범위** | Missing | **Asked → Q5** | 가속기 구매 여부 결정 |
| 시드 특이. 보존 상한·파기 방식 | Missing | Assumed: 상한 90일(무기한 보존 방지 운영 상한, 법정 수치 아님); 파기 = unlink+fsync+일 1회 fstrim(물리 포렌식 잔여 Accept) | 가이드라인 "필요 최소기간"·"복구 불가 방법" 해석 [01-recon 2차] |
| 시드 특이. 알림 채널 | Missing | Assumed: 앱 내 배너(P1) + 웹훅 URL 1개·heartbeat(P2, FR-028); 외부 프로브 운영은 non-goal | SRE "조치 가능한 알람" |

## 질문 배치 (5문항) — 제시 2026-09-03 (오토파일럿: 무응답 처리)

### Q1. 카메라를 어디에 설치하나요? (개인정보보호법 적용 범위가 달라집니다)
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | **사업장·공용 공간 포함(보수적)** — 안내판 정보·관리책임자·보존기간·PTZ 감사로그를 필수로 설계 | 법 제25조 전면 적용 시나리오를 덮으면 가정용도 자동 충족. 비용은 필드 몇 개와 감사로그(STRIDE R 대응에 어차피 필요) |
| B | 자택 내부만 | 안내판·방침 기능 생략 가능하나, 나중에 사업장으로 옮기면 재작업 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q2. 보유 카메라의 프로토콜과 대수는?
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | **ONVIF Profile S 준수 카메라 1~4대** (RTSP + ONVIF PTZ) | go2rtc·python-onvif-zeep로 표준 경로만 구현. Pi 4에서 ~10 스트림 사례 [01-recon] → 4대는 여유 |
| B | ONVIF 5~8대 | 그리드 서브스트림 대역·CPU 재실측 필요, NVMe 용량 상향 |
| C | 벤더 전용 프로토콜(Tapo/Wyze 등) 혼합 | go2rtc가 일부 벤더 입력을 지원하나 PTZ는 벤더별 역공학 → 범위 폭발 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q3. 집·사무실 밖에서도 봐야 하나요?
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | **LAN + Tailscale 사설망**(가족·직원 기기에 Tailscale 설치) | 포트포워딩 0, 인증서·도메인 불필요, 무료 티어. 이중 NAT 시 DERP 릴레이로 지연 증가(≤2.5s 목표) |
| B | 공개 HTTPS(도메인 + reverse proxy + TURN 서버) | 누구나 접속 가능하나 TURN 운영·인증 강화·공격면 증가 |
| C | LAN만 | 가장 단순. 외출 중 확인 불가 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q4. 녹화는 어떻게 남기나요? (저장 매체 구매가 결정됩니다)
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | **이벤트 클립만**(이벤트 전 10s + 후 20s) NVMe 저장, 기본 보존 30일 | 4대 × 하루 50건 × 30s × 4Mbps(메인) ≈ 3GB/일 → 30일 ≈ 90GB(256GB NVMe); 연장 최대 130s·200건/일 최악 시 ≈ 390GB → 90% 자동 정리(FR-016) + 512GB 권장 안내. SD 마모 회피 |
| B | 24/7 상시 녹화 | 4대 × 2Mbps ≈ 86GB/일 → 30일 2.6TB. NAS 필수, Pi I/O 상시 부하 |
| C | 녹화 없음(라이브만) | 가장 단순하나 "무슨 일 있었나" 확인 불가 |
→ 답: 무응답 → **A Assumed(무응답)**

### Q5. 이벤트(움직임) 감지는 어디서 하나요?
| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | **카메라 내장 모션 감지를 ONVIF Events로 수신** | 추가 하드웨어 0, Pi CPU 부담 0. 정확도는 카메라 의존(오탐 있음 → 쿨다운으로 완화) |
| B | Pi에서 객체 인식(Hailo AI HAT ≈ $130 + Frigate 채택) | 사람/차량 구분 가능하나 하드웨어 구매 + Frigate로 아키텍처 전환 |
| C | 감지 없음 | 클립 트리거가 수동뿐 |
→ 답: 무응답 → **A Assumed(무응답)**

## 반영 기록
- Q1 A → 03-prd FR-010~012(안내판 정보·보존기간·감사로그 P0), 04 위협모델 R 범주 (decision-log #4)
- Q2 A → 04 컴포넌트(go2rtc + onvif-zeep, 벤더 어댑터 없음), 05 Camera 엔티티에 onvif_profile (decision-log #5)
- Q3 A → 04 배포 토폴로지(Tailscale, 공개 포트 0), 07 네트워크 절차 (decision-log #6)
- Q4 A → 05 Clip 엔티티·보존 잡, 07 스토리지 용량·백업 정책 (decision-log #7)
- Q5 A → 04 EventIngest 컴포넌트(ONVIF PullPoint), Frigate 마이그레이션은 non-goal (decision-log #8)

- v1.2 GATE 반영 → 03 v1.2(FR-027/028/029, INV-6 Eliminate, 다운로드 Owner), 04 v1.2(런타임 등록·PTZ 범위·백업 경계), 05 v1.2(ID 접두사·촬영범위 필드), 07 v1.2(BOM·암호화 백업·8443) (decision-log #24~#40)

## 미마킹 축: 0 (게이트 조건 충족)
