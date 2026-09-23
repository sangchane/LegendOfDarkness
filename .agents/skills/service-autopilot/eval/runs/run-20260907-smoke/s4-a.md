# PiCam Hub — 라즈베리파이 기반 IP 카메라 제어·실시간 모니터링 서비스 기획·설계 문서

| 항목 | 내용 |
|---|---|
| 문서 버전 | v0.1 (초안, 2026-09-03) |
| 상태 | 구현 착수 가능 설계 패키지 (PRD · 아키텍처 · 위협모델 · API/ERD · 테스트 · IaC/관측성) |
| 작성 방식 | 사용자 질의 없이 작성. 필요한 결정은 §1 "가정"으로 명시하고, 가정이 틀렸을 때 바뀌는 항목을 표시 |
| 표기 규칙 | **[확정]** 근거 있는 사실/결정 · **[가정]** 이 문서가 스스로 정한 전제 · **[미확정]** 검증·의사결정이 남은 항목 |

---

## 0. 한 줄 요약

> 라즈베리파이 한 대에 시판 IP 카메라(RTSP/ONVIF)를 물려 **5분 안에 검색→등록→실시간 시청→PTZ 조작→녹화**까지 되는, 클라우드 계정이 필요 없는 로컬 우선(local-first) 소형 NVR 서비스.

- 미디어 경로는 **무(無)트랜스코딩 패스스루**(카메라 H.264 → 브라우저 WebRTC)로 라즈베리파이의 약한 인코더 제약을 우회한다.
- 제어 경로는 **ONVIF Profile S/T 표준**만 사용해 제조사별 SDK 의존을 없앤다.
- 영상은 기본적으로 **LAN 밖으로 나가지 않으며**, 원격 접근은 VPN(WireGuard/Tailscale)로 해결한다. 클라우드 릴레이는 MVP 범위 밖.

---

## 1. 가정 (Assumptions)

질문이 불가능하므로 아래 가정으로 범위를 고정한다. 가정이 바뀌면 "영향" 열의 섹션을 재검토한다.

| ID | 가정 | 근거/이유 | 가정이 틀리면 영향 |
|---|---|---|---|
| A1 | 대상 사용자는 **가정·소상공인·소규모 사무실의 셀프 설치 사용자**와 홈랩 기술 사용자. B2C/프로슈머. | "라즈베리파이로"라는 표현은 저비용 셀프호스팅 맥락 | B2B 관제라면 §4.8 멀티사이트·§5 RBAC 강화 필요 |
| A2 | 카메라는 **시판 IP 카메라(RTSP + ONVIF Profile S, PTZ는 ONVIF PTZ 서비스)**. 라즈베리파이 CSI 카메라 모듈은 MVP 제외(부록 C). | "IP 카메라를 제어"라고 명시 | CSI 모듈이 주 대상이면 libcamera/rpicam-vid 파이프라인 추가 |
| A3 | 사이트당 카메라 **1~8대**. 하드웨어는 **Raspberry Pi 5 (8GB) 권장, Pi 4 (4GB) 최소**. | §3.3 성능 예산 | 8대 초과면 다중 Pi 또는 x86 미니PC 검토 |
| A4 | 카메라와 Pi는 **같은 LAN**(권장: 카메라 전용 VLAN). 원격 접근은 **VPN**으로. | 포트포워딩·공인 IP 노출 회피 | 클라우드 릴레이 필요 시 §4.8 "Later" 토폴로지 승격 |
| A5 | **단일 사이트 셀프호스팅, 멀티테넌트 없음.** 클라우드 계정/구독 없음. | local-first 가치 제안 | SaaS화 시 인증·과금·릴레이 전면 추가 |
| A6 | 클라이언트는 **웹 브라우저(PWA)**. 네이티브 앱 없음. | 개발 비용, WebRTC 브라우저 지원 성숙 | 푸시 알림은 Web Push + 외부 채널(Telegram 등)로 대체 |
| A7 | 팀 2~3명, **MVP 8~10주**. | 소규모 프로젝트 전제 | 일정 축소 시 M2 이벤트 기능을 M3로 이월 |
| A8 | 한국 배포. 개인정보보호법 제25조(고정형 영상정보처리기기) 준수는 **운영자 책임**, 시스템은 보존기간 설정·접근기록·안내문 템플릿을 제공한다. | 규제 준수 지원 범위 정의 | 해외 배포 시 GDPR 항목 추가 |
| A9 | 오픈소스 구성요소(MIT/Apache/BSD)만 사용 → 상용 배포 가능. GPL 컴포넌트는 프로세스 분리(실행 파일 호출)로만 사용. | 라이선스 리스크 회피 | — |
| A10 | 녹화 저장은 **USB/NVMe SSD 외장 스토리지**. SD 카드에는 OS만. | SD 카드 마모/신뢰성 | SD 전용이면 녹화 기능 기본 OFF + 경고 |
| A11 | 카메라는 최소 **H.264 스트림 1개**(메인 또는 서브)를 제공한다. H.265 전용 카메라는 제한 모드로 지원. | 브라우저 WebRTC H.264 범용성 | §4.5 결정 트리 참조 |

---

## 2. 제품 요구사항 (PRD)

### 2.1 문제 정의

| 현재 대안 | 문제 |
|---|---|
| 제조사 클라우드 앱(Tapo, Reolink, EZVIZ 등) | 영상이 해외 서버 경유, 구독 유도, 제조사 종속, 이기종 카메라 통합 불가 |
| 시판 NVR | 고가, 폐쇄적, 제조사 호환성 제한, UI 노후 |
| 오픈소스 NVR(Frigate, ZoneMinder, Shinobi) | 설치·설정 난이도 높음(YAML, Docker, RTSP URL 수작업), 라즈베리파이에서 무거움(Frigate는 객체검출 중심) |

**해결하려는 핵심 문제**: "카메라 몇 대를 사서 라즈베리파이에 물렸을 뿐인데 왜 보는 것조차 어려운가." → **온보딩·라이브·제어·녹화 4가지를 설정 파일 없이** 브라우저에서 끝낸다.

### 2.2 타깃 사용자 & 페르소나

| 페르소나 | 상황 | 핵심 니즈 | 기술 수준 |
|---|---|---|---|
| P1 소상공인 사장(카페/매장) | 카메라 2~4대, 매장 밖에서 폰으로 확인 | 쉬운 설치, 원격 확인, 사건 시 클립 저장 | 낮음 |
| P2 홈랩/스마트홈 사용자 | 카메라 4~8대, Home Assistant 사용 | 로컬 처리, API/웹훅, 이기종 통합 | 높음 |
| P3 소규모 사무실 관리자 | 카메라 4~6대, 직원 여러 명이 열람 | 계정별 권한, 접근기록, 보존기간 준수 | 중간 |

### 2.3 가치 제안

1. **5분 온보딩**: ONVIF 자동 검색 → 자격증명 입력 → 스트림 자동 선택 → 즉시 라이브.
2. **1초 미만 라이브 지연**(LAN, WebRTC)과 **PTZ 즉시 반응**.
3. **영상은 내 집 안에**: 클라우드 없음. 원격은 VPN.
4. **이기종 카메라 통합**: 제조사 무관, ONVIF 표준 기반.
5. **라즈베리파이 친화**: 트랜스코딩 없이 8대까지 녹화·시청.

### 2.4 범위

**MVP (M1~M2) 포함**

- 카메라 자동 검색(WS-Discovery) + 수동 등록(RTSP URL 직접 입력)
- 라이브 시청(1채널 / 2×2 / 3×3 그리드), WebRTC 우선·HLS 폴백
- PTZ 제어(연속 이동, 정지, 줌, 프리셋 저장/이동), 스냅샷
- 연속 녹화(fMP4 세그먼트), 타임라인 재생, 클립 내보내기(MP4)
- 보존 정책(일수 + 디스크 워터마크)
- 계정/역할(owner, operator, viewer), 감사 로그
- 카메라 이벤트 수신(ONVIF 모션 이벤트) → 이벤트 목록·타임라인 마커
- 시스템 상태(CPU/온도/디스크/카메라 온라인) 대시보드
- 원클릭 설치 스크립트 + 업데이트/롤백

**MVP 제외 (Later)**

- 클라우드 릴레이/계정, 네이티브 앱, 푸시 알림 인프라
- AI 객체 검출(사람/차량) — Pi 5 AI Kit(Hailo-8L) 또는 Coral 기반, M4 후보
- 오디오 양방향(talkback), 카메라 펌웨어 관리
- 다중 사이트 통합 관제, 멀티테넌트
- CSI 카메라 모듈(부록 C)

### 2.5 성공 지표 (KPI)

| 지표 | 목표 | 측정 방법 |
|---|---|---|
| 온보딩 시간(전원 켜고 첫 라이브까지) | ≤ 5분 (P50), ≤ 10분 (P90) | 사용성 테스트 5명 |
| 라이브 지연(LAN, WebRTC) | ≤ 1.0 s (glass-to-glass) | 타임스탬프 화면 촬영 비교 |
| PTZ 명령 응답(클릭→카메라 이동 시작) | ≤ 300 ms | API 로그 + 영상 검증 |
| Pi 5에서 4대 24/7 녹화+시청 1명 시 CPU | ≤ 40 % 평균, 온도 ≤ 75 °C | node_exporter 72h 소크 |
| 녹화 누락률 | ≤ 0.1 % (분 단위 갭 기준) | 세그먼트 인덱스 갭 분석 |
| 무중단 가동 | 7일 연속 재시작 없음 | 소크 테스트 |
| 카메라 호환성 | 상위 6개 브랜드(Hikvision, Dahua, Reolink, TP-Link Tapo, Uniview, Amcrest)에서 온보딩 성공 | 호환성 매트릭스(부록 B) |

### 2.6 사용자 스토리 & 수용 기준

