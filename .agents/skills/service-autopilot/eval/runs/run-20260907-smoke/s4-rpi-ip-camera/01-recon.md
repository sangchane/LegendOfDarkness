# RECON — PiCam Hub (라즈베리파이 IP 카메라 허브)
버전: v1.0 · 조사일 2026-09-07 (초안 sonnet 조사원 26회 + 메인 검증 WebFetch 4회 = 30회) · fit 판정은 말미 "fit 판정" 절
검색 횟수: 26회 (WebSearch 20회 + WebFetch 6회, 목표 15회 초과 — 사유: GitHub 통계·국내법 조문·IETF RFC 번호 교차확인, 실무자 커뮤니티 인용 출처 확보에 추가 소요)

## 도메인 업무 흐름 (출처)

**표준 흐름**: 카메라 발견 → 스트림 등록 → 실시간 뷰 → PTZ 제어 → 이벤트(모션) → 녹화·보존 → 원격 접근 → 알림

| 단계 | 표준 프로토콜 | 비고 |
|---|---|---|
| 카메라 발견 | ONVIF WS-Discovery | ONVIF Profile S/T 공통 |
| 스트림 등록 | RTSP (main/sub 프로파일) | RFC 2326(1998, RTSP 1.0), RFC 7826(2016, RTSP 2.0, 1.0을 obsolete) |
| 코덱 | H.264 / H.265(HEVC) | — |
| 실시간 뷰(브라우저 전달) | WebRTC / HLS / MSE | WebRTC 개요는 RFC 8825(2021-01, IETF Standards Track, 저자 Harald Alvestrand/Google) |
| PTZ 제어 | ONVIF PTZ 서비스 | 아래 참조 |
| 이벤트(모션) | ONVIF Analytics/Events | — |

**사실**: ONVIF Profile S는 스트리밍 설정·재생 제어·PTZ 명령 전송을 다루지만 PTZ는 조건부(선택) 기능이다. Profile T는 기기 쪽 필수 기능에 OSD·메타데이터 스트리밍을, **클라이언트 쪽 필수 기능에 PTZ 제어를 명시**해 PTZ가 사실상 필수가 된다. Profile T는 그 외 HTTPS 스트리밍, PTZ 설정, 모션 영역 설정, 디지털 입출력, 양방향 오디오도 포함한다.
출처: https://www.onvif.org/profiles/profile-t/ , https://www.onvif.org/profiles/profile-s/ , https://www.onvif.org/wp-content/uploads/2018/09/ONVIF_Profile_T_Specification_v1-0.pdf (확인일 2026-09-07)

**사실**: ONVIF는 Profile S 신규 적합성 인증 접수를 2027-03-31부로 종료하며 Profile T를 후속으로 권고한다.
출처: https://www.onvif.org/?post_type=pressrelease&p=8621 (확인일 2026-09-07)

**사실**: RTSP는 RFC 2326(1998)로 표준화됐고, RTSP 2.0은 RFC 7826(2016)이 정의하며 RFC 2326을 obsolete 시킨다.
출처: https://datatracker.ietf.org/doc/html/rfc7826 , https://datatracker.ietf.org/doc/rfc2326/ (확인일 2026-09-07)

**사실**: WebRTC의 "Overview: Real-Time Protocols for Browser-Based Applications"는 RFC 8825(2021-01 발행, IETF Standards Track)이며, 자체 프로토콜을 정의하지 않고 WebRTC 준수를 위해 따라야 할 다른 스펙들을 지정하는 applicability statement다.
출처: https://datatracker.ietf.org/doc/html/rfc8825 (확인일 2026-09-07)

## 이해관계자 (출처)

**추론**: 시드가 "홈/소규모 사업장 자가 운영"으로 추정한 것과 일치 — 사용자=운영자=구매자가 동일인인 셀프호스팅 구조가 Frigate/go2rtc류 오픈소스 커뮤니티의 기본 전제다. 설치업자·관리자 역할은 시드에서 확인되지 않음(미확인).

**실무자 반복 불만 3건 (Frigate 커뮤니티 기준)**:
1. **오탐(false positive) 대응 피로** — Frigate+ 모델을 적용해도 오탐이 다수 발생한다는 반복 보고.
   출처: https://github.com/blakeblackshear/frigate/discussions/11296 , https://github.com/blakeblackshear/frigate/discussions/20391 (확인일 2026-09-07)
