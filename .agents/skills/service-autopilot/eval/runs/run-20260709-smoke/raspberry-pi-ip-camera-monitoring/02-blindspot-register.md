# Blindspot Register - Raspberry Pi IP Camera Control & Monitoring

범위: 공통 10축 + STRIDE 6범주 + P1 IoT/엣지 + P3 관제/SCADA/BMS.
사용자 질문 금지 조건에 따라 모든 질문 후보는 추천안을 자동 채택했다.

| 축/항목 | 상태 | 처리 | 근거/출처 |
|---|---|---|---|
| 기능 범위 | Assumed | MVP는 카메라 등록/검색, 실시간 보기, PTZ/프리셋, 스냅샷, 상태 알림, 감사로그로 제한 | Seed |
| 도메인 데이터 모델 | Assumed | Gateway, Camera, StreamSession, ControlCommand, Event, AuditLog, User/Role 중심 | ONVIF Profile T/S |
| UX 흐름 | Assumed | 관제 대시보드, 카메라 상세, 설치 마법사, 장애/로그 화면 | 관제 도메인 |
| 비기능 목표 | Assumed | 실시간 시작 3초 이내, 명령 ack 1초 이내, gateway heartbeat 10초 | SRE SLI/SLO |
| 통합/프로토콜 | Assumed | ONVIF Profile T 우선, Profile S 호환, RTSP ingest, WebRTC egress, MQTT/WebSocket command channel | ONVIF, WebRTC/RTSP 실무 패턴 |
| 실패 처리 | Assumed | 중복 명령은 idempotency key로 제거, offline gateway는 명령 거절 또는 큐 정책 명시 | Stripe idempotency |
| 제약/트레이드오프 | Assumed | Pi 4/5 이상, 유선 LAN 권장, 폐쇄망 설치 우선 | Mender Pi production |
| 용어 | Clear | IP camera, gateway, stream session, PTZ, preset, event 용어 사용 | ONVIF |
| 완료 신호 | Assumed | SC-001~SC-008 성공 기준 통과 | PRD |
| 비용/라이선스 | Assumed | 오픈소스 구성요소는 Apache/MIT/BSD 우선, GPL 컴포넌트는 배포 영향 검토 전까지 제외 | 배포 리스크 |
| P1 SD 카드 마모 | Assumed | 로그/버퍼는 tmpfs 또는 중앙 전송, 영구 DB는 SSD/NVMe 권장, noatime 적용 | Mender, dzombak SD wear |
| P1 전원 차단 | Assumed | read-only rootfs 또는 쓰기 최소화, UPS 옵션, 부팅 시 fsck/상태 복구 | Mender |
| P1 watchdog | Assumed | systemd watchdog + 하드웨어 watchdog, 무한 재부팅 방지 backoff | Mender |
| P1 OTA 업데이트 | Assumed | A/B 이미지, 서명 검증, 단계 배포, 자동 rollback | AWS IoT Lens |
| P1 시간 동기화 | Assumed | NTP 가능 환경 우선, 폐쇄망은 RTC 모듈 권장 | Pi 현장 운영 패턴 |
| P1 장치 신원 | Assumed | gateway별 X.509 인증서, 카메라 자격증명은 게이트웨이 로컬 암호화 저장 | AWS IoT Lens |
| P1 연결 단절 | Assumed | gateway 로컬 모드 유지, 이벤트/감사로그는 bounded buffer 후 재전송 | IoT Lens |
| P1 영상 스트리밍 | Assumed | WebRTC 저지연 보기, HLS는 호환/녹화 재생용 | WebRTC/RTSP 실무 |
| P1 원격 접근 | Assumed | inbound port-forward 금지, gateway outbound tunnel 또는 broker 연결 | 보안 기본값 |
| P1 프로비저닝 | Assumed | 이미지 플래싱 + enrollment token + 최초 접속 시 인증서 발급 | IoT Lens |
| P3 폐쇄망 | Assumed | 단일 번들 설치, 외부 CDN/API 의존 없음 | 현장 관제 운영 |
| P3 실시간성 | Assumed | 영상 glass-to-glass p95 700ms 이하를 목표, PTZ ack p95 1초 이하 | 관제 UX |
| P3 알람 폭주 | Assumed | 카메라 offline/복구 이벤트는 그룹핑·쿨다운 적용 | SRE alerting |
| P3 프로토콜 | Assumed | ONVIF 편차는 adapter로 격리, 장치 capability matrix 보관 | ONVIF |
| P3 이력 증가 | Assumed | 영상 원본은 기본 저장하지 않고 이벤트/스냅샷 중심, 녹화는 선택 기능 | 저장비용 절감 |
| P3 무중단 | Assumed | 중앙 API 재시작 중 gateway 로컬 보기 유지, 서버 복구 후 재동기화 | 엣지 설계 |

## 질문 후보와 자동 채택

### Q1. Raspberry Pi의 역할은 무엇인가?

| 옵션 | 내용 | 근거/트레이드오프 |
|---|---|---|
| A (추천) | Pi는 IP 카메라 제어/스트림 변환 엣지 게이트웨이로 사용 | 기존 IP 카메라 자산 활용, ONVIF/RTSP 호환성 높음 |
| B | Pi 자체를 카메라로 사용 | 하드웨어 단가는 낮지만 IP 카메라 제어 요구와 어긋남 |

채택: A. decision-log D-001.

### Q2. 실시간 스트리밍 방식은 무엇인가?

| 옵션 | 내용 | 근거/트레이드오프 |
|---|---|---|
| A (추천) | RTSP ingest + WebRTC live egress, HLS 보조 | 저지연과 브라우저 호환 균형 |
| B | HLS만 사용 | 구현은 단순하나 지연이 커서 PTZ 관제에 부적합 |
| C | MJPEG 사용 | 단순하지만 대역폭과 확장성 불리 |

채택: A. decision-log D-002.

### Q3. 배포 환경은 무엇인가?

| 옵션 | 내용 | 근거/트레이드오프 |
|---|---|---|
| A (추천) | 온프레미스/폐쇄망 우선, 클라우드 릴레이 선택 | 시설 영상 보안과 현장 관제 맥락에 적합 |
| B | 클라우드 SaaS 우선 | 운영 편의는 높으나 영상 반출/망분리 리스크 |

채택: A. decision-log D-003.

### Q4. ONVIF 지원 기준은 무엇인가?

| 옵션 | 내용 | 근거/트레이드오프 |
|---|---|---|
| A (추천) | Profile T 우선, Profile S 호환 | ONVIF의 최신 권고와 기존 장비 호환 균형 |
| B | Profile S만 지원 | 구형 호환은 좋지만 보안/기능 최신성 부족 |

채택: A. decision-log D-004.

### Q5. 영상 저장은 MVP에 포함하는가?

| 옵션 | 내용 | 근거/트레이드오프 |
|---|---|---|
| A (추천) | MVP는 실시간+스냅샷+이벤트만, 연속 녹화는 P2 | 저장장치/개인정보/복구 범위 폭증 방지 |
| B | 연속 녹화 포함 | NVR 경쟁력은 높으나 MVP 리스크 큼 |

채택: A. decision-log D-005.

## 반영 기록

- D-001~D-005는 PRD 범위, API 계약, 테스트 설계, 운영 설계에 반영했다.
- 미해결 질문은 0건이다. 사용자별 예산/카메라 대수는 가정값으로 표기하고 구현 전 site sizing 입력으로 검증한다.
