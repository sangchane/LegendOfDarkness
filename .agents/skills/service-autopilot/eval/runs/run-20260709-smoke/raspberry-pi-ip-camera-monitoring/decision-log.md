# Decision Log - Raspberry Pi IP Camera Control & Monitoring

| ID | 결정 | 버린 대안 | 이유 | 반영 문서 |
|---|---|---|---|---|
| D-001 | Raspberry Pi는 IP 카메라 엣지 게이트웨이로 사용 | Pi 자체를 카메라로만 사용 | 사용자의 “IP 카메라 제어” 요구와 기존 카메라 자산 활용에 적합 | 03, 04, 05 |
| D-002 | RTSP ingest + WebRTC live egress, HLS 보조 | HLS-only, MJPEG-only | PTZ 관제에 필요한 저지연과 브라우저 호환 균형 | 03, 04, 05, 06 |
| D-003 | 온프레미스/폐쇄망 우선, 클라우드 릴레이 선택 | 클라우드 SaaS 우선 | 시설 영상 보안, SCADA/관제 맥락, 현장망 조건에 적합 | 03, 04, 07 |
| D-004 | ONVIF Profile T 우선, Profile S 호환 | Profile S only | Profile T가 최신 영상/이벤트/PTZ 기능과 보안 방향에 더 적합하고 S는 기존 장비 호환용 | 01, 03, 04, 05 |
| D-005 | MVP에서 연속 녹화 제외, 스냅샷/이벤트만 포함 | NVR 장기 녹화 포함 | 저장/개인정보/성능/복구 범위가 MVP를 과도하게 키움 | 03, 05, 07 |
| D-006 | Gateway outbound channel 사용, 카메라 포트포워딩 금지 | 공인 포트 개방 | 보안·폐쇄망·현장 NAT 조건에 안전 | 04, 07 |
| D-007 | Gateway별 X.509 인증서와 credential 암호화 저장 | 공유 API key 또는 평문 config | 장치 1대 유출이 전체 fleet 침해로 번지는 것을 방지 | 04, 05, 07 |
| D-008 | Audit log는 append-only로 설계 | 관리자 삭제 허용 | 영상 접근/제어 책임 추적 필요 | 03, 05, 06 |
| D-009 | Pi 저장장치는 SSD/NVMe 권장, SD 쓰기 최소화 | SD에 로그/DB 지속 쓰기 | 현장 출동과 파일시스템 손상 리스크 축소 | 02, 07 |
| D-010 | readiness 판정은 CONCERNS | PASS | 실제 카메라 호환성과 Pi 성능은 하드웨어 smoke 전 확정 불가 | 08 |