2. **SD카드/스토리지 마모 우려** — 사용자들이 tmpfs(메모리 임시 볼륨)를 마운트해 "SSD/SD카드 마모 감소"를 명시적으로 언급하며 설정에 반영. 라즈베리파이 환경에서 SD카드 수명이 실제 운영 이슈로 다뤄진다.
   출처: https://github.com/blakeblackshear/frigate/discussions/13920 (확인일 2026-09-07)
3. **대시보드 카메라 지연(lag)** — Home Assistant 커뮤니티에서 대시보드에 카메라 스트림을 붙였을 때 지연이 누적되는 문제가 별도 스레드로 제기됨.
   출처: https://community.home-assistant.io/t/camera-lag-on-dashboard/925009 (확인일 2026-09-07)

**미확인**: r/homeassistant, r/frigate_nvr 서브레딧 개별 게시물은 WebSearch의 site: 연산자 미지원으로 직접 인용하지 못함(위 GitHub Discussions/HA 커뮤니티로 대체).

## 규제·표준 (출처; 해당 없음도 근거)

**사실 — 개인정보보호법 제25조(고정형 영상정보처리기기의 설치·운영 제한)**: 공개된 장소에는 원칙적으로 고정형 영상정보처리기기를 설치·운영할 수 없고, 법령 근거·범죄예방/수사·시설안전관리·화재예방·교통단속·교통정보수집 등 예외 사유가 있을 때만 허용된다.
출처: https://www.law.go.kr/LSW//lsLawLinkInfo.do?lsJoLnkSeq=900079397&lsId=011357&chrClsCd=010202 , https://casenote.kr/법령/개인정보_보호법/제25조 (확인일 2026-09-07)

**사실 — 제25조의2(이동형 영상정보처리기기의 운영 제한)**: 업무 목적으로 이동형 영상정보처리기기를 운영하는 자는 공개된 장소에서 개인정보에 해당하는 촬영을 원칙적으로 금지하되 예외 사유가 있으며, **촬영 시 촬영 사실을 빛·소리·안내판 등으로 정보주체가 쉽게 인식하도록 표시·고지해야 한다.**
출처: https://casenote.kr/법령/개인정보_보호법/제25조2 , https://www.law.go.kr (국가법령정보센터, 조문 검색) (확인일 2026-09-07)

**사실 — 표준 개인정보 보호지침(개인정보보호위원회고시)**: 영상정보처리기기 관리책임자는 운영·관리 방침에 명시한 **보관기간(최대 30일)**이 만료되면 지체없이 파기해야 한다. 해당 지침은 2024-01-04부터 시행 중(고시 제2024-1호).
출처: https://www.law.go.kr/admRulLsInfoP.do?admRulSeq=2100000234592 , https://www.privacy.go.kr/front/contents/cntntsView.do?contsNo=121 (확인일 2026-09-07)

**추론(법 조문 직접 확인 필요)**: 위 제25조/제25조의2는 "업무 목적" 및 "공개된 장소" 설치·운영을 전제로 한다. 가정 내부(사적 공간)를 향하지 않는 자가용 카메라의 순수 개인적 사용은 통상 개인정보보호법의 적용 예외(사적 이용) 논의 대상으로 알려져 있으나, **본 조사에서 조문의 "사적 이용 예외" 명시 문구를 직접 확인하지 못했다 — 미확인.** PiCam Hub가 매장·사무실 등 "공개된 장소"를 촬영하는 소상공인 용도로 쓰일 경우 제25조/25조의2가 적용될 가능성이 높다(추론).

**사실 — 해외 참고**: GDPR 및 영국 ICO는 CCTV를 개인정보 처리로 간주해 안내 표지·목적 제한·보관기간 최소화 원칙을 요구한다(1줄 요약, 상세 미조사).

**미확인**: 제25조/제25조의2의 구체적 항·호 번호별 전문(全文), 과태료 조항, 안내판 필수 기재사항 목록은 이번 조사에서 원문 전체를 발췌하지 않았다 — 국가법령정보센터 원문 재확인 필요.

## 유사 솔루션 (오픈소스+상용)

