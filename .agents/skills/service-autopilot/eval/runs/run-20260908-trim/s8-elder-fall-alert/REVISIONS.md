# REVISIONS — s8-elder-fall-alert

## R1 — A4 독립 검토(fresh-reviewer, fable, 2026-09-08 15:0x, 판정 FAIL) 반영 · 결정 2026-09-09 01:5x
원문: `_a4-review.md` (검토관 회신 그대로). 재검토 없음(예산) — 반영 내역을 여기와 decision-log DL-25~34에 남기고 GATE가 확인한다.
타당성 필터: 20건 중 20건 채택(#19 파티셔닝은 부분 채택 — fall_event 파티션 제거, PG 18 근거 보강). 거짓 양성 0.
적용 상태: **04 v1.1 적용 완료** · 03·05·02는 `개정 노트 R1 적용 대기` 배지 — 재개 시 아래 표대로 패치 후 배지 제거, 03 v1.1·05 v1.1로 올린다.

| # | 심각도 | 결정 | 영향 문서 |
|---|---|---|---|
| 1 | CRITICAL | FallEvent.resident_id를 resolve 시 확정(다인실은 구역 배정 입소자 목록에서 선택, "미상" 허용), 통보는 resident_id 있는 이벤트만(INV-9). 감지 동의 거부 입소자는 센서 미설치 구역 배정(운영 정책) + 배지·관리자 알림 | 03 FR-005/006/007, INV-9, EC-B5 · 04 데이터 저장·시나리오 3 · 05 E11/E12/DDL(resident_id, 409 resident-unknown) · 02 PX5 |
| 2 | HIGH | 시설 회선 단일 장애 명시 + 엣지 로컬 경보(릴레이 부저/경광등) FR-021(P1), LTE 백업은 시설 옵션 | 03 FR-021, EC-A5, SC-014 · 04 ④ · 05 hb 페이로드(local_alarm_fired_count) · 02 P1 연결 끊김·PX10 |
| 3 | HIGH | 이벤트 INSERT와 ack-timeout 잡 등록을 같은 트랜잭션, 스위퍼(SWEEPER_INTERVAL_SEC=30), pg-boss 재시도 JOB_RETRY_LIMIT=3 → FR-023(P0) | 03 FR-023, SC-004(장애 주입 시험) · 04 시나리오 1·2 |
| 4 | HIGH | 종단 ack: 서버 DB 커밋 후 애플리케이션 ack(seq) 발행, 엣지는 그때 버퍼 삭제. 브로커 영속 세션 | 03 FR-017 · 04 UP·ingest · 05 ack 토픽 |
| 5 | HIGH | (edge_id, seq) 멱등키로 재전송 폐기, 병합은 열린 이벤트(detected/acked)만 | 03 FR-001, EC-A6 · 04 ingest · 05 DDL UNIQUE(edge_id, seq) |
| 6 | HIGH | detected_at = 엣지 노드 RTC(로컬 MQTT 수신 시각), 메시지 sent_at으로 오프셋 계산, CLOCK_SKEW_MAX_SEC=5 초과 = clock_suspect(SC-003 제외·건수 보고) | 03 INV-7, SC-003 · 04 · 05 evt 페이로드 sent_at, DDL clock_suspect |
| 7 | HIGH | 지연 정책 2단: LATE_EVENT_SEC 초과 = 표시만(직원 경로 유지), STALE_EVENT_MIN=30 초과 = 직원 알림·에스컬레이션 없이 관제·관리자만. 버퍼 초과는 오래된 것부터 폐기·dropped_count 보고 | 03 FR-017, EC-A2 · 04 ingest 분기 |
| 8 | HIGH | TB1.5 시설 LAN 경계 추가(GW↔엣지 전용 포트/VLAN, 로컬 MQTT 인증·TLS, GW 관리 UI 계정·격리, 엣지 키 보호·도난 탐지, 관제 개인 로그인·화면 잠금) | 04 ①②③ (적용됨) |
| 9 | HIGH | device 종류에 edge_node 추가, 하트비트에 gw_link·ntp_synced·dropped_count, EC-D1 3단 구분(엣지/GW/센서), SENSOR_HEARTBEAT_SEC→EDGE_HEARTBEAT_SEC 개명, P1 대책(read-only rootfs·watchdog·A/B OTA)을 04 Cross-cutting 엣지 절로 | 03 FR-008, EC-D1, 용어집 · 04 · 05 hb·DEVICE(type, report_interval_sec, last_seen_at) |
| 10 | MEDIUM | 컴포넌트 다이어그램을 시퀀스 엣지에 맞춤, 모듈 12개(staff·reports 추가) | 04 |
| 11 | MEDIUM | 값 재기입 제거 → 이름. 새 상수 CALLBACK_TIMESTAMP_WINDOW_SEC=300·AUDIT_CHAIN_VERIFY_INTERVAL_HOURS=24 승격 | 03 상수 표 · 04 |
| 12 | MEDIUM | ③에 계정 단위 rate limit(ack 완화), MFA, 연결 코드 브루트포스(LINK_CODE_MAX_ATTEMPTS=5·LINK_CODE_TTL_HOURS=72, 423 link-code-locked), API 키 유출, 잠금화면 미리보기(본문에 이름 없음), 퇴직 계정 회수 | 03 상수 · 04 ③ · 05 E26/E27/문제 카탈로그 |
| 13 | MEDIUM | 시설별 해시 체인, 배경 작업 facility 컨텍스트 규약(인증서 CN·잡 페이로드·NOTIFY 페이로드), FORCE RLS + 오너(app_owner)/앱 롤 분리 | 04 · 05 DDL 공통 |
| 14 | MEDIUM | 하트비트 저장 = 엣지 1행/EDGE_HEARTBEAT_SEC + device.last_seen_at 갱신(센서별 행 없음), 원시 hb만 파티션·BRIN. WSS 팬아웃 = PostgreSQL LISTEN/NOTIFY(ID만), APP_INSTANCES_MIN=2 | 03 상수 · 04 · 05 |
| 15 | MEDIUM | 홉별 지연 예산표(PUSH_PROVIDER_LATENCY_BUDGET_SEC=5, 미확인·SC 미사용), 무음 스위치는 Time-Sensitive로 못 뚫음 → ④ 잔여 + SC-003 시험 조건(무음 폰) | 03 SC-003·상수 · 04 |
| 16 | MEDIUM | 억제 스위치를 FR-022(P1)로 승격(SUPPRESS_MAX_HOURS=24 자동 만료·배지·감사·억제 중 기록만) | 03 FR-022 · 05 E19 suppression{until, reason} |
| 17 | MEDIUM | 04 Cross-cutting에 배포·DR 구조 결정(앱 인스턴스 2, 관리형 PG 일 백업+PITR, Mosquitto 단일 노드+엣지 버퍼) — 상세 07 | 04 |
| 18 | MEDIUM | GW 로컬 MQTT 발행 능력·센서 보고 주기를 08 착수 조건으로, 센서 오프라인 = 보고 주기 × SENSOR_OFFLINE_MISSED_REPORTS=3 (SENSOR_OFFLINE_AFTER_SEC 폐지) | 03 FR-008, SC-008, 상수 · 05 DEVICE.report_interval_sec |
| 19 | LOW(부분) | fall_event 파티셔닝 제거(하트비트만), PG 18 근거 = 2025-09 출시·5년 지원·pg-boss 호환 | 04 · 05 DDL |
| 20 | LOW | 모듈 책임에 열람·설정 감사, 당직 저장소(staff), 오탐 집계(reports), 미전달 표시 경로 명시 | 04 |

### 03 패치 목록 (재개 시 그대로 적용)
- 머리 `버전: v1.1` + `개정: R1 적용` 줄, 배지 제거
- FR-001 병합 조건(열린 이벤트) + (edge_id, seq) 멱등 · FR-005 입소자 선택 필수(미상 허용) · FR-006 "입소자가 특정된" · FR-007 다인실 정책 · FR-008 엣지 하트비트·보고 주기 배수 · FR-017 종단 ack·폐기·2단 지연 정책
- FR-021 로컬 경보(P1) · FR-022 억제 스위치(P1) · FR-023 타이머 원자성·스위퍼(P0)
- INV-7 시계 원천·clock_suspect · INV-8 "시설 밖" · INV-9 resident 확정
- EC-A2 2단 정책 · EC-A5 로컬 경보 · EC-A6 재전송 중복 · EC-B5 다인실 선택 · EC-D1 3단 구분
- SC-003 무음 시험·clock_suspect 제외 · SC-004 장애 주입 · SC-008 GW/센서 분리 · SC-014 로컬 경보(신규)
- 상수 표: SENSOR_HEARTBEAT_SEC→EDGE_HEARTBEAT_SEC, SENSOR_OFFLINE_AFTER_SEC→SENSOR_OFFLINE_MISSED_REPORTS(3, 배수), 추가 STALE_EVENT_MIN 30 · CLOCK_SKEW_MAX_SEC 5 · PUSH_PROVIDER_LATENCY_BUDGET_SEC 5 · SWEEPER_INTERVAL_SEC 30 · JOB_RETRY_LIMIT 3 · LOCAL_ALARM_DURATION_SEC 120 · SUPPRESS_MAX_HOURS 24 · CALLBACK_TIMESTAMP_WINDOW_SEC 300 · AUDIT_CHAIN_VERIFY_INTERVAL_HOURS 24 · LINK_CODE_TTL_HOURS 72 · LINK_CODE_MAX_ATTEMPTS 5 · APP_INSTANCES_MIN 2 (전부 설계 결정 DL-26~33)
- 용어집 device 3종 · 가정 목록에 R1 추가 가정 4줄

### 05 패치 목록
- 머리 `버전: v1.1 · 기준 03 v1.1`, 배지 제거
- 문제 카탈로그 resident-unknown(409)·link-code-locked(423) · E11 resident_id|resident_unknown · E12 에러 추가 · E16 type 3종·last_seen_at·report_interval_sec·suppression · E19 suppression{until,reason} · E26 TTL 상수 · E27 잠금 에러
- MQTT 토픽 `gw/{gw_id}`→`edge/{edge_id}`, evt에 sent_at, hb 페이로드(edge{…dropped_count,last_acked_seq,ntp_synced,local_alarm_fired_count}, gw_link), ack = DB 커밋 후, ACL CN `edge:`, 영속 세션, 오프셋·late/stale 주석
- ERD/DDL: fall_event resident_id·clock_suspect·edge_id, UNIQUE(edge_id, seq), 파티션 제거; DEVICE type·report_interval_sec·suppressed_until·suppression_reason·last_seen_at; audit_log 시설별 체인 주석·헤드 인덱스; 공통 절 FORCE RLS·app_owner·배경 컨텍스트 규약
- 커버리지: FR-001/004/005/008/017 갱신, FR-021/022/023 추가, 합계 "FR 23 = P0 10 · P1 9 · P2 4"

### 02 패치 목록
- P1 연결 끊김 처리 열에 "엣지 로컬 경보(FR-021, R1)가 안전망" · PX5 다인실 정책 · PX10 단절/정전 분리 서술