| ID | 스토리 | 수용 기준 |
|---|---|---|
| US-1 | 사용자로서 네트워크의 카메라를 자동으로 찾고 싶다 | 검색 버튼 후 10초 내 ONVIF 장치 목록(IP, 모델, 제조사) 표시. 미검색 시 수동 등록 경로 안내 |
| US-2 | 자격증명을 넣으면 스트림이 자동 선택되길 원한다 | 프로필 목록 중 "라이브용(H.264, ≤1080p)"과 "녹화용(메인)"을 자동 추천, 사용자 변경 가능 |
| US-3 | 브라우저에서 1초 내 지연으로 실시간 영상을 보고 싶다 | Chrome/Safari/Firefox 최신 2개 버전에서 WebRTC 재생, 실패 시 5초 내 HLS 폴백 |
| US-4 | 화면에서 카메라를 상하좌우로 돌리고 줌하고 싶다 | 버튼 누름/뗌 = ContinuousMove/Stop. 키보드(방향키), 터치 조이스틱 지원. 2초 내 Stop 미수신 시 서버가 자동 Stop |
| US-5 | 자주 보는 위치를 프리셋으로 저장하고 싶다 | 프리셋 저장/이동/삭제, 카메라 측 프리셋과 동기화 |
| US-6 | 24시간 녹화되고 원하는 시각을 되돌려보고 싶다 | 타임라인에 녹화 구간 표시, 클릭 시 3초 내 재생, 10초/1분 스킵 |
| US-7 | 특정 구간을 파일로 저장하고 싶다 | 시작/종료 선택 → MP4 다운로드(재인코딩 없음) |
| US-8 | 디스크가 꽉 차도 시스템이 멈추지 않길 원한다 | 사용률 85% 도달 시 가장 오래된 세그먼트부터 삭제, 80% 미만까지. 보존 하한(예: 3일)은 경고 |
| US-9 | 직원에게는 보기만 허용하고 싶다 | viewer는 PTZ·설정 불가, 감사 로그에 열람 기록 |
| US-10 | 움직임이 감지된 구간만 빨리 찾고 싶다 | 카메라 모션 이벤트를 타임라인 마커로 표시, 이벤트 목록에서 점프 |
| US-11 | 시스템이 이상하면 알고 싶다 | 카메라 오프라인 60초, 디스크 90%, 온도 80°C 초과 시 UI 배너 + (M3) 외부 알림 |

### 2.7 기능 요구사항 (FR)

| ID | 요구사항 | 우선순위 | 마일스톤 |
|---|---|---|---|
| FR-01 | WS-Discovery로 ONVIF 장치 검색, 결과에 GetDeviceInformation 병합 | Must | M1 |
| FR-02 | ONVIF GetProfiles/GetStreamUri/GetSnapshotUri로 프로필·URI 확보, RTSP 수동 URL 등록 지원 | Must | M1 |
| FR-03 | 카메라 자격증명 암호화 저장(AES-256-GCM, 장치 마스터키) | Must | M1 |
| FR-04 | 라이브 스트림: WebRTC(WHEP) 우선, HLS(LL-HLS) 폴백, 트랜스코딩 없음 | Must | M1 |
| FR-05 | 그리드 뷰 1/4/9, 채널 전체화면, 스냅샷 캡처 | Must | M1 |
| FR-06 | PTZ ContinuousMove/Stop/Zoom, 속도 조절, 서버측 안전 Stop 워치독 | Must | M1 |
| FR-07 | PTZ 프리셋 CRUD, GotoPreset, 홈 위치 | Should | M1 |
| FR-08 | 사용자 인증(argon2id), 세션 쿠키, 역할 3종, 로그인 제한/잠금 | Must | M1 |
| FR-09 | 연속 녹화(fMP4, 60초 세그먼트), 카메라별 ON/OFF, 녹화 스트림 선택 | Must | M2 |
| FR-10 | 타임라인 재생(플레이백 서버), 구간 내보내기 MP4 | Must | M2 |
| FR-11 | 보존 정책(일수 + 워터마크), 카메라별 우선순위 | Must | M2 |
| FR-12 | ONVIF PullPoint 이벤트 구독(모션), 이벤트 저장·마커·스냅샷 첨부 | Should | M2 |
| FR-13 | 시스템 상태 대시보드(CPU/메모리/온도/디스크/네트워크/카메라 상태) | Must | M1 |
| FR-14 | 감사 로그(로그인, 설정 변경, PTZ, 열람, 내보내기) | Should | M2 |
| FR-15 | 설정 백업/복원(암호화 내보내기) | Should | M3 |
| FR-16 | 업데이트/롤백 스크립트, 헬스체크 기반 자동 롤백 | Must | M3 |
| FR-17 | 외부 알림(웹훅, 이메일, Telegram, ntfy) | Should | M3 |
| FR-18 | Home Assistant 연동용 REST/WS API 토큰(스코프) | Could | M3 |
| FR-19 | 로컬 모션 검출(카메라가 이벤트 미지원 시, 서브스트림 5fps 프레임 차분) | Could | M3 |
| FR-20 | AI 객체 검출(Hailo/Coral) | Later | M4+ |

### 2.8 비기능 요구사항 (NFR)

| ID | 항목 | 목표 |
|---|---|---|
| NFR-01 | 성능 | Pi 5: 8대 패스스루 녹화 + 동시 시청 세션 6개에서 CPU ≤ 60%. Pi 4: 4대 + 시청 3개 |
| NFR-02 | 지연 | WebRTC LAN ≤ 1 s, HLS 폴백 ≤ 4 s, PTZ ≤ 300 ms |
| NFR-03 | 가용성 | 미디어 코어와 제어 API 프로세스 분리. 한쪽 장애 시 다른 쪽 유지. systemd 자동 재시작 ≤ 5 s |
| NFR-04 | 내구성 | 전원 차단 후 재기동 시 DB 무결성(SQLite WAL) 및 마지막 세그먼트 외 녹화 손실 없음 |
| NFR-05 | 보안 | 기본 HTTPS(자체 CA 또는 로컬 도메인 ACME), 기본 비밀번호 없음(첫 부팅 설정 강제), 카메라 자격증명 암호화, OWASP ASVS L1 충족 |
| NFR-06 | 프라이버시 | 외부 네트워크로 영상/메타데이터 전송 없음(옵트인 알림 제외). 텔레메트리 없음 |
| NFR-07 | 운영성 | 모든 컴포넌트 메트릭(Prometheus) + 구조화 로그(JSON, journald). 단일 헬스 엔드포인트 |
| NFR-08 | 설치성 | Raspberry Pi OS Lite 64-bit에서 스크립트 1개로 설치 ≤ 15분(이미지 다운로드 제외) |
| NFR-09 | 저장 효율 | 4 Mbps × 8대 = 약 345 GB/일. 1 TB SSD 기준 약 3일 → 기본은 메인스트림 2 Mbps 권장 및 보존 정책 안내 |
| NFR-10 | 접근성/모바일 | 모바일 브라우저(iOS Safari, Android Chrome) 1열 레이아웃, 터치 PTZ, WCAG 2.1 AA 대비 |
| NFR-11 | 호환성 | ONVIF Profile S 필수, Profile T 선택. 브라우저 최신 2개 버전 |
| NFR-12 | 시간 동기 | NTP(chrony) 필수. 카메라 시간은 ONVIF SetSystemDateAndTime으로 Pi와 동기화(옵션) |

---

## 3. 도메인 조사 요약

### 3.1 IP 카메라 프로토콜 [확정]

| 영역 | 표준 | 이 프로젝트에서의 사용 |
|---|---|---|
| 검색 | WS-Discovery (ONVIF Core) — UDP 멀티캐스트 239.255.255.250:3702 | FR-01. VLAN 분리 시 멀티캐스트가 넘어오지 않으므로 수동 등록 병행 |
| 장치/미디어 | ONVIF Device/Media(1/2) 서비스 — GetDeviceInformation, GetCapabilities, GetProfiles, GetStreamUri, GetSnapshotUri | 프로필 자동 선택, RTSP URI 확보 |
| 인증 | WS-UsernameToken(PasswordDigest) + 시계 오차 보정(GetSystemDateAndTime) | 카메라 시간 오차 ±5분 초과 시 인증 실패 → 보정 로직 필수 |
| 스트리밍 | RTSP(RFC 2326/7826) + RTP, TCP 인터리브 권장 | UDP 패킷 손실 회피 위해 RTSP over TCP 기본 |
| PTZ | ONVIF PTZ 서비스 — ContinuousMove/AbsoluteMove/RelativeMove/Stop/GetPresets/SetPreset/GotoPreset/GotoHomePosition | 연속 이동 속도 벡터 x,y,zoom ∈ [-1,1] |
| 이벤트 | ONVIF Events — PullPoint 구독(CreatePullPointSubscription → PullMessages), 토픽 `tns1:VideoSource/MotionAlarm`, `tns1:RuleEngine/CellMotionDetector/Motion` | 제조사별 토픽 편차 큼 → 토픽 매핑 테이블 |
| 프로필 | Profile S(스트리밍/PTZ), Profile T(H.265, 메타데이터·분석 이벤트) | S 필수, T 선택 |

### 3.2 브라우저 스트리밍 방식 비교 [확정]

| 방식 | 지연 | 코덱 | 장점 | 단점 | 채택 |
|---|---|---|---|---|---|
| WebRTC (WHEP) | 0.2~1 s | H.264 범용, H.265는 Safari 일부·Chrome 일부 HW | 최저 지연, 표준 시그널링(WHEP) | UDP/ICE 필요(VPN 내부는 문제없음), 뷰어당 서버 대역 | **기본** |
| HLS / LL-HLS (fMP4) | 2~6 s | H.264 범용, H.265 Safari·일부 Chrome | 어디서나 재생, 프록시 친화 | 지연 큼 | **폴백** |
| MSE over WebSocket | 0.5~2 s | 브라우저 MSE 지원 코덱 | 낮은 지연, TCP만 사용 | iOS Safari MSE 제한(17.1+ 일부) | Later |
| MJPEG | ~0.3 s | JPEG | 단순 | 대역 큼, 카메라 측 지원 필요 | 미채택(스냅샷만) |

### 3.3 라즈베리파이 하드웨어 제약 [확정]

| 보드 | H.264 디코드 | H.264 인코드 | H.265 디코드 | 시사점 |
|---|---|---|---|---|
| Pi 4 | HW 1080p60 | HW 1080p30 (V4L2 m2m) | HW 4Kp60 | 소규모 트랜스코딩 가능하나 채널 1~2개가 한계 |
| Pi 5 | **SW만**(CPU가 빨라 1080p60 SW 디코드 가능) | **SW만**(HW 인코더 없음) | HW 4Kp60 | 트랜스코딩은 사실상 금지. 패스스루 설계 필수 |

