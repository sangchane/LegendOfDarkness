# Recon - 요양시설 낙상 감지 알림 서비스

조사 시점: 2026-07-09

## 도메인 업무 흐름

1. 입소자별 낙상 위험도와 보호자 연락처를 등록한다.
2. 침상, 화장실 앞, 생활실, 복도 등 위험 구역에 센서 또는 착용형 기기를 배치한다.
3. 센서가 낙상 후보 이벤트를 감지하면 직원에게 우선 알림을 보낸다.
4. 직원은 현장 확인 후 실제 낙상/오탐/도움 요청을 분류하고 조치 내용을 기록한다.
5. 보호자는 시설 정책에 따라 확정 낙상 또는 장기 미확인 이벤트만 수신한다.

## 이해관계자

- 입소자: 사생활과 존엄을 보호받으면서 빠른 도움을 받아야 한다.
- 요양보호사/간호인력: 야간·교대 상황에서도 누락 없이 알림을 받아야 한다.
- 시설장/관리자: 사고 대응 기록, 규제 대응, 센서 상태, 알림 실패를 관리해야 한다.
- 보호자: 실제 낙상 또는 중대 이벤트를 신속하고 신뢰 가능하게 받아야 한다.
- 설치·운영 업체: 센서 배터리, 네트워크, OTA, 장애 복구를 책임져야 한다.

## 규제·표준 고려

- CDC는 65세 이상 낙상이 흔하고 예방 가능한 주요 손상 원인이라고 설명하며, 2021년 미국에서 낙상으로 38,000명 이상이 사망하고 응급실 방문이 약 300만 건이었다고 제시한다. https://www.cdc.gov/falls/about/index.html
- CDC의 2026년 데이터 페이지는 미국 65세 이상 성인 4명 중 1명 이상이 매년 낙상을 보고하고, 이 중 약 37%가 치료 또는 활동 제한을 초래한 손상을 경험했다고 제시한다. https://www.cdc.gov/falls/data-research/index.html
- FDA는 "fall prevention alarm/sensor"를 bed-patient monitor 계열 장치로 분류하며, 사람에게 부착하거나 센서 패드로 사용하는 방식과 nurse call 연동 가능성을 설명한다. https://www.accessdata.fda.gov/scripts/cdrh/cfdocs/cfpcd/classification.cfm?id=PJO
- CMS 장기요양시설 지침은 거주자의 존엄, 선택, 안전, 감독 의무를 강조한다. 낙상 알림은 감시가 아니라 안전 조치와 기록으로 설계해야 한다. https://www.cms.gov/Regulations-and-Guidance/Guidance/Manuals/downloads/som107ap_pp_guidelines_ltcf.pdf
- 한국 보건복지부는 장기요양기관 CCTV 설치·관리와 영상 보관·열람 기준을 다룬 바 있다. 영상 사용형 옵션은 별도 동의, 보관기간, 열람권, 삭제 절차를 설계해야 한다. https://www.mohw.go.kr/board.es?act=view&bid=0027&list_no=376156&mid=a10503010100
- 개인정보보호위원회는 고정형 영상정보처리기기 설치·운영 가이드라인을 제공한다. 영상형 낙상 감지는 목적 제한, 안내, 접근권한, 보관기간, 녹음 금지 등을 검토해야 한다. https://www.pipc.go.kr/np/cop/bbs/selectBoardArticle.do?bbsId=BS217&mCode=D010030000&nttId=9870

## 유사 제품·기능 패턴

- NCOA는 낙상 감지 장치가 주로 가속도계, 기압계, 알고리즘을 조합하고, 낙상 감지 후 모니터링 센터 또는 보호자와 연결한다고 설명한다. https://www.ncoa.org/product-resources/medical-alert-systems/best-medical-alert-systems-with-fall-detection/
- NCOA는 손목형보다 목걸이/허리 착용형이 팔 움직임 오탐의 영향을 덜 받을 수 있다고 설명한다.
- LeadingAge CAST는 장기요양·요양시설 안전 기술이 사고 예방·보고, 비상 알림, nurse call, 위치 추적, 낙상 감지·예방을 포괄하며, 도입 전 인구집단, 직원 요구, IT 인프라, 사이버보안, 파트너 역량을 검토하라고 권고한다. https://leadingage.org/safety-technology-for-long-term-and-post-acute-care-a-primer-and-provider-selection-guide/

## 스택 후보

| 후보 | 구성 | fit | 트레이드오프 |
|---|---|---:|---|
| A. 센서 융합 우선 | 침상/바닥 압력센서 + 착용형 IMU + 직원 앱 | 높음 | 프라이버시 부담 낮고 설치 쉬움. 미착용·배터리 이슈 관리 필요 |
| B. 레이더/비전 AI | mmWave 또는 카메라 기반 감지 + 엣지 추론 | 중간 | 착용 부담 낮음. 비용·튜닝·영상/개인정보 리스크 큼 |
| C. 기존 nurse call 확장 | nurse call 이벤트 브리지 + 보호자 알림 | 중간 | 시설 도입장벽 낮음. 자동 낙상 감지 정확도는 제한 |
| D. 스마트워치 중심 | 상용 워치 앱 + 클라우드 알림 | 낮음 | 빠른 프로토타입 가능. 시설 단체 운영과 착용 순응도 낮을 수 있음 |

## 자동 채택

MVP는 후보 A를 채택한다. 사유: 요양시설은 사생활, 설치비, 오탐 대응, 직원 워크플로가 중요하므로 영상 AI보다 센서 융합과 직원 확인 기반 알림이 초기 적합도가 높다.

