# Seed - Raspberry Pi IP Camera Control & Monitoring

- 원문: 라즈베리파이로 IP 카메라를 제어하고 실시간 모니터링하는 서비스를 만들고 싶다.
- 서비스 유형: IoT/엣지 + 현장 모니터링 + 경량 SaaS/온프레미스 혼합
- 주 도메인: IP CCTV/영상 모니터링, 엣지 게이트웨이
- 인접 도메인: SCADA/BMS 관제, IoT fleet 운영, 영상 스트리밍
- 감지된 제약: Raspberry Pi 사용, IP 카메라 제어, 실시간 모니터링
- 로드할 블라인드스팟 프로파일: P1 IoT/엣지 디바이스, P3 관제/SCADA/BMS
- 자동 채택한 기본 방향: Raspberry Pi는 카메라 자체가 아니라 현장 엣지 게이트웨이로 사용한다. IP 카메라는 ONVIF Profile T 우선, Profile S 호환을 보조로 지원한다. 실시간 브라우저 모니터링은 WebRTC, 저빈도/호환 보기와 녹화 재생은 RTSP/HLS 경로로 분리한다.