Pi 5는 HEVC 하드웨어 디코더만 있고 H.264 디코드·모든 인코드는 소프트웨어다(출처: Raspberry Pi 포럼·Jeff Geerling 리뷰·Frigate 논의, 부록 D). → **설계 원칙: 영상은 복사(copy)만 하고 절대 재인코딩하지 않는다. 브라우저에 맞지 않는 코덱은 카메라의 서브스트림 설정을 바꿔 해결한다.**

기타 제약: SD 카드 쓰기 마모(로그·녹화는 SSD로), 발열(패시브 쿨링 시 스로틀링 80°C, 액티브 쿨러 권장), USB 3.0 대역 공유(NVMe HAT 권장), 기가비트 이더넷(8대 × 4 Mbps 인입 + 시청 트래픽 ≈ 100 Mbps 이내로 여유).

### 3.4 기존 솔루션 포지셔닝

| 솔루션 | 성격 | 라즈베리파이 적합성 | 우리와의 차이 |
|---|---|---|---|
| Frigate | AI 객체검출 NVR | 검출기 없이는 무겁고 설정이 YAML | 우리는 "보기·제어·녹화" 우선, AI는 후순위 |
| MotionEye | 모션 감지 중심 웹 UI | 가벼움, 오래됨, PTZ 약함 | ONVIF PTZ·WebRTC·타임라인 |
| ZoneMinder | 전통 NVR | 무겁고 UI 노후 | 온보딩 UX, 경량 |
| Scrypted | 통합 허브(HomeKit) | 좋으나 플러그인 복잡 | 단일 목적 단순함 |
| go2rtc | 스트림 게이트웨이 | 매우 가볍고 ONVIF 지원 | 라이브러리 계층으로 사용 가능(대안, §4.9 ADR-002) |
| MediaMTX | 미디어 서버(녹화·재생·WHEP·메트릭) | 단일 바이너리, ARM64 지원 | **미디어 코어로 채택** |

### 3.5 규제·윤리 [확정, A8]

- 개인정보보호법 제25조: 공개된 장소의 고정형 영상정보처리기기는 안내판 설치, 목적 외 이용 금지, 녹음 금지, 임의 조작(PTZ로 다른 장소 비추기) 제한.
- 시스템 대응: 보존기간 기본 30일 상한 설정 UI, 안내판 문구 템플릿 제공, 열람·내보내기 감사 로그, PTZ 조작 로그, 오디오 녹음 기본 OFF.
- 사적 공간(가정) 내부 사용은 법 적용이 다르나 동일 기본값을 유지한다.

---

## 4. 아키텍처

### 4.1 설계 원칙

1. **Local-first**: 모든 기능이 인터넷 없이 동작. 외부 통신은 옵트인(알림, 업데이트 확인).
2. **No transcode**: 미디어 경로에서 재인코딩 금지. 코덱 문제는 카메라 설정으로 해결.
3. **작은 부품의 조합**: 미디어 코어(MediaMTX), 제어 API(FastAPI), 프록시(Caddy), UI(정적 PWA). 각자 독립 재시작.
4. **실패 격리**: 미디어 코어가 죽어도 설정·PTZ·상태 UI는 살아있고, 제어 API가 죽어도 진행 중 녹화는 계속된다.
5. **보안 기본값**: HTTPS 강제, 기본 비밀번호 없음, 카메라 자격증명 암호화, 카메라 VLAN 인터넷 차단 권고.
6. **관측 가능**: 모든 프로세스가 메트릭·구조화 로그·헬스를 낸다.

### 4.2 시스템 컨텍스트

```mermaid
flowchart LR
  subgraph LAN["가정/매장 LAN"]
    subgraph CAMVLAN["카메라 VLAN (인터넷 차단 권고)"]
      C1["IP Cam 1<br/>RTSP/ONVIF"]
      C2["IP Cam N<br/>RTSP/ONVIF"]
    end
    subgraph PI["Raspberry Pi 5 (PiCam Hub)"]
      CADDY["Caddy<br/>:443 TLS 리버스 프록시"]
      WEB["hub-web<br/>React PWA (정적)"]
      API["hub-api<br/>FastAPI · ONVIF · 정책 · 인증"]
      MTX["MediaMTX<br/>RTSP pull · WHEP · HLS · 녹화 · 플레이백"]
      DB[("SQLite WAL")]
      SSD[("SSD /data<br/>녹화 세그먼트")]
      MON["node_exporter / (옵션) Prometheus+Grafana"]
    end
    U1["운영자 브라우저 (LAN)"]
  end
  U2["원격 사용자 브라우저"] -- "WireGuard/Tailscale VPN" --> CADDY
  U1 --> CADDY
  CADDY --> WEB
  CADDY -- "/api" --> API
  CADDY -- "/whep, /hls, /playback" --> MTX
  API -- "ONVIF SOAP (검색·프로필·PTZ·이벤트)" --> C1
  API -- "ONVIF SOAP" --> C2
  MTX -- "RTSP/TCP pull" --> C1
  MTX -- "RTSP/TCP pull" --> C2
  API -- "Control API :9997<br/>JWT/JWKS" --> MTX
  API --> DB
  MTX --> SSD
  API -. "보존 정책·인덱스" .-> SSD
  MON -. "scrape :9998, :8000/metrics" .-> MTX
```

### 4.3 컴포넌트 책임

| 컴포넌트 | 기술 | 책임 | 하지 않는 것 |
|---|---|---|---|
| **MediaMTX** (미디어 코어) | Go 단일 바이너리(MIT), arm64 | 카메라별 경로(path)로 RTSP pull, WHEP/HLS 서빙, fMP4 세그먼트 녹화, 플레이백 서버(`/list`, `/get`), Prometheus 메트릭, JWT 인증 | ONVIF, 사용자 관리, 보존 정책의 "우선순위" 판단 |
| **hub-api** | Python 3.12 + FastAPI + SQLAlchemy(SQLite) + `onvif-zeep-async` + `wsdiscovery` | 인증/RBAC/세션, 카메라 CRUD·검색·프로필 선택, 자격증명 암호화, MediaMTX 경로 동기화(Control API), 스트림 JWT 발급 + JWKS, PTZ 프록시·워치독, ONVIF 이벤트 폴링, 녹화 인덱스·보존 정책, 시스템 상태, 감사 로그, WebSocket 이벤트 푸시 | 미디어 바이트 처리 |
| **hub-web** | React 18 + TypeScript + Vite, PWA, hls.js | 라이브 그리드, PTZ 조작, 타임라인, 설정 화면, 상태 대시보드 | 비즈니스 규칙 |
| **Caddy** | 리버스 프록시 | 단일 진입점 :443, 자동 TLS(로컬 CA / `*.local` 내부 CA 또는 사용자 도메인 ACME), 라우팅, 기본 보안 헤더, gzip | 인증 판단(hub-api·MediaMTX가 수행) |
| **event-worker** | hub-api 내부 asyncio 태스크 | 카메라별 PullPoint 루프, 헬스 프로브(RTSP DESCRIBE/ONVIF GetSystemDateAndTime), 디스크 워터마크 스캔(10분), 세그먼트 인덱싱 | — |
| **관측 스택(옵션 프로파일)** | node_exporter, Prometheus, Grafana, (Loki+promtail) | 메트릭 수집·대시보드·알림 | 기본 설치 시 node_exporter만 |

### 4.4 핵심 시퀀스

#### (a) 카메라 온보딩

```mermaid
sequenceDiagram
  participant W as hub-web
  participant A as hub-api
  participant C as Camera(ONVIF)
  participant M as MediaMTX
  W->>A: POST /cameras/discover
  A->>C: WS-Discovery Probe (multicast)
  C-->>A: ProbeMatch (XAddrs, scopes)
  A-->>W: [{ip, xaddr, model?}]
  W->>A: POST /cameras {xaddr, username, password}
  A->>C: GetSystemDateAndTime → 시계 오차 보정
  A->>C: GetDeviceInformation / GetCapabilities / GetProfiles
  A->>C: GetStreamUri(profile) ×N, GetSnapshotUri
  A->>A: 프로필 점수화(코덱 H.264 우선, 해상도, fps) → live/record 추천
  A->>A: 자격증명 AES-GCM 암호화 저장, camera row 생성
  A->>M: POST /v3/config/paths/add/cam_{slug} {source: rtsp://..., record: yes}
  M->>C: RTSP DESCRIBE/SETUP/PLAY (TCP)
  M-->>A: 200
  A-->>W: 201 {camera, streams, ptz_capable}
```

#### (b) 라이브 시청 (WHEP)

1. `hub-web` → `POST /api/v1/cameras/{id}/stream-token` → `hub-api`가 5분 만료 JWT(`mediamtx_permissions: [{action:"read", path:"cam_front"}]`) 발급.
2. `hub-web` → `POST /whep/cam_front?jwt=<token>` (SDP offer) → Caddy → MediaMTX. MediaMTX는 `hub-api`의 JWKS(`/internal/jwks`, 캐시)로 서명 검증.
3. SDP answer 수신 → ICE(LAN/VPN 내부이므로 STUN/TURN 불필요, `webrtcICEServers2` 비움) → 재생.
4. 실패(브라우저 미지원, ICE 실패 5초) → `hls.js`로 `/hls/cam_front/index.m3u8?jwt=` 폴백.
5. 토큰 만료 전 갱신은 `hub-web`이 4분마다 재발급(연결 유지 중에는 재검증 없음, 새 세션에만 적용).

#### (c) PTZ

- 버튼 누름: `POST /cameras/{id}/ptz/move {pan, tilt, zoom, speed}` → `ContinuousMove(velocity)`.
- 버튼 뗌: `POST /cameras/{id}/ptz/stop` → `Stop(PanTilt=true, Zoom=true)`.
- 워치독: 마지막 move 후 2초 내 stop/move 갱신이 없으면 서버가 `Stop` 전송(네트워크 끊김·탭 종료 대비).
- 동시 제어 충돌: 카메라당 "제어권" 락(30초, 마지막 조작자 갱신). 다른 사용자는 잠김 표시. owner는 강제 인계.

#### (d) 녹화·플레이백