| 이름 | 기능 범위 | Pi 지원 | 브라우저 전달 | 라이선스 | 스타/최근 릴리스/최근 커밋 | 출처 |
|---|---|---|---|---|---|---|
| **Frigate NVR** | 실시간 뷰, 객체 감지(로컬 AI), 이벤트 클립, 녹화, HA 연동, 원격 접근(Frigate+ 옵션) | 공식 문서상 "Pi 4 이상 권장", 64비트 OS 필수(Pi 3 이하 미지원) | go2rtc 내장으로 WebRTC/MSE 지원 | MIT | ~35.6K stars, 최신 릴리스 2026-08-28(v0.17.2), 커밋 활발(수일 내) | https://github.com/blakeblackshear/frigate (검색 종합), https://docs.frigate.video/ (확인일 2026-09-07) |
| **go2rtc** | RTSP/RTMP/HTTP-FLV/WebRTC/MSE/HLS/MP4/MJPEG/HomeKit 변환 게이트웨이 (PTZ·녹화·감지 자체 기능 없음, 스트림 전달 전담) | Pi에서 저CPU/저RAM으로 동작한다고 자체 소개 | WebRTC(~0.5s 지연 주장)/MSE/HLS | MIT | 14,129 stars, 최근 push 2026-09-06, open issues 904 | https://api.github.com/repos/AlexxIT/go2rtc (WebFetch, 확인일 2026-09-07) |
| **MediaMTX** | RTSP/RTMP/HLS(LL-HLS)/WebRTC/SRT/Media-over-QUIC 서버·프록시, 녹화·재생 지원 (PTZ·객체감지 없음) | 커뮤니티 포럼에 Pi 설치 사례 다수(공식 "Pi 지원" 문구 자체는 미확인) | WebRTC/LL-HLS/RTSP 등 다중 | MIT | 20,048 stars, 최근 push 2026-09-05, open issues 190 | https://api.github.com/repos/bluenviron/mediamtx (WebFetch, 확인일 2026-09-07) |
| **ZoneMinder** | 실시간 뷰, 모션 감지, 녹화, IP/USB/아날로그 카메라 지원, 이벤트 관리 (PTZ는 카메라별 컨트롤 프로토콜 경유) | 커뮤니티에서 "아직 활발히 개발 중인가"라는 질문이 나올 정도로 개발 속도 논쟁 있음(포럼) | 공식 웹 UI(자체 스트리밍, WebRTC 네이티브 여부 미확인) | GPL-2.0 | 5,931 stars, 최근 push 2026-09-06, open issues 126 | https://api.github.com/repos/ZoneMinder/zoneminder (WebFetch, 확인일 2026-09-07) |
| Shinobi (참고) | 실시간 뷰, 녹화, 모션 감지, 커뮤니티/Pro 이원화 | Pi 지원 커뮤니티 언급 있음 | 미확인 | 오픈소스 커뮤니티 에디션 + 유료 Pro (정확 SPDX 라이선스 미확인) | 정확 통계 미확인(GitHub repo 경로 조회 실패, 404) | https://www.saashub.com/compare-motioneye-vs-shinobi (단일 출처, 확인일 2026-09-07) |
| motionEye (참고) | 모션 감지 기반 녹화·뷰어, motionEyeOS로 Pi 전용 배포판 제공 | motionEyeOS가 Pi 등 SBC 전용 경량 배포판으로 별도 존재 | 미확인 | 오픈소스(정확 라이선스 미확인) | 정확 통계 미확인 | https://community.home-assistant.io/t/my-opinion-zoneminder-vs-motioneye-vs-shinobi/316831 (단일 출처, 확인일 2026-09-07) |

**상용/클라우드**

| 이름 | 가격 | 기능 범위 | 클라우드 의존 | 출처 |
|---|---|---|---|---|
| Ubiquiti UniFi Protect | 전용 NVR(UNVR 등) 하드웨어 구매형, Synology 대비 동급 구성에서 약 $500~$900 더 비싸다는 비교 기사 존재(공식 가격표 직접 대조는 미확인) | 4K 지원, AI 감지, 구독료 없음(로컬 저장 기준) | 로컬 우선, 클라우드는 옵션 | https://ifeeltech.com/blog/unifi-protect-vs-synology-surveillance-station-flagship-comparison (독립 비교 블로그, 확인일 2026-09-07) |
| Synology Surveillance Station | 카메라 1대 추가 라이선스(CLP1) 약 $54.99~$74.19(리테일러별 상이), 자사 카메라(BC500/TC500)는 라이선스 포함, DVA1622는 8대분 포함 | ONVIF 카메라 호환, AI 감지, 구독료 없음 | 로컬 NAS 우선 | https://www.synology.com/en-us/compatibility/camera , https://www.ebay.com/p/1024604656 (확인일 2026-09-07) |

