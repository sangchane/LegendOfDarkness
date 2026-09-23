# Recon - Raspberry Pi IP Camera Control & Monitoring

조사일: 2026-07-09

## 도메인 업무 흐름

1. 현장 관리자가 Raspberry Pi 게이트웨이를 설치하고 카메라 네트워크에 연결한다.
2. 게이트웨이는 같은 LAN의 ONVIF 카메라를 검색하거나 수동 등록한다.
3. 사용자는 웹 대시보드에서 실시간 영상을 보고 PTZ/프리셋/스냅샷/녹화 요청을 보낸다.
4. 게이트웨이는 카메라 제어 명령과 스트림 변환을 수행하고, 중앙 서버는 사용자/권한/감사로그/장치 상태를 관리한다.
5. 네트워크 단절 시 로컬 모니터링과 제한된 제어를 유지하고, 연결 복구 후 상태와 이벤트를 동기화한다.

## 이해관계자

- 시설 운영자: 실시간 영상 확인, PTZ 제어, 장애 알림 확인.
- 현장 설치 기사: Pi 이미지 플래싱, 카메라 검색, 네트워크/시간 설정.
- 보안 관리자: 사용자 권한, 접근 감사, 영상 접근 정책.
- 시스템 운영자: Pi fleet 상태, OTA 업데이트, 로그/백업/복구.
- 최종 고객/건물주: 폐쇄망 또는 온프레미스 운영, 개인정보/영상 반출 통제.

## 규제와 표준

- ONVIF Profile T는 IP 기반 영상 시스템용 프로파일이며 H.264/H.265, 이미지 설정, 모션/탬퍼링 이벤트, PTZ 제어 등을 다룬다. Profile S는 IP 영상 스트리밍과 제어의 기존 호환 기반이나, ONVIF는 Profile S 지원 종료와 Profile T 권고를 발표했다.
- Raspberry Pi 공식 문서는 카메라 모듈과 libcamera 기반 촬영을 다루지만, IP 카메라 VMS/ONVIF 클라이언트 전체 스택을 제공하지 않는다.
- Pi 기반 제품을 현장에 배치할 때는 SD 카드 쓰기, 전원 차단, watchdog, OTA/rollback, 장치별 인증서가 주요 운영 리스크다.
- 영상 서비스는 개인정보와 시설 보안 데이터를 다루므로 최소 권한, 감사로그, 암호화 저장/전송, 접근 만료가 필수다.

## 유사 도구와 적용 가능성

| 도구/제품 | 적용 | 한계 |
|---|---|---|
| Frigate/NVR 계열 | RTSP 카메라 수집, 이벤트 감지, 로컬 운영에 강함 | 본 아이디어의 핵심인 BMS식 권한/감사/장치 fleet 운영은 별도 구현 필요 |
| MediaMTX | RTSP/WebRTC/HLS 변환 게이트웨이에 적합 | 사용자/권한/카메라 관리 API는 외부에서 구현 필요 |
| MotionEye 계열 | Pi 기반 간단 모니터링에 적합 | 실시간 저지연, ONVIF PTZ, 운영 등급 OTA에는 부족 |
| Home Assistant 카메라 통합 | 홈/소형 현장 통합에 적합 | 상업용 다중 테넌트/감사/권한 모델은 별도 필요 |
| 기존 CCTV 모듈 | 기존 HLS/WebRTC CCTV 계획과 접점 | 본 파이프라인은 독립 서비스 기준이며, 구현 시 기존 관제 시스템 통합 범위를 다시 확정해야 함 |

## 스택 후보

| 후보 | 근거 | 트레이드오프 | fit |
|---|---|---|---|
| Pi Gateway: Go 또는 Node.js + MediaMTX + ONVIF 클라이언트 | 엣지에서 낮은 오버헤드와 스트림 변환 분리 가능 | ONVIF 장치별 편차 대응 필요 | 높음 |
| 중앙 API: Spring Boot/Java 또는 FastAPI | 사용자/권한/감사/장치 관리에 안정적 | 작은 MVP에는 무거울 수 있음 | 중간 |
| 실시간 UI: React + WebRTC player | 브라우저 접근성, 관제 UI 구성 용이 | WebRTC signaling/ICE 운영 필요 | 높음 |
| DB: PostgreSQL | 장치/감사/상태 이력 저장에 적합 | 온프레미스 설치 스크립트 필요 | 높음 |
| 메시징: MQTT 또는 WebSocket | Pi가 outbound 연결만 유지 가능 | 명령 멱등성/순서 보장 설계 필요 | 높음 |

## 주요 출처

- Raspberry Pi camera software: https://www.raspberrypi.com/documentation/computers/camera_software.html
- ONVIF Profile T: https://www.onvif.org/profiles/profile-t/
- ONVIF Profile S: https://www.onvif.org/profiles/profile-s/
- ONVIF Profile S support end notice: https://www.onvif.org/?p=8621&post_type=pressrelease
- Mender Raspberry Pi in production: https://mender.io/blog/raspberry-pi-in-production
- AWS IoT Lens: https://docs.aws.amazon.com/wellarchitected/latest/iot-lens/
- Google SRE SLO/monitoring: https://sre.google/sre-book/service-level-objectives/ , https://sre.google/sre-book/monitoring-distributed-systems/
- Zalando REST API Guidelines: https://opensource.zalando.com/restful-api-guidelines/
- RFC 9457 Problem Details: https://www.rfc-editor.org/rfc/rfc9457
- Stripe idempotent requests: https://docs.stripe.com/api/idempotent_requests