- MediaMTX `record: yes`, `recordFormat: fmp4`, `recordSegmentDuration: 60s`, `recordPath: /data/recordings/%path/%Y-%m-%d_%H-%M-%S-%f`.
- `hub-api` 인덱서가 `GET :9997/v3/recordings/list`(또는 파일 스캔)로 `recording_segments` 테이블 갱신(5분 주기 + 파일 이벤트).
- 타임라인: `GET /api/v1/cameras/{id}/recordings?from&to` → 구간 배열. 재생: `<video src="/playback/get?path=cam_front&start=<ISO>&duration=300&format=mp4&jwt=...">`(MediaMTX 플레이백이 fMP4 세그먼트를 이어붙여 표준 MP4로 재먹싱, 재인코딩 없음).
- 내보내기: 동일 엔드포인트에 `Content-Disposition` 부여, 최대 30분 제한.

#### (e) 이벤트

- 카메라별 PullPoint 구독(만료 1분, 갱신), `PullMessages(Timeout=PT60S, MessageLimit=100)` 장기 폴링.
- 토픽 매핑: `MotionAlarm`, `CellMotionDetector/Motion`, `tns1:RuleEngine/MyRuleDetector/*`(Reolink/Tapo 계열 people/vehicle 포함) → `event.type ∈ {motion, person, vehicle, tamper, unknown}`.
- 상태 전이(`IsMotion true/false`)로 start/end 생성. 시작 시 스냅샷 1장 저장(`/data/snapshots/`).
- 이벤트 미지원 카메라: M3에서 로컬 프레임 차분(서브스트림 5 fps, OpenCV) 폴백.

### 4.5 스트리밍 결정 트리 [확정, A11]

```
카메라 프로필 목록 수집
├─ H.264 ≤1080p 프로필 있음 → live = 그 프로필(WebRTC), record = 메인(H.264면 그대로, H.265면 H.265 저장·재생은 HLS/MP4 다운로드)
├─ H.264 프로필이 4K 메인만 있음 → live = 4K H.264 WebRTC(브라우저 디코드 부담 경고) + 카메라 서브스트림 H.264 설정 안내
└─ H.265 전용
   ├─ Safari/Chrome(HW HEVC) → HLS(fMP4) 재생, 지연 2~4s, "제한 모드" 배지
   └─ 그 외 브라우저 → 스냅샷 폴링(1 fps) + "카메라 서브스트림을 H.264로 바꾸세요" 안내 (Pi에서 트랜스코딩하지 않음)
```

### 4.6 성능 예산 [가정 — HIL 테스트로 검증 필요]

| 항목 | 가정치 | 근거 |
|---|---|---|
| 카메라당 인입 | 메인 4 Mbps + 서브 1 Mbps | 일반 1080p/720p 설정 |
| MediaMTX CPU (패스스루, 채널당) | Pi 5 ≈ 2~4 %, Pi 4 ≈ 4~7 % | Go 패스스루 벤치 경험치 |
| WebRTC 뷰어 세션당 추가 CPU | ≈ 1~3 % (SRTP 암호화) | |
| hub-api 상시 | ≈ 3~5 % (이벤트 폴링, 헬스) | |
| 메모리 | MediaMTX 150~300 MB(8채널), hub-api 150 MB, Caddy 50 MB | |
| 디스크 쓰기 | 8대 × 4 Mbps = 4 MB/s 연속 → SSD 필수 | |
| 상한 | **Pi 5: 8채널 + 6 뷰어**, **Pi 4: 4채널 + 3 뷰어** | 설정에서 채널·뷰어 상한 강제, 초과 시 거부(429) |

### 4.7 기술 스택 & 근거

| 계층 | 선택 | 근거 | 대안(기각 이유) |
|---|---|---|---|
| 미디어 코어 | **MediaMTX** | 단일 바이너리·MIT, RTSP pull·WHEP·LL-HLS·fMP4 녹화·플레이백·JWT 인증·Prometheus 메트릭을 모두 내장. arm64 지원 | go2rtc(녹화·플레이백 없음, 별도 ffmpeg 세그먼터 필요) / ffmpeg 직접(시그널링·재생 서버를 직접 작성해야 함) |
| 제어 API | **Python 3.12 + FastAPI** | ONVIF 생태계(onvif-zeep-async, Home Assistant에서 검증), async, 빠른 개발. Pi에서 CPU 부담 낮은 워크로드 | Go(ONVIF 라이브러리 성숙도 낮음) / Node(onvif 패키지 있으나 SOAP 편차 처리 사례 적음) |
| DB | **SQLite(WAL)** | 단일 장치, 운영 부담 0, 백업 = 파일 복사 | PostgreSQL(과함) |
| 프론트 | **React + TS + Vite, PWA** | WebRTC/hls.js 예제 풍부, 팀 친숙도 | Svelte(팀 학습비용), HTMX(실시간 타임라인 UI에 부족) |
| 프록시/TLS | **Caddy** | 자동 TLS, 설정 짧음, 내부 CA 발급 | nginx(TLS 자동화 별도) |
| 배포 | **Docker Compose + systemd 유닛** | 재현성, 롤백 = 태그 변경 | 네이티브 패키지(초기엔 유지비 큼) |
| 프로비저닝 | **Ansible** + 설치 스크립트 | 멱등, 여러 Pi 재현 | 쉘 스크립트만(드리프트) |
| 관측 | node_exporter + (옵션) Prometheus/Grafana, journald JSON | Pi 자원 고려해 옵션 프로파일 | |
| 원격 접근 | WireGuard/Tailscale 가이드 | 포트포워딩 회피, WebRTC UDP 통과 | 클라우드 릴레이(Later) |

### 4.8 배포 토폴로지

| 모드 | 구성 | 대상 |
|---|---|---|
| **로컬(기본)** | Pi 하나, LAN 내 접속. Caddy 내부 CA 인증서(설치 시 루트 CA 다운로드 안내) 또는 `picamhub.local` mDNS | P1, P2 |
| **VPN 원격** | Tailscale(설치 스크립트 옵션) 또는 라우터 WireGuard. 브라우저는 VPN 통해 동일 URL 접속. WebRTC는 VPN 내부 사설 IP ICE 후보로 연결 | P1, P3 |
| **(Later) 클라우드 릴레이** | 시그널링·TURN·계정 서비스. 영상은 E2E 암호화 옵션 | 미확정 |

### 4.9 아키텍처 결정 기록 (ADR 요약)

| ADR | 결정 | 이유 | 결과/트레이드오프 |
|---|---|---|---|
| ADR-001 | 무트랜스코딩 패스스루 | Pi 5 HW 인코더 없음(§3.3) | H.265 전용 카메라는 제한 모드. 카메라 설정 변경을 UX로 안내 |
| ADR-002 | 미디어 코어로 MediaMTX (go2rtc 아님) | 녹화·플레이백·JWT·메트릭 내장 | ONVIF는 hub-api가 담당. go2rtc의 ONVIF 서버 기능(타 NVR에 노출)은 포기 |
| ADR-003 | 스트림 인증은 JWT+JWKS | Caddy에서 세션 검증하지 않아도 MediaMTX가 독립 검증. 프로세스 간 결합 최소 | 토큰 유출 시 5분 창. 쿼리스트링 토큰은 로그 마스킹 필요 |
| ADR-004 | SQLite 단일 파일 | 단일 장치, 운영 단순 | 다중 Pi 통합 시 마이그레이션 필요 |
| ADR-005 | 세션 쿠키 인증(JWT 세션 아님) | 즉시 폐기 가능, CSRF는 SameSite=Strict + 커스텀 헤더 | 서버 세션 테이블 관리 |
| ADR-006 | 클라우드 없음(MVP) | 프라이버시 가치 제안, 팀 규모 | 원격 접근 UX가 VPN 설치에 의존 → 가이드 품질이 중요 |
| ADR-007 | 이벤트는 카메라 ONVIF 이벤트 우선 | Pi CPU 절약 | 제조사 편차 → 토픽 매핑 테이블 유지 |

---

## 5. 위협 모델

### 5.1 자산

| 자산 | 민감도 | 비고 |
|---|---|---|
| 실시간·녹화 영상, 스냅샷 | 매우 높음 | 개인정보(얼굴·행동) |
| 카메라 자격증명 | 높음 | 카메라 탈취 → 영상·네트워크 피벗 |
| 사용자 계정·세션 | 높음 | |
| 설정(RTSP URL, 네트워크 정보) | 중간 | 내부망 정보 노출 |
| 감사 로그 | 중간 | 무결성이 중요 |
| Pi 자체(SD/SSD) | 높음 | 물리 접근 시 전체 노출 |

### 5.2 신뢰 경계

```mermaid
flowchart TB
  INET["인터넷 (신뢰 없음)"]
  VPN["VPN 터널 (인증된 사용자, 중간 신뢰)"]
  LAN["사용자 LAN (중간 신뢰)"]
  CAM["카메라 VLAN (낮은 신뢰: 펌웨어 취약·phone-home)"]
  PI["Pi 내부 (높은 신뢰, 물리 접근 제외)"]
  INET -. "차단(포트포워딩 금지)" .-> PI
  INET --> VPN --> PI
  LAN --> PI
  PI -- "RTSP/ONVIF만 허용" --> CAM
  CAM -. "인터넷 아웃바운드 차단 권고" .-> INET
```

### 5.3 STRIDE 분석