## 스택 후보 — 후보별 근거·트레이드오프

| 영역 | 후보 | 근거 / 트레이드오프 |
|---|---|---|
| 스트림 게이트웨이 | **go2rtc** | RTSP→WebRTC/MSE 변환 전담, MIT, Pi에서 저CPU 동작 자체 소개. **사실(단일 출처, 재확인 필요)**: "Pi 4에서 스트림 10개 정도는 무리 없이 처리"라는 커뮤니티 코멘트 존재. 하드웨어 디코딩 없이도 RTSP 패스스루(재인코딩 없이 컨테이너만 바꿔 전달)는 가능하나, 브라우저에서 H.265를 WebRTC로 바로 재생 못 하는 경우가 있어 트랜스코딩이 필요할 수 있음(일반 지식, 이번 조사에서 go2rtc 공식 문서로 개별 확인은 못함 — 미확인). ONVIF 지원 범위는 검색 결과에서 확인 못함 — 미확인. 출처: https://api.github.com/repos/AlexxIT/go2rtc , https://github.com/AlexxIT/go2rtc/issues/658 (확인일 2026-09-07) |
| 스트림 게이트웨이 | **MediaMTX** | 더 많은 프로토콜(SRT, Media-over-QUIC, LL-HLS 포함) 지원, MIT. 포럼 사례로 Pi 설치가 이뤄지나 "카메라→MediaMTX→go2rtc" 구성에서 프레임 드랍을 겪었다는 사용자 보고가 있어 파이프라인 단순화(둘 중 하나만 사용)가 유리하다는 커뮤니티 의견 존재. 출처: https://github.com/fuatakgun/eufy_security/discussions/878 , https://github.com/AlexxIT/go2rtc/issues/658 (확인일 2026-09-07) |
| ONVIF 클라이언트 (Python) | onvif-zeep-async | Snyk/Libraries.io 기준 "Healthy" 유지보수(최근 3개월 내 신규 배포), Device/Media/PTZ/Events/Analytics/Recording 서비스 지원. 출처: https://libraries.io/pypi/onvif-zeep-async , https://snyk.io/advisor/python/onvif-zeep-async (확인일 2026-09-07) |
| ONVIF 클라이언트 (Node) | agsh/onvif | TypeScript 우선, WS-Discovery·Media/Media2·PTZ(연속/절대 이동, 프리셋)·Profile S/T 코어 지원, 레거시 0.x API 호환 레이어 포함. 라이선스·스타 수는 이번 조사에서 미확인. 출처: https://github.com/agsh/onvif (확인일 2026-09-07) |
| ONVIF 클라이언트 (Go) | use-go/onvif | 이번 검색에서 결과가 뚜렷이 노출되지 않아 유지보수 상태·라이선스 미확인 — 재조사 필요. |
| 백엔드 언어 | Python FastAPI vs Node(Fastify/NestJS) vs Go | **추론(일반 아키텍처 지식, 이번 세션 벤치마크 검색은 안 함)**: Go는 단일 정적 바이너리로 ARM64 크로스컴파일·배포가 가장 단순하고 런타임 메모리 오버헤드가 작아 Pi에 유리. Node는 이벤트 루프 기반이라 WebSocket 알림/실시간 UI 푸시에 강점. Python/FastAPI는 onvif-zeep-async 등 ONVIF 라이브러리 생태계가 가장 풍부하지만 인터프리터 메모리 오버헤드가 상대적으로 큼. 세 후보 모두 사실상 Pi 4/5(RAM 2~8GB)에서 구동 가능 — 세부 벤치마크는 미확인. |
| 로컬 DB | SQLite(WAL) | WAL은 순차 append + 적은 fsync로 플래시 저장장치의 소거 블록 특성과 궁합이 좋다는 설명 확인. 다만 **카드 내구성(TBW) 등급은 순차 워크로드 기준이라 소규모 랜덤 동기 쓰기(DB 워크로드)의 실제 마모를 그대로 반영하지 않는다**는 지적 존재 — 상한선으로만 해석 권고. 소비자용 SD카드는 TBW 등급이 아예 없는 경우가 많고, 감시·블랙박스·산업용 등급 카드만 TBW를 명시. 출처: https://blog.pecar.me/sqlite-wal/ , https://spin.atomicobject.com/sqlite-raspberry-pi/ , https://forums.raspberrypi.com/viewtopic.php?t=257514 (확인일 2026-09-07). **미확인**: mender.io·dzombak 원문은 이번 검색에서 직접 노출되지 않음(대체 출처로 갈음).|
| 원격 접근 | Tailscale | **사실**: 무료(Personal) 플랜은 "최대 6 사용자", 기기 수는 사실상 무제한(비상업적 개인 용도 한정), WireGuard 기반 메시 네트워크로 포트포워딩 불필요. 출처: https://tailscale.com/pricing (WebFetch, 확인일 2026-09-07) |
| 원격 접근 | Cloudflare Tunnel | **사실**: 사용량 제한 없이 완전 무료로 제공된다고 소개됨(freemium 구조, 유료 부가기능 있음). 다만 Cloudflare가 트래픽 경로에 개입하는 구조라 영상처럼 민감한 데이터에는 트레이드오프가 있다는 독립 의견 존재. 출처: https://toolradar.com/tools/tailscale/pricing , https://hometechops.com/guides/home-remote-access-tailscale-vs-cloudflare-tunnel (확인일 2026-09-07, 독립 블로그 — 교차확인 약함) |
| 원격 접근 | WireGuard(직접) | Tailscale과 달리 인바운드 경로(포트포워딩/DDNS)를 직접 구성해야 함 — NAT 뒤 기기에는 상대적으로 진입장벽 높음(추론 겸 커뮤니티 비교 기사 인용). |
| 알림 | Telegram Bot / ntfy / Home Assistant 연동 | **미확인(가격 페이지 직접 재확인 안 함)**: Telegram Bot API·ntfy는 업계에 널리 알려진 무료·자가호스트 가능 채널이나, 이번 세션에서 공식 가격/약관 페이지를 별도로 열람하지 않았다 — 다음 라운드에서 ntfy.sh, core.telegram.org/bots 확인 필요. |
| 라즈베리파이 HW | Pi 5 | **사실**: BCM2712는 HEVC(H.265) 4K60 하드웨어 디코더는 유지하지만 **Pi 4에 있던 H.264 하드웨어 디코더/인코더는 Pi 5에서 빠졌다**(H.264는 소프트웨어 처리). 이는 라즈베리파이 공식 포럼(엔지니어 답변 스레드 다수, 공식 datasheet 페이지 직접 열람은 이번 세션에서 실패 — 404)에서 반복 확인됨. H.265 인코딩(HW)도 없음. 출처: https://forums.raspberrypi.com/viewtopic.php?t=387547 , https://forums.raspberrypi.com/viewtopic.php?t=391283 , https://forums.raspberrypi.com/viewtopic.php?t=357870 (확인일 2026-09-07, 공식 사이트 아닌 라즈베리파이 공식 포럼 — 반공식으로 취급) |
| 라즈베리파이 HW | Pi 4 | **사실**: 공식 5.1V/3A(15W) USB-C 전원 권장, 다운스트림 USB 소비가 500mA 미만이면 2.5A 전원도 가능. 출처: https://www.raspberrypi.com/products/raspberry-pi-4-model-b/specifications/ (확인일 2026-09-07) |
| SD카드 등급 | A1/A2 | **사실(부분)**: A1 등급은 최소 1,500 random read IOPS / 500 random write IOPS를 보장. A2의 정확한 IOPS 수치는 이번 검색 스니펫에서 잘려 확인 못함 — **미확인**(SD Association 공식 표 재확인 필요). |

