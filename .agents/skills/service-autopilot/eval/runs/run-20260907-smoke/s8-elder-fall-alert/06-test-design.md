# 테스트 설계 — 요양시설 낙상 감지·알림 서비스 (FallGuard)
버전: v1.2 · 기준 03 v1.2
개정 v1.2: GATE 1차 반영 — TS-001·002·009·014·016·029·048 수정, TS-059~065 추가(시설 오프라인·ack 링크·입소자 지정·미매핑·카나리·부트스트랩·next_escalation_at).
개정 v1.1: A4 검토 반영 — TS-003·004·005·007·016·017·026·028·034·038 수정, TS-052~058 추가(수신 보장·ID 충돌·수신 보고·리마인더·스테일·RLS·지연 플러시).

> 근거 (A6 진입 사전조사, 검색 1회, 2026-09-07)
> - **정량** — pg-boss 지연 잡은 JS `Date`를 가짜 시계로 바꿔도 DB 시각을 쓰기 때문에 스푸핑이 통하지 않는다는 보고(pg-boss 이슈 #210); singleton key가 `created`+`retry` 상태로 공존하면 batch_size>1에서 UniqueKeyViolation(이슈 #535) — [pg-boss #210](https://github.com/timgit/pg-boss/issues/210), [pg-boss #535](https://github.com/timgit/pg-boss/issues/535). → 타이머 로직은 **앱 계층 `Clock` 포트**로 분리해 단위 테스트하고, pg-boss는 통합 테스트에서 `ACK_TIMEOUT`을 초 단위로 축소한 설정으로만 검증한다.
> - **정성** — pg-boss 운영 후기 "footguns we hit": 워커 재시작 시 잡 유실이 아니라 **중복 실행**이 실제 문제 — [AGLedger](https://agledger.ai/blog/pg-boss-production-lessons/). 이중 발송 시나리오(TS-026)를 P0로 둔다.
> - **사용자 영향** — 직원이 보는 카운트다운("2분 40초 후 근무조 전체에게 알려요")이 실제 에스컬레이션 시각과 일치해야 신뢰가 생긴다(L-07 테슬러 — 계산은 시스템이). SC-002가 이를 보증한다.

## 원칙
- AC는 개발 시작 전에 존재한다. 각 시나리오는 처음엔 반드시 실패해야 한다 (RED 게이트 — ecc:tdd-workflow Step 3).
- 피라미드: unit 다수 → integration → contract(05 엔드포인트) → E2E 최소 (~70/20/10). "가능한 한 아래층으로" (Fowler).
- 커버리지는 **리스크 기반** (ecc:tdd-workflow의 일률 80%는 채택하지 않음 — skill-routing 충돌 우선순위): 안전 경로(감지→알림→에스컬레이션)와 위협모델 상위(TM-01·06·10·21·26)는 분기 100%, 관리 CRUD는 통합 테스트 1개씩.
- 시각 의존 로직은 `Clock` 포트 주입(단위) + 축소 상수(통합). 실제 시간 대기(`sleep`) 금지 — flaky 원인(ecc:e2e-testing).
- 외부 어댑터(FCM·SOLAPI)는 계약 테스트용 페이크 서버 + 실패 주입 스위치. 실제 발송은 파일럿 스모크에서만.

## 수용 기준 → 시나리오 변환표
레이어: U=unit · I=integration(DB·pg-boss·Mosquitto 컨테이너) · C=contract · E=E2E(Playwright/Expo Detox)

| ID | SC/FR | Gherkin 시나리오 (Given/When/Then) | 레이어 | 데이터/목킹 |
|---|---|---|---|---|
| TS-001 | SC-001 · FR-003 · FR-005 | Given 디바이스 D1이 침상 B1(입소자 R1, 근무 중 직원 S1)에 매핑됨 / When M-01 이벤트 발행 / Then `received_at` 부여, 상태 `notified`, S1에게 push notifications 행 1건, `notified_at - received_at` ≤ `INGEST_TO_NOTIFY_MAX` | I | Mosquitto 컨테이너, FCM 페이크(200) |
| TS-002 | SC-001 | Given 파일럿 `PILOT_DURATION` 로그 / When SQL로 `push_ack_at - received_at` p95 계산 / Then ≤ `ALERT_LATENCY_P95` | 운영 검증(07 대시보드 쿼리) | 실데이터 |
| TS-003 | SC-002 · FR-008 | Given `notified` 이벤트, ack 없음 / When Clock을 `ACK_TIMEOUT` 전진 ×1, ×2, ×3 / Then `escalation_level` 0→1(전체 근무조 push+SMS)→2(시설 책임자 SMS), 3번째 전진에 추가 발송 없음 | U (EscalationModule + Clock 포트) | Clock 페이크, NotifyModule 스파이 |
| TS-004 | SC-002 · FR-008 | Given `notified` 이벤트, pg-boss 실제 워커, `ACK_TIMEOUT`=2초 설정 / When 2.5초 후 / Then level 1 notifications 행 생성, singleton `event:1` 잡 1건만 존재, `escalation_job_id` 저장됨 | I | pg-boss + Postgres 컨테이너, 축소 상수 |
| TS-005 | SC-002 · FR-007 | Given `notified` + level 1 잡 예약 / When ack / Then 잡 실행 시 CAS(`status IN (detected, notified)`) 0행으로 no-op, 발송 0; ack와 escalate가 같은 순간 커밋돼도 행 잠금으로 책임자 SMS 0건 | U+I | Clock 페이크, 동시성 |
| TS-006 | SC-003 · FR-006 | Given FCM 페이크가 500 반환 / When 이벤트 발생 / Then push attempt 1..`NOTIFY_RETRY_MAX` 모두 `failed`, `CHANNEL_FALLBACK_DELAY` 안에 SMS notifications 행 `sent` | I | FCM 실패 주입, SOLAPI 페이크 |
| TS-007 | SC-003 · FR-006 · FR-026 | Given FCM 200이지만 E-31 수신 보고 없음(`push_ack_at IS NULL`) / When J-07 실행(`CHANNEL_FALLBACK_DELAY`) / Then SMS 폴백 행 생성; 수신 보고가 있으면 0건 | U+I | Clock 페이크 |
| TS-008 | SC-004 · FR-017 | Given 이벤트 20건 중 resolutions `false_alarm` 1건 / When E-26 `group_by=device` / Then rate 0.05, 상한 `FALSE_ALARM_RATE_MAX`와 비교 필드 | I | 시드 데이터 |
| TS-009 | SC-004 | Given 파일럿 `PILOT_DURATION` / When E-26 전체 / Then rate ≤ `FALSE_ALARM_RATE_MAX` | 운영 검증 | 실데이터 |
| TS-010 | SC-005 | Given 시설 사고보고서 낙상 목록 N건 / When 이벤트 테이블과 시각±10분·입소자로 대사 / Then 매칭 ≥ `DETECTION_RECALL_MIN` | 운영 검증(스크립트) | 시설 제공 기록 |
| TS-011 | SC-006 · FR-009 · FR-010 | Given `acked` 이벤트, 보호자 G1(notify=true) / When resolution `confirmed_fall` / Then family_links 1건, alimtalk notifications `sent`, `family_notified_at - resolved_at` ≤ `FAMILY_NOTICE_LATENCY_MAX`, 본문에 이름 원문 없음 | I | SOLAPI 페이크 |
| TS-012 | SC-006 · FR-010 | Given `acked` 이벤트 / When resolution `false_alarm` / Then notifications(guardian) 0건, family_links 0건, 상태 `resolved` | I | — |
| TS-013 | SC-006 · FR-010 · EC-C2 | Given SOLAPI alimtalk 실패 주입 / When `confirmed_fall` / Then 재시도 후 SMS 폴백 `sent`; SMS도 실패면 E-27 `sms: down` + 관리자 알림 | I | SOLAPI 실패 주입 |
| TS-014 | SC-007 · FR-018 | Given 시설 A의 admin 토큰 / When 시설 B의 단건 자원(E-11·E-12·E-13·E-18·E-21) 호출 / Then 전부 404 `not-found`(RLS 비가시); 목록(E-08·E-10·E-17·E-23·E-28)은 200이되 시설 B 행 0건 | I (역할 3 × 자원 전부 매트릭스 생성) | 두 시설 시드 |
| TS-015 | SC-007 · FR-018 · TM-12 | Given staff 토큰 / When 본문에 `role: facility_admin` 넣어 E-23 호출 / Then 403, DB role 불변 | I | — |
| TS-016 | SC-008 · FR-012 | Given 디바이스 `online`, `last_seen_at` = now / When Clock을 `HEARTBEAT_TIMEOUT`+`OFFLINE_SCAN_INTERVAL` 전진 후 J-02 실행 / Then `offline`, 관리자 notifications 1건(`event_id` NULL, `kind=device_offline`, 채널 SMS + 배너); J-02 3회 더 실행해도 추가 0건; heartbeat 수신 시 `online` + 추가 알림 0 | U+I | Clock 페이크 |
| TS-017 | SC-009 | Given 카나리 디바이스 합성 M-01 발행 1분 주기 + 외부 HTTP 헬스체크 / When 파일럿 월 / Then `fall_detections` 왕복 성공률·E-30 200 비율 모두 ≥ `API_AVAILABILITY` | 운영 검증 | 07 카나리·업타임 모니터 |
| TS-018 | SC-010 · UX-01 | Given 푸시 수신 / When 탭 → 알림 상세 → "확인했어요, 갈게요" / Then 결정 지점 2, E-12 호출 1회 | E (Expo Detox) | 페이크 API |
| TS-019 | SC-010 · UX-03 | Given 알림 상세 화면 / When ack 탭 / Then 400ms 안에 버튼 상태 변경(낙관적 UI), 응답 지연 2초 주입 시 스켈레톤 표시 | E | 지연 주입 |
| TS-020 | SC-011 · FR-019 | Given `app_rw` 롤 접속 / When `UPDATE audit_log …`, `DELETE FROM audit_log …` / Then 권한 오류; `migrator` 롤로 시도해도 트리거 예외 | I (DB) | 롤 2종 |
| TS-021 | SC-012 | Given 전날 스냅샷 + WAL / When 복원 리허설 스크립트 / Then `RTO` 안에 기동, 복원 시점 `RPO` 이전 이벤트 조회 OK | 운영 리허설(07) | 스테이징 |
| TS-022 | FR-001 | Given E-05로 claim_code 발급 / When E-06 `claim_code`+CSR / Then 201 인증서(CN=device_id), 같은 코드 재사용·만료는 400 `claim-invalid` 동일 응답 | I+C | 테스트 CA |
| TS-023 | FR-001 · TM-06 | Given 디바이스 D1 인증서 / When `fallguard/D2/fall`에 publish / Then 브로커 ACL 거부, 서버 수신 0 | I (Mosquitto) | ACL 파일 |
| TS-024 | FR-002 | Given D1이 B1에 매핑 / When B2로 재매핑 / Then `device_placements` 이전 행 `to_at` 설정, 새 행 `to_at NULL`, 부분 유니크 위반 없음; 둘 다 지정하면 409 `placement-conflict` | I | — |
| TS-025 | FR-003 · EC-A3 | Given 같은 `(device_id, device_event_id)` M-01 2회 / When 수신 / Then 이벤트 1건, 두 번째는 무시 로그; `detected_at`이 `received_at`보다 `DEDUP_WINDOW` 이상 과거면 알림 본문에 지연 표기 | U+I | — |
| TS-026 | FR-004 · TM-26 · EC-A1 | Given D1 활성 이벤트 / When D1 재감지 (동시 2요청 포함) / Then fall_events 1건, fall_detections 3건, `redetect_count` 2, 추가 push 0, `fall_events_one_active_per_device`가 경합을 막음 | I (동시성 — Promise.all 2건) | — |
| TS-027 | FR-004 · EC-A4 | Given R1·R2·R3 / When 30초 안 3건 / Then 이벤트 3건, push 3건 | I | — |
| TS-028 | FR-004 · EC-B3 | Given D1 `false_alarm`으로 resolved / When `DEDUP_WINDOW` 밖 재감지 / Then 새 이벤트 `notified` | I | Clock |
| TS-029 | FR-005 · FR-015 · EC-A2 | Given 생활실 302호에 근무 중 shifts 0명 / When 이벤트 / Then 시설 전체 staff push + 관리자 알림 `shift_unassigned`, `escalation_level` 1로 INSERT, 같은 tx의 J-01이 level 2로 예약돼 `ACK_TIMEOUT` 후 시설 책임자 SMS(+ack 링크) 도달 | U+I (RoutingModule + EscalationModule) | shifts 시드 |
| TS-030 | FR-005 · FR-015 | Given S1 근무 월~금 22:00–07:00, S2 07:00–22:00 / When 화요일 02:00 이벤트 / Then 수신자 S1만; 자정 넘김 시간대 계산 정확 | U | Clock |
| TS-031 | FR-007 · EC-B1 | Given `notified` / When S1·S2가 같은 초에 E-12 / Then 첫 요청 `acked_by=S1`, 두 번째 200 `already_acked: true`, DB acked_by 불변 | I (동시성) | Idempotency-Key 각각 |
| TS-032 | FR-007 | Given 같은 Idempotency-Key로 E-12 2회 / When 두 번째 / Then 최초 응답 재생(본문·상태 동일) | C | — |
| TS-033 | FR-007 · FR-013 | Given S1 ack / When SSE 구독 중인 콘솔 / Then `fall_event` 이벤트에 `status: acked, acked_by_role`, 다른 수신자 앱에 "확인했어요" push | I | SSE 클라이언트 |
| TS-034 | FR-009 | Given resolved(`false_alarm`) / When 같은 직원이 `confirmed_fall`로 수정 / Then resolutions 2행, 이전 `is_current=false`, family-notice 잡 생성; `family_notified` 이후 판정 변경은 409 `event-closed`, 관리자 메모 추가는 201 | I | — |
| TS-035 | FR-009 | Given `notified`(ack 전) / When E-13 / Then 409 `not-acked` | C | — |
| TS-036 | FR-010 · EC-C1 | Given 보호자 없음 또는 전원 `notify=false` / When `confirmed_fall` / Then 통지 0, 이벤트 `resolved` 유지, 콘솔 "보호자 연락처 없음" 플래그 | I | — |
| TS-037 | FR-011 · EC-C3 · TM-16 | Given family_link 토큰 / When E-16 유효 / Then 200, 응답에 `name` 필드 없음, `view_count`+1; 만료 후 410; 토큰 1바이트 변조 404; 다른 이벤트 ID로 재사용 불가 | I+C | Clock |
| TS-038 | FR-012 · EC-D1 | Given offline 디바이스 / When heartbeat 간헐 복귀(flapping 5회) / Then `FLAP_SUPPRESS_WINDOW` 안 offline 알림 1건, 복귀 알림 0 | U | Clock |
| TS-039 | FR-013 · EC-D2 | Given 콘솔 SSE 연결 / When `detected` 이벤트 존재 / Then 클라이언트 사이렌 상태 true, ack 후 false; SSE 끊김 시 15초 안 재연결 + `Last-Event-ID` 재전송 | E (Playwright) | 페이크 SSE |
| TS-040 | FR-014 | Given 입소자 등록 본문 / When `consent.signed_at` 없음 / Then 422 `validation-error` errors[field=consent.signed_at]; 정상이면 201 + 침상 점유 유니크 | C+I | — |
| TS-041 | FR-014 | Given B1에 R1 활성 / When R2를 B1에 등록 / Then 409 `bed-occupied` | I | — |
| TS-042 | FR-015 | Given E-24 PUT 본문 빈 배열 / When 저장 / Then 200 + 경고 필드 `warning: no_shifts`; 관리자 콘솔 배너 | C | — |
| TS-043 | FR-016 | Given 이벤트가 family_notified까지 진행 / When E-11 / Then `timeline` 6단계 시각 오름차순, 각 `actor_role` 존재 | I | 시드 |
| TS-044 | FR-018 | Given 만료된 access 토큰 / When E-10 / Then 401 `unauthenticated`; E-02로 갱신 성공; E-03 후 E-02는 401 `session-revoked` | I | Clock |
| TS-045 | FR-018 · TM-11 | Given E-01 실패 11회/분/IP / When 12회째 / Then 429 + `Retry-After` | I | 레이트리밋 저장소 |
| TS-046 | FR-019 | Given ack·resolution·notification 각 1건 / When 처리 완료 / Then audit_log 3행, `payload`에 이름·전화 없음(정규식 검사) | I | — |
| TS-047 | FR-020 | Given `discharged_at` + `EVIDENCE_RETENTION` 경과 / When J-05 / Then `residents.name`·`guardians.phone` = `[purged]`, `purged_at` 설정, fall_events 행 수 불변 | I | Clock |
| TS-048 | FR-022 | Given SOLAPI 5분 실패율 ≥ `CHANNEL_DEGRADED_FAIL_RATE` / When J-06 / Then `degraded` + 배너; 연속 실패 `CHANNEL_DOWN_STREAK` 도달 → `down`, 관리자 알림 1건(전이 시, `kind=channel_state`), E-27 `since`; 회복 시 `ok` 알림 1건 | U+I | 실패 주입 |
| TS-049 | FR-023 | Given 앱·콘솔·알림톡 템플릿 문자열 자산 / When 금지어 검사("진단", "치료", "예방 효과", "의료기기") / Then 0건, "안전 보조" 표기 존재 | U (스냅샷·정규식) | 문구 자산 파일 |
| TS-050 | TM-01 | Given 폐기(E-09)된 디바이스 인증서 / When MQTT 접속 / Then TLS 핸드셰이크 거부(CRL) | I (Mosquitto) | 테스트 CA + CRL |
| TS-051 | TM-05 | Given D1이 분당 `DEVICE_RATE_LIMIT`+5건 publish / When 수신 / Then 초과분 드롭 로그, 관리자 알림 1건, 정상 디바이스 D2 지연 없음 | I | — |
| TS-052 | FR-003 | Given 서버가 M-01 수신 후 DB 커밋 전에 강제 종료(`SIGKILL`) / When 재기동 / Then 브로커 영속 세션이 재전달, fall_detections 1건(소실 0·중복 0) | I (Mosquitto persistence, manualAck) | 프로세스 킬 |
| TS-053 | FR-003 | Given D1이 `device_event_id` X·detected_at T1 발행 후 재부팅해 X·T2 발행 / When 수신 / Then 감지 2건(두 번째는 서버가 파생 키로 저장), 관리자 경고 1건 | I | — |
| TS-054 | FR-026 | Given push 발송 수락 / When 앱이 E-31로 수신 보고 / Then `push_ack_at` 기록, J-07 실행 시 폴백 0; 앱 오프라인 큐잉 후 일괄 보고도 동일 | I | 페이크 앱 |
| TS-055 | FR-027 | Given `acked` / When `RESOLUTION_REMINDER_DELAY` 경과, resolution 없음 / Then 관리자 알림 1건 + SSE `needs_resolution`; resolution 있으면 0 | U+I | Clock |
| TS-056 | FR-008 | Given 이벤트 INSERT 직후 크래시로 `detected`에 머묾(FCM 미호출) / When 같은 트랜잭션에 예약된 J-01 또는 J-02 스테일 스캔 / Then level 1 에스컬레이션 실행, 이벤트가 `detected`에 `ACK_TIMEOUT`+`OFFLINE_SCAN_INTERVAL` 넘게 머물지 않음 | I | 프로세스 킬 |
| TS-057 | FR-018 | Given 워커가 시설 A 컨텍스트(`SET LOCAL app.facility_id`)로 J-02 실행 / When 시설 B 디바이스도 오프라인 / Then A 디바이스만 갱신; 컨텍스트 미설정 세션의 SELECT는 0행(FORCE RLS); `migrator` 롤도 동일 | I (DB) | 두 시설 시드 |
| TS-058 | FR-004 | Given D1 버퍼 200건 지연 플러시(`delayed=true`) / When 수신 / Then fall_detections 200건, fall_events 1건 `delayed`, push 1건 본문 "지연 200건", `DEVICE_RATE_LIMIT` 드롭 0 | I | 시뮬레이터 |
| TS-059 | FR-028 · FR-012 | Given 시설 A 디바이스 5대 online / When 전부 heartbeat 중단 후 `HEARTBEAT_TIMEOUT`+`FACILITY_OFFLINE_DELAY` 경과, J-02 / Then 시설 `offline`, 관리자 알림 1건(`facility_offline`), 디바이스별 offline 알림 0건, AL-04 발화; 1대 복귀 시 시설 `online` 알림 1건 | U+I | Clock |
| TS-060 | FR-029 · FR-008 | Given level 2 SMS의 ack 링크 / When `ACK_LINK_TTL` 안에 E-32 POST / Then 이벤트 `acked`, `acked_by` = escalation_contact 사용자, 사이렌 종료, J-08 예약; 만료 후 410, 재사용 404; E-25에 `staff` 사용자 지정 시 422 `contact-not-admin` | I+C | Clock |
| TS-061 | FR-009 · FR-010 · EC-C4 | Given 생활실 매핑 디바이스 이벤트(`resident_id` NULL) / When `resident_id` 없이 `confirmed_fall` / Then 422 `resident-required`, E-11 `family_notice_blocked=true`; `resident_id` 지정 시 이벤트에 연결 + family_notice 생성 | I+C | 다인실 시드 |
| TS-062 | FR-002 · FR-003 · EC-A5 | Given 등록됐으나 미매핑 디바이스 / When M-01 / Then fall_detections 1건, fall_events 0건, 관리자 알림 1건(`unmapped_device`), 브로커 ack 완료(재전달 0) | I | — |
| TS-063 | SC-009 · FR-003 | Given `devices.is_canary=true` / When M-01 / Then fall_detections 저장, fall_events·notifications 0건, E-26 집계 제외, E-30 `canary_last_ok_at` 갱신; 카나리 `CANARY_FAIL_STREAK` 연속 실패 주입 시 E-30 503 | I | 카나리 시드 |
| TS-064 | FR-018 · SC-007 | Given 컨텍스트 없는 `app_rw` 세션 / When `auth_lookup_user(email)`·`device_lookup(id)`·`token_lookup(kind, hash)` 호출 / Then 단일 행 반환; 같은 세션의 직접 `SELECT * FROM users`는 0행(FORCE RLS) | I (DB) | — |
| TS-065 | FR-008 · FR-013 | Given `notified` 이벤트 / When E-11·E-14 / Then `next_escalation_at` = `notified_at` + `ACK_TIMEOUT`(±1초), 앱 카운트다운이 이 값으로 렌더, ack 후 NULL | I+E | Clock |

**누락 검사**: SC-001~SC-012 전부 ≥1 시나리오(SC-001: TS-001·002 / 002: 003·004·005 / 003: 006·007 / 004: 008·009 / 005: 010 / 006: 011·012·013 / 007: 014·015 / 008: 016 / 009: 017 / 010: 018·019 / 011: 020 / 012: 021). P0·P1 FR-001~FR-020·022·023·026~029 전부 ≥1 시나리오. `check_package.py` C3로 검증.

## 계약 테스트 (05 엔드포인트 표 기준)
- **스키마**: E-01~E-30 응답을 OpenAPI 스키마로 검증(성공·에러 둘 다). 에러는 `application/problem+json` + `type` URI가 05 슬러그 목록에 있는지.
- **에러 포맷**: 모든 4xx/5xx에 `type/title/status/instance` 존재, `stack`·`sql` 문자열 부재 (정규식).
- **멱등성 재시도**: E-05·E-12·E-13·E-17·E-19 — 같은 키 재전송 = 동일 응답, 같은 키 다른 본문 = 422 `idempotency-key-reuse`, 키 없음 = 400.
- **페이지네이션**: E-08·E-10·E-28 — `limit=201` → 422, `next_cursor` 순회 시 중복·누락 0(시드 250건).
- **MQTT 계약**: M-01 페이로드 스키마(JSON Schema) — 벤더 어댑터 출력이 정규 스키마를 만족하는지. **착수 첫 작업 1**(07)에서 벤더 샘플 페이로드로 먼저 RED.
- **버저닝**: `Accept: …; version=2` → 406 `unsupported-version`.

## E2E 후보 — 안전이 걸린 여정만 3개
| ID | 여정 | 도구 | 검증 |
|---|---|---|---|
| E2E-1 | 감지→직원 앱 푸시→ack→대응 기록→보호자 링크 열람 (US-1·2·4 전 구간) | 스테이징: 디바이스 시뮬레이터(MQTT publish) + Expo Detox + Playwright(E-16 페이지) | 상태 5단계 전이, 링크 페이지 이름 없음, 총 소요 |
| E2E-2 | 미응답 에스컬레이션 3단계 (US-5) | 스테이징 축소 상수(`ACK_TIMEOUT`=20초) | 시설 책임자 SMS 페이크 수신 → ack 링크(E-32)로 확인 → 사이렌 종료, 콘솔 카운트다운이 `next_escalation_at`과 일치 |
| E2E-3 | 관리자 콘솔: 디바이스 등록→매핑→근무조 설정→활성 보드 사이렌→ack 반영 (US-3 + FR-013) | Playwright POM(`DevicesPage`, `ShiftsPage`, `BoardPage`), `data-testid` 셀렉터, `waitForResponse`(임의 timeout 금지) | SSE 반영, 빈/로딩/에러/stale 4상태 스크린샷 |

flaky 전략(ecc:e2e-testing): `retries: CI ? 2 : 0`, `trace: on-first-retry`, 실패 시 스크린샷·비디오 보존, `--repeat-each=10`으로 신규 E2E 안정성 확인 후 머지.

## 리스크 기반 커버리지 목표
| 영역 | 목표 | 왜 |
|---|---|---|
| IngestModule·EventModule 상태 기계·RoutingModule·EscalationModule | 분기 100%, 동시성 시나리오(TS-026·031) 필수 | 안전 경로(G1) — 틀리면 미알림 |
| NotifyModule 재시도·폴백 | 분기 100% + 실패 주입 매트릭스(채널 3 × 실패 유형 3) | SC-003·006, R3 |
| 테넌시·권한(AuthModule, 리포지토리 필터) | 역할 3 × 엔드포인트 30 매트릭스 자동 생성 100% | TM-10·12, SC-007 |
| audit_log 불변·개인정보 부재 | DB 통합 100% | TM-21·27, SC-011 |
| 디바이스 mTLS·ACL | Mosquitto 통합 100% | TM-01·06 |
| 관리 CRUD(E-17~E-25) | 통합 1개 + 계약 스키마 | 저위험 |
| 화면(콘솔·앱) | E2E 3개 + 컴포넌트 단위(4상태 렌더) | UX-01~05 |
| 전체 라인 커버리지 | 보고만, 게이트 아님 | 리스크 기반 원칙 |