| 위협 | 시나리오 | 심각도 | 대응 (설계 반영) |
|---|---|---|---|
| **S**poofing | 로그인 브루트포스 | 높음 | argon2id, IP·계정별 5회/분 제한, 10회 실패 시 15분 잠금, (M3) TOTP |
| Spoofing | 가짜 ONVIF 장치가 검색에 응답해 자격증명 수집 | 중간 | 검색 결과에 자격증명 자동 전송 금지, 사용자가 선택한 장치에만 인증. WS-UsernameToken은 다이제스트(평문 비전송) |
| Spoofing | 스트림 JWT 도용 | 중간 | 5분 만료, path·action 한정, 세션 폐기 시 kid 회전 옵션 |
| **T**ampering | 감사 로그 변조 | 중간 | append-only 테이블 + 일별 해시 체인, owner도 삭제 불가(보존기간 만료만) |
| Tampering | 녹화 파일 변조/삭제 | 중간 | 파일 소유자 `mediamtx` 사용자, hub-api는 보존 정책 경로로만 삭제, 내보내기 파일 SHA-256 표시 |
| **R**epudiation | 누가 PTZ를 돌렸는지·누가 열람했는지 부인 | 중간 | 감사 로그: 사용자·시각·IP·행위·대상. PTZ·열람·내보내기 모두 기록 |
| **I**nfo disclosure | RTSP 평문(카메라↔Pi) 스니핑 | 중간 | 카메라 VLAN 격리 권고, 유선 연결 권장. RTSPS 지원 카메라는 우선 사용(설정) |
| Info disclosure | 카메라 자격증명 DB 유출 | 높음 | AES-256-GCM, 마스터키 `/etc/picamhub/keys/master.key`(0600, root), 로그·API 응답에 비밀번호 절대 미포함(RTSP URL 마스킹) |
| Info disclosure | 물리 탈취(SD/SSD) | 높음 | (옵션) `/data` LUKS 암호화 + 부팅 시 키 입력 또는 키파일. **잔여 리스크**: Pi에 TPM 없음 → 자동 부팅 시 키파일이 SD에 존재. 문서화·경고 |
| Info disclosure | 카메라 phone-home으로 영상 유출 | 높음 | nftables로 카메라 VLAN→WAN 차단 룰 제공(Pi가 게이트웨이가 아니면 라우터 설정 가이드) |
| **D**oS | 뷰어 폭주로 Pi 과부하 | 중간 | 채널·뷰어 상한(§4.6), 429, 우선순위(owner 우선) |
| DoS | 디스크 풀 → 녹화·DB 중단 | 높음 | 워터마크 삭제, DB는 SSD와 별도 파티션(또는 예약 공간 5%), 디스크 90% 알림 |
| DoS | 카메라 응답 지연으로 API 스레드 고갈 | 중간 | 모든 ONVIF 호출 타임아웃 5초, 카메라별 동시성 1, 서킷브레이커(3회 실패 시 30초 오픈) |
| **E**oP | viewer가 PTZ/설정 호출 | 높음 | 서버측 RBAC(엔드포인트별 권한 데코레이터), 프론트 숨김에 의존 안 함. 객체 소유 검증(IDOR) |
| EoP | CSRF로 카메라 삭제 | 중간 | SameSite=Strict 쿠키 + `X-Requested-With` 필수 + Origin 검증 |
| EoP | 컨테이너 탈출/취약 의존성 | 중간 | 비루트 컨테이너, read-only rootfs, 최소 capabilities, 주간 `trivy`·`pip-audit`·`npm audit` CI |
| EoP | SSRF: 사용자가 입력한 RTSP/XAddr URL로 내부 서비스 접근 | 중간 | URL 스킴 화이트리스트(rtsp/rtsps/http/https), 목적지 사설대역만 허용·Pi 자신(127.0.0.1, 링크로컬) 차단 |

### 5.4 보안 기본값 체크리스트

- [ ] 첫 부팅 시 owner 계정 생성 강제(기본 비밀번호 없음), 비밀번호 12자 이상 + zxcvbn 점수 ≥ 3
- [ ] HTTPS 전용, HSTS, `Content-Security-Policy`(self + blob: for video), `X-Frame-Options: DENY`
- [ ] 쿠키: `HttpOnly; Secure; SameSite=Strict`, 세션 12시간, 유휴 1시간
- [ ] MediaMTX·hub-api 내부 포트는 127.0.0.1 바인딩, Caddy만 0.0.0.0:443
- [ ] SSH 키 인증만, `unattended-upgrades` 보안 업데이트 ON
- [ ] 로그에서 비밀번호·토큰 마스킹(정규식 필터 + 구조화 필드 분리)
- [ ] API 응답의 RTSP URL은 `rtsp://***:***@host/...` 형태
- [ ] 카메라 기본 비밀번호(admin/admin 등) 감지 시 온보딩에서 경고

### 5.5 잔여 리스크 [미확정 포함]

| 리스크 | 상태 |
|---|---|
| Pi 물리 탈취 시 마스터키 노출 | **[확정]** 하드웨어 한계. LUKS + 수동 키 입력 옵션 제공, 기본은 편의성 우선 |
| 카메라 펌웨어 취약점 | **[확정]** 통제 불가. VLAN 격리·아웃바운드 차단 가이드로 완화 |
| VPN 오설정으로 공개 노출 | **[가능성]** 설치 스크립트가 공인 IP에서 443 도달 가능 여부를 점검해 경고 |

---

## 6. API 계약

### 6.1 공통 규약

- Base: `https://<host>/api/v1`, JSON(UTF-8). 시각은 ISO 8601 UTC(`Z`), ID는 UUIDv7.
- 인증: 세션 쿠키(`pch_session`) 또는 `Authorization: Bearer <api_token>`(스코프 토큰, M3).
- 상태 변경 요청은 `X-Requested-With: PiCamHub` 헤더 필수(CSRF).
- 에러: RFC 9457 `application/problem+json`
  ```json
  { "type": "https://picamhub.dev/errors/camera-unreachable", "title": "Camera unreachable", "status": 502, "detail": "ONVIF GetProfiles timed out after 5s", "instance": "/api/v1/cameras/018f...", "code": "CAMERA_UNREACHABLE", "trace_id": "..." }
  ```
- 페이지네이션: `?cursor=&limit=`(기본 50, 최대 500), 응답 `next_cursor`.
- 레이트리밋: 로그인 5/min/IP, PTZ 20/s/카메라, 일반 60/s/세션. 초과 시 429 + `Retry-After`.
- 멱등: `POST /cameras`는 `Idempotency-Key` 헤더 지원(24h).
- 버전: 경로 버전. 하위호환 깨지면 `/v2`.

### 6.2 엔드포인트

| 메서드·경로 | 역할 | 권한 | 설명 |
|---|---|---|---|
| `POST /auth/setup` | 초기 owner 생성 | 비인증(사용자 0명일 때만) | |
| `POST /auth/login` · `POST /auth/logout` · `GET /auth/me` | 세션 | — | |
| `GET/POST /users` · `PATCH/DELETE /users/{id}` | 사용자·역할 | owner | |
| `POST /cameras/discover` | WS-Discovery 실행(≤10 s) | owner | 결과는 저장하지 않음 |
| `POST /cameras/probe` | 자격증명으로 프로필·PTZ 능력 조회(등록 전 미리보기) | owner | 응답에 추천 프로필 |
| `GET /cameras` · `POST /cameras` · `GET/PATCH/DELETE /cameras/{id}` | 카메라 CRUD | viewer(GET) / owner | PATCH로 live/record 프로필 변경 |
| `POST /cameras/{id}/test` | 연결 테스트(RTSP DESCRIBE + 스냅샷) | owner | |
| `POST /cameras/{id}/stream-token` | 스트림 JWT 발급 | viewer+ | `{ "jwt": "...", "expires_at": "...", "whep_url": "/whep/cam_front", "hls_url": "/hls/cam_front/index.m3u8" }` |
| `GET /cameras/{id}/snapshot` | JPEG 스냅샷 프록시 | viewer+ | 캐시 1 s |
| `POST /cameras/{id}/ptz/move` | ContinuousMove | operator+ | `{ "pan": -1..1, "tilt": -1..1, "zoom": -1..1, "timeout_ms": 2000 }` |
| `POST /cameras/{id}/ptz/stop` | Stop | operator+ | |
| `POST /cameras/{id}/ptz/home` | GotoHome | operator+ | |
| `GET/POST /cameras/{id}/ptz/presets` · `POST /cameras/{id}/ptz/presets/{pid}/goto` · `DELETE ...` | 프리셋 | operator+(goto) / owner(CRUD) | 카메라 측 프리셋과 동기화 |
| `POST /cameras/{id}/ptz/lock` · `DELETE /cameras/{id}/ptz/lock` | 제어권 | operator+ | 30 s 자동 만료 |
| `GET /cameras/{id}/recordings?from&to` | 녹화 구간 목록 | viewer+ | `[{ "start": "...", "end": "...", "bytes": 12345 }]` |
| `POST /cameras/{id}/recordings/export` | 구간 MP4 내보내기(≤30 min) | operator+ | 202 + job id → `GET /jobs/{id}` → 다운로드 URL |
| `GET /events?camera_id&type&from&to` · `GET /events/{id}` | 이벤트 | viewer+ | 스냅샷 URL 포함 |
| `GET /system/health` | 컴포넌트 상태 | 비인증(요약) / owner(상세) | `{ "status": "ok|degraded|down", "components": { "mediamtx": ..., "db": ..., "disk": ... } }` |
| `GET /system/stats` | CPU/메모리/온도/디스크/네트워크 | viewer+ | |
| `GET/PATCH /settings` | 보존 정책, 뷰어 상한, 알림 채널 등 | owner | |
| `GET /audit?from&to&user&action` | 감사 로그 | owner | |
| `POST /system/backup` · `POST /system/restore` | 설정 백업/복원 | owner | 암호화 zip |
| `GET /ws` | WebSocket 이벤트 스트림 | viewer+ | §6.4 |
| `GET /internal/jwks` | MediaMTX용 JWKS | 127.0.0.1만 | |
| `GET /metrics` | Prometheus | 127.0.0.1만 | |

### 6.3 주요 요청/응답 예시

**POST /cameras**
```json
{
  "name": "정문",
  "xaddr": "http://192.168.10.21/onvif/device_service",
  "username": "admin",
  "password": "********",
  "live_profile_token": "Profile_2",
  "record_profile_token": "Profile_1",
  "record_enabled": true,
  "transport": "tcp"
}
```
```json
{
  "id": "018f6b2e-....",
  "slug": "cam_front",
  "name": "정문",
  "vendor": "HIKVISION", "model": "DS-2DE4A425IW", "firmware": "V5.7.3",
  "status": "online",
  "capabilities": { "ptz": true, "presets": true, "events": true, "snapshot": true, "audio": true },
  "streams": {
    "live":   { "profile": "Profile_2", "codec": "H264", "width": 1280, "height": 720, "fps": 25, "rtsp_url": "rtsp://***:***@192.168.10.21:554/Streaming/Channels/102" },
    "record": { "profile": "Profile_1", "codec": "H264", "width": 1920, "height": 1080, "fps": 25, "rtsp_url": "rtsp://***:***@192.168.10.21:554/Streaming/Channels/101" }
  },
  "record_enabled": true,
  "created_at": "2026-09-03T01:00:00Z"
}
```

**수동 등록(ONVIF 없음)**: `xaddr` 대신 `rtsp_url`만 제공하면 `capabilities.ptz=false`, 이벤트 없음. 

