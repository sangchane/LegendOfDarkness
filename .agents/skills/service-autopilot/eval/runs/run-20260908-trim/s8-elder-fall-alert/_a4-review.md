# A4 독립 검토 결과 — 04-architecture.md (낙상 감지·알림) · fresh-reviewer(fable) · 2026-09-08 15:0x

(생성 에이전트가 한도로 끊겨 받지 못한 검토관 회신을 메인이 그대로 저장한 것. 재실행하지 말고 이 파일을 읽어 타당성 필터링 후 반영한다.)

대상: `04-architecture.md` (기준 03-prd.md, 보조 02-blindspot-register.md·00-seed.md·decision-log.md·01-recon.md 스택 fit 절).

## 발견 목록 (심각도순)

1. **CRITICAL** · 03:FR-001/FR-005/FR-006 + 04:데이터 저장(fall_event)·시나리오 3 — FallEvent에 `resident`가 없다(zone·device만). 요양시설 침실은 다인실이 표준이고 센서는 침실당 1대(02 축2)인데, "누가 넘어졌는지"를 정하는 규칙이 PRD·아키텍처 어디에도 없다. 시나리오 3 line 165는 `보호자 목록(resident)`을 조회하지만 resident 출처가 없고, resolve 페이로드 `{status, action, transported, label}`에도 입소자 식별이 없다. 따라서 FR-006·INV-3(동의는 입소자 단위)·FR-007 배지(구역↔입소자 조인)가 실현 불가. 또한 4인실에 감지 미동의 입소자 1명이 있으면 센서가 전원을 감지하는 문제도 미다룸. → 03에 "resolve 시 입소자 선택(구역 내 입소자 목록, 필수)"과 다인실 동의 정책을 추가하고, 04에 zone↔bed↔resident 배정 모델과 `fall_event.resident_id`(resolve 시 확정)를 넣어라.

2. **HIGH** · 04:Context line 10, ③ "대행사 장애"(관제 화면이 최후 경로), ④ 1 — 알림 경로 전체가 하나의 실패 도메인이다: 시설 인터넷 회선 1개 → Pi 1대 → Mosquitto 1노드 → NestJS 1인스턴스. 관제 PC도 같은 시설 회선으로 클라우드에 붙으므로 "관제가 최후 경로"라는 주장은 성립하지 않는다. 회선 단절 시 최대 EDGE_BUFFER_HOURS 동안 낙상 알림 0건이며 ④ 잔여 리스크는 "정전·재밍 중 감지 불능"만 적고 인터넷 단절 중 알림 불능은 빠졌다(02 P1 연결 끊김은 이를 04 ④에 위임했다). → 최소한 ④에 명시 + 시설 고지, 설계 옵션으로 엣지 로컬 경보(LAN 관제/릴레이) 또는 LTE 백업 회선을 검토하라.

3. **HIGH** · 04:시나리오 1 line 119-125, 시나리오 2 — ack-timeout 잡 등록이 이벤트 INSERT·푸시 발송과 원자적이지 않다(생성 → 푸시 → 잡 순서). 그 사이 크래시하면 타이머 없는 이벤트가 생기고, 푸시까지 실패하면 아무도 모른다. 잡 핸들러 실패(음성 대행 오류) 시 pg-boss 재시도 정책도 미명시. SC-004 "100%"를 구조로 보장하지 않는다. → 이벤트 INSERT와 잡 INSERT를 같은 트랜잭션에(pg-boss db 옵션), `detected` 상태로 STAFF_ACK_TIMEOUT_SEC 초과 + 에스컬레이션 플래그 없는 이벤트를 찾는 스위퍼 잡을 추가하라.

4. **HIGH** · 04:컴포넌트 UP "QoS1, 재전송", 검토한 대안 MQTT 행 — 엣지 버퍼의 `sent_at`이 브로커 PUBACK 기준이면 앱이 DB 커밋 전에 죽거나 ingest 구독이 clean session이면 이벤트가 유실된다(SC-009 위반). 브로커 영속성·persistent session·수동 ack 여부가 없다. → 애플리케이션 레벨 ack(서버가 `f/{facility}/ack` seq 발행, 엣지는 그때 sent_at 기록) 또는 DB 커밋 후 MQTT ack로 바꿔라.

