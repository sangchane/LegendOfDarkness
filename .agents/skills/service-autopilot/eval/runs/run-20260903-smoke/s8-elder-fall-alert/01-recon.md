# Recon — 요양시설 낙상 감지·알림 서비스 (FallGuard-Care)

조사 시점: 2026-09-03 · 조사 스킬: `ecc:research-ops`(근거 경계 표기) + `ecc:market-research`(경쟁 실기능·다운사이드) + `ecc:search-first`(스택 "사라/만들라" 판단)
표기 규칙: **[사실]** = 출처 있는 사실 / **[추론]** = 사실에서 도출 / **[추천]** = 이 파이프라인의 선택. 전 항목 URL·확인일(2026-09-03) 병기.

## 1. 도메인 업무 흐름 — 요양시설에서 낙상은 실제로 이렇게 처리된다

| # | 단계 | 현행 절차 [사실] | 출처 |
|---|---|---|---|
| 1 | 예방 | 시설은 검증된 도구로 수급자별 낙상위험도를 평가하고, 낙상예방 지침을 수급자·보호자에게 연 1회 이상 안내해야 한다(장기요양기관 평가 항목) | [2025 시설급여 평가매뉴얼 Q&A(carefor 게시)](https://www.carefor.co.kr/ct_att/contents_article/0/202501/45565/XkSmdrjD76.pdf) |
| 2 | 발생·발견 | 낙상은 야간·화장실·침상 주변에서 무목격 상태로 자주 발생. 발견자는 요양보호사(순회) 또는 동실 어르신 | [장기요양기관 안전관리 대응 가이드 2023.5(복지부 요양기준실)](https://www.carefor.co.kr/ct_att/contents_article/0/202305/41028/LXEHC3xulI.pdf) |
| 3 | 1차 대응 | 요양보호사는 대상자를 안정시키고 상황 확인, 통증 심하면 무리하게 움직이지 않음 → 간호(조무)사 등 응급보고체계 상위로 보고 | [DSHS 요양보호사 안전교육(한국어)](https://www.dshs.wa.gov/sites/default/files/publications/documents/22-1963KO.pdf) · [1424재가노인복지센터 응급상황 대처](https://1424care.com/%EC%9D%91%EA%B8%89%EC%83%81%ED%99%A9-%EB%8C%80%EC%B2%98-%EC%9A%94%EC%96%91%EB%B3%B4%ED%98%B8/) |
| 4 | 보호자 통보 | 가장 가까운 가족·보호자에게 사고 사실을 반드시 알린다. 경미하면 기관장 보고로 충분하며 **기관 방침에 따라 기관장이 보호자에게 연락**할 수 있다 | 상동 |
| 5 | 기록 | 상황 종료 후 사무실에서 <상태기록지> 또는 <사고보고서> 작성 — 발생 시각·상황·취한 조치를 사실 위주로 기록 | 상동 · [급여제공 지침 10가지](https://lawinus.co.kr/%EA%B8%89%EC%97%AC%EC%A0%9C%EA%B3%B5-%EC%A7%80%EC%B9%A8-10%EA%B0%80%EC%A7%80/) |
| 6 | 법적 책임 | 법원은 낙상 사고에서 요양원·원장·요양보호사의 공동 배상 책임을 인정한 사례가 있다 → "언제 감지했고 언제 대응했는지"의 **타임스탬프 증거**가 시설의 방어 자료가 된다 | [월간장기요양 판례 기사](https://www.carekim.com/news_view.jsp?ncd=3749) · [요사나모 법률 행동요령](https://m.cafe.daum.net/carelover/JDfO/877) |

**왜 "감지→알림 지연"이 핵심 지표인가 [사실]**: 낙상 후 1시간 이상 바닥에 방치되는 "long lie"는 6개월 내 사망률과 강하게 연관되고(65세 이상 125명 코호트에서 1시간 초과 방치군 절반이 6개월 내 사망), 1년 추적에서 long lie 경험자의 60%가 낙상으로 재입원했다. [BMC Geriatrics 2022 scoping review](https://bmcgeriatr.biomedcentral.com/articles/10.1186/s12877-022-03258-2) · [Physiopedia Long Lie](https://www.physio-pedia.com/Long_Lie) · [Int Emerg Nurs 2022 systematic review](https://www.sciencedirect.com/science/article/abs/pii/S1755599X22000052)
→ **[추론]** 제품의 1차 가치는 "낙상 예측"이 아니라 **바닥 체류 시간(time-on-ground) 단축**이다. SafelyYou는 AI 감지로 바닥 체류를 29.6분, 직원 도착까지 28.3분 단축했다고 보고했다. [SafelyYou 연구 블로그](https://www.safely-you.com/blog/fall-detection/)

## 2. 이해관계자 — 누가 쓰고, 누가 돈을 내는가

| 이해관계자 | 역할·요구 | 근거 |
|---|---|---|
| 요양보호사 (1차 대응자) | 야간 1인이 다수 어르신 담당. 손이 비어 있지 않음 → 알림은 **소리·진동으로 즉시**, 조작은 한 손·한 탭. 오탐이 잦으면 알림을 끈다 | [안전관리 대응 가이드](https://www.carefor.co.kr/ct_att/contents_article/0/202305/41028/LXEHC3xulI.pdf) · [유스연합 기획리포트 "위기의 요양 현장"](https://www.youthassembly.kr/news/909740) |
| 간호(조무)사 | 낙상 후 사정(assessment)·의료 판단·기록 책임. 사고보고서에 시각·상황이 필요 | 상동 |
| 시설장·사무국장 (구매 결정자, 지불자) | 평가 지표 대응, 법적 방어(대응 시각 증빙), 보호자 신뢰(민원 감소), CCTV 의무화와의 연계 | [복지부 CCTV 의무화 보도자료](https://www.mohw.go.kr/board.es?mid=a10503010100&bid=0027&act=view&list_no=376156) |
| 보호자 (외부 수신자) | "확정된 낙상"만, 신뢰할 수 있는 채널로, 조치 내용과 함께 받고 싶음. 오탐 통보는 신뢰 붕괴 | [급여제공 지침](https://lawinus.co.kr/%EA%B8%89%EC%97%AC%EC%A0%9C%EA%B3%B5-%EC%A7%80%EC%B9%A8-10%EA%B0%80%EC%A7%80/) |
| 어르신 (데이터 주체) | 사생활(침실·화장실), 착용형 기기 거부·이탈 가능(인지저하). 카메라 침실 촬영은 **전원 동의**가 법적 요건 | [복지부 보도자료](https://www.mohw.go.kr/board.es?mid=a10503010100&bid=0027&act=view&list_no=376156) · [MBC 보도](https://imnews.imbc.com/news/2023/society/article/6481428_36126.html) |
| 지자체·건보공단 | 평가·지도점검, 지자체 오픈이노베이션 사업으로 시범 도입(평택시 2026) | [평택시 AI 위험징후 포착 시스템(2026-08)](https://www.sidae.com/article/2026080315573615995) |
| 설치·운영 업체 (우리) | 센서 전원·네트워크·OTA·장애 복구 책임. 현장 출동 비용이 수익성 결정 | [Mender Pi 체크리스트](https://mender.io/blog/raspberry-pi-in-production) |

## 3. 규제·표준 — 반드시 준수해야 하는 것

| 항목 | 내용 [사실] | 우리 설계에 미치는 영향 [추론] | 출처 |
|---|---|---|---|
| 장기요양기관 CCTV 의무화 (노인장기요양보험법 시행규칙, 2023-06-22 시행) | 공동거실·침실·현관·치료실·프로그램실·식당·엘리베이터에 1대 이상. **침실은 수급자 또는 보호자 전원 동의 시에만**. 영상 **60일 경과 시 삭제**(열람 요청 시 사유 해소까지 보관). 열람 요청 후 **10일 이내 서면 통지**, **열람대장 3년 보관**. 미설치는 전원 동의서 + 지자체 신고 | 카메라 방식 채택 시 침실은 동의 없으면 불가 → 침실·화장실은 **비영상(레이더) 기본**. 낙상 클립 보관·열람은 이 규칙과 동일 정책으로 통일 | [복지부 보도자료](https://www.mohw.go.kr/board.es?mid=a10503010100&bid=0027&act=view&list_no=376156) · [복지부 영상정보처리기기 설치·운영 가이드라인 2023.5.24](https://www.carefor.co.kr/ct_att/contents_article/0/202306/44113/56VVsReMFl.pdf) · [인하대 박인환 입법평가 논문](https://ils.inha.ac.kr/bbs/ils/3464/109430/download.do) |
| 개인정보보호법 §25 고정형 영상정보처리기기 | 안내판·목적 외 이용 금지·보관기간·안전조치. 개인정보위 통합 안내서 2024-12 개정 | AI 분석 서버·클립 저장소가 "영상정보처리기기 운영자"의 수탁자가 됨 → 위탁 계약·안전성 확보조치 기준 적용 | [개인정보위 고정형 영상정보처리기기 안내서(2024.12)](https://www.privacy.go.kr/front/bbs/bbsView.do?bbsNo=BBSMSTR_000000000049&bbscttNo=20779) · [찾기쉬운 생활법령](https://www.easylaw.go.kr/CSP/CnpClsMain.laf?csmSeq=1257&ccfNo=2&cciNo=3&cnpClsNo=3) |
| 개인정보 안전성 확보조치 기준 (2024-10 안내서) | 접근권한 관리·접근기록 보관·암호화·백업 의무 | 건강정보(낙상 이력)는 **민감정보** → 접근기록 2년 보관, 저장 암호화, 접근권한 최소화 | [개인정보 안전성 확보조치 기준 안내서 2024.10](https://business.cch.com/CybersecurityPrivacy/KoreanGuidetotheStandardsforEnsuringtheSafetyofPersonalInformationOctober2024.pdf) |
| 의료기기 해당 여부 (식약처) | SaMD는 "질병의 진단·치료·예방·관리 목적"이면 의료기기. 식약처는 "소프트웨어 의료기기 해당 여부 자주 묻는 사례"를 공개. 낙상 **감지·알림(안전 관리)** 은 진단·치료 목적이 아니면 비의료기기로 볼 여지가 있으나 **확정 아님** — "낙상 위험 예측·재활 개입"까지 표방하면 경계가 바뀐다 | **[가능성]** 1차 범위를 "안전 알림"으로 한정하고 의학적 판단 문구(진단·치료·위험도 산출)를 제품·마케팅에서 제거. 정식 출시 전 식약처 **해당 여부 질의(민원)** 를 착수 조건으로 둔다 | [식약처 SW 의료기기 해당 여부 FAQ](https://mfds.go.kr/brd/m_99/down.do?brd_id=ntc0021&data_tp=A&file_seq=2&seq=46453) · [디지털의료기기SW 허가·심사 가이드라인(KHIDI 게시)](https://www.khidi.or.kr/board/view?pageNum=3&rowCnt=20&menuId=MENU01502&maxIndex=00489320239998&minIndex=00489105729998&schType=0&schText=&categoryId=&continent=&country=&upDown=0&boardStyle=&no1=2448&linkId=48929657) · 참고(미국): [FDA PJO "fall prevention alarm/sensor" 분류](https://www.accessdata.fda.gov/scripts/cdrh/cfdocs/cfpcd/classification.cfm?id=PJO) |
| 장기요양기관 평가 (2025 시설급여) | 낙상위험도 평가 도구 사용·낙상예방 안내(연 1회) 항목 존재 | 제품 이력(낙상 건수·대응 시간)을 평가 증빙으로 내보내는 리포트가 구매 동기 | [2025 시설급여 평가매뉴얼 Q&A](https://www.carefor.co.kr/ct_att/contents_article/0/202501/45565/XkSmdrjD76.pdf) |
| 알림 채널 규제 | 카카오 알림톡은 인증 대행사(솔라피·NHN Cloud 등)를 통해서만 발송, 템플릿 사전 심사 | 보호자 통보 문구는 템플릿 심사 통과형으로 고정. 긴급 직원 알림은 알림톡이 아닌 앱 푸시+전화 | [Solapi 가격](https://solapi.com/pricing) · [NHN Cloud 카카오 비즈메시지](https://www.nhncloud.com/kr/service/notification/kakaotalk-bizmessage?lang=ko) |
| 모바일 OS 긴급 알림 | iOS Critical Alerts는 Apple 승인 entitlement 필요(건강·안전 용도 한정), 사용자 동의도 별도. Android는 채널 `setBypassDnd(true)` + full-screen intent | 직원 앱은 **Android 우선**(승인 불필요), iOS는 entitlement 신청을 착수 조건으로 | [Apple Critical Alerts 문서](https://developer.apple.com/documentation/bundleresources/entitlements/com.apple.developer.usernotifications.critical-alerts) · [PagerTree 해설](https://pagertree.com/blog/critical-alerts-for-ios-and-iphone) · [Android 구현 글](https://medium.com/@surendar1006/implementing-critical-alerts-on-android-aa49b4d75705) |

## 4. 유사 솔루션 — 실제 기능 범위 (마케팅 문구 아님)

| 솔루션 | 센서 | 실제 기능 [사실] | 강점 / 약점 | 출처 |
|---|---|---|---|---|
| **SafelyYou** (미, 상용) | 벽면 카메라 + 클라우드 AI | 낙상 감지 → 직원 기기·너스콜로 알림, 낙상 클립만 저장(비낙상 영상 60초 내 삭제, 음성 없음), Discover 포털로 원인 분석·케어플랜. 바닥체류 −29.6분 실증 | 강점: 원인분석·재발방지 루프 / 약점: 카메라 → 국내 침실은 전원 동의 필요, 클라우드 영상 전송 | [How It Works](https://www.safely-you.com/safelyyou-safety-ai/) · [AJMC 연구](https://www.ajmc.com/view/safelyyou-new-research-reveals-safelyyous-aienabled-fall-detection-reduces-need-for-emergency-service-care-in-dementia-care-facilities) · [NIA 소개](https://www.nia.nih.gov/news/nia-funded-small-business-spotlight-safelyyou-trains-ai-improve-care-older-adults) |
| **Vayyar Care** (이스라엘, 상용) | 4D 이미징 레이더(천장/벽) | 카메라 없이 낙상(대·소)·재실·이동성·화장실 방문 감지, 습기·조명 무관, 너스콜/플랫폼(K4Connect) 연동 | 강점: 사생활 무해, 화장실 설치 가능 / 약점: 사후 원인 확인용 영상 없음, 단가 높음 | [Vayyar How](https://vayyar.com/care-pages/how/) · [K4Connect 파트너십](https://www.k4connect.com/k4connect-and-vayyar-care-partner-to-bring-next-level-radar-fall-detection-technology-to-the-senior-living-industry/) · [2026 리뷰](https://elderlivinghub.com/reviews/vayyar-care-review/) |
| **Inspiren AUGi** (미, 상용) | 벽면 AI 비전(스틱피겨 처리) | 선명 영상 저장·표시 없음(스틱피겨+배경 블러), HIPAA 준수, 낙상 위험 행동 감지, PointClickCare 연동. 6개월 파일럿 400건 "save" | 강점: 영상 없이 시각적 맥락 제공 / 약점: 미국 EHR 생태계 종속 | [Aegis Living](https://www.aegisliving.com/services/inspirens-augi-smart-fall-management-technology/) · [PointClickCare 마켓](https://marketplace.pointclickcare.com/s/partner-app/aFC5G00000003lVWAQ/inspiren-augi) |
| **Nobi 스마트램프** (벨기에, 상용) | 천장 램프 일체형 광학 센서 | 낙상 감지 + 기상 시 자동 조명(예방), 80개 시설 800대에서 낙상 31%↓·대응 3분56초 | 강점: 예방+감지 결합 / 약점: 조명 교체 공사 | [McKnight's](https://www.mcknightsseniorliving.com/news/next-gen-lamps-combine-smart-lighting-and-passive-monitoring-for-fewer-senior-falls-incidents/) · [Healthcare Brew 2026-04](https://www.healthcare-brew.com/stories/2026/04/22/nobi-smart-lights-detecting-patient-falls) |
| **국내: 스페이스뱅크(부천·의왕시립요양원, 2026-06) · 인지니어스(60GHz 레이더) · 사이렌케어** | mmWave 레이더 / AI 카메라 | 레이더 기반 비접촉 낙상·기상·침대이탈·재실 감지, AIoT 관제 플랫폼. 평택시립요양원 2026-08 시범 호실 운영 | 강점: 국내 레퍼런스·지자체 사업 / 약점: 공개된 정확도 수치 없음 | [전자신문 2026-06-19](https://www.etnews.com/20260619000001) · [인지니어스](http://www.inzinious.com/) · [사이렌케어](https://bighavesolution.com/sirencare/index.html) · [평택시](https://www.sidae.com/article/2026080315573615995) |
| **오픈소스: YOLOv8-Pose 낙상 감지 리포** | 카메라 + 포즈 키포인트 | 17 COCO 키포인트 → 바운딩박스 종횡비/키포인트 각도로 낙상 판정. 데모 수준, 야간·가림·다인실 검증 없음 | 강점: 무료·엣지 실행(Jetson TensorRT 13ms/frame) / 약점: 프로덕션 검증 0, Ultralytics AGPL-3.0 | [16dina/fall-detection](https://github.com/16dina/fall-detection) · [andmydignity/fall_detection_yolov8s](https://github.com/andmydignity/fall_detection_yolov8s) · [arXiv 2603.29777 엣지 행동감지](https://arxiv.org/pdf/2603.29777) |

**정확도 현실 [사실]**: 연구 환경 레이더 민감도 97%/특이도 90~92%, 비전 민감도 71~100%/특이도 73~97%로 폭이 넓다. 실제 아파트 10곳 2년 실증(Skubic)에서 "오경보 없는 견고한 낙상 감지는 여전히 큰 도전"으로 결론. [PMC 레이더 센서](https://pmc.ncbi.nlm.nih.gov/articles/PMC5746778/) · [Sensors 2025 systematic review](https://www.mdpi.com/1424-8220/25/21/6540) · [Skubic 실환경 보고서](https://c2ship.missouri.edu/wp-content/uploads/2024/01/Skubic-Fall-detection-Final.pdf) · [JAMDA 2024 리뷰](https://www.jamda.com/article/S1525-8610(24)00752-7/pdf)
→ **[추론]** 단일 센서로 오탐 0은 불가능. 제품은 **"감지 → 직원 확인(confirm) → 보호자 통보"의 2단계 확정 구조**여야 하고, 오탐률 자체를 핵심 KPI로 관리해야 한다.

## 5. 스택 후보 — `ecc:search-first` 판단 (Adopt / Extend / Build)

| 영역 | 후보 | 판단 | 근거·트레이드오프 | fit |
|---|---|---|---|---|
| 낙상 감지 센서 (침실·화장실) | **60GHz mmWave 레이더 모듈** (Seeed MR60FDA2 $24.90, XIAO ESP32C6 동봉, 낙상·재실·설치높이·민감도 설정, ESPHome 펌웨어) | **Adopt(파일럿)** → 양산 시 산업용 모듈로 교체 검토 | 카메라 없이 침실 동의 문제 회피. 정량 정확도 미공개 → 파일럿 실측 필수 | 높음(규제·사생활 매칭) — [Seeed 제품](https://www.seeedstudio.com/MR60FDA2-60GHz-mmWave-Sensor-Fall-Detection-Module-p-5946.html) · [Hackster 소개](https://www.hackster.io/news/seeed-adds-home-assistant-ready-breathing-fall-detection-sensors-to-its-mmwave-range-c97eeb97b8d9) |
| 낙상 감지 센서 (공용 공간) | 기존 의무 CCTV 스트림 + 엣지 포즈 추론 (YOLO-Pose, TensorRT) | **Extend** — 기존 CCTV 재활용, 모델은 오픈 가중치 + 자체 파인튜닝 | 이미 설치된 CCTV(법정 의무) 활용 → 추가 HW 최소. Ultralytics AGPL-3.0 → 상용 배포 시 Enterprise 라이선스 또는 Apache 모델(RTMPose) 검토 | 중간(라이선스 리스크) — [Hackster YOLOv8-Pose 낙상](https://www.hackster.io/zhangygfw/yolov8-pose-based-fall-detection-for-public-spaces-or-elderl-2346c0) |
| 엣지 게이트웨이 | NVIDIA Jetson Orin Nano(비전 포함 시) / Raspberry Pi 5(레이더만) | Adopt | Jetson: TensorRT 포즈 13~21ms/frame 실측. Pi5: 레이더 이벤트만이면 충분 | 높음 — [arXiv 2603.29777](https://arxiv.org/pdf/2603.29777) |
| 메시지 브로커 | **Eclipse Mosquitto**(EPL/EDL, 단일 노드 오픈소스) vs EMQX 5.9+(BSL 1.1, 단일노드 무료·클러스터 유료, "타사 호스팅/임베디드 제공" 제한) | **Adopt Mosquitto** (시설당 1노드) | 시설 단위 수십 센서 규모에 클러스터 불필요. EMQX BSL 제한은 우리가 시설에 임베드 배포하는 모델과 충돌 가능 | 높음 — [EMQX BSL 공지 2025-05](https://www.emqx.com/en/news/emqx-adopts-business-source-license) · [EMQX 라이선스 FAQ](https://www.emqx.com/en/content/license-faq) |
| 백엔드 | **NestJS(TypeScript)** + **PostgreSQL 16** (TimescaleDB 확장은 보류) | Adopt | 이벤트·알림·감사로그는 관계형으로 충분. 시계열 볼륨(시설당 이벤트 <1만/일)에 Timescale 불필요 → YAGNI | 높음 |
| 직원 긴급 알림 | Android 앱 + FCM 고우선순위 + `setBypassDnd` 채널 + full-screen intent; iOS는 Critical Alerts entitlement 신청 | Adopt(FCM/APNs) | 자체 푸시 인프라 만들지 않음. FCM 미도달 대비 **전화(TTS) 폴백** | 높음 — [Apple 문서](https://developer.apple.com/documentation/bundleresources/entitlements/com.apple.developer.usernotifications.critical-alerts) |
| 보호자 통보 | **카카오 알림톡 (Solapi 또는 NHN Cloud)** → 실패 시 SMS 폴백 | Adopt(대행사) | 건당 약 8원, 템플릿 사전 심사. 직접 카카오 API 불가 | 높음 — [Solapi](https://solapi.com/pricing) · [Omago 단가 해설](https://www.omago.ai/ko/blog/kakaotalk-message-pricing-logic) |
| 전화 폴백 | 국내 음성 API(NHN Cloud/네이버 클라우드 SENS 계열) 또는 Twilio | Adopt | 야간 무응답 에스컬레이션용. 벤더는 A4에서 확정 | 중간 |
| 너스콜 연동 | 시설별 기존 너스콜(건식 접점/릴레이) | Extend(릴레이 출력) | 국내 시설 너스콜은 표준 프로토콜 부재 → 접점 신호가 가장 범용 | 중간 |
| OTA·디바이스 관리 | Mender(오픈소스) 또는 balena | Adopt | A/B 파티션·서명·롤백 기본 제공. 자체 구축 금지 | 높음 — [Mender](https://mender.io/blog/raspberry-pi-in-production) |

**"만들지 말고 사라" 결론 [추천]**: 감지 센서·브로커·푸시·알림톡·OTA는 전부 기존 도구 채택. **우리가 만드는 것은** ①센서 이벤트를 "확정 낙상"으로 승격시키는 **알림 상태기계·에스컬레이션 정책**, ②직원 확인 앱, ③보호자 통보·이력·평가 리포트, ④시설별 온프레미스 게이트웨이 형상 — 이 넷뿐이다.

## 6. 미확인·다운사이드 (market-research 게이트)

- 국내 레이더 솔루션들의 **실환경 민감도·오탐률 미공개** → 파일럿에서 자체 실측 필요 (A6 테스트 설계에 실측 프로토콜 포함).
- 식약처 의료기기 해당 여부는 **미확정** → 착수 조건(질의 민원)으로 register에 기록.
- 시설의 네트워크 품질(Wi-Fi 음영·인터넷 단절)은 **미확인** → 온프레미스 우선 설계 + 오프라인 동작 필수.
- 요양보호사의 **알림 피로(alarm fatigue)** 는 정성 근거만 있고 국내 정량 데이터 없음 → 오탐률 KPI와 "묵음 임계" 설계로 대응.
