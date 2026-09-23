# RECON — PiCam Watch (라즈베리파이 IP 카메라 제어·실시간 모니터링)

조사일: 2026-09-03 · 방법: WebSearch 8회 + WebFetch 4회 (GitHub 저장소·법령 페이지 직접 확인) · 스킬: `ecc:research-ops`(근거 유형 분리), `ecc:search-first`(사라/써라 판정)
표기: **[사실]** 출처 직접 확인 · **[2차]** 블로그·요약 등 2차 출처(교차 확인 필요) · **[추론]** 사실에서 도출

## 도메인 업무 흐름 — 이 영역이 실제로 어떻게 돌아가는가

셀프호스팅 NVR(Frigate·ZoneMinder·MotionEye·Shinobi)의 공통 워크플로 [사실 — 각 프로젝트 README·비교 기사 교차]:

1. **카메라 등록** — ONVIF 디스커버리(WS-Discovery) 또는 RTSP URL 수동 입력. ONVIF Profile S가 스트리밍·PTZ의 사실상 표준 ([ONVIF PTZ Service Spec v26.06](https://www.onvif.org/specs/srv/ptz/ONVIF-PTZ-Service-Spec.pdf), 확인 2026-09-03).
2. **스트림 중계** — 카메라 RTSP(H.264/H.265)를 게이트웨이가 받아 브라우저용(WebRTC/MSE/HLS)으로 **재패키징(트랜스코딩 없이)**. go2rtc·MediaMTX가 이 계층을 담당 ([go2rtc](https://github.com/AlexxIT/go2rtc), [MediaMTX](https://github.com/bluenviron/mediamtx), 확인 2026-09-03).
3. **라이브 뷰** — 브라우저 그리드/단일 뷰. 저지연은 WebRTC, 호환성 폴백은 MSE·HLS.
4. **제어(PTZ)** — ONVIF PTZ 서비스: `ContinuousMove`(timeout으로 자동 정지, 축별 0 전송 시 정지)·`GotoPreset`(non-blocking, 다른 move로 인터럽트 가능)·`SetPreset` ([ONVIF PTZ Spec](https://www.onvif.org/specs/srv/ptz/ONVIF-PTZ-Service-Spec.pdf) · [python-onvif-zeep continuous_move.py](https://github.com/FalkTannhaeuser/python-onvif-zeep/blob/zeep/examples/continuous_move.py), 확인 2026-09-03).
5. **이벤트·녹화** — 모션/객체 감지 → 클립 저장 → 알림. 상시 녹화는 저장 용량·SD 마모 문제로 별도 스토리지(NVMe/NAS) 전제 [2차: [helgeklein Pi5+Frigate](https://helgeklein.com/blog/frigate-nvr-with-object-detection-on-raspberry-pi-5-coral-tpu/)].
6. **보존·파기** — 보관기간 만료 클립 자동 삭제(법적 요구, 아래 규제 절).

## 이해관계자 — 누가 쓰고, 누가 돈을 내는가

| 역할 | 누구 | 하는 일 / 관심사 | 근거 |
|---|---|---|---|
| 운영자(Owner) | 집주인·소상공인·소규모 시설 관리자 | 카메라 등록, PTZ, 이벤트 확인, 보존 정책 설정. **비용 지불자**(하드웨어 1회 구매, 구독 없음이 셀프호스팅 선택 이유) | [2차: raspberrytips 비교](https://raspberrytips.com/best-raspberry-pi-security-camera/) — "구독 없는 로컬 제어"가 공통 동기 |
| 시청자(Viewer) | 가족·직원 | 라이브 뷰·클립 조회만, 설정·PTZ 불가 | [추론] 권한 분리 필요 — 법 제25조⑤ 임의조작 금지와 연결 |
| 정보주체 | 촬영되는 사람(방문객·직원·행인) | 안내판·목적 외 촬영 금지·보관기간 | 개인정보보호법 제25조 (아래) |
| 카메라 벤더 | ONVIF 준수 카메라 제조사 | 프로파일·펌웨어 호환성 | ONVIF 사양 |

## 규제·표준 — 반드시 준수해야 하는 것

**[사실] 개인정보보호법 제25조(고정형 영상정보처리기기) — 2023.3.14 개정** ([법령정보센터 제25조](https://www.law.go.kr/LSW//lsLawLinkInfo.do?lsJoLnkSeq=900079397&lsId=011357&chrClsCd=010202&print=print) · [찾기쉬운 생활법령](https://www.easylaw.go.kr/CSP/CnpClsMain.laf?csmSeq=1257&ccfNo=2&cciNo=3&cnpClsNo=3), 확인 2026-09-03):

| 조항 | 요구 | 이 서비스에 미치는 영향 |
|---|---|---|
| 제25조① | 공개된 장소 설치는 시설 안전·관리, 범죄 예방 등 열거 사유에 한함 | 설치 목적을 시스템에 **기록**(카메라 메타데이터) |
| 제25조② | 목욕실·화장실·탈의실 등 설치 금지 | 범위 밖(설치자 책임) — 문서에 고지만 |
| 제25조④·시행령 제24조 | 안내판: 설치 목적·장소, 촬영 범위·시간, 관리책임자 성명·연락처 | 안내판 문구 생성 기능은 P2 후보. 관리책임자 필드 필요 |
| **제25조⑤** | **설치 목적과 다른 목적으로 임의 조작하거나 다른 곳을 비추는 행위 금지, 녹음 기능 사용 금지.** 위반 시 3년 이하 징역/3천만원 이하 벌금(제72조①) | **PTZ 기능의 핵심 제약** — 조작 감사로그 필수, 역할 분리(Viewer는 PTZ 불가), 오디오 기본 OFF |
| 제25조⑦·시행령 제25조 | 운영·관리 방침에 촬영시간·**보관기간**·보관장소·처리방법 명시 | 보관기간을 설정값으로 강제, 만료 자동 파기 |
| 보관기간 기본값 | 별도 정함 없으면 **30일** 후 복구 불가 방식 파기 — 개인정보위 [영상정보처리기기 가이드라인](https://www.privacy.go.kr/cmm/fms/FileDown.do?atchFileId=FILE_000000000814419&fileSn=0) 기준 [2차: 검색 요약, 가이드라인 원문 재확인 권고] | 기본 보존 30일, 상한 설정 가능 |

- **가정 내 사적 이용**: 법 제25조는 "공개된 장소"의 개인정보처리자를 규율. 자택 내부만 촬영하는 개인 사용은 규제 강도가 낮으나 **명시적 예외 조문은 확인되지 않음**(easylaw 페이지 기준) → *가능성*으로 분류, 사업장 설치 시나리오를 기본 전제로 설계한다 (보수적).
- **이동형 기기(제25조의2)**: 2023 개정 신설. 본 서비스는 고정형만 대상 → 해당없음(근거: 카메라가 고정 설치).
- **기술 표준**: ONVIF Profile S(스트리밍·PTZ), RTSP(RFC 2326/7826), WebRTC(WHEP 시그널링). 규제 아닌 상호운용 표준.

## 유사 솔루션 — 실제 기능 범위 (확인 2026-09-03)

| 솔루션 | 라이선스·규모 | 실제 기능 | 라즈베리파이 적합성 | 이 프로젝트 관점 |
|---|---|---|---|---|
| [Frigate](https://github.com/blakeblackshear/frigate) | MIT, 35.6k★ [사실] | NVR + 객체감지(AI 가속기 권장), go2rtc 내장, ONVIF PTZ·오토트래킹 | Pi 5 8GB + NVMe + Hailo AI HAT(≈$130) 권장 [2차: [terminalbytes](https://terminalbytes.com/best-hardware-for-frigate-nvr-2026/), [helgeklein](https://helgeklein.com/blog/frigate-nvr-with-object-detection-on-raspberry-pi-5-coral-tpu/)]. Coral 드라이버 2026-04 아카이브 [2차, 공식 확인 필요] | **가장 강한 "사서 쓰기" 후보**. 단, 객체감지가 시드 요구 밖이고 가속기 추가 비용 → 범위 초과. 설계 참조 |
| [go2rtc](https://github.com/AlexxIT/go2rtc) | MIT, 14.1k★ [사실] | RTSP/ONVIF 입력 → WebRTC/MSE/HLS/RTSP 출력, 트랜스코딩 없음, ARM 바이너리·Docker(arm64/v7/v6) [사실] | Pi 4에서 약 10 스트림 [2차: [korben](https://korben.info/en/go2rtc-swiss-army-knife-video-streaming.html)] | **스트림 계층 채택**. PTZ 제어는 미포함 → 별도 ONVIF 클라이언트 필요 |
| [MediaMTX](https://github.com/bluenviron/mediamtx) | MIT, 20k★ [사실] | RTSP proxy·WebRTC(WHEP)·SRT·LL-HLS·fMP4 녹화, 제로 의존성 [사실]; v1.15.4 (2025-11) [2차] | Linux ARM 지원 [사실] | go2rtc 대안. 녹화 내장이 장점, ONVIF 디스커버리 없음 |
| [MotionEye](https://raspberrytips.com/best-raspberry-pi-security-camera/) | GPL (motion 기반) [2차] | motion 데몬 웹 프론트, 모션 감지·녹화 | Pi Zero 2 W도 가능. MotionEyeOS는 2021 이후 미유지 [2차: [raspberry.tips](https://raspberry.tips/en/raspberrypi-tutorials/raspberry-pi-security-camera)] | 저사양 대안. PTZ·WebRTC 없음 |
| [ZoneMinder](https://www.pistack.xyz/posts/self-hosted-video-surveillance-nvr-frigate-zoneminder-motioneye/) / Shinobi | GPL / 자체 라이선스 [2차] | 풀 NVR, 다중 카메라, PTZ(ZM) | 무겁고 설정 복잡 [2차] | 기능 참조만 |

`ecc:search-first` 판정: **Extend/Compose** — go2rtc(스트림) + python-onvif-zeep(PTZ) + 얇은 제어 API·UI. "Adopt as-is"(Frigate)는 객체감지·가속기가 시드 범위 밖이라 보류하되, 향후 확장 시 Frigate 마이그레이션 경로를 열어둔다.

## 스택 후보 — 근거·트레이드오프 (fit = 사용자 제약 매칭, 인기 ≠ 적합)

| 계층 | 후보 | 근거 | 트레이드오프 | fit |
|---|---|---|---|---|
| 엣지 HW | **Raspberry Pi 5 (8GB) + NVMe SSD** | Pi 5는 H.265 HW 디코드만, **H.264 HW 인코더 없음·H.264 디코드도 SW** [사실: [Pi 포럼](https://forums.raspberrypi.com/viewtopic.php?t=357999), [HN](https://brianlovin.com/hn/38068801)] → 트랜스코딩 회피가 설계 원칙 | Pi 4도 가능하나 NVMe 없음(USB SSD) | 높음 — 트랜스코딩 안 하면 CPU 여유 |
| 스트림 게이트웨이 | **go2rtc** | ONVIF 입력·WebRTC 출력·트랜스코딩 없음·ARM 바이너리 [사실] | 기본 웹UI(1984 포트) 무인증 [2차: [webrtc.link](https://webrtc.link/en/articles/go2rtc-ultimate-streaming-solution/)] → 외부 노출 금지, 제어 API 뒤에 격리 | 높음 |
| 스트림 대안 | MediaMTX | 녹화 내장, WHEP | ONVIF 디스커버리 없음 | 중간 |
| PTZ 제어 | **python-onvif-zeep** | ContinuousMove/GotoPreset 예제 존재 [사실] | zeep/SOAP 의존, 카메라 벤더별 편차 | 높음 |
| 제어 API | **Python FastAPI** | onvif-zeep와 동일 런타임, 비동기 | Node 대비 Pi 메모리 사용 유사 | 높음 |
| 저장 | **SQLite(WAL) — 메타/이벤트**, 클립은 NVMe 파일시스템 | 단일 노드·저볼륨, 운영 단순 | 다중 Pi 집계 시 중앙 DB 필요(범위 밖) | 높음 |
| 프론트 | **React + Tailwind (SPA, 정적 번들)** + go2rtc `video-rtc` WebRTC 플레이어 | 정적 파일로 Pi에서 서빙, 외부 CDN 0 | — | 높음 |
| 원격 접근 | **Tailscale(사설)** | NAT 뒤 Pi 접근, 포트포워딩 불필요, 이중 NAT 시 DERP 릴레이 [사실: [SunFounder](https://www.sunfounder.com/blogs/news/how-to-access-raspberry-pi-remotely-with-tailscale-no-port-forwarding)] | 공개 서비스에는 부적합(reverse proxy 필요) [2차: [wirelog](https://www.wirelog.net/posts/2025-02-15-api-server-on-raspberry-pi/)]; WebRTC는 hole-punching 실패 시 TURN 필요 [사실: [RaspberryPi-WebRTC](https://github.com/TzuHuanTai/RaspberryPi-WebRTC)] | 높음(가족·직원 소수) |
| 원격 대안 | Cloudflare Tunnel | 공개 HTTPS | UDP/WebRTC 미디어 경로 미지원 → MSE/HLS 폴백 강제 | 낮음(지연 증가) |
| OS 강화 | Raspberry Pi OS Lite 64-bit + overlayfs read-only + log2ram | Mender Pi 체크리스트·dzombak SD 마모 대책 (`blindspot-checklists.md` 출처) | 쓰기 구역 설계 필요 | 높음 |

## 미확인 (A2로 이월)
- 카메라 대수·설치 장소(가정/사업장)·시청 경로(LAN만/외부) — 사용자 환경 의존.
- 카메라 ONVIF 준수 여부(구형 카메라는 벤더 전용 PTZ) — 보유 장비 의존.
- 상시 녹화 필요 여부 — 저장 매체 구매 결정에 직결.