5. **HIGH** · 04:ING line 77/118 "중복 병합" — 재전송 중복을 잡는 멱등키가 없다. DEDUP_WINDOW_SEC 시간창 병합은 재감지용이지 재전송용이 아니며, 버퍼 재생으로 3시간 뒤 도착한 동일 이벤트는 received_at 기준이면 새 이벤트+새 알림이 된다. 또 EC-A1은 "열린 이벤트"에만 병합인데 04는 상태 조건이 없어 resolved 직후 두 번째 실제 낙상을 흡수할 수 있다. → `UNIQUE(device_id, seq)` 인제스트 멱등키 + 병합 대상은 `state IN (detected, acked)`로 명시하라.

6. **HIGH** · 04:시나리오 1 line 115 "t=detected_at", Context "NTP" · 03:INV-7, SC-003 — detected_at을 찍는 시계가 미정이다(센서? GW 내장 NS? Pi RTC?). 시계가 세 개(GW·Pi·직원 폰)인데 엣지↔서버 오프셋 측정이 없어 SC-003(detected_at↔앱 표시)과 LATE_EVENT_SEC 판정이 오염된다. → detected_at = edge-agent RTC(로컬 MQTT 수신 시각)로 고정, 메시지마다 `sent_at`을 실어 서버가 오프셋을 계산·하트비트에 NTP 동기 상태 포함, 오프셋 초과 이벤트는 SC-003 집계에서 표시.

7. **HIGH** · 03:FR-017(P0)/EC-A2 · 04 전체 — `LATE_EVENT_SEC`·"지연 도착" 등급·`EDGE_BUFFER_HOURS` 초과 시 정책(오래된 것부터 버림?)이 04에 한 번도 나오지 않는다. 게다가 EC-A2대로 24시간 전 이벤트를 전체 직원 알림+에스컬레이션 전화에 태우면 새벽 3시 시설장 통화가 발생한다. → ingest에 지연 판정 분기를 넣고, LATE 이벤트는 이벤트 생성+관제·관리자 알림만 하고 에스컬레이션 타이머는 걸지 않는 쪽으로 03과 함께 정하라.

8. **HIGH** · 04:① DFD, ② STRIDE 표 — GW→엣지 로컬 MQTT(시설 LAN)에 trust boundary가 없다. 시설 LAN에는 법정 CCTV·직원 PC·손님 Wi-Fi가 같이 있고, 로컬 MQTT는 보통 평문·무인증이라 LAN 접근자가 가짜 낙상 주입·하트비트 위조/억제(DoS)가 가능하다. 빠진 경계 3개: LoRaWAN GW 관리 UI(기본 계정, AppKey가 GW 내장 NS에 저장 — A4 자산에 미포함), Pi 물리 접근(SD카드에 X.509 개인키+SQLite 이벤트, 디스크 암호화·도난 탐지 없음), 관제 공용 PC(공유 로그인 → 행위자 귀속·부인 방지 약화). → TB1.5 추가: GW↔Pi 전용 포트/VLAN + 로컬 MQTT 인증·TLS, GW 관리 UI 계정 변경·LAN 격리, Pi 키 보호, 관제는 개인 로그인+화면 잠금.

9. **HIGH** · 03:FR-008/EC-D1 용어집 device(센서·게이트웨이) · 04:Context line 10 — 04가 PRD에 없는 세 번째 장치(엣지 노드 Pi)를 도입했지만 Pi 단독 장애·LoRaWAN GW 단독 장애를 클라우드가 어떻게 구분하고 EC-D1 그룹 판정을 하는지 없다. 02 P1이 Assumed한 read-only rootfs·HW watchdog·A/B OTA가 04에 전혀 없다(SQLite WAL·log2ram만). "게이트웨이 1~2대"인데 Pi는 몇 대인지도 없다. → device 종류에 edge_node 추가, Pi가 로컬 MQTT 단절을 하트비트에 실어 보고, P1 대책을 04 Cross-cutting에 옮겨라.