## 수치 근거 (A3 상수 표에 쓸 것)

| 항목 | 값 | 출처 | 확인일 |
|---|---|---|---|
| 실시간 뷰 glass-to-glass 지연 — WebRTC | 약 200~500ms | https://www.forasoft.com/learn/video-streaming/articles-streaming/latency-glass-to-glass-explained , https://transitiverobotics.com/blog/webrtc-latency-breakdown/ | 2026-09-07 |
| 실시간 뷰 glass-to-glass 지연 — 표준 HLS | 약 15~30초(세그먼트 버퍼링) | https://www.mux.com/articles/low-latency-live-streaming-developers-guide-ll-hls-webrtc-cmaf | 2026-09-07 |
| 실시간 뷰 glass-to-glass 지연 — LL-HLS | 약 2~5초 | https://www.mux.com/articles/low-latency-live-streaming-developers-guide-ll-hls-webrtc-cmaf , https://www.wink.co/documentation/Ultra-Low-Latency-HLS-Experiments-2025 | 2026-09-07 |
| 카메라 sub-stream 권장 | 480p/720p, 저비트레이트(그리드 뷰용) | https://bokysee.com/ip-security-camera-main-stream-vs-sub-stream-settings/ | 2026-09-07 |
| RTSP 1080p H.264 카메라 1대 대역폭 | 약 2~4Mbps (일부 소스는 30fps 기준 3.5~6Mbps), 감시용 권장 인코더 비트레이트 3Mbps(최소 1.5Mbps) | https://reolink.com/blog/ip-camera-bandwidth-calculation/ , https://cctvhelpdesk.net/cctv-encode-settings/ | 2026-09-07 |
| 24시간 연속 녹화 1대당 저장 용량 | 공식(추정) = 비트레이트(Mbps) × 10.8 → 1080p H.264 4~6Mbps 시 약 43~65GB/일 | https://pvrblog.com/cameras/tech/storage-calculator/ , https://www.digitaltrends.com/home/how-much-space-does-security-camera-video-footage-require/ | 2026-09-07 |
| Pi 4 + Frigate(Coral TPU) 동시 카메라 수 | 커뮤니티 실측 1건: 12대 카메라, detect ~VGA 5fps 시 load 2~4로 원활, **단 5대 풀해상도(4K15fps) 녹화·클립 생성 시 load 20+ 급등·드랍 발생** (단일 사례, 검출 해상도·AI 가속기 유무에 크게 좌우) | https://github.com/blakeblackshear/frigate/discussions (검색 종합, 개별 discussion 링크 재확인 필요 — 단일 출처) | 2026-09-07 |
| Pi 4 + go2rtc 동시 스트림 수 | 커뮤니티 코멘트: "스트림 10개 정도는 무리 없이 처리"(재인코딩 없는 패스스루 전제로 추정, 조건 명시 안 됨) | https://github.com/AlexxIT/go2rtc/issues/658 (단일 출처, 재확인 필요) | 2026-09-07 |
| SD카드 A1 성능 등급 | 최소 1,500 random read IOPS / 500 random write IOPS | https://raspberry.tips/en/raspberrypi-einsteiger/best-sd-card-for-raspberry-pi | 2026-09-07 |
| Pi 4 권장 전원 | 5.1V/3A(15W) USB-C 공식 전원, 경부하 시 2.5A 가능 | https://www.raspberrypi.com/products/raspberry-pi-4-model-b/specifications/ | 2026-09-07 |
| 영상정보 보관기간(국내 CCTV 관행) | 최대 30일(운영·관리 방침에 명시 후 만료 시 지체없이 파기) | https://www.law.go.kr/admRulLsInfoP.do?admRulSeq=2100000234592 | 2026-09-07 |
| Tailscale 무료 플랜 | 최대 6사용자, 기기 수 사실상 무제한(비상업 개인용) | https://tailscale.com/pricing | 2026-09-07 |
| Frigate GitHub 통계 | ~35.6K stars, 최신 릴리스 v0.17.2(2026-08-28), MIT | https://docs.frigate.video/ (검색 종합) | 2026-09-07 |

