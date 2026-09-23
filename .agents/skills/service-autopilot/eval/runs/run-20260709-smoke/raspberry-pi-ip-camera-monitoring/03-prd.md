# PRD - Raspberry Pi IP Camera Control & Monitoring

## 배경

시설 현장에는 이미 RTSP/ONVIF IP 카메라가 설치된 경우가 많다. Raspberry Pi는 저렴한 현장 게이트웨이로 배치하기 쉽지만, 생산 운영에서는 SD 카드 마모, 전원 차단, 장치 인증, OTA rollback, 시간 동기화가 핵심 리스크다. 실시간 관제는 단순 HLS보다 WebRTC 같은 저지연 경로가 필요하며, ONVIF는 Profile T 우선과 Profile S 호환 전략이 합리적이다.

## 제품 목표

1. 현장 운영자가 브라우저에서 IP 카메라 영상을 3초 이내에 열고 PTZ 명령 결과를 1초 이내에 확인한다.
2. 설치 기사가 Raspberry Pi 게이트웨이를 15분 이내에 등록하고 같은 LAN의 ONVIF 카메라를 검색/등록한다.
3. 보안 관리자가 사용자별 카메라 접근·제어 권한과 감사로그를 확인한다.

## 사용자 스토리

- P1: As a 시설 운영자, I want 실시간 카메라 영상을 낮은 지연으로 보고 PTZ를 제어한다, so that 현장 이상 상황을 즉시 확인한다.
- P1: As a 설치 기사, I want Pi gateway가 ONVIF 카메라를 검색하고 capability를 표시한다, so that 장치 등록 시간을 줄인다.
- P1: As a 보안 관리자, I want 사용자별 보기/제어 권한과 감사로그를 관리한다, so that 영상 접근 책임을 추적한다.
- P2: As a 운영자, I want 이벤트 스냅샷과 짧은 클립을 조회한다, so that 장애 원인을 사후 확인한다.
- P2: As a 시스템 운영자, I want gateway fleet 상태와 업데이트 진행률을 본다, so that 현장 출동을 줄인다.

## 요구사항

| ID | 요구사항 | 우선순위 | 출처 |
|---|---|---|---|
| FR-001 | 게이트웨이 enrollment token으로 Pi를 등록하고 장치별 인증서를 발급한다 | P0 | D-003 |
| FR-002 | ONVIF Profile T/S 카메라 검색, 수동 등록, capability 조회를 제공한다 | P0 | D-004 |
| FR-003 | RTSP ingest를 WebRTC live session으로 변환해 브라우저에서 표시한다 | P0 | D-002 |
| FR-004 | PTZ move/stop/preset 명령을 권한 검증 후 카메라에 전달하고 ack를 기록한다 | P0 | D-001 |
| FR-005 | 사용자/역할/카메라 그룹별 보기·제어 권한을 적용한다 | P0 | 보안 |
| FR-006 | 모든 보기 시작, 스냅샷, PTZ 명령, 로그인, 설정 변경을 감사로그로 남긴다 | P0 | 보안 |
| FR-007 | gateway heartbeat, camera online/offline, stream session 상태를 표시한다 | P1 | 운영 |
| FR-008 | 스냅샷 캡처와 이벤트 이미지를 저장하고 조회한다 | P1 | D-005 |
| FR-009 | 네트워크 단절 중 gateway 로컬 모드와 bounded event buffer를 제공한다 | P1 | P1/P3 |
| FR-010 | OTA 업데이트는 서명 검증, staged rollout, rollback 상태를 기록한다 | P2 | 운영 |
| FR-011 | 연속 녹화/NVR 보관 정책을 제공한다 | P2 | D-005 |

## 유스케이스

### UC-001 실시간 보기

Given 운영자가 카메라 보기 권한을 보유하고 gateway가 online일 때  
When 운영자가 카메라를 선택한다  
Then WebRTC 세션이 생성되고 p95 3초 이내에 첫 프레임이 표시된다.

### UC-002 PTZ 제어

Given 운영자가 카메라 제어 권한을 보유하고 카메라가 PTZ capability를 제공할 때  
When 운영자가 pan/tilt/zoom 또는 preset 명령을 보낸다  
Then 명령은 idempotency key와 함께 gateway로 전달되고 p95 1초 이내 ack 또는 실패 사유가 표시된다.

### UC-003 설치 등록

Given 설치 기사가 enrollment token을 보유하고 Pi가 서버에 outbound 연결할 수 있을 때  
When 설치 기사가 gateway 등록을 완료한다  
Then 서버는 인증서를 발급하고 gateway는 ONVIF discovery 결과를 보고한다.

### UC-004 권한 차단

Given 사용자가 특정 카메라 제어 권한이 없을 때  
When 사용자가 PTZ 명령 API를 호출한다  
Then 서버는 403 Problem JSON을 반환하고 거부 감사로그를 남긴다.

## 성공 기준

| ID | 기준 | Pass/Fail |
|---|---|---|
| SC-001 | 단일 gateway, 4개 1080p 카메라 환경에서 live session p95 시작 시간이 3초 이하 | 자동 측정 |
| SC-002 | PTZ 명령 ack p95가 1초 이하이고 실패 시 원인 코드가 표시됨 | 자동 측정 |
| SC-003 | 권한 없는 카메라 보기/제어 요청은 100% 차단되고 감사로그가 남음 | 테스트 |
| SC-004 | gateway가 60초 이상 offline이면 UI와 알림에 offline 상태가 표시됨 | 테스트 |
| SC-005 | 카메라 자격증명과 gateway 인증서는 평문 저장되지 않음 | 보안 테스트 |
| SC-006 | 동일 idempotency key의 PTZ 명령 재시도는 중복 실행되지 않음 | 통합 테스트 |
| SC-007 | 브라우저 새로고침/네트워크 재연결 후 stream session이 정리됨 | E2E |
| SC-008 | 스냅샷 파일은 권한 검사를 통과한 요청에만 다운로드됨 | E2E/보안 |

## UI 방향

- 첫 화면은 관제 대시보드다. 카메라 그리드, 좌측 그룹 트리, 우측 이벤트/상태 패널을 배치한다.
- 영상 카드에는 live/offline/degraded 상태, latency, 제어 가능 여부를 아이콘과 짧은 상태 텍스트로 표시한다.
- PTZ 컨트롤은 카메라 상세 패널에 고정하고, 권한이 없거나 capability가 없으면 비활성 사유를 표시한다.
- 설치 화면은 gateway 등록, 네트워크 상태, discovery 결과, 카메라 매핑의 4단계로 구성한다.

## 범위 밖

- MVP에서 연속 녹화/NVR 장기 보관은 제외한다.
- 얼굴 인식, 객체 탐지, 침입 탐지 AI는 제외한다.
- 카메라 펌웨어 업데이트 대행은 제외한다.
- 공인 인터넷에 카메라 포트를 직접 노출하는 기능은 제공하지 않는다.

## 가정 목록

- Pi는 4GB RAM 이상의 Raspberry Pi 4/5, 유선 LAN 사용을 권장한다.
- 카메라는 ONVIF Profile T 또는 S와 RTSP 스트림을 제공한다.
- 설치 현장은 온프레미스/폐쇄망 우선이며, 클라우드 릴레이는 선택 기능이다.
- MVP는 gateway당 4개 1080p live stream 동시 표시를 기준으로 sizing한다.

## 미해결

0건.