10. **MEDIUM** · 04:컴포넌트 구조 line 87-95 vs 시나리오 1·2·3 — 시퀀스는 `ALT→RT`(line 124, 149), `API→NTF`(167), `ALT→audit`(144), `J→EVT/ALT`(141-146)를 쓰는데 컴포넌트 다이어그램에 그 엣지가 없다. 컨텍스트 다이어그램 APP 라벨은 모듈 7개, 컴포넌트는 10개(devices·consent·audit 누락). → 다이어그램을 시퀀스에 맞춰 갱신.

11. **MEDIUM** · 04 규칙 9 위반 — line 174 "3년 보존"(EVENT_RETENTION_YEARS 값 재기입), line 5 "100침상"(PILOT_MAX_BEDS), line 229 "타임스탬프 5분 창"(03 표에 없는 새 상수, 이름 없음), line 231 "일 1회 체인 검증"(새 운영 상수). → 값 대신 이름, 새 상수 2개는 03 표로 승격.

12. **MEDIUM** · 04:② vs ③ — ② TB3 D "rate limit"이 ③에 없고, ack 엔드포인트를 IP로 제한하면 시설 NAT 뒤 직원 전원이 다발 낙상 때 막힐 수 있다. ② TB5 E "MFA"는 ③에 없다(④에만). 미다룬 위협: 보호자 연결 코드(FR-012) 브루트포스→타인 입소자 건강정보 열람, 외부 API 키 유출로 우리 명의 SMS/알림톡 발송(TB4 S는 콜백만), A5 I "해당없음"이지만 BYOD 개인폰 잠금화면 미리보기에 "302호 낙상" 노출, 퇴직 직원 계정 회수. → ③에 항목 추가, ack는 계정 단위 완화 한도로.

13. **MEDIUM** · 04:데이터 저장 audit_log, 구현 접근 RLS 행 — 해시 체인이 전역 1개면 모든 감사 INSERT가 체인 헤드 잠금으로 직렬화되고 시설 간 결합이 생긴다; 시설별 체인인지 미정. 검증 잡·파기 잡·MQTT ingest·pg-boss 핸들러·WSS 푸시는 요청 컨텍스트가 없는데 `app.facility_id`를 어떻게 설정/우회하는지 없다. 앱 롤이 테이블 오너면 RLS가 조용히 우회되는데 FORCE RLS·마이그레이션 롤 분리 언급 없음. → 시설별 체인, 배경 작업용 컨텍스트 규약, 오너/앱 롤 분리 명시.

14. **MEDIUM** · 04:검토한 대안, Cross-cutting — 사이징 근거가 "이벤트 수십/일"뿐이다. 실제 지배 부하는 하트비트: 60대×1/분 = 시설당 8.6만 행/일, 50시설·30일이면 약 1.3억 행인데 하트비트 저장 모델·HEARTBEAT_RAW_RETENTION_DAYS 참조가 없다(fall_event만 파티셔닝). WSS는 앱 인스턴스 2개(HA를 위해 필요)부터 인스턴스 간 pub/sub이 필요한데 Redis를 배제하고 대안(LISTEN/NOTIFY·Mosquitto 재사용)이 없다 — 50시설이 아니라 "인스턴스 2개"에서 먼저 깨진다. → 하트비트 롤업 표 + 파티셔닝, RT 팬아웃 매체 결정.

15. **MEDIUM** · 04:구현 접근 line 59, ④ 1 — ALERT_LATENCY_P95를 홉별(LoRaWAN 업링크·NS·Pi·MQTT·DB·APNs/FCM 배달)로 배분한 지연 예산이 없어 15초 p95 가능성이 논증되지 않았다(DL-12도 "설계상 가능"뿐). Time-Sensitive는 iOS 무음 스위치를 못 뚫는다(Critical만) — ④는 방해금지만 적고 야간 무음 폰을 빠뜨렸다. → 홉별 예산표, 무음 폰을 잔여 리스크와 SC-004 시험 조건에 추가.

16. **MEDIUM** · 04:구현 접근 line 64 "센서별 억제 스위치" · 03 FR 없음 — PRD에 없는 기능이며 억제 중 실제 낙상은 알림이 안 간다. 자동 만료·관제 배지·감사가 없다. → 03에 FR로 올리거나 제거; 두려면 만료 시간·배지·감사로그 필수.