## 미확인 항목 (못 찾은 것 목록)

- 개인정보보호법 제25조/제25조의2의 "가정 내 사적 이용 예외"를 명시한 정확 조문 문구·항번호 (원문 전체 발췌 안 함)
- ONVIF Profile S/T 각각에서 PTZ 서비스의 정확한 WSDL 서비스명·필수/선택 항목 표(스펙 PDF 전체를 읽지 않음, 요약만 확인)
- go2rtc의 ONVIF 지원 범위(공식 문서 직접 확인 못함)
- go2rtc/MediaMTX가 Pi에서 하드웨어 디코딩 없이 순수 패스스루로 몇 대까지 처리 가능한지의 정확한 벤치마크(커뮤니티 코멘트 1건만 확보, 조건 불명)
- use-go/onvif(Go ONVIF 클라이언트)의 유지보수 상태·라이선스·PTZ 지원 여부
- Shinobi, motionEye의 정확한 GitHub 스타 수·최근 커밋일·SPDX 라이선스명(저장소 경로 조회 실패/미시도)
- SD카드 A2 등급의 정확 IOPS 수치(SD Association 공식 표 미확인, A1=1500read/500write만 확인)
- ntfy, Telegram Bot API의 공식 가격·약관 페이지 직접 확인(업계 통념상 무료·자가호스트 가능하나 이번 세션에서 재검증 안 함)
- mender.io, dzombak의 SD카드 마모 관련 원문(검색에 직접 노출 안 됨, 대체 출처로 갈음)
- UniFi Protect 공식 가격표(비교 블로그의 간접 인용만 확보, ui.com 공식 페이지 직접 열람 안 함)
- Reolink 앱 / Wyze / Ring 개별 가격·기능(과제에서 "2개"로 UniFi Protect+Synology를 충족시켜 조사 생략)
- r/homeassistant, r/frigate_nvr 서브레딧 개별 게시물 원문(WebSearch가 site: 연산자를 지원하지 않아 GitHub Discussions/HA 커뮤니티 포럼으로 대체)