**POST /cameras/{id}/ptz/move** → `204`. 실패: `409 PTZ_LOCKED_BY_OTHER`, `502 CAMERA_UNREACHABLE`, `400 PTZ_NOT_SUPPORTED`.

### 6.4 WebSocket 이벤트 (`/api/v1/ws`)

| type | payload | 용도 |
|---|---|---|
| `camera.status` | `{camera_id, status: online|offline|degraded, since}` | 그리드 배지 |
| `event.started` / `event.ended` | `{event_id, camera_id, type, at, snapshot_url?}` | 타임라인 마커·토스트 |
| `ptz.lock` | `{camera_id, user_id|null, expires_at}` | 제어권 표시 |
| `system.alert` | `{level, code, message}` | 디스크/온도/코어 다운 배너 |
| `recording.gap` | `{camera_id, from, to}` | 녹화 누락 표시 |

### 6.5 MediaMTX 연동 규약

- Control API(127.0.0.1:9997): `POST /v3/config/paths/add/{name}`, `PATCH /v3/config/paths/patch/{name}`, `DELETE /v3/config/paths/delete/{name}`, `GET /v3/paths/list`, `GET /v3/recordings/list`.
- 인증: `authMethod: jwt`, `authJWTJWKS: http://127.0.0.1:8000/api/v1/internal/jwks`, 클레임 `mediamtx_permissions: [{"action":"read","path":"cam_front"}]`, 브라우저는 `?jwt=` 쿼리로 전달. (MediaMTX 버전별 키 이름 변경 가능 → 설치 시 버전 핀 고정 `[미확정: 핀 버전은 M0에서 결정]`)
- 플레이백(127.0.0.1:9996): `GET /list?path=&start=&end=`, `GET /get?path=&start=&duration=&format=mp4`. Caddy가 `/playback/*`로 프록시.
- 메트릭(127.0.0.1:9998) → Prometheus.
- 경로 이름 규칙: `cam_{slug}` (라이브/녹화 동일 소스가 아닐 때 `cam_{slug}_rec` 별도 경로).

---

## 7. 데이터 모델

```mermaid
erDiagram
  users ||--o{ sessions : has
  users ||--o{ audit_log : writes
  users ||--o{ api_tokens : owns
  cameras ||--|| camera_credentials : secures
  cameras ||--o{ camera_streams : exposes
  cameras ||--o{ ptz_presets : has
  cameras ||--o{ recording_segments : produces
  cameras ||--o{ events : emits
  cameras ||--o{ camera_status_log : logs
  events ||--o{ snapshots : attaches

  users { uuid id PK
          text username UK
          text password_hash
          text role "owner|operator|viewer"
          bool is_active
          text totp_secret_enc "nullable"
          datetime created_at
          datetime last_login_at }
  sessions { text id PK "random 256bit"
             uuid user_id FK
             text ip
             text user_agent
             datetime created_at
             datetime last_seen_at
             datetime expires_at }
  api_tokens { uuid id PK
               uuid user_id FK
               text name
               text token_hash
               text scopes "json"
               datetime expires_at
               datetime revoked_at }
  cameras { uuid id PK
            text slug UK
            text name
            text xaddr "nullable"
            text vendor
            text model
            text firmware
            text serial
            text transport "tcp|udp"
            bool record_enabled
            int retention_days "nullable → 전역 기본"
            int priority "보존 삭제 우선순위"
            text capabilities "json"
            text status "online|offline|degraded"
            datetime created_at
            datetime updated_at }
  camera_credentials { uuid camera_id PK
                       text username
                       blob password_ct "AES-256-GCM"
                       blob nonce
                       int key_version }
  camera_streams { uuid id PK
                   uuid camera_id FK
                   text role "live|record"
                   text profile_token
                   text codec
                   int width
                   int height
                   int fps
                   int bitrate_kbps
                   text rtsp_url_enc
                   text mtx_path }
  ptz_presets { uuid id PK
                uuid camera_id FK
                text name
                text camera_preset_token
                int sort_order }
  recording_segments { uuid id PK
                       uuid camera_id FK
                       datetime start_at
                       datetime end_at
                       int bytes
                       text path
                       bool protected }
  events { uuid id PK
           uuid camera_id FK
           text type "motion|person|vehicle|tamper|unknown"
           datetime start_at
           datetime end_at "nullable"
           text source "onvif|local"
           text raw_topic
           real score "nullable" }
  snapshots { uuid id PK
              uuid event_id FK
              text path
              datetime taken_at }
  camera_status_log { uuid id PK
                      uuid camera_id FK
                      text status
                      text reason
                      datetime at }
  audit_log { uuid id PK
              uuid user_id FK "nullable(system)"
              text action
              text target_type
              text target_id
              text ip
              text detail "json"
              text prev_hash
              text hash
              datetime at }
  settings { text key PK
             text value "json"
             datetime updated_at }
```

**주요 인덱스·규칙**

- `recording_segments(camera_id, start_at)`, `events(camera_id, start_at)`, `audit_log(at)`, `sessions(expires_at)`.
- 삭제 정책: 카메라 삭제 시 세그먼트 파일은 즉시 삭제하지 않고 `orphan` 처리 후 보존 정책이 정리(실수 복구 여지).
- `camera_credentials`는 별도 테이블로 두고 ORM 기본 조회에서 제외(실수 노출 방지).
- `audit_log`는 트리거로 UPDATE/DELETE 금지, `hash = sha256(prev_hash || row)`.
- 마이그레이션: Alembic, 부팅 시 자동 적용, 적용 전 `.backup`.

---

## 8. UI/UX 개요

### 8.1 화면 목록

| 화면 | 핵심 요소 | 페르소나 |
|---|---|---|
| 초기 설정 마법사 | owner 계정 → 저장소 선택(SSD 감지) → 첫 카메라 검색 → 완료 | 전원 |
| 라이브 그리드 | 1/4/9 레이아웃, 채널 상태 배지, 더블클릭 전체화면, 드래그 정렬, 지연·코덱 표시 | 전원 |
| 채널 상세 | 큰 영상 + PTZ 패드(8방향+줌 슬라이더+프리셋 칩), 스냅샷, 제어권 표시, 이벤트 미니 타임라인 | P1, P3 |
| 타임라인/재생 | 24h 바(녹화 구간·이벤트 마커), 스크럽, 배속, 구간 선택 내보내기 | 전원 |
| 이벤트 목록 | 카드(스냅샷·시각·카메라·유형), 필터, 클릭→재생 | P1, P3 |
| 카메라 관리 | 목록·상태, 추가(검색/수동), 프로필 변경, 연결 테스트, 삭제 | owner |
| 시스템 | CPU/온도/디스크/네트워크, 컴포넌트 상태, 로그 보기, 업데이트 | owner |
| 사용자·감사 | 계정/역할, 감사 로그 검색 | owner |
| 설정 | 보존 정책, 뷰어 상한, 알림, 백업/복원, 안내판 문구 템플릿 | owner |

### 8.2 핵심 플로우 원칙

- **온보딩은 설정 파일 0개**: 자동 검색 실패 시 브랜드별 RTSP URL 템플릿(Hikvision `/Streaming/Channels/101`, Dahua `/cam/realmonitor?channel=1&subtype=0`, Reolink `/h264Preview_01_main`, Tapo `/stream1`)을 드롭다운으로 제공.
- **PTZ는 누르는 동안만**: `pointerdown/up`, 키보드 방향키, 모바일 가상 조이스틱. 뗌 이벤트 손실 대비 서버 워치독.
- **지연·모드 투명성**: 채널 우측 상단에 `WebRTC 0.4s` / `HLS 3s` / `제한 모드` 표시.
- **모바일**: 1열, 하단 PTZ 시트, 가로 모드 전체화면.
- **자동재생 정책**: 음소거 기본, 사용자 클릭 후 오디오 활성.

---

## 9. 테스트 설계

### 9.1 전략

- 피라미드: 단위(빠름, 다수) → 통합(카메라 시뮬레이터) → E2E(브라우저) → HIL(실기 Pi + 실카메라, 주 1회/릴리스 전).
- 목표 커버리지: hub-api 라인 80% 이상, 핵심 모듈(ONVIF 파서·보존 정책·인증·PTZ 워치독) 95% 이상.
- 모든 버그는 재현 테스트 먼저 작성.

### 9.2 테스트 환경 — 카메라 시뮬레이터 스택 (CI용, 하드웨어 불필요)

| 요소 | 구현 |
|---|---|
| RTSP 카메라 | `mediamtx`(sim 인스턴스) + `ffmpeg -re -stream_loop -1 -i fixtures/1080p_h264.mp4 -c copy -f rtsp rtsp://sim:8554/cam1` (H.264/H.265/타임코드 오버레이 샘플 3종) |
| ONVIF 장치 | 자체 제작 최소 SOAP 목(FastAPI): GetSystemDateAndTime, GetDeviceInformation, GetCapabilities, GetProfiles, GetStreamUri, GetSnapshotUri, ContinuousMove, Stop, GetPresets/SetPreset/GotoPreset, CreatePullPointSubscription, PullMessages. 호출 기록·지연/오류 주입 API 제공 |
| WS-Discovery | 목이 멀티캐스트 ProbeMatch 응답(Docker 네트워크 `--net=host` 또는 유니캐스트 폴백 테스트) |
| 실제 응답 픽스처 | Hikvision/Dahua/Reolink/Tapo 실장비에서 캡처한 GetProfiles/PullMessages XML(자격증명 제거) |

### 9.3 테스트 케이스 (레벨별)

