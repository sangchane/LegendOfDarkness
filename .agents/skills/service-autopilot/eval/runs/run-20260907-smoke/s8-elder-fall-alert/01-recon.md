# RECON — 요양시설 낙상 감지·알림 서비스 (FallGuard)
버전: v1.0
조사일: 2026-09-07 · 조사: sonnet 서브에이전트(검색 26회 = WebSearch 25 + WebFetch 1, 132,689 tok, 8.7분) · fit 판정: 메인 세션


## 도메인 업무 흐름 — 이 산업이 실제로 어떻게 돌아가는가 (출처)

| 항목 | 내용 | 태그 | 출처 |
|---|---|---|---|
| 요양보호사 배치기준 | 노인요양시설 요양보호사 인력배치기준이 2025.1.1부터 입소자 2.1명당 1명으로 상향. 기존시설은 2026.12.31까지 유예 | [sourced] | [보건복지부고시 제2025-247호 관련 기사](https://xn--zb0bnwa350v8db07b.com/kwa-5870889-191), [angelsitter.co.kr](https://angelsitter.co.kr/board.view.php?board=bbs3&no=462) |
| 야간 인력 | 주야간보호기관에서 24시간 돌봄 제공 시 야간 요양보호사 1인 배치 필요 (입소시설 전체의 야간 최소 인원수를 명시한 별도 전국단위 수치는 확인 못함) | [sourced](배치 필요성) / [미확인](구체 인원수 산식) | [angelsitter.co.kr](https://angelsitter.co.kr/board.view.php?board=bbs3&no=462) |
| 선임 요양보호사 제도 | 2026.7.1 시행. 60개월 이상 근무·승급교육 이수자 대상, 입소자 25명당 1명 선임 가능, 월 15만원 수당 | [sourced] | [yoyang.ai.kr](https://yoyang.ai.kr/%EA%B8%B0%EA%B3%A0-2026%EB%85%84-%EB%85%B8%EC%9D%B8%EC%9A%94%EC%96%91%EC%8B%9C%EC%84%A4-%EA%B8%89%EC%97%AC-%EA%B8%B0%EC%A4%80-%EB%B0%8F-%EC%A2%85%EC%82%AC%EC%9E%90-%EC%B2%98%EC%9A%B0-%EB%8C%80/) |
| 요양병원 낙상 안전사고 건수 | 2021~2024년 4년간 요양병원 보고 낙상 환자안전사고 5,107건 | [sourced] | [KCI 논문 "국내 요양병원 최근 4년 낙상사고 분석"](https://www.kci.go.kr/kciportal/ci/sereArticleSearch/ciSereArtiView.kci?sereArticleSearchBean.artiId=ART003318357) |
| 치매군 vs 비치매군 위해발생률 | 치매환자 81.6% vs 비치매환자 74.6% (낙상 후 위해 발생 비율) | [sourced] | 상동 |
| 요양병원 환자안전사고 중 낙상 비중 | 전체 보고 1,960건 중 1,542건(79%)이 낙상 | [sourced] | [mediwelfare.com](http://www.mediwelfare.com/news/articleView.html?idxno=3541) |
| 고령자 낙상사고 추이 | 2020년 3,721건 → 2024년 11,866건(3.2배 증가). 최근 기준 시설별 순위: 노인요양시설 523건 > 버스 295건 > 의료시설 187건 | [sourced](건수) / [inference](통계 모집단·정의가 어느 기관 집계인지 기사에 명확히 안 나와 있어 타 통계와 직접 비교 시 주의) | [nongmin.com](https://www.nongmin.com/article/20251020500272) |
| 장기요양기관 평가 — 8대 안전지침 | 모든 수급자(보호자)에게 욕창예방·낙상예방·탈수예방·배변도움·관절구축예방·치매예방·감염예방·노인인권보호 8개 지침 설명 의무 | [sourced] | [케어포 시설급여 평가매뉴얼 PDF](https://www.carefor.co.kr/ct_att/contents_article/0/202412/45437/tQpnCkiztp.pdf) |
| 낙상 위험도 평가 | 수급자의 낙상·욕창 위험도, 인지기능 상태를 정기 평가하고 낙상 방지 실내환경을 조성하는지가 평가지표에 포함 | [sourced] | 상동 |

**미확인**: 낙상 발생 시 보호자 통지 시한(예: "몇 시간 이내 통지" 같은 법정 기준)과 사고 기록 서식(사고보고서 표준 양식)의 전국 공통 규정 존재 여부는 이번 조사에서 확인하지 못함 — 개별 시설 내규로 처리될 가능성 [inference].

## 이해관계자 — 누가 쓰고, 누가 돈을 내는가 (출처)

| 항목 | 내용 | 태그 | 출처 |
|---|---|---|---|
| 노인의료복지시설 수 | 2025년 기준 총 6,211개소(노인요양시설 4,758 + 노인요양공동생활가정 1,453). 2008년(1,332개소) 대비 노인요양시설 257.2%↑ | [sourced](단, AI 요약 도구로 페이지 추출 — 원자료 대조 권장) | [e-나라지표](https://www.index.go.kr/unity/potal/main/EachDtlPageDetail.do?idx_cd=2766) |
| 입소정원 | 노인의료복지시설 입소정원 412,917명(2024년 기준) | [sourced](출처 동일, 대조 권장) | 상동 |
| 전체 노인복지시설 수 | 2024년 말 기준 96,430개소(전체 노인복지시설 유형 합산 — 재가·여가 등 포함 추정) | [sourced](대조 권장) / [inference](세부 유형별 분해는 원문 PDF 확인 필요) | 상동, [보건복지부 "2025 노인복지시설 현황"](https://www.mohw.go.kr/board.es?mid=a10411010100&bid=0019&act=view&list_no=1486600) |
| 스마트 사회서비스 시범사업 | 보건복지부 "2025년도 스마트 사회서비스 시범사업" 공고 존재 확인 | [sourced](존재) / [미확인](예산 규모, 선정 시설/지자체 수, 낙상감지 품목 포함 여부) | [mohw.go.kr 공고](https://www.mohw.go.kr/board.es?mid=a10501010100&bid=0003&tag=&act=view&list_no=1485327) |
| 보건복지부 R&D 예산 | 2025년 9,327억원(전년대비 18.3%↑, 72개 사업) — 전체 R&D 예산이며 요양시설 낙상감지 특정 예산 아님 | [sourced](총액) / [inference](낙상감지 서비스와의 관련성은 불명) | [rehahomecare.com PDF](https://www.rehahomecare.com/homelib/board_file_down.asp?B_CODE=TB_NEWS_KR&R_FILE=20250106154915.pdf) |
| 2026년 장기요양보험료율 | 0.9448% | [sourced] | [mohw.go.kr 보도자료](https://www.mohw.go.kr/board.es?mid=a10503010100&bid=0027&tag=&act=view&list_no=1487817) |

**지불 주체 구조**: 국민건강보험공단(장기요양보험 재정)이 급여비용 대부분을 부담하고 시설/보호자가 본인부담금을 내는 구조이나, 낙상감지 "기기·서비스" 자체가 장기요양 급여수가에 포함되는지(즉 건보 재정으로 커버되는지)는 이번 조사에서 확인하지 못함 — **미확인**. 시설 자체 구매 또는 지자체 복지기술 지원사업 의존 가능성이 높다는 것은 [inference].

## 규제·표준 — 반드시 준수해야 하는 것 (출처; 해당 없음도 확인 근거)

| 구분 | 내용 | 태그 | 출처 |
|---|---|---|---|
| (a) 개인정보보호법 제23조 | 사상·신념, 건강, 성생활 등 민감정보 처리 원칙적 금지, 예외적 허용. 건강정보(낙상 이력·생체신호)는 민감정보 해당 | [sourced] | [law.go.kr 제23조](https://www.law.go.kr/lsLawLinkInfo.do?chrClsCd=010202&lsJoLnkSeq=1000575255) |
| (a) 개인정보보호법 제25조 | 고정형 영상정보처리기기는 공개된 장소에서 원칙 금지, 예외(법령 허용·범죄예방·시설안전관리 등)에만 허용. 촬영 영상을 저장하지 않는 경우 별도 허용 규정 존재 | [sourced] | [law.go.kr 제25조](https://www.law.go.kr/LSW//lsLawLinkInfo.do?lsJoLnkSeq=900079397&lsId=011357&chrClsCd=010202) |
| (a) 인지저하 어르신 동의 | 이번 검색으로는 "14세 미만 아동"에 대한 법정대리인 동의 확인 절차(개인정보포털 안내)만 확인됨. **인지저하 성인(피성년후견인 등)에 대한 개인정보 동의의 법정대리인/후견인 대리 절차를 명시한 정부 가이드라인은 찾지 못함** | [미확인] | [privacy.go.kr](https://www.privacy.go.kr/front/contents/cntntsView.do?contsNo=94) (아동 기준만 확인) |
| (b) 요양시설 CCTV 의무화 | **정정**: 조사항목은 "노인복지법 개정"으로 표현했으나, 실제 근거법은 **노인장기요양보험법 제33조의2 및 그 시행규칙**. 2023.5.8 공포, 2023.6.22 시행(신규시설). 기존시설은 유예기간을 거쳐 2023.12.21까지 설치 | [sourced] | [mohw.go.kr 보도자료](https://mohw.go.kr/board.es?act=view&bid=0027&list_no=376156&mid=a10503010100) |
| (b) CCTV 설치 범위 | 공동거실(복도 포함)·침실·현관·물리(작업)치료실·프로그램실·식당·자체운영 엘리베이터 각 1대 이상. 침실은 수급자·보호자 전원 동의 시에만 설치 | [sourced] | [SBS 뉴스](https://news.sbs.co.kr/news/endPage.do?news_id=N1007182843) |
| (c) 낙상감지 SW의 식약처 의료기기 해당 여부 | 식약처 "의료기기 소프트웨어 허가심사 가이드라인" 문서 존재는 확인. **"낙상감지 알림 서비스"가 구체적으로 의료기기 해당/비해당으로 판정된 사례·공식 유권해석은 확인하지 못함** | [미확인] | [식약처 가이드라인](https://www.mfds.go.kr/brd/m_1060/view.do?seq=15655) |
| (d) EN 50134 (사회적 알람) | 유럽 표준, Part 1(시스템 요구사항)·Part 2(트리거 장치)·Part 5(상호연결·통신)·Part 9(IP 통신 프로토콜) 등 다부작 구성. 국내 낙상알림 서비스에 직접 적용 의무는 아니며 참고용 | [sourced](표준 존재·구성) / [inference](국내 강제성 없음) | [telecare.ie](https://telecare.ie/european-en50134-standards-for-social-alarms/) |
| (d) Apple Watch 낙상감지 FDA | Apple Watch Series 4(2018)가 FDA로부터 심전도·낙상감지 관련 Class II 의료기기로 clearance 받음 | [sourced] | [Forbes](https://www.forbes.com/sites/jeanbaptiste/2018/09/14/apple-watch-4-is-now-an-fda-class-2-medical-device-detects-falls-irregular-heart-rhythm/) |
| (d) CMS 너싱홈 F-tag | F689 "Free of Accident Hazards/Supervision/Devices" — 시설이 사고 위험을 통제하고 낙상 방지를 위한 감독·보조기기를 제공해야 한다는 규정. 주 서베이어가 가장 흔히 인용하는 태그 중 2위 | [sourced] | [AAPACN](https://www.aapacn.org/article/f689-accident-survey-citations-whats-behind-these-immediate-jeopardies/) |
| (e)(f) 정보통신망법 제50조 | 광고성 정보 전송은 원칙적으로 사전 명시적 동의 필요, 예외(거래관계 6개월 내 등) 존재. 카카오 알림톡은 "정보성 메시지"로 분류되어 템플릿 심사에서 광고성 문구가 금지되며 야간 발송도 가능 | [sourced](알림톡 분류 관행) / [미확인](낙상 "안전 알림"이 정보통신망법상 광고성 정보 정의에서 명시적으로 제외된다는 조문·유권해석 원문) | [Omago 블로그](https://www.omago.ai/ko/blog/kakaotalk-marketing-message-law-korea), [카카오 알림톡 템플릿 가이드 PDF](https://static.godo.co.kr/download/echost/alimtalk_template_guide.pdf) |

## 유사 솔루션 — 실제 기능 범위 (출처)

| 업체(국가) | 감지 방식 | 오탐/미탐·정확도 | 알림 경로 | 가격 단서 | 프라이버시 처리 | 출처 |
|---|---|---|---|---|---|---|
| LG유플러스 U+스마트레이더 (국내) | 77GHz mmWave 레이더 천장/벽 설치, 자세변화 감지 | **98% 정확도**로 자세변화·낙상 감지(보도자료 수치) [sourced] | 이상 발생 시 관리자에게 문자메시지(SMS) 즉시 통보 | 미확인(사업 매출목표 "100억원" 언급은 있으나 단가 비공개) | 영상 없이 레이더 신호만 사용 → CCTV 대비 사생활 노출 적음(자체 홍보 문구, 제3자 검증 자료는 미확인) | [뉴시스](https://www.newsis.com/view/NISX20220908_0002008438), [ZDNet Korea](https://zdnet.co.kr/view/?no=20220908104806), [LG 공식](https://www.lg.co.kr/media/release/22641) |
| 스마트레이더시스템 (국내) | 천장형 mmWave 레이더, 비접촉·비영상 | 미확인(정량 수치 비공개) | 미확인 | 미확인 | 영상 미저장(레이더 기반) [sourced: 방식] | [데일리한국](https://daily.hankooki.com/news/articleView.html?idxno=1368934) |
| Vayyar Care (이스라엘/미국) | 4D RF 이미징 레이더(벽/천장 설치), 웨어러블·카메라 불필요 | "타 자동낙상알림 대비 4배 정확" 마케팅 문구, 정량 오탐률 수치는 확인 못함 [미확인] | 앱/모니터링센터 알림(가정용은 Alexa Together 연동) | 가정용 Amazon 리스팅은 구독제(정확 금액 미확인), B2B 요양시설向 가격 비공개 | 카메라 없이 RF 신호만 사용 — 영상 저장 자체가 없음 | [Vayyar 공식](https://vayyar.com/care-pages/how/), [Amazon 리스팅](https://www.amazon.com/Vayyar-Care-Touchless-Detection-Subscription/dp/B09JXV82Z6) |
| SafelyYou (미국) | 벽부착 카메라 + 컴퓨터비전 AI | 치매케어 시설에서 응급실 이송 80% 감소(효과 연구 수치, 오탐률 자체는 별도 확인 못함) | 직원 근무기기 또는 간호콜 시스템으로 알림 | 미확인(엔터프라이즈 B2B 계약, 공개가 없음) | 이벤트 기반 녹화(낙상 감지 시에만 영상 저장, 상시 감시 아님), 오디오·실시간 스트리밍 없음 | [SafelyYou 공식](https://www.safely-you.com/safelyyou-safety-ai/), [AJMC](https://www.ajmc.com/view/safelyyou-new-research-reveals-safelyyous-aienabled-fall-detection-reduces-need-for-emergency-service-care-in-dementia-care-facilities) |
| Nobi (벨기에) | 천장 스마트조명(카메라+스피커+온보드 프로세서) | 미확인(정량 수치 비공개) | 낙상 의심 시 음성으로 안내 확인 후 지정 케어팀에 연락 | 가정용 $2,287.60(기기)+$19/월 또는 $119/월 렌탈. 요양시설向 침실전용 약 $240/월, 침실+화장실 $395/월, 전체(침실+화장실+거실) $525/월 | 온디바이스(엣지) 영상처리로 이미지를 클라우드에 업로드하지 않음(자체 홍보 문구) | [TechHive](https://www.techhive.com/article/579134/the-nobi-smart-lamp-can-detect-when-a-person-falls-in-their-home-and-will-call-for-assistance.html), [Healthcare Brew](https://www.healthcare-brew.com/stories/2026/04/22/nobi-smart-lights-detecting-patient-falls) |
| Apple Watch 낙상감지 (미국) | 손목 가속도계+자이로+심박센서 조합 | 정량 오탐률 비공개, "hard fall" 임계값 알고리즘만 공개 | 60초 무동작 시 자동으로 응급전화(국가별 119 등)+긴급연락처 통보, 그 전엔 사용자 알림으로 취소 가능 | 워치 자체 가격(별도 구독료 없음, Apple Watch 본체 필요) | 기기 자체 처리, 위치정보는 SOS 호출 시에만 전송 | [Apple 공식 지원](https://support.apple.com/en-us/108896), [Forbes](https://www.forbes.com/sites/jeanbaptiste/2018/09/14/apple-watch-4-is-now-an-fda-class-2-medical-device-detects-falls-irregular-heart-rhythm/) |

## 스택 후보 — 후보별 근거·트레이드오프

### (a) 감지 방식 비교
| 방식 | 근거 있는 특징 | 태그 |
|---|---|---|
| mmWave 레이더 | LG U+ 사례에서 자세변화·낙상 98% 정확도 실사례 보도. 어두운 환경에서도 동작, 최대 5명 동시 감지, 영상 미저장 | [sourced](LG U+ 수치) |
| 카메라+AI(엣지 추론, 영상 비저장) | SafelyYou는 이벤트 기반 녹화로 상시감시 아님(응급실 이송 80%↓ 효과 보고), Nobi는 온디바이스 처리로 클라우드 미전송 | [sourced](정성적 방식) / [미확인](정량 오탐률) |
| 웨어러블(가속도계) | Apple Watch가 대표 사례, FDA Class II. 다만 **요양시설 입소자의 착용 거부·분실·충전 문제**는 이번 검색에서 정량 데이터를 찾지 못함 | [sourced](FDA·동작원리) / [미확인](요양시설 맥락 착용순응도 수치) |
| 침대·바닥 압력센서 | 이번 조사 예산 내에서 검색을 수행하지 못함 | [미확인] |

### (b) 서버·알림 파이프라인
| 후보 | 핵심 사실 | 태그 | 출처 |
|---|---|---|---|
| MQTT — EMQX | **v5.9.0부터 오픈소스/엔터프라이즈 기능을 통합해 Business Source License(BSL) 1.1로 전환**(그 이전 v5.8까지는 Apache 2.0). 그린필드 설계 시 라이선스 재검토 필요 | [sourced] | [EMQ 블로그](https://www.emqx.com/en/blog/emqx-vs-mosquitto-2023-mqtt-broker-comparison) |
| MQTT — Mosquitto | EPL/EDL 라이선스로 완전 오픈소스 유지. 경량 싱글스레드 구조로 임베디드/엣지 기기에 강점 | [sourced] | 상동 |
| DB — TimescaleDB | 코어는 Apache 2.0(완전 오픈소스, 경쟁 매니지드 서비스로 재판매 가능). 압축·연속 집계(continuous aggregates)·하이퍼함수 등 고급기능은 Timescale License(TSL)로 별도 제한(경쟁 매니지드 서비스 금지) | [sourced] | [Tiger Data 라이선스 페이지](https://www.tigerdata.com/legal/licenses) |
| DB 회사명 변경 | Timescale Inc가 2025.6.17 "Tiger Data"로 사명 변경(제품명은 TimescaleDB 유지) | [sourced] | [en.wikipedia.org/TimescaleDB](https://en.wikipedia.org/wiki/TimescaleDB) |
| 푸시 — FCM | **Legacy HTTP/XMPP API는 2024.6~7월(연장 마감 7/22) 완전 폐지됨. 반드시 FCM HTTP v1으로 신규 구현해야 함**(레거시 코드 예제 주의) | [sourced] | [Google/Microsoft 발표 정리](https://devblogs.microsoft.com/azure-notification-hubs/support-of-azure-notification-hubs-firebase-cloud-messaging-fcm-legacy-api-will-be-deprecated-on-june-20-2024/) |
| 국내 알림톡/SMS — SOLAPI | 표준단가(공식 가격표 기준): 카카오 알림톡 8원, 친구톡 14원, 친구톡 이미지 22원, SMS(단문) 13원, LMS(장문) 29원, MMS(사진) 60원. 월 기본료 없음, 발송량에 따라 단가 할인 | [sourced] | [solapi.com/pricing](https://solapi.com/pricing) |
| 국내 알림톡/SMS — NHN Cloud Notification | 알림톡·친구톡·SMS·국제SMS·푸시·이메일·RCS 통합 제공 서비스 존재 확인. **구체적 단가는 공식 요금 페이지에서 즉시 확인 못함**(로그인/견적 필요 가능성) | [sourced](서비스 존재) / [미확인](단가) | [nhncloud.com](https://www.nhncloud.com/kr/service/notification/notification-hub) |
| 국내 알림톡/SMS — 네이버클라우드 SENS | SMS/LMS/MMS/국제SMS, 카카오톡 비즈메시지(알림톡·친구톡) 제공 확인. **구체적 단가 미확인** | [sourced](서비스 존재) / [미확인](단가) | [ncloud.com/product/applicationService/sens](https://www.ncloud.com/product/applicationService/sens) |
| 음성 자동발신(SENS voice / Twilio 국내) | 이번 조사 예산 내에서 검색을 수행하지 못함 | [미확인] | — |
| 백엔드 프레임워크(FastAPI/NestJS/Spring Boot) 최신 안정버전 | 이번 조사 예산 내에서 검색을 수행하지 못함 — 별도 확인 필요 | [미확인] | — |

### (c) 엣지 디바이스
| 후보 | 가격·재고 단서 | 태그 | 출처 |
|---|---|---|---|
| Raspberry Pi 5 | 8GB 모델 기준 약 $80(자료별 $60~80 범위) | [sourced] | [모델 비교 블로그 종합](https://thinkrobotics.com/blogs/learn/nvidia-jetson-orin-nano-vs-raspberry-pi-5-the-ultimate-edge-computing-showdown) |
| NVIDIA Jetson Orin Nano | Super Developer Kit 출시가 $249, **2026년 7월 리프라이싱 이후 약 $399로 인상**된 것으로 확인(자료 간 $249~$499 범위 편차 있음 — 재검증 권장) | [sourced](가격 변동 존재) / [inference](정확한 현재 실거래가는 벤더 직접 확인 필요) | [tannatechbiz.com](https://tannatechbiz.com/blog/post/raspberry-pi-5-vs-jetson-orin-nano) |
| 레이더 모듈 벤더 SDK | 국내 벤더로 스마트레이더시스템(LG유플러스 협력, 삼성서울병원 스마트병실 공급 이력)이 확인됨. SDK 공개 여부·개발자 접근성은 미확인 | [sourced](벤더 존재) / [미확인](SDK 공개 여부) | [데일리한국](https://daily.hankooki.com/news/articleView.html?idxno=1368934) |

## 미확인 항목 (조사로 못 찾은 것)
- 낙상감지 소프트웨어/기기가 식약처 의료기기로 분류된 구체적 판정 사례 또는 공식 유권해석
- 인지저하 어르신(피성년후견인 등) 개인정보 처리 동의의 법정대리인 대리 절차를 명시한 정부 가이드라인(아동 기준만 확인됨)
- 정보통신망법상 "안전 알림"이 광고성 정보 정의에서 명시적으로 제외된다는 조문 원문·유권해석
- 낙상 발생 시 보호자 통지 시한, 사고 기록 표준 서식의 전국 공통 법정 기준 존재 여부
- 2025년도 스마트 사회서비스 시범사업의 예산 규모, 선정 시설/지자체 수, 낙상감지 품목 포함 여부
- 낙상감지 기기·서비스가 장기요양 급여수가에 포함되는지(건보 재정 커버 여부)
- Vayyar Care, SafelyYou, Nobi(요양시설向)의 정식 B2B 가격 및 정량 오탐/미탐률
- 침대·바닥 압력센서 방식의 정확도·설치비·거부감 데이터
- 백엔드 프레임워크(FastAPI/NestJS/Spring Boot) 최신 안정 버전·라이선스
- 국내 음성 자동발신 API(네이버클라우드 SENS voice, Twilio 한국 발신 가능 여부)
- NHN Cloud Notification, 네이버클라우드 SENS의 구체적 발송 단가
- NVIDIA Jetson Orin Nano의 2026-09-07 시점 정확한 실거래가·재고(자료 간 편차 큼)
- 레이더 모듈 벤더(스마트레이더시스템 등)의 개발자용 SDK 공개 여부·라이선스

## 출처 목록 (URL · 확인일)
모두 확인일: 2026-09-07

- https://xn--zb0bnwa350v8db07b.com/kwa-5870889-191 — 요양보호사 인력기준 2.1대1 변경
- https://angelsitter.co.kr/board.view.php?board=bbs3&no=462 — 노인요양의료시설 인력배치기준·수가 변경
- https://yoyang.ai.kr/기고-2026년-노인요양시설-급여-기준-및-종사자-처우-대폭 — 2026년 노인요양시설 급여기준·선임요양보호사
- https://www.mohw.go.kr/board.es?mid=a10503010100&bid=0027&tag=&act=view&list_no=1487817 — 2026년 장기요양보험료율
- https://www.kci.go.kr/kciportal/ci/sereArticleSearch/ciSereArtiView.kci?sereArticleSearchBean.artiId=ART003318357 — 국내 요양병원 4년 낙상사고 분석(치매군 비교)
- http://www.mediwelfare.com/news/articleView.html?idxno=3541 — 요양병원 환자안전사고 79% 낙상
- https://www.nongmin.com/article/20251020500272 — 고령자 낙상사고 3.2배 증가 통계
- https://www.carefor.co.kr/ct_att/contents_article/0/202412/45437/tQpnCkiztp.pdf — 시설급여(노인요양시설) 평가 매뉴얼
- https://www.index.go.kr/unity/potal/main/EachDtlPageDetail.do?idx_cd=2766 — e-나라지표 노인의료복지시설 현황
- https://www.mohw.go.kr/board.es?mid=a10411010100&bid=0019&act=view&list_no=1486600 — 2025 노인복지시설 현황
- https://www.mohw.go.kr/board.es?mid=a10501010100&bid=0003&tag=&act=view&list_no=1485327 — 2025년도 스마트 사회서비스 시범사업 공고
- https://www.rehahomecare.com/homelib/board_file_down.asp?B_CODE=TB_NEWS_KR&R_FILE=20250106154915.pdf — 2025 보건복지부 R&D 통합 시행계획
- https://www.law.go.kr/lsLawLinkInfo.do?chrClsCd=010202&lsJoLnkSeq=1000575255 — 개인정보보호법 제23조
- https://www.law.go.kr/LSW//lsLawLinkInfo.do?lsJoLnkSeq=900079397&lsId=011357&chrClsCd=010202 — 개인정보보호법 제25조
- https://www.privacy.go.kr/front/contents/cntntsView.do?contsNo=94 — 법정대리인 동의확인 의무(아동 기준)
- https://mohw.go.kr/board.es?act=view&bid=0027&list_no=376156&mid=a10503010100 — 장기요양기관 CCTV 설치 의무화 보도자료
- https://news.sbs.co.kr/news/endPage.do?news_id=N1007182843 — 노인요양원 CCTV 설치 의무 침실 동의 규정
- https://ils.inha.ac.kr/bbs/ils/3464/109430/download.do — 노인장기요양시설 CCTV 설치 입법 평가와 과제(논문)
- https://www.mfds.go.kr/brd/m_1060/view.do?seq=15655 — 의료기기 소프트웨어 허가심사 가이드라인
- https://telecare.ie/european-en50134-standards-for-social-alarms/ — EN 50134 사회적 알람 표준 개요
- https://support.apple.com/en-us/108896 — Apple Watch 낙상감지 공식 지원문서
- https://www.forbes.com/sites/jeanbaptiste/2018/09/14/apple-watch-4-is-now-an-fda-class-2-medical-device-detects-falls-irregular-heart-rhythm/ — Apple Watch 4 FDA Class II
- https://www.aapacn.org/article/f689-accident-survey-citations-whats-behind-these-immediate-jeopardies/ — CMS F689 낙상 관련 규정
- https://www.omago.ai/ko/blog/kakaotalk-marketing-message-law-korea — 카카오 광고메시지·정보통신망법
- https://static.godo.co.kr/download/echost/alimtalk_template_guide.pdf — 카카오 알림톡 템플릿 검수 가이드
- https://www.newsis.com/view/NISX20220908_0002008438 — LGU+ AI 낙상 감지 실시간 모니터링 플랫폼
- https://zdnet.co.kr/view/?no=20220908104806 — LGU+ 스마트레이더 낙상 사고 탐지 매출 목표
- https://www.lg.co.kr/media/release/22641 — LG유플러스 레이다 센서 낙상감지 실증
- https://daily.hankooki.com/news/articleView.html?idxno=1368934 — 스마트레이더시스템 스마트병실 낙상감지 솔루션
- https://vayyar.com/care-pages/how/ — Vayyar Care 동작 원리
- https://www.amazon.com/Vayyar-Care-Touchless-Detection-Subscription/dp/B09JXV82Z6 — Vayyar Care 가정용 리스팅
- https://www.safely-you.com/safelyyou-safety-ai/ — SafelyYou Safety AI 동작 원리
- https://www.ajmc.com/view/safelyyou-new-research-reveals-safelyyous-aienabled-fall-detection-reduces-need-for-emergency-service-care-in-dementia-care-facilities — SafelyYou 응급이송 80% 감소 연구
- https://www.techhive.com/article/579134/the-nobi-smart-lamp-can-detect-when-a-person-falls-in-their-home-and-will-call-for-assistance.html — Nobi 스마트램프 소개
- https://www.healthcare-brew.com/stories/2026/04/22/nobi-smart-lights-detecting-patient-falls — Nobi 요양시설 가격 구간
- https://www.emqx.com/en/blog/emqx-vs-mosquitto-2023-mqtt-broker-comparison — EMQX vs Mosquitto 비교(라이선스 변경 포함)
- https://www.tigerdata.com/legal/licenses — TimescaleDB(Tiger Data) 라이선스 구조
- https://en.wikipedia.org/wiki/TimescaleDB — TimescaleDB 개요·사명변경
- https://devblogs.microsoft.com/azure-notification-hubs/support-of-azure-notification-hubs-firebase-cloud-messaging-fcm-legacy-api-will-be-deprecated-on-june-20-2024/ — FCM Legacy API 폐지 공지
- https://solapi.com/pricing — SOLAPI 알림톡/SMS 표준단가
- https://www.nhncloud.com/kr/service/notification/notification-hub — NHN Cloud Notification 서비스 개요
- https://www.ncloud.com/product/applicationService/sens — 네이버클라우드 SENS 서비스 개요
- https://thinkrobotics.com/blogs/learn/nvidia-jetson-orin-nano-vs-raspberry-pi-5-the-ultimate-edge-computing-showdown — Raspberry Pi 5 가격
- https://tannatechbiz.com/blog/post/raspberry-pi-5-vs-jetson-orin-nano — Jetson Orin Nano 가격·리프라이싱

## 메인 fit 판정 — 스택 후보 × 사용자 제약 (evidence-map fit_score)

사용자 제약은 입력에 없어 A2에서 Assumed로 채택한 값을 기준으로 매칭한다: **팀 2~3인(TypeScript 중심), 클라우드 SaaS, 첫 파일럿 시설 1곳(침상 ≤100), 초기 예산 소규모, 국내 리전.**
"인기 ≠ 적합" — 점수는 요구 충족·팀 적합·운영 환경·생태계·총비용 5요소.

| 후보 | fit | 어떤 요소 때문에 |
|---|---|---|
| 감지 — **mmWave 레이더(천장형, 영상 없음)** | **높음** | 요구 충족(착용 불필요·비영상·다인실 동시 감지 — LG U+ 98% 보도) + 운영 환경(개인정보보호법 25조·침실 CCTV 전원 동의 부담 회피). 리스크: 국내 벤더 SDK 공개 여부 [미확인] → 디바이스 어댑터 계층으로 벤더 격리 |
| 감지 — 카메라+엣지 AI | 중간 | 정확도 잠재는 높으나 침실 영상 처리 동의(수급자·보호자 전원)·25조 예외 요건·모델 개발 부담이 2~3인 팀에 과함 |
| 감지 — 웨어러블 | 낮음 | 인지저하 입소자 착용 거부·분실·충전 운영 부담 [inference — 정량 미확인]. 자기 호출이 불가능한 대상에게 착용 의존은 미탐 경로 |
| 감지 — 침상 압력센서 | 낮음 | 낙상이 아니라 "이탈"을 감지 — 낙상 판정은 별도 필요. 데이터 [미확인] |
| MQTT — **Mosquitto** | **높음** | 라이선스(EPL/EDL) + 규모(시설당 디바이스 ≤100)에 경량 브로커로 충분 |
| MQTT — EMQX | 낮음 | v5.9.0부터 BSL 1.1 — SaaS 재판매 시 라이선스 검토 비용 |
| 백엔드 — **NestJS(TypeScript, Node LTS)** | **높음** | 팀 적합(웹 콘솔 React·직원 앱 Expo와 단일 언어), MQTT/큐 통합 모듈 존재. 버전은 착수 시 LTS로 고정 (최신 안정 버전 [미확인]) |
| 백엔드 — Spring Boot / FastAPI | 중간 | 요구는 충족하나 팀 언어 분산. 팀이 Java/Python 중심이면 역전 |
| DB — **PostgreSQL 16+ (월 파티셔닝)** | **높음** | 볼륨이 작다: 낙상 이벤트 시설당 일 ≤10건, heartbeat는 행으로 저장하지 않고 `last_seen_at` 갱신만 → TimescaleDB(TSL 기능 제약) 불필요 |
| 푸시 — **FCM HTTP v1** | **높음** | Legacy API 2024.7 폐지 확인 → v1 필수. iOS는 APNs를 FCM 경유 |
| 알림톡/SMS — **SOLAPI(단가 공개: 알림톡 8원·SMS 13원)** | **높음** | 총비용 투명. NHN Cloud·SENS는 단가 [미확인] → 발송 어댑터 인터페이스로 교체 가능하게 |
| 음성 자동발신 | — | [미확인] → MVP 범위 밖, 어댑터 인터페이스만 정의 (A2 P7 알림 채널 이중화) |
| 엣지 게이트웨이 — RPi 5 | 조건부 | 레이더 완제품이 MQTT/HTTP로 이벤트를 직접 발행하면 불필요. 모듈(SDK) 방식이면 RPi 5(약 $80) 게이트웨이 1대/생활실군. Jetson은 영상 AI가 없으므로 불필요 |
| 직원 앱 — **Expo(React Native)** | **높음** | 팀 단일 언어, 푸시(FCM/APNs) 표준 지원, 내부 테스트 트랙으로 파일럿 배포 |
| 보호자 채널 — **앱 없음(알림톡/SMS + 서명 링크 열람 페이지)** | **높음** | 보호자 앱 설치·심사 부담 제거, 알림톡 정보성 메시지로 야간 발송 가능 |

**정정 반영**: 요양시설 CCTV 의무화 근거법은 노인복지법이 아니라 **노인장기요양보험법 제33조의2**(2023.6.22 시행)다. 이후 산출물은 이 명칭을 쓴다.