---

## 메인 추가 검증 (2026-09-07, WebFetch 4회)

| 항목 | 결과 | 출처 |
|---|---|---|
| go2rtc ONVIF 지원 | **사실**: 입력 소스로 `onvif` 프로토콜 지원("A popular ONVIF protocol for receiving media in RTSP format"), 출력으로 ONVIF 서버 모드("Output stream using ONVIF protocol") 있음. WebRTC H.265는 Chrome 136+ / Safari 18+에서 지원. HLS는 "live streaming에 최악, iPhone 때문에 존재"라고 자체 명시. 하드웨어 가속 트랜스코딩 지원 문구 있음. MIT | https://github.com/AlexxIT/go2rtc (README, 확인 2026-09-07) |
| Pi 5 코덱 | **사실(공식)**: 제품 페이지 사양에 "4Kp60 HEVC decoder"만 기재, H.264 하드웨어 디코더/인코더 기재 없음 → 포럼 발견(H.264 HW 제거)과 일치. RAM 1/2/4/8/16GB, 16GB $305 | https://www.raspberrypi.com/products/raspberry-pi-5/ (확인 2026-09-07) |
| Pi 5 전원 | **사실(공식)**: USB-C 5V/5A(25W) 또는 5V/3A(15W, 주변기기 600mA 제한) | https://www.raspberrypi.com/documentation/computers/raspberry-pi-5.html (확인 2026-09-07) |
| ntfy | **사실(부분)**: 공식 문서에 Self-hosting(Installation/Configuration) 절 존재, 공개 서버 + ntfy Pro 프리미엄 구조. 라이선스·공개 서버 레이트리밋은 이 페이지에서 미확인 → A2에서 Assumed(자가호스트 전제) | https://docs.ntfy.sh/ (확인 2026-09-07) |

## fit 판정 (메인 — 사용자 제약 매칭, `evidence-map.md` fit_score 5요소)

전제 제약(A0·A2 가정): 자가 운영 1인, Pi 1대(ARM64, RAM 4GB급), 카메라 ≤4대(ONVIF Profile S/T, H.264 sub-stream), 브라우저 뷰어, 인터넷 가능(NAT 뒤), 예산 = Pi + USB SSD 수준.

