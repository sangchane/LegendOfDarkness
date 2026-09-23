# Blindspot Register - 요양시설 낙상 감지 알림 서비스

범위: 공통 10축 + STRIDE 6범주 + IoT/엣지 + B2B SaaS + 모바일 + 관제 알림

| 축/항목 | 상태 | 처리 | 근거·출처 |
|---|---|---|---|
| 기능 범위 | Assumed | MVP는 감지, 직원 확인, 보호자 알림, 대응 기록으로 제한 | Seed |
| 도메인 데이터 모델 | Assumed | facility, resident, guardian, staff, sensor, fall_event, alert_delivery, response_log | 요양시설 운영 흐름 |
| UX 흐름 | Assumed | 직원 우선 확인 후 보호자 알림 | 오탐 보호, 야간 대응 |
| 비기능 | Assumed | 직원 알림 p95 10초, 확정 이벤트 보호자 알림 p95 30초 | 생명안전 알림 기준을 보수적으로 설정 |
| 통합 | Assumed | nurse call/webhook/MQTT/FCM/SMS 게이트웨이 | FDA 분류 설명의 nurse call 연동 가능성 |
| 실패 처리 | Assumed | 중복 알림 억제, 미확인 escalaton, 오프라인 버퍼 | SRE 알림 원칙 |
| 제약 | Assumed | 폐쇄망 옵션, 영상 미사용 기본, 의료 판단 금지 | 개인정보·의료기기 리스크 축소 |
| 용어 | Clear | 낙상 후보, 확정 낙상, 오탐, 미확인, 장기 미응답 정의 | PRD에 반영 |
| 완료 신호 | Assumed | SC-001~SC-008 pass/fail 기준 | quality-decomposition 적용 |
| 비용·라이선스 | Assumed | 센서+게이트웨이 월 구독, SMS 종량 과금 | B2B SaaS 관행 |
| STRIDE Spoofing | Assumed | 센서별 X.509 또는 per-device key, 직원 MFA 옵션 | AWS IoT Lens 원칙 |
| STRIDE Tampering | Assumed | 이벤트 해시, append-only 감사로그 | 사고기록 신뢰성 |
| STRIDE Repudiation | Assumed | 알림 수신/확인/조치자/시각 기록 | CMS류 감독·기록 요구 |
| STRIDE Information Disclosure | Assumed | 보호자별 입소자 범위 권한, 영상 기본 제외 | 개인정보 최소수집 |
| STRIDE Denial of Service | Assumed | 알림 큐 분리, rate limit, storm suppression | SRE 알림 원칙 |
| STRIDE Elevation of Privilege | Assumed | 시설/입소자/보호자 스코프 RBAC | 멀티테넌트 SaaS |
| IoT 배터리 | Asked -> Assumed | 추천안 채택: 센서 배터리 6개월 이상 목표, 14일 전 교체 알림 | 운영 출동 비용 |
| IoT 오프라인 | Asked -> Assumed | 추천안 채택: 게이트웨이 로컬 버퍼 24시간, 복구 후 순서 보장 전송 | 폐쇄망·장애 대비 |
| IoT OTA | Assumed | 서명된 OTA, canary, 자동 롤백 | IoT 운영 기본값 |
| IoT 시각 동기화 | Assumed | 게이트웨이 NTP, 폐쇄망은 로컬 NTP | 감사로그 순서 |
| 모바일 알림 | Asked -> Assumed | 추천안 채택: 직원 앱 push + SMS fallback, 보호자는 앱/카카오/SMS 선택 | 알림 실패 대응 |
| 보호자 공개 범위 | Asked -> Assumed | 추천안 채택: 확정 낙상·장기 미확인만 보호자 알림, 원시 센서값 비공개 | 사생활·오탐 민원 축소 |
| 영상 사용 | Asked -> Assumed | 추천안 채택: MVP 영상 미사용, 선택 모듈로만 설계 | 개인정보·설치 리스크 |
| 규제 포지셔닝 | Asked -> Assumed | 추천안 채택: 의료 진단·치료가 아닌 안전 알림/기록 서비스 | 인허가 리스크 관리 |

## 질문 배치와 자동 채택

### Q1. 감지 방식

| 옵션 | 내용 | 트레이드오프 |
|---|---|---|
| A (추천) | 침상/바닥 센서 + 착용형 IMU 융합 | 프라이버시 낮음, 정확도 보완 가능, 착용 순응도 관리 필요 |
| B | 카메라 AI | 감지 범위 넓음, 개인정보·동의·보관 부담 큼 |
| C | 상용 워치 | 빠른 시작, 시설 단체 운영·충전 관리 어려움 |

채택: A

### Q2. 보호자 알림 조건

| 옵션 | 내용 | 트레이드오프 |
|---|---|---|
| A (추천) | 직원 확인 후 확정 낙상만 즉시 알림, 미확인 3분 초과도 알림 | 오탐 민원과 은폐 의심을 균형 처리 |
| B | 모든 낙상 후보 즉시 보호자 알림 | 투명하지만 오탐 불안과 야간 민원 증가 |
| C | 시설이 수동으로만 보호자 알림 | 업무 부담과 누락 위험 증가 |

채택: A

### Q3. 영상 사용

| 옵션 | 내용 | 트레이드오프 |
|---|---|---|
| A (추천) | MVP 영상 미사용, 센서 이벤트와 조치 기록만 저장 | 프라이버시·동의 부담 낮음 |
| B | 이벤트 전후 짧은 영상 클립 저장 | 확인성 높음, 영상정보 규제 대응 필요 |
| C | 상시 녹화 분석 | 정확도 가능성, 도입·규제 리스크 큼 |

채택: A

### Q4. 네트워크 장애 대응

| 옵션 | 내용 | 트레이드오프 |
|---|---|---|
| A (추천) | 게이트웨이 로컬 알림·버퍼, 복구 후 중앙 동기화 | 현장 대응 유지, 구현 복잡도 증가 |
| B | 클라우드 연결 필수 | 단순하지만 장애 시 치명적 |

채택: A

### Q5. 서비스 포지셔닝

| 옵션 | 내용 | 트레이드오프 |
|---|---|---|
| A (추천) | 안전 알림/대응 기록 서비스, 의료 진단 주장 금지 | 출시 리스크 낮음, 마케팅 표현 제한 |
| B | 의료기기 인증 전제로 진단·환자감시장치 주장 | 신뢰도 가능, 일정·인허가 비용 큼 |

채택: A

## 반영 기록

- decision-log #1: 센서 융합 MVP 채택
- decision-log #2: 직원 확인 우선 보호자 알림 채택
- decision-log #3: 영상 미사용 기본 채택
- decision-log #4: 게이트웨이 로컬 장애 대응 채택
- decision-log #5: 안전 알림 서비스 포지셔닝 채택

