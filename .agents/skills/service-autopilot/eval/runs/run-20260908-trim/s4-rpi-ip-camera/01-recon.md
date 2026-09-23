# RECON — PiCam Watch
초안: sonnet 서브에이전트 · 조사일 2026-09-08 · 검색 36회 (WebSearch 24 + WebFetch 12)

## 도메인 업무 흐름 — 이 산업이 실제로 어떻게 돌아가는가 (출처)

**ONVIF 프로파일 체계 (확정).** ONVIF는 카메라/NVR/VMS 상호운용을 위한 컨포먼스 프로파일을 운영한다.
- Profile S — 기본 스트리밍(레거시). ONVIF가 2025-10-09 Profile S 지원 종료를 발표했고, 2027-03-31 이후로는 신제품/신펌웨어의 Profile S 신규 컨포먼스 제출이 불가하다. 사유는 "인증 메커니즘이 현재 사이버보안 권고와 맞지 않음". (출처: [ONVIF to End Support for Profile S](https://www.onvif.org/?post_type=pressrelease&p=8621), 2026-09-08 확인)
- Profile T — 2026년 기준 스트리밍 baseline. H.265 + H.264, 이미징 설정, 모션/탬퍼링 이벤트, 메타데이터 스트리밍, 양방향 오디오 포함. 2018년 도입. (출처: [ONVIF Profiles](https://www.onvif.org/profiles/), forasoft.com 교차확인, 2026-09-08)
- Profile G — 엣지(카메라/레코더 자체) 저장 및 시간/이벤트/모션 기준 검색·조회를 표준화. (출처: [ONVIF Profiles](https://www.onvif.org/profiles/), 2026-09-08)
- Profile M — 메타데이터·분석. Profile A/C/D는 출입통제 계열(본 서비스와 무관).
→ 본 서비스는 스트리밍+PTZ가 핵심이므로 **Profile T가 2026년 시점의 표준 타깃**, Profile S는 레거시 카메라 호환용으로만 취급해야 함.

**ONVIF PTZ 서비스 (확정).** PTZ 제어(Pan/Tilt/Zoom, Stop, 좌표계, 프리셋)는 공식 ONVIF PTZ Service Specification으로 표준화돼 있다. 최신은 v26.06, 좌표는 space URI로 표현되는 여러 좌표계를 지원. (출처: [ONVIF PTZ Service Spec v26.06 PDF](https://www.onvif.org/specs/srv/ptz/ONVIF-PTZ-Service-Spec.pdf), 2026-09-08) → "제어 = ONVIF PTZ 표준 채택"은 표준화된 접근이며 벤더 종속 프로토콜을 만들 필요가 없다는 근거.

**RTSP 메인/서브 스트림 관행 (가능성 — 벤더 문서 교차확인).** 업계 관행은 메인스트림(고해상도, 저장/증거용)과 서브스트림(저해상도·저비트레이트, 모바일/그리드뷰용)을 동시에 송출하는 것. Hikvision 공식 FAQ 문서와 다수 벤더 블로그가 일치. (출처: [Hikvision 공식 Bit Rate FAQ PDF](https://www.hikvision.com/content/dam/hikvision/ca/faq-document/H.2645-&-H.2645-Recommended-Bit-Rate-at-General-Resolutions.pdf), [getscw.com](https://www.getscw.com/knowledge-base/nvr-bitrate), 2026-09-08)

**소규모 시설 CCTV 운영 흐름 (가능성 — 산업 블로그 교차확인, 1차 표준 문서 아님).** 설치(자가설치 DIY 또는 통신사 결합상품/설치업체, 렌탈 vs 구매) → 라이브 확인 → 이벤트 알림 → 클립 확인 → 보관(권고 30일+) → 파기. 3년 미만 운영 예정 매장은 렌탈, 3년 이상은 구매가 합리적이라는 시장 통념. (출처: [매장 CCTV 설치 비용 가이드](https://mejindan.ai/blog/cctv-install-cost-guide/), [keeper.ceo](https://keeper.ceo/blog/cctv-installation-cost), [보안뉴스 소호 CCTV 서비스 비교](https://m.boannews.com/html/detail.html?idx=67718), 2026-09-08) — 단일 블로그가 아니라 3개 독립 소스 교차확인.

## 이해관계자 — 누가 쓰고, 누가 돈을 내는가 (출처)

- **시설 운영자(관리자, 비용 지불자)** — 매장·사무실·창고·농장의 소상공인 1~수 명. seed 파일 가정 7과 일치.
- **시청자** — 관리자 본인 또는 위임 직원(별도 확인 소스 없음, seed 가정 유지).
- **설치자** — 자가설치(DIY 패키지, 예: Hikvision 자가설치 스토어) 또는 통신사 결합상품/전문 설치업체. 자가설치는 비용 절감이나 "어렵고 복잡, A/S 애매, 야간 촬영 실패 등 오설치 리스크"가 지적됨. (출처: [CCTV 자가설치 하지 마세요](https://xn--hz2b19j9ogo9h.com/55/?bmode=view&idx=167084034), [Hikvision 자가설치 패키지](https://hikvisionmall.co.kr/category/%EC%9E%90%EA%B0%80%EC%84%A4%EC%B9%98-%ED%8C%A8%ED%82%A4%EC%A7%80/43/), 2026-09-08)
- **구매 형태(시장 규모 맥락)** — "영상보안 시장의 35%가 소상공인 시장"이며 통신업체가 인터넷·전화와 결합한 CCTV 패키지를 판매. (출처: [보안뉴스](https://m.boannews.com/html/detail.html?idx=67718), 2026-09-08, 단일 소스 — 교차확인 소스 못 찾음, **가능성**으로 표기)

## 규제·표준 — 반드시 준수해야 하는 것 (출처; 해당 없음도 확인 근거)

**개인정보보호법 제25조 (고정형 영상정보처리기기의 설치·운영 제한, 2025-10-02 시행분 기준) — 확정.**
- 설치 허용 목적 한정(범죄예방, 시설안전관리, 교통단속·정보수집 등 법령상 사유).
- 금지 장소: 화장실·탈의실 등 사생활 침해 우려 높은 장소(교정·요양시설 등 예외 있음).
- 안내판 의무: 설치 목적 및 장소, 촬영 범위 및 시간, 관리책임자 연락처 게시(군사시설 등 예외).
- 목적 외 조작·다른 곳 비추기·녹음기능 사용 금지.
- 안전조치 의무 및 운영·관리방침 수립 의무.
(출처: [law.go.kr 제25조](https://www.law.go.kr/LSW//lsLawLinkInfo.do?lsJoLnkSeq=900079397&lsId=011357&chrClsCd=010202&print=print), [easylaw.go.kr 해설](https://www.easylaw.go.kr/CSP/CnpClsMain.laf?csmSeq=1257&ccfNo=2&cciNo=3&cnpClsNo=3), 2026-09-08)

**보존기간 30일 권고 — 가능성(원문 조문 직접 인출 실패, 검색 스니펫 기반).** 표준 개인정보 보호지침 제41조제2항에 따라 보유 목적 달성을 위한 최소 기간 산정이 곤란한 경우 영상정보 수집 후 30일 이내로 보관기간을 정할 수 있으며, 30일 초과 시 운영·관리 방침에 반영해야 한다는 설명이 검색 결과에서 반복 확인됨. law.go.kr 행정규칙 페이지(admRulSeq=2100000234592)는 존재를 확인했으나 WebFetch로는 JS 렌더링 때문에 조문 원문 텍스트를 직접 인출하지 못했다 — **미확인: 제41조 정확한 원문, 재확인 필요**. 개인정보보호위원회는 2024-12 "고정형 영상정보처리기기 설치·운영 안내서"를 발간해 제25조 준수사항을 상세 설명한다(발간일 확인됨). (출처: [privacy.go.kr 안내서 게시글](https://www.privacy.go.kr/front/bbs/bbsView.do?bbsNo=BBSMSTR_000000000049&bbscttNo=20779) — 발간일 2024-12-31 확인, [easylaw.go.kr](https://www.easylaw.go.kr/CSP/CnpClsMain.laf?csmSeq=1257&ccfNo=2&cciNo=3&cnpClsNo=3), [catchsecu.com](https://www.catchsecu.com/archives/16550), 2026-09-08)

**개인정보의 안전성 확보조치 기준 — 접속기록 — 가능성(원문 미인출, 요약 소스 기반).** 개인정보처리시스템 접속기록은 원칙 1년 이상 보관, 5만 명 이상 정보주체 처리 또는 고유식별정보·민감정보 처리시스템은 2년 이상 보관. 접속기록은 월 1회 이상 점검 의무. CCTV 영상정보는 보관기간 만료 시 지체없이(=만료일로부터 5일 이내) 파기. law.go.kr 원문(admRulSeq=2100000229672)은 존재 확인했으나 조문 텍스트 직접 인출 실패 — **미확인: 정확한 조번호, 재확인 필요**. 본 서비스는 소규모 시설(정보주체 5만명 미만 추정)이라 1년 기준이 적용될 가능성이 높으나 **확정 아님**. (출처: [itwiki.kr](https://itwiki.kr/w/%EA%B0%9C%EC%9D%B8%EC%A0%95%EB%B3%B4%EC%B2%98%EB%A6%AC%EC%8B%9C%EC%8A%A4%ED%85%9C_%EC%A0%91%EC%86%8D%EA%B8%B0%EB%A1%9D), [catchsecu.com](https://www.catchsecu.com/archives/16550), 2026-09-08)

**GDPR — 해당 없음(확정, 사유 명시).** 한국 내 소규모 시설 대상 서비스로 EU 거주자에게 재화·서비스를 제공하거나 EU 내 행동을 모니터링하지 않는 한 GDPR 제3조(역외적용) 요건(설립 기준 또는 타겟팅 기준)을 충족하지 않는다. 단순히 웹에서 접근 가능하다는 사실만으로는 역외적용이 발동하지 않는다. (출처: [GDPR Article 3 해설](https://gdpr.eu/companies-outside-of-europe/), [gdpr-text.com](https://gdpr-text.com/read/article-3/), 2026-09-08)

## 유사 솔루션 3~5개 — 오픈소스+상용, 실제 기능 범위 (출처)

모든 스타 수·라이선스·커밋 수는 2026-09-08 GitHub 저장소 페이지 직접 열람(WebFetch) 기준.

| 솔루션 | 유형 | 스타 | 라이선스 | 활동 지표 | Pi 지원 | PTZ | 원격 접근 방식 |
|---|---|---|---|---|---|---|---|
| [Frigate](https://github.com/blakeblackshear/frigate) | OSS, AI 객체탐지 NVR | 35.7k | MIT (코드/설정/문서; "Frigate" 브랜드는 상표) | dev 브랜치 6,098 커밋 (최근 커밋 날짜는 fetch로 미확인) | 미확인(공식 docs.frigate.video 재확인 필요) | 미확인(이번 조사에서 확인 안 됨) | RTSP 재스트리밍 + WebRTC/MSE 저지연 라이브뷰 + MQTT 이벤트 통합, Home Assistant 연동 |
| [go2rtc](https://github.com/AlexxIT/go2rtc) | OSS, 스트림 중계 서버 | 14.1k | MIT | master 2,171 커밋 (최근 날짜 미확인) | 확정 — ARM 64/32bit, ARMv6 바이너리 명시 배포 | 해당 없음(미디어 릴레이 전용, 카메라 제어 기능 없음) | 자체 WebRTC(제로딜레이) + HLS 출력 |
| [MediaMTX](https://github.com/bluenviron/mediamtx) | OSS, 미디어 서버/프록시 | 20.1k | MIT | master 3,435 커밋 (최근 날짜 미확인) | 확정 — "Raspberry Pi Cameras" 발행 지원 명시 | 해당 없음(미디어 서버, 카메라 제어 기능 없음) | SRT/WebRTC/RTSP/RTMP/LL-HLS/MPEG-TS/RTP |
| [Scrypted](https://github.com/koush/scrypted) | OSS, 홈 비디오 통합 플랫폼 | 5.9k | 저장소별 상이(LICENSE.md, 단일 라이선스명 미확인) | main 8,550 커밋 (최근 날짜 미확인) | 미확인 | 지원 — ONVIF 플러그인으로 PTZ 권장 | HomeKit/Google Home/Alexa 저지연 스트리밍 |
| [ZoneMinder](https://github.com/ZoneMinder/zoneminder) | OSS, 전통 NVR/VMS | 5.9k | GPL-2.0 | master 29,259 커밋 (최근 날짜 미확인) | 미확인 | 저장소 구조에 onvif 폴더 존재(시사됨) — 세부 미확인 | 웹 인터페이스(원격접근 메커니즘 세부 미확인) |
| [MotionEye](https://github.com/motioneye-project/motioneye) | OSS, motion 데몬 웹 프론트엔드 | 4.7k | GPL-3.0 | dev 브랜치 2,741 커밋; 검색 스니펫상 "2026년 6월 릴리스로 HTTP basic auth 제거 등 보안 개선" 언급(WebFetch로 날짜 직접 재확인은 못함) | 역사적으로 Pi 대상(motionEyeOS) — 이번 조사에서 공식 재확인 안 됨 | 미확인 | 웹 인터페이스 |
| UniFi Protect (상용) | 상용, 클로즈드 | — | 상용 | — | 해당없음(전용 콘솔 하드웨어 필요) | ONVIF 지원(서드파티 카메라 호스팅, PTZ 제어 가능) | 클라우드 릴레이(Ubiquiti SSO 계정 경유, 포트포워딩/VPN 불필요) |

Frigate/Scrypted/ZoneMinder/MotionEye의 최근 커밋 정확한 날짜, 정확한 Pi/PTZ 지원 세부는 이번 조사 예산 안에서 공식 문서까지 파고들지 못했다 — **미확인 항목으로 남김, 후속 조사 시 각 공식 docs 사이트 재확인 필요**. UniFi Protect 정보는 서드파티 리뷰 사이트(ifeeltech.com, dongknows.com) 기반이라 공식 ui.com 문서 대비 교차확인이 약함(**가능성**).

## 스택 후보 — 후보별 근거·트레이드오프

**a. 스트림 중계: go2rtc vs MediaMTX**
- go2rtc(MIT, 14.1k★): Pi ARM 바이너리 공식 배포, H.264 "zero-delay" 패스스루(재인코딩 없이 중계) 명시, WebRTC+HLS 동시 출력. Frigate 생태계에서 재스트리밍 엔진으로 채택된 사례가 이슈 트래커에서 확인됨.
- MediaMTX(MIT, 20.1k★ — go2rtc보다 스타 많음): "Raspberry Pi Cameras" 발행 지원 명시, SRT/LL-HLS 등 더 넓은 프로토콜 폭. Frigate+MediaMTX+go2rtc를 함께 쓰는 조합 사례도 커뮤니티에서 확인(MediaMTX가 카메라 쪽 안정화, go2rtc가 브라우저 쪽 WebRTC 담당).
- 트레이드오프: 두 도구 모두 활발(둘 다 MIT, 최근까지 커밋 수천 건). go2rtc는 Pi 패스스루 문서화가 더 명확, MediaMTX는 생태계/프로토콜 폭이 더 넓음. 직접구현(자체 FFmpeg+WebRTC 시그널링)은 두 OSS가 이미 검증돼 있어 근거상 우선순위가 낮음.

**b. Pi 하드웨어: Pi 4 vs Pi 5**
- **확정 — Pi 5(BCM2712)는 하드웨어 H.264/H.265 인코더가 없다.** "no hardware H264 or H265 encoder on Pi5", "레거시 하드웨어 비디오 코덱 블록이 BCM2712에는 없고 라즈베리파이 자체 개발 H.265 디코더 블록만 남음" — 인코딩은 ARM CPU 소프트웨어(libx264)로 수행. (출처: [Raspberry Pi 공식 포럼 스레드](https://forums.raspberrypi.com/viewtopic.php?t=391283), [Hacker News 교차토론](https://news.ycombinator.com/item?id=38068801), 2026-09-08) → Pi에서 재인코딩(트랜스코딩)은 CPU 부담이 크므로, ONVIF 카메라의 H.264 스트림을 **재인코딩 없이 패스스루 중계**하는 아키텍처(go2rtc/MediaMTX)가 사실상 필수.
- Pi 4는 레거시 V4L2 하드웨어 코덱 블록을 가졌다는 것이 위 검색 결과에서 간접 확인되나(Pi5에는 "레거시 블록이 없다"는 표현으로 대비됨), Pi4 자체의 공식 V4L2 인코더 스펙은 이번 조사에서 별도 1차 소스로 재확인하지 못함(**가능성**).
- **확정 — Pi 5는 전용 RTC 배터리 커넥터('BAT')를 갖는다.** ML2020 충전식 리튬 배터리로 전원 차단 시에도 RTC 유지, 전원 인가 시 재충전. (출처: [raspberrypi.com 공식 제품 페이지](https://www.raspberrypi.com/products/rtc-battery/), 2026-09-08) — Pi 4에는 이 온보드 RTC 커넥터가 없다는 것이 Pi5 신규 기능으로 소개되는 방식에서 간접 확인됨(Pi4 자체의 "RTC 없음"에 대한 별도 공식 문구는 이번 조사에서 직접 인용하지 못함, **가능성**).

**c. ONVIF 클라이언트 라이브러리**
- python-onvif-zeep(FalkTannhaeuser, zeep 브랜치) — 커뮤니티에서 가장 널리 참조되는 저장소지만 이번 조사에서 최근 커밋일을 직접 확인하지 못함(**미확인**).
- python-onvif-zeep-async(openvideolibs) — 50★, MIT, 406 커밋, Python 3.10+ async/await 재작성판. FalkTannhaeuser 저장소의 공식 후계자로 자처하지는 않지만 더 최신 아키텍처(zeep[async]). (출처: WebFetch github.com/openvideolibs/python-onvif-zeep-async, 2026-09-08)
- node-onvif(futomi 계열, 포크 다수 존재) — 존재는 확인되나 유지보수 활성도는 이번 조사에서 미확인.
- 세 후보 모두 벤더 공식 SDK가 아닌 커뮤니티 유지보수 라이브러리이며, 정확한 최근 커밋일자는 **미확인 — GitHub API로 재확인 필요**.

**d. 제어·상태 채널: MQTT vs WebSocket**
- MQTT: pub/sub, QoS, 재연결 강함, TCP 네이티브, 대역폭 최소화 — IoT 표준. 브로커 대부분이 MQTT-over-WebSocket 리스너도 함께 제공해 브라우저 대시보드가 같은 브로커·같은 토픽에 WebSocket으로 붙는 혼합 구성이 일반적.
- WebSocket: 지속 연결 필요(Pi는 상시전원이라 배터리 이슈 무관), 443 포트 사용 시 방화벽/프록시 통과에 유리.
- Pi→서버 아웃바운드 상시연결(포트포워딩 금지 전제)에는 둘 다 적합. 표준 패턴은 "디바이스는 MQTT(mosquitto/EMQX 등 브로커), 브라우저는 같은 브로커에 MQTT-over-WebSocket"인 혼합 구성. (출처: [hivemq.com](https://www.hivemq.com/blog/understanding-the-differences-between-mqtt-and-websockets-for-iot/), [websocket.org](https://websocket.org/comparisons/mqtt/), [svix.com](https://www.svix.com/resources/faq/mqtt-vs-websocket/), 2026-09-08, 3개 독립 소스 교차확인)

**e. WebRTC 원격 시청의 NAT 통과: TURN(coturn)**
- coturn: 13,998~14.1k★(소스 간 근소한 차이), BSD-3-Clause, 위키 기준 "2주 전 업데이트"(정확 커밋일 미확인). (출처: [github.com/coturn/coturn](https://github.com/coturn/coturn), 2026-09-08)
- Pi는 NAT 뒤, 시청자도 다양한 네트워크(대칭형 NAT 포함)에 있을 수 있어 STUN만으로 연결 실패 시 TURN 릴레이가 필요할 가능성이 높다 — 이는 WebRTC 아키텍처 일반론이며, 이번 조사에서 본 서비스 규모(사이트≤10, 시청자≤5)에 대한 별도 벤치마크나 릴레이 대역폭 비용 수치는 확보하지 못함(**미확인**).

**f. 서버 백엔드 언어/프레임워크 + DB**
- 이번 조사 예산 안에서 Go/FastAPI/Node 3자 비교에 대한 전용 검색을 수행하지 못했다 — **미확인, 후속 조사 필요**.
- SQLite(Pi 로컬) + PostgreSQL(서버) 조합은 통상적 패턴이나, 이번 조사에서 별도 벤치마크·공식 문서 소스로 확인하지 않았다 — **미확인**.

**g. OTA/fleet: Mender vs RAUC vs balenaOS vs systemd+서명 tarball**
- RAUC: 보안 중심, 가장 가벼운 풋프린트(바이너리 약 512KB — SWUpdate 1.3MB, Mender 6.9MB 대비 작음). (출처: [32blog.com Yocto OTA 비교](https://32blog.com/en/yocto/yocto-ota-update-comparison), [proteanos.com 2026 비교](https://proteanos.com/doc/ota-updates-rauc-swupdate-mender-2026/), [rugix.org](https://rugix.org/blog/2026-02-28-ota-update-engines-compared/), 2026-09-08, 3개 소스 교차확인)
- Mender: 오픈소스 코어는 Apache-2.0이지만 델타 업데이트 등 일부 기능은 Enterprise/Professional 전용 — 완전 무료 티어가 아니다.
- balenaOS/balena: 빌드/배포/프로비저닝/제어/폐기까지 아우르는 풀사이클 컨테이너 기반 플릿 관리로 평가되나, 무료 티어의 정확한 조건은 이번 조사에서 확인하지 못함(**미확인**, balena.io 공식 요금 페이지 재확인 필요).
- systemd+서명 tarball(단순 자체구현): 비교 자료에 별도 언급 없음 — 가장 가볍지만 자체 구현 부담. 세 OSS 도구 모두 임베디드 리눅스(Yocto) 생태계에서 검증됐으나 Raspberry Pi OS(Debian 기반) 공식 지원 여부는 도구별 별도 확인이 필요(**가능성**).

**h. 엣지 OS 안정화: overlayfs, log2ram**
- overlayfs — **가능성(공식 문서 원문 미열람, 다수 커뮤니티 소스 일치)**: Raspberry Pi OS는 raspi-config/데스크톱 Control Centre에서 "Overlay File System" 옵션을 제공한다고 다수 소스가 서술 — 루트를 읽기전용 하위 레이어 + RAM 기반 쓰기 가능 상위 레이어로 구성, 부팅파티션 보호 여부 별도 선택. 공식 documentation.raspberrypi.com 원문 페이지는 이번 조사에서 직접 열람하지 못함 — 재확인 필요.
- log2ram(azlux/log2ram) — 저장소 존재 확인(확정). systemd용 ramlog 유사 도구로 로그를 RAM에 저장 후 주기적으로 디스크에 동기화, SD카드 쓰기 마모 감소. Raspberry Pi OS 공식 내장 기능이 아닌 서드파티 도구. (출처: [github.com/azlux/log2ram](https://github.com/azlux/log2ram), [pimylifeup.com](https://pimylifeup.com/raspberry-pi-log2ram/), 2026-09-08)

## 참조 제품 규칙 상세 — 해당 없음

사유: 본 서비스는 의료기기(SaMD)·금융 라이선스 상품처럼 정부·업계 "참조 제품 규칙"이 지정된 규제 산업 유형이 아니라 일반 소상공인 대상 CCTV 보조 도구이며, seed 파일(00-seed.md)에도 참조 제품이 지정돼 있지 않다. 규제·표준 절의 개인정보보호법은 일반 데이터보호 규범이지 참조 제품 규칙이 아니다.

## 데이터 공급 실측 — 설계 상수의 근거 숫자

- **1080p H.264 메인스트림 비트레이트**: 약 2~5 Mbps(30fps 기준, 고모션·증거용 화질은 5Mbps 권장). (출처: [getscw.com](https://www.getscw.com/knowledge-base/nvr-bitrate), [montavue.com](https://montavue.com/blogs/news/understanding-bitrate-frame-rate-and-resolution-in-security-cameras), [Hikvision 공식 FAQ PDF](https://www.hikvision.com/content/dam/hikvision/ca/faq-document/H.2645-&-H.2645-Recommended-Bit-Rate-at-General-Resolutions.pdf), 2026-09-08 — 공식(Hikvision) + 독립 2개 교차확인)
- **서브스트림(CIF/D1) 비트레이트**: 약 512 kbps 권장. (출처: [unifore.net](https://www.unifore.net/ip-video-surveillance/simple-guide-of-ip-camera-bitrate-setting.html), 2026-09-08 — 벤더 블로그 단일 소스, **가능성**)
- **한국 가정 평균 업로드 속도**: 2025년 기준 약 211 Mbps(3개 통신사 모두 210Mbps 이상). (출처: 검색 스니펫 기반, 1차 통계기관(NIA 등) 원문 미열람 — **가능성**, 2026-09-08 시점 최신 공식 통계 미확인). 참고: 이는 회선 계약 속도이며 Pi가 실제로 카메라 스트림 업로드에 쓸 수 있는 가용 대역과는 다를 수 있음.
- **WebRTC glass-to-glass 지연**: 약 200~500ms(최적 조건 <200ms). **LL-HLS**: 2~5초. **전통 HLS**: 15~30초. (출처: [forasoft.com](https://www.forasoft.com/blog/article/webrtc-video-steaming-app-vs-hls), [mux.com](https://www.mux.com/articles/low-latency-live-streaming-developers-guide-ll-hls-webrtc-cmaf), [videosdk.live](https://www.videosdk.live/blog/hls-vs-webrtc), 2026-09-08 — 3개 소스 교차확인, 벤더/기술블로그성이라 1차 표준기구 벤치마크는 아님, **가능성**)
- **microSD 고내구 카드**: SanDisk High Endurance 20,000시간 등급, Transcend High Endurance 240,000시간(Full HD 연속 녹화 기준), Samsung Pro Endurance는 일반 카드 대비 최대 25배 수명 주장. 정량 TBW 스펙 원문은 제조사 스펙시트로 직접 확인하지 못함. (출처: [tomshardware.com](https://www.tomshardware.com/best-picks/raspberry-pi-microsd-cards), [Raspberry Pi 포럼](https://forums.raspberrypi.com/viewtopic.php?t=317568), 2026-09-08 — **가능성**, 제조사 공식 스펙 재확인 필요)
- **Pi 4/5 SD카드 쓰기 내구 공식 벤치마크**: 라즈베리파이 재단 공식 수치는 이번 조사에서 찾지 못함 — **미확인**.

## 정량 근거 모음 (후속 단계용)

| 항목 | 값 | 출처 URL | 확인일 |
|---|---|---|---|
| ONVIF Profile S 신규 컨포먼스 종료 | 2027-03-31 이후 불가 (발표 2025-10-09) | https://www.onvif.org/?post_type=pressrelease&p=8621 | 2026-09-08 |
| ONVIF Profile T 특징 | H.265+H.264, 이미징, 모션/탬퍼 이벤트, 메타데이터, 양방향 오디오 | https://www.onvif.org/profiles/ | 2026-09-08 |
| ONVIF PTZ Service Spec 최신 버전 | v26.06 | https://www.onvif.org/specs/srv/ptz/ONVIF-PTZ-Service-Spec.pdf | 2026-09-08 |
| 영상정보 보관기간 기본 권고 | 수집 후 30일 이내(표준 개인정보 보호지침 제41조제2항, 원문 미인출) | https://www.privacy.go.kr/front/bbs/bbsView.do?bbsNo=BBSMSTR_000000000049&bbscttNo=20779 | 2026-09-08 |
| 접속기록 보관기간 | 원칙 1년, 5만명↑ 또는 고유식별정보/민감정보 처리시 2년 | https://itwiki.kr/w/개인정보처리시스템_접속기록 | 2026-09-08 |
| 영상 파기 시한 | 보관기간 만료 후 5일 이내("지체없이") | https://www.catchsecu.com/archives/16550 | 2026-09-08 |
| Frigate GitHub 스타 | 35.7k | https://github.com/blakeblackshear/frigate | 2026-09-08 |
| go2rtc GitHub 스타 | 14.1k | https://github.com/AlexxIT/go2rtc | 2026-09-08 |
| MediaMTX GitHub 스타 | 20.1k | https://github.com/bluenviron/mediamtx | 2026-09-08 |
| Scrypted GitHub 스타 | 5.9k | https://github.com/koush/scrypted | 2026-09-08 |
| ZoneMinder GitHub 스타 | 5.9k | https://github.com/ZoneMinder/zoneminder | 2026-09-08 |
| MotionEye GitHub 스타 | 4.7k | https://github.com/motioneye-project/motioneye | 2026-09-08 |
| coturn GitHub 스타 | 약 14.0k(13,998~14.1k) | https://github.com/coturn/coturn | 2026-09-08 |
| python-onvif-zeep-async GitHub 스타 | 50 | https://github.com/openvideolibs/python-onvif-zeep-async | 2026-09-08 |
| Pi 5(BCM2712) H.264/H.265 하드웨어 인코더 | 없음(소프트웨어 libx264로 인코딩) | https://forums.raspberrypi.com/viewtopic.php?t=391283 | 2026-09-08 |
| Pi 5 RTC 배터리 커넥터 | 있음('BAT' 커넥터, ML2020 충전식 리튬) | https://www.raspberrypi.com/products/rtc-battery/ | 2026-09-08 |
| RAUC 바이너리 크기 | 약 512KB (SWUpdate 1.3MB, Mender 6.9MB 대비 최소) | https://32blog.com/en/yocto/yocto-ota-update-comparison | 2026-09-08 |
| Mender 무료 티어 제약 | 델타 업데이트 등 Enterprise/Professional 전용 | https://proteanos.com/doc/ota-updates-rauc-swupdate-mender-2026/ | 2026-09-08 |
| 1080p H.264 메인스트림 비트레이트 | 약 2~5 Mbps | https://www.hikvision.com/content/dam/hikvision/ca/faq-document/H.2645-&-H.2645-Recommended-Bit-Rate-at-General-Resolutions.pdf | 2026-09-08 |
| 서브스트림(CIF/D1) 비트레이트 | 약 512 kbps | https://www.unifore.net/ip-video-surveillance/simple-guide-of-ip-camera-bitrate-setting.html | 2026-09-08 |
| 한국 가정 평균 업로드 속도(2025) | 약 211 Mbps | (검색 스니펫, 1차 통계 미확인) | 2026-09-08 |
| WebRTC glass-to-glass 지연 | 약 200~500ms | https://www.mux.com/articles/low-latency-live-streaming-developers-guide-ll-hls-webrtc-cmaf | 2026-09-08 |
| LL-HLS glass-to-glass 지연 | 약 2~5초 | https://www.videosdk.live/blog/hls-vs-webrtc | 2026-09-08 |
| 전통 HLS glass-to-glass 지연 | 약 15~30초 | https://www.forasoft.com/blog/article/webrtc-video-steaming-app-vs-hls | 2026-09-08 |
| SanDisk High Endurance 등급 | 20,000시간(연속 녹화 기준) | https://www.tomshardware.com/best-picks/raspberry-pi-microsd-cards | 2026-09-08 |
| Transcend High Endurance 등급 | 240,000시간(Full HD 연속 녹화 기준) | https://www.tomshardware.com/best-picks/raspberry-pi-microsd-cards | 2026-09-08 |

**미확인으로 남은 핵심 항목 (후속 조사 필요):**
1. 표준 개인정보 보호지침 제41조·안전성 확보조치 기준의 정확한 조문 원문(law.go.kr JS 렌더링으로 이번 조사에서 직접 인출 실패)
2. Frigate/Scrypted/ZoneMinder/MotionEye의 정확한 최근 커밋 날짜, 공식 Pi 지원 여부, PTZ 지원 세부
3. python-onvif-zeep(FalkTannhaeuser) 및 node-onvif의 최근 커밋일·유지보수 활성도
4. 서버 백엔드 프레임워크(Go/FastAPI/Node) 3자 비교 — 이번 조사에서 전용 검색 미수행
5. balenaOS 무료 티어 정확한 조건
6. TURN 릴레이 대역폭 비용 정량 수치
7. Raspberry Pi OS overlayfs 공식 documentation 원문(현재는 커뮤니티 소스 기반)
8. microSD TBW 제조사 공식 스펙시트 수치
9. 한국 가정 평균 업로드 속도의 1차 통계기관(NIA 등) 공식 수치

## 스택 후보 fit 판정 (메인 · 세션 모델 · 2026-09-08)

전제(사용자 제약 = 00-seed 해석): 팀 1~2명 · 사이트 ≤10 · 카메라 ≤4/사이트 · 동시 시청 ≤5 · 자가 운영 · 소상공인 예산(서버 VPS 1대).
메인 추가 확인 5회(WebFetch): HA ONVIF 통합 문서·core manifest, FastAPI 저장소, raspberrypi.com 설정 문서, raspi-config 소스.
fit은 `evidence-map.md` 5요소(요구충족·팀·운영환경·생태계·총비용) 중 **결정 요소**를 한 줄로 밝힌다. 인기 지표는 근거의 하나일 뿐이다.

| 영역 | 선택 | fit | 결정 요소 | 기각 대안 (fit · 이유) |
|---|---|---|---|---|
| 스트림 중계 | **go2rtc** (Pi 상주) | 높음 | 요구충족 — H.264 무재인코딩 패스스루 + WebRTC/MSE/HLS 동시 출력이 Pi 5 HW 인코더 부재 제약을 정면으로 피한다. 운영환경 — ARM 단일 바이너리. 생태계 — MIT·14.1k★·Frigate가 채택 | MediaMTX(중간 — SRT/RTMP 등 프로토콜 폭은 넓으나 본 서비스에 불필요, 브라우저 WebRTC 문서화는 go2rtc가 명확) · 직접 구현(낮음 — 검증된 OSS 대비 재작업 위험) |
| 엣지 HW | **Raspberry Pi 5 (4GB)** 기준 사양 | 높음 | 운영환경 — 온보드 RTC 배터리 커넥터가 P1 "시계 드리프트" 항목을 HW로 해소. 요구충족 — 패스스루 설계라 HW 인코더가 필요 없어 Pi 5의 부재가 결격이 아니다. CPU 여유 = ONVIF 이벤트 구독 + 스냅샷 + 중계 | Pi 4(중간 — HW 인코더는 있으나 RTC 모듈 추가 구매·별도 검증 필요 → MVP 지원 대상 제외, non-goal) |
| 엣지 에이전트 언어·ONVIF | **Python 3.12 + onvif-zeep-async** (openvideolibs) | 높음 | 생태계 — Home Assistant ONVIF 통합의 공식 requirements가 `onvif-zeep-async==4.2.1` (HA core `components/onvif/manifest.json`, 2026-09-08 확인). 스타 50이지만 대형 프로젝트가 의존·유지하며 PTZ 액션도 그 위에서 동작. 팀 — 서버와 언어 통일 | python-onvif-zeep FalkTannhaeuser(낮음 — 활성도 미확인·동기 API) · node-onvif(낮음 — 활성도 미확인) · Go 자체 구현(낮음 — 성숙한 ONVIF 클라이언트 부재) |
| 제어·상태 채널 | **MQTT** (mosquitto, TLS 8883, Pi→서버 아웃바운드) | 높음 | 요구충족 — LWT로 기기 오프라인 감지, QoS1 명령 전달, retained 상태가 프로토콜 내장 → 하트비트·ACK·프레즌스 자체 구현 불필요. 운영환경 — NAT 뒤 아웃바운드 상시 연결(P1 원격 접근 기본값) | WebSocket 단독(중간 — 프레즌스·재전송을 직접 구현) · 포트포워딩(기각 — P1 위반) |
| 원격 시청 NAT 통과 | **WebRTC**(go2rtc) + **coturn** TURN 폴백, 시그널링은 서버 경유 | 높음 | 요구충족 — WebRTC 200~500ms가 "실시간" 상수를 만족하는 유일한 후보(LL-HLS 2~5초). 생태계 — coturn BSD-3·약 14k★ | LL-HLS(중간 — 폴백·녹화 재생 용도 P2) |
| 서버 백엔드 | **Python 3.12 + FastAPI** | 높음 | 팀 — 엣지 에이전트와 같은 언어, ONVIF 모델·이벤트 스키마(pydantic)를 공유. 생태계 — MIT·102.2k★(2026-09-08 확인). 요구충족 — WebSocket·비동기 내장 | Go(중간 — 단일 바이너리 장점 < 언어 2개 운영 부담) · Node(중간 — 동일) |
| 서버 DB | **PostgreSQL 16** | 높음 | 요구충족 — 접속기록 1년 보관·다중 사이트 동시 쓰기·pg_dump/PITR 성숙 | SQLite 서버(중간 — 규모상 가능하나 온라인 백업·동시 쓰기 운영 부담) |
| 엣지 로컬 저장 | **SQLite(WAL)** 이벤트 인덱스·명령 큐 + 클립은 **USB SSD** 파일 | 높음 | 운영환경 — 무서버·전원 차단 내성(WAL). SD 마모 — DB·클립을 SD가 아닌 USB SSD에 둔다 | SD 카드 저장(기각 — P1 SD 마모) |
| 엣지 OS 안정화 | **Raspberry Pi OS Lite 64-bit** + raspi-config **Overlay File System**(공식 메뉴 "P3 Overlay File System — Enable/disable read-only file system", raspi-config 소스 2026-09-08 확인) + log2ram + HW watchdog | 높음 | 운영환경 — 공식 도구로 read-only rootfs 구성 → 커스텀 이미지 빌드 불필요 | Yocto/Buildroot(낮음 — 팀 1~2명에 과잉) |
| OTA | **앱 계층 A/B**: 컨테이너 이미지 digest 고정 + 헬스체크 실패 시 이전 digest 자동 롤백 + 이미지 서명 검증. OS는 unattended-upgrades security만 | 중간 | 총비용 — 사이트 ≤10·유인 현장 전제에서 OS A/B 파티션 도구 도입 비용 > 기대 이득. **OS 계층 벽돌은 잔여 리스크**(A4 Accept 사유 기재, Q5) | RAUC(중간 — 가장 가볍지만 Pi OS 부트로더(tryboot) 통합 검증 필요) · Mender(낮음 — 델타 등 유료) · balena(미확인 — 무료 티어 미확인) |
| 알림 | **웹 푸시(VAPID)** + 이메일 | 중간 | 총비용 — 네이티브 앱 없이 브라우저 푸시로 충분, 외부 SaaS 의존 최소 | 카카오 알림톡/SMS(P2 — 발신 사업자 인증·건당 비용) |
| 웹 프론트엔드·TLS 종단 | **React + Tailwind + Zustand (PWA)** + **Caddy** | 높음 | 팀 — 1~2인 팀이 이미 쓰는 프론트 스택(dial·토큰 규약 보유)이라 학습 비용 0. 요구충족 — PWA로 네이티브 앱 없이 웹 푸시(FR-017)·홈 화면 설치. 운영환경 — Caddy 자동 TLS로 인증서 갱신 무인화 | Vue/Svelte(중간 — 기능 동등하나 팀 스택 밖) · Nginx(중간 — TLS 갱신 자동화를 별도로 얹어야 함) · 네이티브 앱(낮음 — 2개 스토어 배포 비용, seed 범위 밖) |

**fit에서 파생된 하드 제약 (03 가정 목록으로 전파)**
- 카메라는 **H.264 스트림을 최소 1개** 제공해야 한다. HA ONVIF 문서: "the ONVIF integration looks for H.264 (AVC) video streams" — H.265 전용 카메라는 브라우저 WebRTC 재생도 제한된다 (2026-09-08 확인).
- Pi에서 **재인코딩 금지** — 모든 경로가 패스스루. 해상도 변환은 카메라의 서브스트림으로 해결한다.
- 서버 1대(VPS) + coturn 1대. 고가용성은 non-goal.