| 영역 | 선택 | fit | 왜 (요소) | 버린 대안(왜) |
|---|---|---|---|---|
| **전체 접근 (search-first)** | **Extend** — go2rtc를 스트림 엔진으로 채택하고, 제어·모니터링·알림 얇은 계층만 자체 구현 | 높음 | 요구사항: 스트리밍은 해결된 문제(Adopt), "제어+상태 모니터링+알림"만 도메인 고유. 총비용: 스트리밍 자체 구현 시 WebRTC/MSE 호환 지옥(go2rtc README의 브라우저 호환표가 그 증거) | Frigate 통째 Adopt — 객체 감지 NVR로 범위가 과잉(Pi에서 Coral 없이 감지 부담, 시드는 "제어·모니터링"). 단 **객체 감지가 진짜 목적이면 Frigate가 정답** — 03 non-goal에 명시. ZoneMinder — GPL-2.0·모놀리식·WebRTC 미확인 |
| 스트림 게이트웨이 | **go2rtc** | 높음 | 요구: RTSP→WebRTC(≈0.5s)·MSE·ONVIF 소스, Pi 저CPU 자체 소개. 생태계: MIT, push 2026-09-06, Frigate가 내장 채택(검증된 운영 사례). 운영: 단일 바이너리 | MediaMTX — 프로토콜은 더 많으나(SRT·MoQ 불필요) ONVIF 소스 없음, 둘을 직렬로 두면 프레임 드랍 보고 → 하나만 |
| 제어 백엔드 | **Python 3.12 + FastAPI + onvif-zeep-async** | 높음 | 요구: ONVIF PTZ·Events·Discovery 라이브러리가 가장 성숙(Healthy, 최근 3개월 배포; Home Assistant ONVIF 통합이 같은 계열 사용). 팀: 1인 자가 운영자에게 가장 흔한 언어(추론). 운영: Pi 4GB에서 FastAPI 프로세스 ≈ 60~100MB(추론, 미측정 → 07 Saturation 지표로 실측) | Node agsh/onvif — 라이브러리 품질 양호하나 라이선스·유지보수 통계 미확인. Go use-go/onvif — 유지보수 상태 미확인(재조사 실패), 정적 바이너리 장점보다 ONVIF 리스크가 큼 |
| 로컬 DB | **SQLite (WAL)** — 파일은 USB SSD | 높음 | 요구: 단일 노드, 카메라 ≤4·이벤트 수천 건/일 — RDBMS 불필요. 운영: WAL의 순차 append가 플래시에 유리. **SD 마모 대책으로 DB·클립은 USB SSD, 로그는 tmpfs/log2ram** | Postgres — 프로세스·메모리 과잉(YAGNI). SD카드 위 SQLite — TBW 없는 소비자 카드에 랜덤 쓰기 → 수명 리스크 |
| 원격 접근 | **Tailscale** | 높음 | 요구: NAT 뒤 Pi에 포트포워딩 없이 접근, WireGuard 기반 E2E. 비용: 무료 6사용자(개인). 운영: 설치 1명령 | Cloudflare Tunnel — 무료지만 영상 트래픽이 제3자 경유(민감정보). 직접 WireGuard — 인바운드 포트/DDNS 필요. 포트포워딩 — 금지(P1 기본값) |
| 알림 | **ntfy (자가호스트 또는 ntfy.sh)** 1순위, Telegram Bot P2 | 중간 | 요구: 푸시 1개면 충분, 자가호스트로 데이터 로컬 유지. 라이선스·레이트리밋 미확인 → Assumed(A2) | Home Assistant 연동 — HA 미보유 사용자 배제(추론). 이메일 — 실시간성 부족 |
| 하드웨어 | **Pi 5 4GB + USB SSD + 공식 27W 전원**; Pi 4 4GB 허용(성능 하향) | 높음 | Pi 5: HEVC HW 디코더만 있고 H.264 HW 없음 → **트랜스코딩 안 하는 설계**(패스스루)를 전제로 하면 무관. 전원: 5V/5A 공식(USB SSD 주변기기 600mA 제한 회피). 비용: Pi 5 4GB ≈ $60(추론, 미확인) + SSD | Pi Zero 2 W — RAM 512MB로 go2rtc+FastAPI+브라우저 세션 동시 부담 위험(추론). x86 미니PC — 시드가 라즈베리파이를 명시 |
| 브라우저 전달 | **WebRTC 우선, MSE 폴백, HLS 미채택** | 높음 | 요구: "실시간" = glass-to-glass ≤1s 목표 (WebRTC 200~500ms 근거). HLS 15~30s는 목표 미달 | LL-HLS 2~5s — 목표 미달, iPhone은 MSE(iOS 17.1+)로 대체 |
| 프론트엔드 | **서버 렌더 HTML + htmx + go2rtc 내장 WebRTC 플레이어(video-rtc.js)** | 중간 | 요구: 화면 2~3장(그리드·카메라 상세·설정). 팀: 1인, 빌드 파이프라인 없이 Pi에서 서빙. UI 밀도는 A3 dial | React/Vite SPA — 화면 수 대비 빌드·번들 부담 과잉(YAGNI). 단 화면이 5장 이상으로 커지면 재검토 |

**search-first 결론**: Adopt(go2rtc·Tailscale·ntfy·SQLite) + Build(ONVIF 제어 서비스·카메라 레지스트리·헬스 모니터·알림 라우터·UI). 자체 코드는 "카메라 등록→제어→상태→알림" 도메인 로직에 한정한다.