17. **MEDIUM** · 04 Cross-cutting · 02 P2 백업·DR(Assumed) · 03 RPO_HOURS/RTO_HOURS/SC-012 — 백업·복원 리허설·롤링 배포·HA가 04에 전무하다. SLO 99.5%(월 3.6h 다운)를 안전 알림 경로에 그대로 두는 것이 타당한지 논의도 없다. → 배포·DR 절을 추가(07로 넘기더라도 04에 구조 결정은 있어야 함).

18. **MEDIUM** · 04:Context line 10 · 01-recon line 166 · 03 상수 표 DL-15 — "상용 LoRaWAN GW의 내장 NS가 로컬 MQTT를 발행한다"는 능력이 모델 미확정 상태의 가정이고, "GW 펌웨어는 버퍼·재전송을 못 담는다"는 대안 기각 근거도 출처가 없다. SENSOR_HEARTBEAT_SEC=60·SENSOR_OFFLINE_AFTER_SEC=300은 LoRaWAN 센서의 실제 업링크 주기(배터리형은 수 분~수십 분이 흔함)와 대조되지 않아 FR-008·SC-008이 항상 오프라인으로 판정될 수 있다. → GW 모델·센서 보고 주기를 08 착수 조건으로 올리고 상수 근거를 스펙으로 바꿔라.

19. **LOW** · 04:데이터 저장 line 174 · decision-log DL-10 — 3년에 약 3.6만 행인 fall_event를 월 파티셔닝하는 것은 과잉(파기는 retention 잡으로 충분). PostgreSQL 18(최신 메이저) 선택 근거가 DL-10에 없다(pg-boss 호환·관리형 서비스 가용성). → 파티셔닝은 하트비트에만, PG 버전은 LTS급으로 근거 명시.

20. **LOW** · 04:컴포넌트 구조 — FR-009의 "열람"·"설정 변경" 감사, FR-011 당직 구역 on/off 저장소(ALT 수신자 결정의 입력), FR-016 오탐율 집계, FR-018 "미전달" 표시 경로가 컴포넌트에 자리가 없다. → api/devices/staff 모듈 책임에 명시.

## 문제없음 확인

- 상수 이름 참조: 04가 쓰는 9개 이름(SENSOR_COVERAGE_M2·ALERT_LATENCY_P95·DEDUP_WINDOW_SEC·STAFF_ACK_TIMEOUT_SEC·ESCALATION_TIMEOUT_SEC·ESCALATION_CALL_RETRY·STAFF_SESSION_HOURS·EVENT_RETENTION_YEARS·CONSENT_WITHDRAW_PURGE_DAYS)는 전부 03 상수 표에 존재(grep 대조).
- INV-5 동시 ack: 04 line 63·128의 `UPDATE … WHERE acked_by IS NULL` 단일 트랜잭션 승자 확정 + 영향 행 0 → "먼저 확인됨" 200 경로가 EC-B1과 일치.
- INV-3 통보 게이트: 시나리오 3 line 164-166에서 `label == actual_fall && state == resolved` 아니면 409, consent 표 조회 후 없으면 409 — EC-C2·C4·SC-007 구조로 충족.
- 검토한 대안 표 7건은 `decision-log.md` DL-16~21과 내용 일치(파일 존재·항목 대조 확인), 단 04 본문은 "DL-16~20"으로 적어 DL-21(푸시)이 참조에서 빠짐.
- INV-8: 센서 판정 내장 + LoRaWAN 페이로드가 이벤트 코드뿐(04 ② TB1 I)이라 원시 데이터가 구조적으로 존재하지 않음 — 게이트웨이 코드 강제가 필요 없는 수준으로 충족.

## 판정: FAIL — 재실행 필요
사유: (1) CRITICAL #1(이벤트↔입소자 연결 부재)은 04 수정만으로 닫히지 않고 03 FR-001/FR-005/FR-007 개정이 선행돼야 한다. (2) 안전 경로의 구조적 보장(#3 타이머 원자성, #4 종단 ack, #5 멱등키, #6 시계 원천)은 데이터 모델과 시나리오 1을 다시 그려야 하는 수준이라 부분 패치로 끝나지 않는다. (3) 위협모델은 시설 LAN 경계(#8)가 통째로 빠져 있다. 나머지 MEDIUM 이하는 개정 시 함께 반영하면 된다.
