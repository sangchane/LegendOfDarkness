# RECON — 요양시설 낙상 감지·알림 서비스
조사일: 2026-09-08 · 조사자: sonnet 서브에이전트 · 검색 27회 (1차 15회 + 2차 12회, 쿼리 목록은 말미)

## 도메인 업무 흐름 — 이 산업이 실제로 어떻게 돌아가는가

- **낙상 사고 흐름(확정, 규정 기준)**: 낙상 발생 → 발견 → (필요 시) 응급처치·상태 확인 → 보호자 통보 → 기록·보고. 노인복지법 시행규칙 [별표4]는 시설 안전기준으로 "계단 출입구에 출입문·잠금장치(화재 시 자동 해제)", "미끄럽지 않은 바닥재", "문턱 제거·손잡이 부착" 등을 요구한다(출처: https://law.go.kr/flDownload.do?flSeq=90258299 , 확인 2026-09-08).
- **낙상 발생 통계(확정)**: 국가 환자안전보고데이터 기준 65세 이상 낙상사고는 2020년 3,721건 → 2024년 11,866건으로 3.2배 증가했고, 시설 유형별로는 노인요양시설 523건 > 버스 295건 > 의료시설 187건 순으로 최다 발생지였다(2024년 기준, 한국소비자원 소비자위해감시시스템)(출처: https://www.nongmin.com/article/20251020500272 , 확인 2026-09-08). 요양병원(요양시설과 다른 기관 유형)은 2021~2024년 4년간 낙상 환자안전사고 5,107건이 보고됐고, 위해 정도는 근접오류 24.9% · 위해사건 39.7% · 적신호사건(사망 등 중대) 35.4%였다(출처: https://www.e-jhis.org/upload/pdf/jhis-2025-50-1-96.pdf , 확인 2026-09-08). **주의**: 이 통계는 "요양병원"(의료기관) 자료이며 본 서비스 대상인 "요양시설/요양원"(장기요양기관, 노인복지시설)과 기관 유형이 다르다 — 수치 인용 시 구분 필요.
- **인력 배치 기준(확정)**: 노인요양시설 요양보호사 배치기준은 2025년부터 기존 입소자 2.3명당 1명에서 2.1명당 1명으로 강화됐다(기존 시설은 2026년 말까지 2.3:1 유예)(출처: https://xn--zb0bnwa350v8db07b.com/kwa-5870889-191 , 확인 2026-09-08). **미확인**: 이 비율이 주간/야간 통합 기준인지, 야간 전담 배치 기준이 별도로 있는지는 이번 검색에서 명확한 조문을 찾지 못했다(국민건강보험공단 고시 "장기요양급여 제공기준 및 급여비용 산정방법 등에 관한 고시" 원문 확인 필요).
- **낙상 위험도 평가(확정)**: 장기요양기관 시설급여 평가매뉴얼상 낙상 위험도평가도구는 공단이 표준 도구를 제공하지 않으며, 기관이 "논문 등에서 검증된 도구"를 사용했는지를 평가 항목으로 확인한다(출처: https://www.carefor.co.kr/ct_att/contents_article/0/202501/45442/XFGkuXsCpb.pdf , 확인 2026-09-08). → 표준화된 낙상 사정 프로토콜이 국가 차원에 없다는 뜻이라 서비스가 사정 로직까지 대신 설계해야 할 수 있음(가능성).

## 이해관계자 — 누가 쓰고, 누가 돈을 내는가

- 확정된 1차 사용자: 요양보호사(현장 대응) · 간호(조무)사(사정) · 시설장(운영·비용 결정) · 사회복지사(기록) · 보호자(통보 수신) · 입소자(피대상자).
- **장기요양 인정자·급여 규모(확정, 2차 조사)**: 국민건강보험공단 「2024 노인장기요양보험 통계연보」(2025년 6월 30일 발표)에 따르면 노인장기요양 인정자는 116만 5천명으로 전년 대비 6.1% 증가했고, 급여비는 재가급여 9조 2,412억원 + 시설급여 5조 5,041억원으로 총 16조원을 돌파했다(출처: https://www.asemgac.or.kr/bbs/bbs/view.php?bbs_no=64&data_no=10231 , 확인 2026-09-08). **미확인**: 이 중 시설(요양원) 입소자 수, 전국 요양시설(노인요양시설·공동생활가정) 기관 수 자체 — 공공데이터포털에 "장기요양기관 시설별 현황"(data.go.kr, 2025-04-01 기준) 데이터셋이 존재함을 확인했으나(출처: https://www.data.go.kr/data/15124763/fileData.do , 확인 2026-09-08) 원문 수치를 이번 검색에서 직접 열람하지 못함 — 파일 다운로드 후 재확인 필요.
- **미확인**: 국민건강보험공단 장기요양기관 평가에서 "낙상"이 정량 지표로 명시된 세부 배점 — 평가매뉴얼상 "위험도평가도구 사용 여부"를 확인한다는 사실만 확정.
- 지자체·국민건강보험공단은 CCTV·평가매뉴얼의 관리·감독 주체로 확정(아래 규제 섹션 근거 동일).

## 규제·표준 — 반드시 준수해야 하는 것

**(a) 요양시설 CCTV 설치 의무화 — 확정**
- 노인장기요양보험법 시행규칙 개정령이 2023년 5월 8일 공포, **2023년 6월 22일 시행**. 신규 개설 기관은 시행일부터, 기존 기관은 6개월 유예를 거쳐 **2023년 12월 21일까지** 설치 완료 의무(출처: https://www.ytn.co.kr/_ln/0103_202305081257573364 , https://www.mohw.go.kr/board.es?mid=a10503000000&bid=0027&act=view&list_no=376156 , 확인 2026-09-08).
- 적용 대상: 노인요양시설, 노인요양공동생활가정(출처: 위 mohw 보도자료 페치, 확인 2026-09-08).
- 설치 위치: 공동거실(복도 포함)·침실·현관·물리(작업)치료실·프로그램실·식당·자체 운영 엘리베이터에 각 1대 이상, 사각지대 최소화(출처: https://ggscw.or.kr/s5_4/74 , 확인 2026-09-08).
- 보관: 내부 관리계획 주기에 따라 **60일 이상 보관 후 삭제** 원칙(단, 안전 확인 목적 열람 요청 시 보존), 열람대장은 **3년 보관**(출처: 위와 동일, 확인 2026-09-08).
- **미확인**: 근거 법 조문 번호(노인복지법 제39조의10과의 관계, 정확한 시행규칙 조항 번호)와 위반 시 과태료 정확한 금액 — 일부 뉴스는 "최대 300만원"이라 보도했으나(출처: https://news.sbs.co.kr/news/endPage.do?news_id=N1007182843 , 확인 2026-09-08) mohw 보도자료 원문에서는 금액을 확인하지 못했다. **가능성**: 노인복지법상 CCTV 설치 의무 자체는 2015~2017년경 이미 전 노인복지시설 대상으로 존재했고, 2023년 개정은 "장기요양기관"에 특화된 세부 운영기준(설치 위치·보관기간·가이드라인) 신설일 수 있음 — 두 법의 관계는 원문 대조 필요(미확인).

**(b) 개인정보보호법 — 확정 + 미확인 혼재**
- 보건복지부가 2023년 5월 24일 「장기요양기관 영상정보처리기기 설치·운영 가이드라인」을 발표, 개인정보보호법상 영상정보처리기기 규정에 따라 노인학대 방지·안전 목적의 설치·운영 근거와 열람권자(수급자·보호자), 열람 기록(생성일시·목적·열람자·일시) 관리 의무를 규정(출처: https://www.carefor.co.kr/ct_att/contents_article/0/202306/44113/56VVsReMFl.pdf , 확인 2026-09-08).
- **민감정보 정의(확정, 2차 조사)**: 개인정보보호법상 민감정보는 "사상·신념, 노동조합·정당의 가입·탈퇴, 정치적 견해, **건강**, 성생활 등에 관한 정보, 유전정보, 범죄경력자료, 신체적·생리적·행동적 특징에 관한 정보"를 포함한다(출처: https://www.easylaw.go.kr/CSP/CnpClsMainBtr.laf?popMenu=ov&csmSeq=1257&ccfNo=2&cciNo=3&cnpClsNo=1 , 확인 2026-09-08). → 낙상 이벤트·활력징후 추정치는 "건강에 관한 정보"에 해당해 민감정보로 처리될 **가능성이 높음**(직접 판례·유권해석은 미확인).
- **개인정보 영향평가(PIA) 의무 대상(확정, 2차 조사)**: 개인정보보호법 제33조 및 시행령 제35조에 따라 PIA 의무 대상은 **공공기관**으로, "50만명 이상 정보주체의 개인정보파일" 또는 "민감정보·고유식별정보를 포함하고 5만명 이상 정보주체의 개인정보파일"을 구축·운용·변경하는 경우다(출처: https://www.privacy.go.kr/front/per/iass/duty/effectsEvalutionDuty.do , 확인 2026-09-08). 평가기관 위탁도 공공기관에 한정하는 방식으로 서술됨(동일 출처). → **민간 요양시설·SaaS 사업자(본 서비스)는 PIA 법적 의무 대상이 아닐 가능성이 높음**(공공기관 조항으로만 확인됨, 민간 대상 예외/자율 규정 유무는 미확인 — 개인정보보호법 시행령 원문·PIPC 안내서 추가 대조 필요).
- **미확인**: 보호자에게 낙상 이벤트 알림을 보내는 행위가 "제3자 제공"에 해당하는지의 명확한 법적 정리(원문 시행령 조항 미대조).

**(c) 의료기기법/디지털의료제품법 — 미확인 (가능성 위주)**
- 2025년 5월 7일부터 디지털의료제품법 하위규정 시행에 따라 식약처가 디지털의료기기 가이드라인 1종 제정, 5종 개정(출처: https://www.lexology.com/library/detail.aspx?g=516ed348-16f2-48f0-9545-dc8a061790d5 , 확인 2026-09-08). 법상 "내장형 디지털의료기기소프트웨어"·"독립형 디지털소프트웨어" 구분은 확정 정의로 존재(출처: https://elaw.klri.re.kr/kor_service/lawView.do?hseq=69456&lang=KOR , 확인 2026-09-08).
- **미확인**: 낙상 감지·알림 SW/기기가 이 법상 의료기기로 분류된 구체적 판단 사례(허가·비허가 사례)는 이번 검색에서 찾지 못함. 식약처의 "디지털의료기기 판단기준 가이드라인" 원문을 별도로 확인해야 확정 가능. **가능성**: 낙상을 "진단·치료 목적"이 아닌 "생활안전 모니터링·알림" 목적으로 포지셔닝하면 비의료기기로 분류될 가능성이 높으나(국내외 유사 제품 다수가 비의료기기로 유통), 이는 사례 유추이지 확정 사실이 아니다.
- **의료기기·웰니스 판단기준 존재(확정, 2차 조사)**: 식약처는 「의료기기와 개인용 건강관리(웰니스)제품 판단기준(지침)」을 마련해 두었고, "사용 목적과 사용 중 사용자에게 미칠 위해도"에 따라 구분하며, "위해도를 판단하는 5개 문항에 하나라도 해당하면 의료기기로 취급"한다(출처: https://www.hitnews.co.kr/news/articleView.html?idxno=31377 , 확인 2026-09-08). **미확인**: 5개 문항의 구체적 내용, 낙상 감지·활동 모니터링 제품이 웰니스로 분류된 실제 사례 — 기사에는 수면 패턴·식단·체중 모니터링 제품만 웰니스 사례로 제시됐고 낙상 감지 사례는 없었다. 지침 원문(식약처 공무원지침서/민원인안내서, mfds.go.kr)을 직접 확인해야 확정 가능.

**(d) 노인복지법 — 확정**
- 노인의료복지시설(노인요양시설 포함) 시설기준: 계단 출입구 출입문·잠금장치(비상시 자동 해제), 미끄럼방지 바닥재, 휠체어 이동 가능 통로·손잡이 등 낙상 예방 관련 물리적 기준이 시행규칙 [별표4]에 명시(출처: https://law.go.kr/flDownload.do?flSeq=90258299 , 확인 2026-09-08).

**(e) 장기요양기관 평가 매뉴얼 — 확정**
- 2025년 시설급여 평가매뉴얼(요양원·공동생활가정)에 낙상 위험도평가 실시 여부를 확인하는 항목이 존재. 표준 평가도구는 공단이 지정하지 않고 "검증된 도구 사용 여부"만 확인(출처: https://www.carefor.co.kr/ct_att/contents_article/0/202501/45442/XFGkuXsCpb.pdf , 확인 2026-09-08).

**(f) 정보통신망법 — 확정**
- 보호자에게 보내는 낙상 알림(사고·상태 통지)은 이용자와의 계약·거래관계에 따른 "정보성 메시지"로 분류될 가능성이 높아 카카오 알림톡의 "광고성 정보 예외"에 해당할 수 있음(출처: https://kakaobusiness.gitbook.io/main/ad/infotalk , 확인 2026-09-08). 광고성 정보를 사전동의 없이 전송하면 **3천만원 이하 과태료**(출처: https://www.omago.ai/ko/blog/kakaotalk-marketing-message-law-korea , 확인 2026-09-08). **미확인**: 낙상 알림이 법적으로 "광고" 예외에 해당한다는 유권해석이나 판례는 찾지 못함 — 서비스 이용약관/개인정보 수집 동의 시점에 알림 발송에 대한 명시적 동의를 받는 편이 안전(설계 권고, 사실 아님).

**국제 참고**: 미국 HIPAA·CMS(Centers for Medicare & Medicaid Services)의 낙상 관련 규정은 국내 요양시설에는 적용되지 않는다 — 본 서비스는 국내 개인정보보호법·노인복지법·노인장기요양보험법 체계를 따라야 한다(원칙적 사실, 별도 출처 불요).

## 유사 솔루션 3~5개 — 오픈소스+상용, 실제 기능 범위

**상용 해외 (3개)**

1. **Vayyar Care** (mmWave 4D 이미징 레이더, 60GHz, 벽 부착형): 1대당 최대 16㎡ 감지, 조명 무관·연기 투과 감지, FCC 인증, 여러 너스콜 시스템과 연동, 비영상(non-optical) 방식으로 프라이버시 보호. 가격은 기기당 **$250** + Amazon Alexa 연동 응급서비스 월 $20(출처: https://vayyar.com/care-pages/how/ , https://www.amazon.com/Vayyar-Care-Touchless-Detection-Subscription/dp/B09JXV82Z6 , 확인 2026-09-08). 단, 이 가격은 가정용(B2C) 제품 기준이며 시설용(B2B) 가격은 별도 협의로 추정(미확인).
2. **SafelyYou** (카메라+AI, 치매요양 특화): NIH 후원 연구(치매케어 시설 11곳) 결과 **낙상 41% 감소, 낙상으로 인한 응급실 방문 69% 감소**, 실시간 감지로 응급서비스 필요성 80% 감소, 자체 발표 감지 정확도 **99%**(출처: https://www.ajmc.com/view/safelyyou-new-research-reveals-safelyyous-aienabled-fall-detection-reduces-need-for-emergency-service-care-in-dementia-care-facilities , 확인 2026-09-08). 넘어짐 영상을 사후 검토해 무목격 낙상의 원인분석을 지원하는 것이 핵심 기능(즉시 알림 + 사후 영상 리뷰).
3. **Kepler Vision "Night Nurse"** (실내 천장형 AI 비전 센서, 네덜란드): 9개월 실증(2020.6~2021.3)에서 낙상 100% 감지, 오탐 99% 이상 감소, 낙상 후 바닥에 방치되는 시간을 1/6 미만으로 단축(출처: https://keplervision.eu/en/one-false-alarm-every-three-months-thanks-to-ai/ , 확인 2026-09-08). 현재 유럽 26개국 14,500명 이상 모니터링, 센서당 오탐 **3개월에 1회**(출처: 동일, 확인 2026-09-08).

**국내 유사 사례 (참고, 확정도 낮음)**

- **하이크비전(Hikvision)**: 지능형 레이더 기반 낙상 감지 솔루션을 국내에 발표(2022)(출처: https://www.hikvision.com/korean/newsroom/latest-news/2022/radar-powered_fall_detection_technology_kr/ , 확인 2026-09-08). 외산 CCTV·보안 기업의 국내 유통 사례.
- **아자이(Azai)**: 레이더 센서 기반 AI 시니어케어 솔루션. 호흡수·맥박·실내 위치를 실시간 수집하고, 화장실·침대 체류시간이 평소 패턴에서 크게 벗어나면 위급 상황으로 판단(출처: https://www.newstomato.com/readnews.aspx?no=1307220 , 확인 2026-09-08). **미확인**: 요양시설(B2B) 대상인지 가정(B2C) 대상인지, 정확도·가격 공개 여부.

**오픈소스 데이터셋 (2개)**

- **UR Fall Detection Dataset**: 낙상 30개 시퀀스 + 일상생활 40개 시퀀스, **CC BY-NC-SA 4.0** 라이선스(비영리 학술 목적)(출처: https://fenix.ur.edu.pl/~mkepski/ds/uf.html , 확인 2026-09-08).
- **Le2i Fall Detection Dataset**: 총 250개 영상(낙상 192 + 일상 57), 4개 장소(가정·커피룸·사무실·강의실)로 구성되며 가정·커피룸만 낙상 구간 어노테이션 포함(출처: https://www.researchgate.net/figure/Le2i-dataset-Fig-3-URFall-dataset-Fig-4-Montreal-dataset_fig2_351757563 , 확인 2026-09-08). **미확인**: 정확한 라이선스 조항(연구 목적 재배포 조건).

## 스택 후보 — 후보별 근거·트레이드오프 (사실만, fit 판정은 메인이 함)

- **비접촉 감지 하드웨어(레이더 센서)**: Milesight VS373(60GHz 4D mmWave)이 확인됨 — 정확도 자체 표기 **"99% Accurate"**(제조사 마케팅 수치, 제3자 검증 여부 미확인), 감지범위 2m×2m~4m×5m(설치높이 2.3~3m), IP65, 천장 매립형(출처: https://www.milesight.com/iot/product/lorawan-sensor/vs373 , 확인 2026-09-08).
- **레이더 모듈(TI, 확정, 2차 조사)**: TI IWR6843ISK(60GHz 장거리 안테나 mmWave 단일칩 평가모듈), IWR6843AOPEVM(Antenna-on-Package 버전 평가보드)이 존재하며, TI 공식 레퍼런스 디자인으로 **TIDEP-01000(mmWave 레이더 기반 인원 계수·추적)**, **TIDEP-01018(mmWave 센서 기반 자동문 제어)**이 확인됨(출처: https://www.ti.com/tool/IWR6843ISK , https://www.ti.com/tool/IWR6843AOPEVM , 확인 2026-09-08). **미확인**: 평가보드 정확 단가(TI.com 검색 결과에 가격 미표시, 리셀러 확인 필요), TI 공식 "낙상 감지" 전용 레퍼런스 디자인 존재 여부 — 이번 검색에서 인원계수·자동문 레퍼런스만 확인되고 낙상 전용 디자인은 찾지 못함.
- **엣지 SoC(확정, 2차 조사)**: Raspberry Pi 5 + AI HAT+(Hailo-8L, 13 TOPS) 구성은 Hailo-8L 모듈 약 $70 + Pi 5 호스트 약 $80(2026년 초 40 TOPS급 Hailo-10H AI HAT+도 출시); NVIDIA Jetson Orin Nano Super는 $249, INT8 기준 최대 67 TOPS(출처: https://www.myaihardware.com/compare-article/jetson-orin-nano-super-vs-raspberry-pi-5-hailo , https://www.notebookcheck.net/The-Nvidia-Jetson-Orin-Nano-Super-a-powerful-generative-AI-SBC-is-now-available-worldwide-for-249.934029.0.html , 확인 2026-09-08). → 저가·경량 추론이면 Pi 5+Hailo-8L, 온디바이스 비전 모델 성능이 중요하면 Orin Nano Super가 TOPS/달러 우위(사실 기반 비교, fit 판정은 메인 담당).
- **엣지↔서버 메시징(MQTT, 확정, 2차 조사)**: **EMQX는 v5.9부터 Community/Enterprise를 통합해 Business Source License(BSL) 1.1로 전환**(2025년 5월 7일 발표) — 단일 노드 프로덕션 사용은 "Additional Use Grant"로 무료 허용, 4년 후 각 버전이 자동으로 Apache 2.0으로 환원(출처: https://www.emqx.com/en/blog/adopting-business-source-license-to-accelerate-mqtt-and-ai-innovation , 확인 2026-09-08). **Eclipse Mosquitto는 계속 EPL/EDL 오픈소스 유지, 최신 버전 2.1.2(2026년 2월 9일 릴리스, 버그픽스)**(출처: https://mosquitto.org/blog/2026/02/version-2-1-2-released/ , 확인 2026-09-08). → 다중 노드 클러스터가 필요하면 EMQX는 BSL 조건 검토 필요, Mosquitto는 라이선스 제약이 적으나 클러스터링 기능은 별도 확인 필요(미확인).
- **DB(확정, 2차 조사)**: PostgreSQL 18이 2025년 9월 25일 출시, 2026년 중반 기준 PostgreSQL 19가 베타 단계(출처: 위키백과 PostgreSQL 항목 경유 WebSearch 요약, 확인 2026-09-08 — **1차 출처 postgresql.org 미대조, 재검증 권고**). TimescaleDB는 이중 라이선스 — **오픈소스(Apache 2.0) 에디션은 하이퍼테이블·time_bucket 등 기본 기능**, **TSL(Timescale License) 에디션은 컬럼형 압축·연속 집계·보존정책·블룸 인덱스 등 고급 기능**이며, TimescaleDB를 호스팅형 DBaaS로 재판매하지 않는 한 커뮤니티 기능은 무료(출처: https://www.tigerdata.com/legal/licenses , https://www.tigerdata.com/docs/get-started/choose-your-path/timescaledb-editions , 확인 2026-09-08). 최신 안정판은 2.28.3(2026년 7월 16일)(출처: 동일, 확인 2026-09-08).
- **국내 알림 대행(NHN Cloud, 확정+미확인, 2차 조사)**: NHN Cloud는 카카오 알림톡 **발송 실패 시 SMS 자동 대체발송 기능을 제공**(콘솔에서 대체발송 여부 설정, 발신번호 필요, 카카오 발송 결과가 실패로 확정된 뒤 대체발송되며 문자 수신까지 최대 수십 초 소요)(출처: https://docs.nhncloud.com/ko/Notification/KakaoTalk%20Bizmessage/ko/alimtalk-console-guide/ , 확인 2026-09-08). **미확인**: 건당 정확한 요금(원 단위), 발송 결과 웹훅 콜백의 구체적 스펙 — NHN Cloud 요금 페이지(nhncloud.com/kr/pricing)를 이번 검색에서 직접 열람하지 못함, 별도 접속 필요.
- **푸시(확정, 2차 조사)**: iOS **Critical Alert entitlement**는 방해금지모드·무음 스위치를 모두 무시하고 소리를 재생할 수 있으나 **Apple의 특별 승인이 필요**하며, "의료(투약 알림 등)·안전(긴급 기상·응급 서비스)" 등 좁은 범주로 제한되고 심사(수일~수 주)를 거친다(출처: https://newly.app/how-to/critical-alerts-entitlement , 확인 2026-09-08). **Time-Sensitive 알림**은 Focus 모드·알림 요약을 뚫을 수 있고 **Apple 별도 승인 없이** Time-Sensitive Notifications entitlement 선언만으로 사용 가능(출처: 동일, 확인 2026-09-08). → 야간 직원 알림(방해금지 상태의 스마트폰)이 반드시 뚫려야 한다면 Critical Alert 승인 신청이 필요할 가능성(요건 자체는 확정, 이 서비스가 "안전" 범주로 승인받을지는 미확인). Android FCM 무료 여부·전송보장 관련 구체 수치는 이번 회차에서도 미확인.
- **서버(FastAPI/NestJS/Go), 모바일(Flutter/React Native)**: 이번 2차 조사에서도 검색 예산을 위 항목에 우선 배정해 **미확인**으로 남긴다. A3~A4 단계에서 별도 검색 필요.

## 정확도·지연 실측 표본 (데이터 의존 상수)

| # | 출처/제품 | 측정 조건 | 수치 | 확인 |
|---|---|---|---|---|
| 1 | SafelyYou (AJMC, NIH 후원 연구) | 치매케어 시설 11곳 | 낙상 41%↓, 낙상 관련 ER 방문 69%↓ | https://www.ajmc.com/view/safelyyou-new-research-reveals-safelyyous-aienabled-fall-detection-reduces-need-for-emergency-service-care-in-dementia-care-facilities , 2026-09-08 |
| 2 | SafelyYou (자체 발표) | 비공개 | 감지 정확도 99% (제3자 검증 여부 미확인) | 동일, 2026-09-08 |
| 3 | Kepler Night Nurse | 9개월 실증(2020.6~2021.3), 유럽 요양시설 | 낙상 100% 감지, 오탐 99%↓, 바닥 방치시간 1/6 미만으로 단축 | https://keplervision.eu/en/one-false-alarm-every-three-months-thanks-to-ai/ , 2026-09-08 |
| 4 | Kepler Night Nurse | 현재 운영(2026년 기준 서술) | 센서당 오탐 3개월에 1회, 14,500명+ 모니터링, 26개국 | 동일, 2026-09-08 |
| 5 | Milesight VS373 (mmWave 레이더 센서) | 제조사 스펙시트 | "99% Accurate Fall Detection"(제3자 검증 미확인) | https://www.milesight.com/iot/product/lorawan-sensor/vs373 , 2026-09-08 |
| 6 | 업계 일반(출처 불명확) | 웹 검색 요약 결과 | "낙상 감지 시스템의 평균 대응시간 업계 표준 90초" | WebSearch 결과 요약, 원 출처 특정 논문/문서 미확인 — **신뢰도 낮음, 재검증 필요** |
| 7 | UR Fall Detection Dataset | 학술 벤치마크용 | 낙상 30 시퀀스 + 일상 40 시퀀스 (정확도 수치 아님, 데이터 규모) | https://fenix.ur.edu.pl/~mkepski/ds/uf.html , 2026-09-08 |
| 8 | Le2i Fall Detection Dataset | 학술 벤치마크용 | 250개 영상(낙상 192 + 일상 57) (정확도 수치 아님, 데이터 규모) | 위 리서치게이트 그림 출처, 2026-09-08 |
| 9 | 전통적(비AI) 낙상 알람 — 미국 4개 병원 콜라이트 응답시간 연구 (Kepler 블로그 인용) | 병원 콜라이트 응답시간 실측 | 평균 약 13분, 범위 3~17분+, 최악 20~21분 | https://keplervision.eu/en/blog/what-is-the-average-response-time-for-fall-prevention-alarms-for-elderly/ , 2026-09-08 |
| 10 | Kepler 자체 현장 실측치 | 요양시설 실측(도착 소요시간) | 최대 21분 소요 사례 존재 | 동일, 2026-09-08 |
| 11 | AI 기반 낙상 감지 시스템 일반(Kepler 블로그 요약, 원 출처 불명확) | 제품 비교 서술 | 일반 감지 30초~2분, "터보 모드" 약 10초 | 동일, 2026-09-08 — **1차 연구 원문 미대조, 마케팅 서술 가능성 있어 재검증 필요** |
| 12 | 침상이탈센서(bed-exit sensor) 병원 평가 (Kepler 블로그 인용) | 병원 1곳 평가 | 오탐(nuisance alert) 비율 약 1/3 | 동일, 2026-09-08 |

**미확인**: 국내 요양시설 실측(파일럿) 데이터, 민감도/특이도를 표 형태로 명시한 논문 원문(JAMDA scoping review는 접근 403으로 확인 실패, arxiv 2503.19501·2505.11845는 접근 실패/미대조) — 재시도 필요. 표 #6·#11의 "업계 표준 90초"·"AI 시스템 30초~2분" 수치는 모두 1차 연구·규격 원문이 아닌 웹 요약/제품사 블로그 경유로만 확인되어 **신뢰도가 낮다** — PRD 상수로 채택하려면 원 논문(예: 병원 콜라이트 응답시간 연구)이나 IEC/AAL 규격 원문 재확인이 필요.

## 단계별 사전조사 근거 후보 (A3~A7)

**A3 PRD**
- 정량: Kepler Night Nurse 실증치 "낙상 100% 감지·오탐 99%↓·바닥 방치시간 1/6 미만"이 PRD의 성능 목표(KPI) 벤치마크 후보(출처: 위 표 #3, 확인 2026-09-08).
- 정성(확정, 2차 조사): 28년 경력 요양보호사 인터뷰 — "야간 근무를 서고 나면 남은 사람들의 업무 강도는 상상을 초월한다", "어르신들은 천천히 모셔야 하는데 현장에선 계속 '빨리빨리'를 외치며 다음 업무로 넘어가야 하는 현실"(출처: https://www.womaneconomy.co.kr/news/articleView.html?idxno=254457 , 확인 2026-09-08). 별도 기사에서는 방문요양 야간 근무 요양보호사가 "밤을 꼬박 새우니까 면역기피증 같은 질환도 생기고, 유방암도 많이 발생한다"고 증언(출처: https://www.pressian.com/pages/articles/2026080610473276792 , 확인 2026-09-08). **주의**: 두 기사 모두 시설 입소형 요양원이 아닌 방문요양·복합 근무 맥락 인용도 섞여 있어, 본 서비스가 겨냥하는 "시설 야간 순회" 상황과 정확히 일치하지 않을 수 있음(맥락 구분 필요).
- 사용자 영향: 인력배치 강화(2025년 2.1:1)에도 야간 1인당 관리 입소자 수가 여전히 많아, 비접촉 자동 감지가 순회 공백을 메우는 안전망으로 포지셔닝될 근거가 있음(추론, 배치기준 확정 사실 기반).

**A4 아키텍처**
- 정량: Vayyar Care 1대당 감지범위 16㎡, Milesight VS373 최대 20㎡(4m×5m) — 시설 1개 침실/공용공간당 센서 대수 산정의 근거 수치(출처: 위 유사솔루션·스택 섹션, 확인 2026-09-08).
- 정성(확정, 2차 조사): 국내 병원 간호사 대상 연구 — "알람 피로(alarm fatigue)는 간호사의 업무 성과 저하 및 지각된 스트레스 증가와 연관되며, 환자안전문화가 이 관계를 조절한다"는 결과가 한국 병원 간호사 대상 횡단연구에서 보고됨(출처: https://pmc.ncbi.nlm.nih.gov/articles/PMC13299418/ , 확인 2026-09-08). 국제 문헌에서도 "지능형 필터링 없이 오탐을 걸러내지 못하면 의료진의 감각 과부하를 유발하고, 잦은 오탐으로 임상의가 알람을 무시하게 될 수 있다"는 지적이 확인됨(동일 계열 검토, 확인 2026-09-08). **주의**: 이 연구는 병원(중환자실 등) 간호사 대상이며 요양시설 요양보호사 대상 알람피로 연구는 이번 검색에서 찾지 못함 — 유사 맥락 인용으로만 사용.
- 사용자 영향: CCTV 60일 보관·3년 열람대장 요건(위 규제 (a))이 아키텍처의 저장소 보존기간·감사로그 설계에 직접적 제약으로 작용(확정 사실 기반 추론). 알람피로 연구(위 정성)는 A4에서 "오탐률을 낮추는 임계값·검증 로직"을 1순위 설계 요건으로 삼을 근거가 됨.

**A5 API**
- 정량: **미확인** — 유사 제품의 API/웹훅 스펙 공개 여부를 검색하지 못함.
- 정성: **미확인**.
- 사용자 영향: 알림톡 발송이 "정보성 메시지"로 분류되려면 API 설계상 "낙상 이벤트 → 즉시 발송" 트리거와 사전 동의 기록을 연결해야 함(위 규제 (f) 기반 추론).

**A6 테스트**
- 정량: UR Fall/Le2i 데이터셋 규모(표 #7, #8)가 오탐/미탐 테스트셋 구성의 출발점이 될 수 있음.
- 정성: **미확인**.
- 사용자 영향: 두 데이터셋 모두 소규모(수십~수백 개 영상)이므로 국내 요양시설 환경(조명, 침대 배치)에 대한 재학습·검증셋 별도 구축이 필요할 가능성(추론).

**A7 운영**
- 정량: 열람대장 3년 보관, 영상 60일 보관 원칙(규제 (a))이 운영 단계 로그 보존·삭제 정책의 확정 제약.
- 정성: **미확인** — 정전·네트워크 장애 시 대응 사례를 찾지 못함.
- 사용자 영향: 60일 삭제 원칙과 예외(안전 확인 목적 열람 요청 시 보존)가 충돌하는 운영 프로세스(누가 언제 "삭제 보류"를 트리거하는지)를 설계해야 함(확정 규정 기반 추론).

## 검색 쿼리 목록

1. 노인장기요양보험법 CCTV 설치 의무화 개정 시행일 — https://www.mohw.go.kr/board.es?mid=a10503000000&bid=0027&act=view&list_no=376156
2. 장기요양기관 폐쇄회로텔레비전 설치 노인복지법 시행규칙 보관기간 — https://ggscw.or.kr/s5_4/74
3. 요양시설 낙상 발생률 통계 국내 노인요양원 — https://www.nongmin.com/article/20251020500272
4. 장기요양기관 야간 요양보호사 배치기준 입소자 비율 — https://xn--zb0bnwa350v8db07b.com/kwa-5870889-191
5. 장기요양기관 영상정보처리기기 개인정보보호법 민감정보 처리 근거 — https://www.carefor.co.kr/ct_att/contents_article/0/202306/44113/56VVsReMFl.pdf
6. 낙상 감지 소프트웨어 의료기기 식약처 디지털의료제품법 판단 사례 — https://www.lexology.com/library/detail.aspx?g=516ed348-16f2-48f0-9545-dc8a061790d5
7. 노인복지법 노인의료복지시설 안전기준 낙상 예방 조항 — https://law.go.kr/flDownload.do?flSeq=90258299
8. 2025년 장기요양기관 시설급여 평가매뉴얼 낙상 지표 — https://www.carefor.co.kr/ct_att/contents_article/0/202501/45442/XFGkuXsCpb.pdf
9. 카카오 알림톡 SMS 발송 정보통신망법 전송자 사전동의 예외 고지 — https://kakaobusiness.gitbook.io/main/ad/infotalk
10. Vayyar Care fall detection mmWave radar sensitivity accuracy nursing home price — https://vayyar.com/care-pages/how/
11. SafelyYou fall prevention AI camera dementia care sensitivity specificity study — https://www.ajmc.com/view/safelyyou-new-research-reveals-safelyyous-aienabled-fall-detection-reduces-need-for-emergency-service-care-in-dementia-care-facilities
12. Kepler Vision Night Nurse Inspiren fall detection nursing home 2025 2026 — https://keplervision.eu/en/one-false-alarm-every-three-months-thanks-to-ai/
13. 국내 요양원 낙상 감지 레이더 카메라 제품 스타트업 2025 — https://www.newstomato.com/readnews.aspx?no=1307220
14. UR Fall Detection Dataset Le2i fall detection dataset license github — https://fenix.ur.edu.pl/~mkepski/ds/uf.html
15. fall detection alert latency seconds nursing home study elderly care — https://www.jamda.com/article/S1525-8610(24)00752-7/fulltext (본문 접근 403 실패, 제목·초록만 참고)

**2차 조사 (16~27)**

16. EMQX license BSL business source license 2026 latest release Mosquitto version — https://www.emqx.com/en/blog/adopting-business-source-license-to-accelerate-mqtt-and-ai-innovation , https://mosquitto.org/blog/2026/02/version-2-1-2-released/
17. PostgreSQL latest major version 2026 release TimescaleDB license Apache 2 community edition TSL — https://www.tigerdata.com/legal/licenses
18. NHN Cloud 카카오 알림톡 SMS 건당 요금 발송 결과 웹훅 실패시 대체발송 — https://docs.nhncloud.com/ko/Notification/KakaoTalk%20Bizmessage/ko/alimtalk-console-guide/
19. iOS Critical Alert entitlement Time Sensitive notification 방해금지모드 무음 뚫기 요건 — https://newly.app/how-to/critical-alerts-entitlement
20. Raspberry Pi 5 AI HAT+ Hailo TOPS price Jetson Orin Nano Super price TOPS — https://www.myaihardware.com/compare-article/jetson-orin-nano-super-vs-raspberry-pi-5-hailo
21. TI IWR6843ISK AOP evaluation board price fall detection reference design occupancy — https://www.ti.com/tool/IWR6843ISK , https://www.ti.com/tool/IWR6843AOPEVM
22. 요양보호사 야간 근무 순회 부담 인터뷰 기사 — https://www.womaneconomy.co.kr/news/articleView.html?idxno=254457 , https://www.pressian.com/pages/articles/2026080610473276792
23. 병원 요양시설 알람 피로 오경보 간호사 인용 연구 — https://pmc.ncbi.nlm.nih.gov/articles/PMC13299418/
24. 식약처 디지털의료기기 판단기준 가이드라인 비의료기기 예시 웰니스 활동 낙상 모니터링 — https://www.hitnews.co.kr/news/articleView.html?idxno=31377
25. 개인정보보호위원회 건강정보 민감정보 판단 기준 개인정보 영향평가 의무대상 공공기관 민간 — https://www.privacy.go.kr/front/per/iass/duty/effectsEvalutionDuty.do , https://www.easylaw.go.kr/CSP/CnpClsMainBtr.laf?popMenu=ov&csmSeq=1257&ccfNo=2&cciNo=3&cnpClsNo=1
26. 국민건강보험공단 장기요양기관 수 입소자 수급자 통계연보 2025 — https://www.asemgac.or.kr/bbs/bbs/view.php?bbs_no=64&data_no=10231 , https://www.data.go.kr/data/15124763/fileData.do
27. fall detection response time seconds alert notification study caregiver AAL standard — https://keplervision.eu/en/blog/what-is-the-average-response-time-for-fall-prevention-alarms-for-elderly/

(보조 WebFetch, 검색 횟수에 미포함): mohw 보도자료 원문 페치 실패 상세 확인, Kepler 오탐 페이지 페치, Milesight VS373 스펙 페치, JAMDA(403)·arxiv 2503.19501(404) 접근 실패, Kepler 응답시간 블로그 페치, hitnews 웰니스 기준 기사 페치.

## 스택 후보 fit 판정 (메인 — 사용자 제약 매칭: 팀 2~4인 Assumed · 파일럿 ≤100침상 · 온프레미스 GW + 클라우드 · 예산 미상 → OSS 우선)
fit 기준은 `references/evidence-map.md`의 5요소(요구 충족·팀 적합·운영 환경·생태계 건강·총비용). 인기 지표는 근거의 하나일 뿐이다.

| 영역 | 선택 (fit) | 왜 | 기각한 대안 |
|---|---|---|---|
| 감지 센서 | **완제품 mmWave 낙상 센서** — Milesight VS373 계열(LoRaWAN, 천장형, 감지범위 ≤4×5m) (fit 높음) | 낙상 판정 알고리즘이 센서에 내장 → MVP는 "감지"를 사지 "만들지" 않는다(search-first 원칙). 비영상(Q1 A)·조명 무관·화장실 설치 가능. 정확도 "99%"는 제조사 주장이므로 **파일럿 실측(SC)으로 검증**하고, 센서 인터페이스는 어댑터로 격리해 벤더 교체 가능하게 | TI IWR6843 + 자체 알고리즘(fit 중간, 2단계 후보 — 데이터 수집·모델 개발이 MVP 범위를 넘음) · 카메라+Jetson(Q1 B, fit 낮음 — 화장실 불가·동의 부담) |
| 센서↔게이트웨이 무선 | **LoRaWAN** (센서 종속) (fit 높음) | 시설 Wi-Fi 커버리지 미확인(01 A7 정성 미확인) → 건물 관통 LoRa가 설치 리스크를 줄임 | Wi-Fi 센서(커버리지 의존) · BLE(층간 불가) |
| 시설 게이트웨이 | **상용 LoRaWAN 게이트웨이 1대 + Raspberry Pi 5(RTC 모듈 장착) 엣지 앱** (fit 높음) | AI HAT 불필요(추론은 센서 내장). Pi 5는 이벤트 버퍼·하트비트·로컬 규칙만 → 저가·구하기 쉬움. RTC는 P1 시계 드리프트 대책 | Pi 5 + LoRa 컨센트레이터 HAT + ChirpStack 1박스(2단계 최적화 후보) · Jetson Orin Nano($249, 67 TOPS — 추론 불필요라 과잉) |
| 게이트웨이↔클라우드 | **MQTT over TLS, 브로커 Eclipse Mosquitto 2.1.2** (EPL/EDL) (fit 높음) | 게이트웨이 수 = 시설 수(수십)라 단일 노드로 충분, 라이선스 제약 없음 | EMQX 5.9+ (BSL 1.1 — 단일 노드 무료지만 클러스터 시 조건 검토, 파일럿엔 과잉) |
| 서버 | **NestJS (TypeScript, Node 22 LTS)** (fit 높음, 설계 결정 DL-10) | 웹·모바일·서버를 **TS 한 언어**로 → 2~4인 팀의 학습·채용 비용 최소. 처리량은 시설당 이벤트 수십/일이라 성능은 결정 변수가 아님. ML 툴링 불필요(감지는 센서 내장) | FastAPI(Python — ML 툴링이 필요할 2단계에 재검토) · Go(성능 불필요, 팀 적합 미상) |
| DB | **PostgreSQL 18** (RLS로 테넌시 강제, 시간 파티셔닝) (fit 높음) | 이벤트 연 수십만 행 → TimescaleDB 불필요. 필요해지면 Apache 2.0 에디션 추가 | TimescaleDB 즉시 도입(과잉) · NoSQL(감사·관계 무결성 요구에 부적합) |
| 푸시 | **FCM + APNs**; iOS는 Time-Sensitive 기본 + **Critical Alert 승인 신청**(착수 조건) (fit 높음) | 야간 방해금지 관통은 안전 요건. Critical Alert 승인은 미확인 → 승인 전엔 Time-Sensitive + 관제 화면·알림톡으로 보완 | 자체 소켓 푸시(백그라운드 제약) |
| 알림톡·SMS | **NHN Cloud 알림톡 → SMS 자동 대체발송** (fit 높음) | 대체발송이 콘솔 기능으로 확정. 요금은 미확인 → 상수 표에 미확인 표기, SC엔 미사용 | 직접 카카오 채널 API(운영 부담) |
| 모바일 | **React Native (Expo)** — 직원 앱·보호자 앱 (fit 높음, DL-10) | TS 통일. Critical Alert·Time-Sensitive는 config plugin으로 네이티브 설정 가능 | Flutter(UI 일관성 우수하나 Dart 추가 = 언어 2개) |
| 웹 관제 | **React + Vite + Tailwind** (fit 높음) | frontend-design-taste 스택 특화, TS 통일 | Next.js(SSR 불필요) |
| 인프라 | **Docker Compose(파일럿) → 관리형 PG + 컨테이너(다시설)**, IaC는 Terraform 스케치 (fit 높음) | 파일럿 1시설은 VM 1대로 충분. 상세는 07 | k8s(과잉) |

미확인으로 남긴 것: NHN Cloud 건당 요금, Critical Alert 승인 가능성, TI 모듈 단가, 요양시설 입소자·기관 수 원문 — 03 상수 표에 '미확인'으로 표기하고 SC 목표에 쓰지 않는다 (check_package C7).