| 레벨 | 대상 | 주요 케이스 |
|---|---|---|
| 단위 | ONVIF 응답 파서 | 브랜드별 GetProfiles 픽스처 → 프로필 점수화 결과, 누락 필드·비표준 네임스페이스 허용 |
| 단위 | 시계 오차 보정 | 카메라 시간 ±10분일 때 WS-UsernameToken Created 값 보정 |
| 단위 | PTZ | 속도 클램프 [-1,1], 워치독 2 s 후 Stop, 락 만료 |
| 단위 | 보존 정책 | 워터마크 85→80%, 카메라 우선순위, protected 세그먼트 제외, 보존 하한 경고 |
| 단위 | 인증 | argon2id 검증, 잠금, 세션 만료, RBAC 매트릭스(모든 엔드포인트 × 3역할) |
| 단위 | 암호화 | AES-GCM 라운드트립, 키 버전 회전, 로그 마스킹 정규식 |
| 단위 | SSRF 가드 | `rtsp://127.0.0.1`, `http://169.254.169.254`, `file://` 거부 |
| 계약 | OpenAPI | `schemathesis`로 스키마 준수·퍼징, problem+json 형식 |
| 통합 | 온보딩 | discover → probe → create → MediaMTX path 생성 확인 → `/v3/paths/list`에 ready |
| 통합 | WHEP | 헤드리스 `aiortc` 클라이언트로 offer→answer→RTP 수신 프레임 ≥ 1 within 3 s; 만료 JWT 401 |
| 통합 | 녹화 | 3분 가동 후 세그먼트 3개, 인덱스 일치, 플레이백 `/get` MP4 유효(ffprobe) |
| 통합 | 이벤트 | 목이 MotionAlarm true→false 발행 → events start/end, 스냅샷 파일 존재, WS 푸시 |
| 통합 | 장애 | 카메라 중단 → 60 s 내 offline, 복구 → online, 서킷브레이커 동작; MediaMTX kill → API 정상, 자동 재시작 후 path 재동기화 |
| E2E(Playwright) | 브라우저 | 로그인, 카메라 추가, 라이브 `video.currentTime` 증가·프레임 픽셀 변화, PTZ 버튼 누름/뗌 → 목에 Move/Stop 기록, 타임라인 클릭 재생, viewer 계정에서 PTZ 버튼 비활성 + API 403 |
| 성능 | API | k6: PTZ 20 rps 카메라당 p95 < 150 ms(목 지연 0) |
| 성능 | 미디어 | WHEP 동시 뷰어 1/3/6/10, 채널 4/8 — CPU·메모리·드롭 프레임·지연 측정 |
| 보안 | 자동화 | OWASP ZAP baseline, `pip-audit`, `npm audit`, `trivy image`, 시크릿 스캔(gitleaks) |
| 내구성(HIL) | 실기 | Pi 5/Pi 4 + 실카메라 2~4대 72 h 소크: 재시작 0, 녹화 갭 ≤ 0.1%, 온도 곡선. 전원 차단 10회 후 DB 무결성·세그먼트 재생 가능 |
| 카오스 | 실기/시뮬 | 디스크 100% 채움, 네트워크 플랩(30 s), NTP 점프(+1 h) 후 녹화 타임스탬프 일관성 |

### 9.4 수용 테스트 (릴리스 게이트)

- [ ] 신규 Pi에서 설치 스크립트 → 첫 라이브까지 5분 이내(스톱워치)
- [ ] 6개 브랜드 호환성 매트릭스에서 온보딩·라이브·PTZ(지원 시)·이벤트(지원 시) 통과
- [ ] 72 h 소크 KPI 충족
- [ ] 보안 자동화 스캔 High 0건
- [ ] 업데이트→헬스 실패→자동 롤백 시나리오 통과

### 9.5 CI 파이프라인

`lint/type(ruff, mypy, eslint, tsc)` → `unit` → `contract` → `integration(docker compose: sim-mediamtx + onvif-mock + hub-api)` → `e2e(Playwright, Chromium/WebKit)` → `security scan` → `arm64 이미지 빌드(QEMU/buildx)` → 태그 푸시 시 릴리스 노트·이미지 서명(cosign).

---

## 10. 배포·운영 (IaC · 관측성)

### 10.1 OS 이미지·프로비저닝

- 기반: **Raspberry Pi OS Lite 64-bit(Debian 12 기반)**. Raspberry Pi Imager로 SSH 키·호스트명 사전 설정.
- 설치: `curl -fsSL https://.../install.sh | bash` → 로컬에서 Ansible 플레이북 실행(pull 모드) 또는 관리 PC에서 `ansible-playbook -i pi, site.yml`.
- 첫 부팅 후 `https://picamhub.local` 접속 → 설정 마법사.

### 10.2 저장소 레이아웃

```
/                      SD 카드: OS, 컨테이너 런타임(docker data-root는 SSD)
/data                  SSD(ext4, noatime, LUKS 옵션)
  /recordings/<path>/  fMP4 세그먼트 (mediamtx 소유)
  /snapshots/
  /db/picamhub.sqlite  (+ WAL)  ← 별도 서브볼륨/예약공간으로 디스크 풀에서 보호
  /backups/            일일 sqlite .backup + 설정 export (7일 보관)
  /docker/             docker data-root
/etc/picamhub/         env, keys/master.key(0600), Caddyfile, mediamtx.yml, compose.yml
/var/log/journal       journald(영속, 500 MB 상한), log2ram 옵션
```

### 10.3 docker-compose 스케치

```yaml
services:
  caddy:
    image: caddy:2
    ports: ["443:443", "80:80"]
    volumes: ["/etc/picamhub/Caddyfile:/etc/caddy/Caddyfile:ro", "caddy_data:/data", "web_dist:/srv/web:ro"]
    depends_on: [hub-api, mediamtx]
    restart: unless-stopped
  mediamtx:
    image: bluenviron/mediamtx:1.x.y          # 핀 고정 [미확정: M0에서 확정]
    network_mode: host                        # WebRTC ICE 후보에 호스트 IP 노출, UDP 8189
    volumes: ["/etc/picamhub/mediamtx.yml:/mediamtx.yml:ro", "/data/recordings:/data/recordings"]
    user: "1001:1001"
    read_only: true
    restart: unless-stopped
  hub-api:
    image: ghcr.io/picamhub/hub-api:0.1.0
    network_mode: host                        # WS-Discovery 멀티캐스트 필요
    env_file: /etc/picamhub/hub-api.env
    volumes: ["/data/db:/data/db", "/data/snapshots:/data/snapshots", "/data/recordings:/data/recordings:ro", "/etc/picamhub/keys:/keys:ro"]
    user: "1002:1002"
    cap_drop: [ALL]
    restart: unless-stopped
    healthcheck: { test: ["CMD", "curl", "-fsS", "http://127.0.0.1:8000/api/v1/system/health"], interval: 30s, timeout: 5s, retries: 3 }
  node-exporter:
    image: prom/node-exporter:v1.x
    network_mode: host
    pid: host
    volumes: ["/:/host:ro,rslave"]
    command: ["--path.rootfs=/host", "--web.listen-address=127.0.0.1:9100"]
    restart: unless-stopped
  # profiles: [monitoring] → prometheus, grafana (옵션)
volumes: { caddy_data: {}, web_dist: {} }
```

`network_mode: host`인 hub-api·mediamtx는 내부 포트를 127.0.0.1에 바인딩하고 Caddy만 외부 노출. 보존 정책 삭제는 hub-api가 recordings를 읽기 전용으로 마운트하므로 **삭제는 MediaMTX `recordDeleteAfter` + hub-api가 Control API/전용 삭제 사이드카(쓰기 권한 최소)로 수행** `[미확정: 삭제 경로 최종 결정은 M2]`.

### 10.4 Ansible 롤 구조

```
site.yml
roles/
  common/       hostname, timezone(Asia/Seoul), chrony, unattended-upgrades(security), sshd 강화, fail2ban(sshd)
  storage/      SSD 감지·파티션·ext4·fstab(noatime), /data 트리·소유권, (옵션) LUKS, log2ram
  docker/       docker-ce arm64, data-root=/data/docker, 로그 드라이버 json-file(10m×3)
  picamhub/     /etc/picamhub 렌더링(Caddyfile, mediamtx.yml, env, compose), master.key 생성(없을 때만), compose up, systemd `picamhub.service`(WantedBy=multi-user, Restart=always)
  firewall/     nftables: in 443/22(LAN·VPN 대역만), 내부 포트 127.0.0.1 한정, (옵션) 카메라 VLAN 포워딩 차단
  monitoring/   node_exporter(기본), prometheus+grafana+alert 룰(옵션 프로파일)
  tailscale/    (옵션) 설치·`tailscale up --ssh` 안내
  update/       `picamhub-update` 스크립트(이미지 태그 교체 → health 3회 확인 → 실패 시 이전 태그 복귀), `picamhub-backup` 타이머
```

### 10.5 관측성

**메트릭 (Prometheus 형식)**

| 소스 | 메트릭 | 알림 규칙 |
|---|---|---|
| MediaMTX `:9998` | `paths`(ready, bytesReceived), `webrtc_sessions`, `hls_muxers`, `recordings` | path not ready 60 s → `CameraOffline` |
| hub-api `/metrics` | `hub_camera_online{camera}`, `hub_onvif_request_seconds`, `hub_onvif_errors_total{op}`, `hub_ptz_commands_total`, `hub_events_total{type}`, `hub_recording_gap_seconds_total`, `hub_disk_usage_ratio{mount}`, `hub_retention_deleted_bytes_total`, `hub_ws_clients` | 디스크 > 0.9 → `DiskAlmostFull`; 녹화 갭 > 300 s → `RecordingGap`; ONVIF 오류율 > 20%/5m → `CameraFlapping` |
| node_exporter | CPU, 메모리, `node_hwmon_temp_celsius`(cpu_thermal), 디스크 IO/에러, 네트워크 | 온도 > 80 °C 5 m → `Throttling`; `node_filesystem_readonly` → `FsReadOnly`(SD 손상 징후) |
| Caddy | 요청 수/지연/상태코드 | 5xx > 5%/5m |

**로그**: 모든 컨테이너 JSON 구조화(`ts, level, component, trace_id, camera_id, msg`), journald 영속 500 MB, 시크릿 마스킹. 옵션 프로파일에서 promtail→Loki.

**헬스**: `GET /api/v1/system/health` — `db`(SELECT 1), `mediamtx`(Control API `/v3/paths/list`), `disk`(워터마크), `clock`(chrony 동기), `cameras`(온라인 수/전체). systemd `WatchdogSec=60` + `sd_notify`.

**UI 알림 배너**: `system.alert` WebSocket으로 즉시 표시. 외부 알림(M3): 웹훅/이메일/Telegram/ntfy — Alertmanager 없이 hub-api가 규칙 내장(간소화).

### 10.6 업데이트·백업·복구

- 업데이트: 주간 확인(옵트인), `picamhub-update <version>`: 이미지 pull → DB `.backup` → compose up → health 3/3 → 실패 시 롤백. 마이그레이션은 전방 호환(다운 마이그레이션 없음, 롤백은 백업 복원).
- 백업: 매일 03:00 `sqlite3 .backup` + `/etc/picamhub`(키 제외) → `/data/backups`, 7일 보관. UI에서 암호화 export(사용자 패스프레이즈, 키 포함 옵션 경고).
- 복구: 새 Pi에 설치 → 설정 마법사 "백업에서 복원" → 카메라·사용자·프리셋 복원(녹화는 SSD 이동으로 승계).

### 10.7 운영 런북(요약)

| 증상 | 1차 확인 | 조치 |
|---|---|---|
| 카메라 offline | `/system/health`, ping, `ffprobe rtsp://` | 자격증명/IP 변경 확인, 카메라 재부팅, ONVIF 포트(80/8080/2020) 확인 |
| 라이브는 되는데 녹화 없음 | 디스크, MediaMTX `record` 플래그, 세그먼트 디렉토리 권한 | 워터마크 확인, 권한 복구 `chown 1001:1001` |
| 영상 끊김·프레임 드롭 | CPU 온도, 뷰어 수, 카메라 비트레이트 | 쿨러, 뷰어 상한, 서브스트림 비트레이트 하향 |
| WebRTC 실패(HLS만 됨) | 브라우저 콘솔 ICE 상태, UDP 8189, VPN 설정 | 방화벽 UDP 허용, `webrtcAdditionalHosts`에 VPN IP 추가 |
| SD 읽기전용 전환 | `dmesg | grep mmc` | SD 교체, 백업 복원(OS 재설치 15분) |
| 시각 오류(녹화 시각 틀림) | `chronyc tracking` | NTP 서버 도달성, 카메라 시각 동기화 옵션 |

---

## 11. 로드맵·마일스톤 [가정 A7]

| 마일스톤 | 기간 | 산출물 | 완료 기준 |
|---|---|---|---|
| **M0 PoC** | 2주 | MediaMTX+FastAPI 골격, 카메라 1대 온보딩·WHEP 라이브·PTZ, Pi 5/Pi 4 CPU 측정 | 라이브 지연 ≤ 1 s, PTZ ≤ 300 ms 실측. MediaMTX 버전 핀 확정, 성능 예산(§4.6) 1차 검증 |
| **M1 MVP-Live** | 4주 | 온보딩 마법사, 그리드, PTZ/프리셋, 인증/RBAC, 상태 대시보드, 설치 스크립트 | US-1~5, 9 수용. 3개 브랜드 호환 |
| **M2 MVP-Record** | 3주 | 녹화·타임라인·내보내기·보존 정책·ONVIF 이벤트·감사 로그 | US-6~8, 10 수용. 72 h 소크 통과 |
| **M3 Ops** | 3주 | 외부 알림, 업데이트/롤백, 백업/복원, VPN 가이드, 모니터링 프로파일, API 토큰, 로컬 모션 폴백 | 릴리스 게이트(§9.4) 전부 통과, 6개 브랜드 호환 |
| **M4+ Later** | — | AI 검출(Hailo), 클라우드 릴레이, 네이티브 앱, CSI 카메라, 다중 사이트 | 별도 기획 |

---

## 12. 리스크 & 오픈 이슈

### 12.1 리스크

| 리스크 | 확률/영향 | 완화 |
|---|---|---|
| ONVIF 구현 편차(비표준 XML, 인증 방식, PTZ 축 반전) | 높음/높음 | 픽스처 기반 파서, 브랜드 프로파일(quirks) 테이블, 수동 RTSP 폴백, 호환성 매트릭스 공개 |
| H.265 전용 저가 카메라 증가 | 중간/중간 | 제한 모드 + 카메라 설정 가이드. Pi 4는 옵션으로 단일 채널 HW 트랜스코딩 `[미확정]` |
| WebRTC가 VPN/이중 NAT에서 실패 | 중간/중간 | `webrtcAdditionalHosts`, HLS 폴백, 진단 페이지 |
| Pi 발열·SD 마모로 장기 신뢰성 저하 | 중간/높음 | 액티브 쿨러·SSD 권장을 설치 마법사에서 강제 수준으로 안내, 온도 알림 |
| MediaMTX 업스트림 변경(설정 키·API) | 중간/중간 | 버전 핀, 통합 테스트가 실제 바이너리로 실행 |
| 브라우저 정책 변경(자동재생, H.264 지원) | 낮음/중간 | 음소거 기본, HLS 폴백 |
| 법적 이슈(녹화 보존·안내판) | 낮음/높음 | 기본값 30일, 안내 템플릿, 운영자 책임 명시 |

### 12.2 오픈 이슈 [미확정]

1. 사용자군이 B2C 셀프호스팅인지 B2B 관제인지(A1) — 답에 따라 멀티사이트·RBAC 깊이 변경.
2. 클라우드 릴레이 필요 여부(A4/A5) — VPN 온보딩 이탈률을 M3 이후 측정해 판단.
3. AI 검출 우선순위(FR-20) — Pi 5 AI Kit 보유 사용자 비율 확인.
4. 하드웨어 번들(케이스·쿨러·SSD 포함 키트) 판매 여부 — 지원 범위와 KPI 측정에 영향.
5. MediaMTX 버전 핀과 삭제 경로 설계(§6.5, §10.3) — M0/M2에서 확정.
6. 라즈베리파이 CSI 카메라 지원(A2) — 부록 C 방식 채택 여부.

---

## 부록

### A. 용어

| 용어 | 뜻 |
|---|---|
| ONVIF | IP 카메라 상호운용 표준(SOAP/WS). Profile S=스트리밍·PTZ, T=고급 스트리밍·분석 |
| WS-Discovery | 멀티캐스트 기반 장치 검색 프로토콜 |
| RTSP/RTP | 실시간 스트리밍 제어/전송 프로토콜 |
| WebRTC / WHEP | 브라우저 실시간 미디어 / HTTP 기반 WebRTC 수신 시그널링 표준 |
| fMP4 | fragmented MP4, 세그먼트 녹화·LL-HLS에 사용 |
| PTZ | Pan/Tilt/Zoom |
| 패스스루 | 재인코딩 없이 컨테이너만 바꿔 전달 |
| PullPoint | ONVIF 이벤트 장기 폴링 구독 방식 |

### B. 카메라 호환성 매트릭스(초안, 검증 대상)

| 브랜드 | ONVIF | PTZ | 이벤트 토픽 | RTSP 템플릿 | 비고 |
|---|---|---|---|---|---|
| Hikvision | S/T | ✔ | `VideoSource/MotionAlarm`, `RuleEngine/*` | `/Streaming/Channels/10{1,2}` | ONVIF 사용자 별도 생성 필요 |
| Dahua/Amcrest | S/T | ✔ | `VideoSource/MotionAlarm`, `RuleEngine/CellMotionDetector/Motion` | `/cam/realmonitor?channel=1&subtype={0,1}` | |
| Reolink | S | 일부 모델 | `RuleEngine/MyRuleDetector/{PeopleDetect,VehicleDetect,DogCatDetect,Visitor}` | `/h264Preview_01_{main,sub}` | H.265 메인이 기본인 모델 주의 |
| TP-Link Tapo | S | 일부(C2xx) | `RuleEngine/CellMotionDetector/Motion`, `MyRuleDetector/*` | `/stream{1,2}` | 앱에서 카메라 계정 생성 필요, ONVIF 포트 2020 |
| Uniview | S/T | ✔ | `VideoSource/MotionAlarm` | `/media/video{1,2}` | |
| 기타(무브랜드) | 편차 큼 | ? | ? | 수동 입력 | quirks 테이블로 대응 |

### C. (Later) 라즈베리파이 CSI 카메라 모듈 지원 방안

`rpicam-vid --codec h264 --inline -o - | ffmpeg -f h264 -i - -c copy -f rtsp rtsp://127.0.0.1:8554/cam_csi` 형태로 MediaMTX에 publish하면 이후 경로는 IP 카메라와 동일. Pi 5는 CSI 인코딩도 SW이므로 1080p30 1채널 정도로 제한. PTZ는 서보 HAT(PCA9685) 드라이버를 PTZ 어댑터 인터페이스로 추상화.

### D. 참고

- Raspberry Pi 5 코덱: HEVC HW 디코드만, H.264 디코드·모든 인코드는 SW — Raspberry Pi 포럼 "RPi5 Codec confusion" 및 "Lack of Hardware H.264 Decoding" 스레드, Jeff Geerling "Can the Raspberry Pi 5 handle 4K?", Frigate Discussion #18431.
- MediaMTX: 공식 문서(mediamtx.org) — WHIP/WHEP, fMP4/MPEG-TS 녹화 및 플레이백 서버(세그먼트 재먹싱→MP4), Prometheus 메트릭.
- ONVIF Profile S/T 규격, ONVIF Core Specification(WS-Discovery, 이벤트 PullPoint).
- RFC 2326/7826(RTSP), RFC 9457(Problem Details), WHEP(IETF draft-ietf-wish-whep).
- 개인정보보호법 제25조(고정형 영상정보처리기기의 설치·운영 제한).

### E. 다음 단계 — 구현 착수용 SPEC 입력 요약

```
제품: PiCam Hub v0.1
스택: MediaMTX(핀) · Python 3.12/FastAPI/SQLite · React+TS/Vite PWA · Caddy · Docker Compose · Ansible
M0 목표(2주): 카메라 1대 온보딩 → WHEP 라이브(≤1s) → PTZ(≤300ms) → Pi 5/Pi 4 CPU 실측 → 성능 예산 검증
첫 작업 순서:
 1. 리포 골격(monorepo: apps/hub-api, apps/hub-web, deploy/ansible, deploy/compose, tools/sim) → 검증: CI lint/test 통과
 2. 카메라 시뮬레이터(sim-mediamtx + ffmpeg loop + onvif-mock) → 검증: docker compose up 후 rtsp/onvif 응답
 3. hub-api: /auth/setup,/auth/login, /cameras/discover,/probe,/cameras POST → MediaMTX path 생성 → 검증: 통합 테스트
 4. 스트림 JWT + JWKS + MediaMTX jwt 인증 → 검증: aiortc WHEP 클라이언트 프레임 수신
 5. hub-web: 로그인·카메라 추가·라이브 1채널·PTZ 패드 → 검증: Playwright E2E
 6. 실기 HIL: Pi 5/Pi 4 + 실카메라 2대, CPU/온도/지연 측정 → 검증: §4.6 표 갱신
```
