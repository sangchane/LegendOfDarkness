# API 계약 & 데이터 스키마 — 요양시설 낙상 감지·알림 서비스 (FallGuard)
버전: v1.2 · 기준 03 v1.2
개정 v1.2: GATE 1차 반영 — 타 시설 404, 레이트리밋·멱등 TTL 상수 참조, E-10/11/14 `next_escalation_at`, E-13 `resident_id`, E-25 `escalation_contact_user_id`, E-30 카나리·데드맨, E-32 ack 링크, J-01 level 규칙, J-02 시설 오프라인, J-06 임계, J-09 운영 알람, 카나리 규칙, `notifications.kind`·nullable `event_id`, `ack_links`, RLS 부트스트랩 함수, 보존 정책 통일.
개정 v1.1: A4 검토 반영 — M-01 수신 보장·ID 계약, E-31·J-07·J-08 추가, `fall_detections` 원시 테이블, dedup 키 device, RLS MVP 적용, E-13 종결 규칙.

> 근거 (A5 진입 사전조사, 검색 1회, 2026-09-07)
> - **정량** — `Idempotency-Key` 헤더 IETF 초안은 -07(2025-10-15, Standards Track 의도)까지 나왔고 RFC는 아니지만 Stripe가 대중화한 사실상 표준이다 — [draft-ietf-httpapi-idempotency-key-header-07](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-idempotency-key-header-07). 이 계약은 헤더 이름·의미를 그대로 쓴다.
> - **정성** — NestJS MQTT 이슈 #2236(04 근거)처럼 토픽·핸들러 불일치가 흔하므로 MQTT 토픽도 HTTP 엔드포인트와 같은 표에 계약으로 고정한다.
> - **사용자 영향** — 직원의 "확인" 탭이 네트워크 재시도로 두 번 전송돼도 한 번만 기록되고 오류 화면이 없다(L-06 막다른 에러 0, EC-B1).

## 규약 (전 엔드포인트 공통)
- **에러 포맷**: RFC 9457 Problem JSON — `type`(`https://fallguard.example/problems/<slug>`) · `title` · `status` · `detail` · `instance`. 스택트레이스·SQL·내부 ID 노출 금지 (Zalando #176·#177). 검증 오류는 `errors[]{field, code}` 확장 필드.
- **버저닝**: 미디어타입 — 요청 `Accept: application/vnd.fallguard+json; version=1`, 생략 시 최신. URL 버저닝 회피 (Zalando #115; ecc:api-design의 `/api/v1` 권고는 채택하지 않음 — decision-log #20). 스펙 파일은 semver.
- **페이지네이션**: 커서 기반 — `?cursor=<opaque>&limit=50`(최대 200), 응답 `meta.next_cursor`. offset 금지 (Zalando #160).
- **멱등성**: 부작용 있는 POST(`ack`, `resolution`, `devices`, `residents` 등)는 `Idempotency-Key`(클라 생성 UUID) 필수. 서버는 `(facility_id, key)`로 `IDEMPOTENCY_KEY_TTL` 보관, 재시도 시 최초 응답을 그대로 재생(Stripe 방식). 같은 키·다른 본문은 422 `idempotency-key-reuse`.
- **네이밍·권한**: 경로 kebab-case 복수형, 필드 snake_case, 시각 UTC ISO 8601(`timestamptz`). 권한 스코프 `fall:<자원>:<행위>` (Zalando #225 동형). 역할→스코프: `facility_admin` = 전부, `staff` = `fall:event:read|ack|resolve`, `fall:me:push-token`, `family_viewer` = 열람 토큰으로 `fall:family:view`만.
- **테넌시**: 모든 경로는 토큰의 `facility_id`로 암묵 필터. 경로에 `facility_id`를 두지 않는다. 타 시설 자원은 RLS로 보이지 않으므로 단건은 404 `not-found`, 목록은 타 시설 행 0건 (v1.1의 403 통일은 폐기 — decision-log #21 superseded, GATE H3). 403 `forbidden`은 역할·스코프 부족에만.
- **레이트리밋**: 로그인 `LOGIN_RATE_LIMIT`, 인증 API `API_RATE_LIMIT`, 보호자 링크·ack 링크 `FAMILY_LINK_RATE_LIMIT`. 초과 시 429 + `Retry-After`.
- **인증**: `Authorization: Bearer <access>`(`ACCESS_TOKEN_TTL`), 리프레시는 httpOnly 쿠키(콘솔) 또는 보안 저장소(앱), 서버측 세션 행으로 폐기 가능(`REFRESH_TOKEN_TTL`).

## 엔드포인트 표

### HTTP (REST + SSE)
| ID | 메서드 경로 | 요청(핵심 필드) | 응답 | 주요 에러(RFC 9457 type) | 권한 스코프 |
|---|---|---|---|---|---|
| E-01 | `POST /auth/login` | `email`, `password` | 200 `{access_token, expires_in}` + 리프레시 쿠키 | `invalid-credentials`(401), `rate-limited`(429) | 공개 |
| E-02 | `POST /auth/refresh` | 리프레시 쿠키/본문 | 200 새 access | `session-revoked`(401) | 공개(쿠키) |
| E-03 | `POST /auth/logout` | — | 204, 세션 행 폐기 | — | 인증 |
| E-04 | `GET /app/min-version` | `?platform=ios|android` | 200 `{min_version, latest}` | — | 공개 |
| E-05 | `POST /devices` | `label`, `vendor`, `model` · Idempotency-Key | 201 `{device_id, claim_code, claim_expires_at}` | `validation-error`(422) | `fall:device:manage` |
| E-06 | `POST /devices/claim` | `claim_code`, `csr`(PEM) | 201 `{device_id, certificate, ca_chain, mqtt_host}` | `claim-invalid`(400, 만료·재사용 동일 응답) | 공개(1회용 코드) |
| E-07 | `PUT /devices/{id}/placement` | `bed_id` 또는 `room_id`(둘 중 하나) | 200 placement(이력 신규 행) | `placement-conflict`(409 둘 다/둘 다 없음), `not-found`(404) | `fall:device:manage` |
| E-08 | `GET /devices` | `?status=online|offline|revoked&cursor&limit` | 200 `data[] {id, label, status, last_seen_at, placement}` | — | `fall:device:read` |
| E-09 | `DELETE /devices/{id}` | — | 204 (인증서 revoke, status=revoked) | `not-found` | `fall:device:manage` |
| E-10 | `GET /fall-events` | `?status&from&to&resident_id&cursor&limit` | 200 `data[] {id, status, resident, room, bed, received_at, detected_at, redetect_count, escalation_level, next_escalation_at, acked_by_role, acked_at}` | — | `fall:event:read` |
| E-11 | `GET /fall-events/{id}` | — | 200 이벤트 + `next_escalation_at` + `timeline[] {step, at, actor_role}` + 현재 판정 + `family_notice_blocked`(입소자 미확정) | `not-found`(404, 타 시설 포함) | `fall:event:read` |
| E-12 | `POST /fall-events/{id}/ack` | Idempotency-Key | 200 `{status: acked, acked_at, acked_by}` — 이미 acked면 200 + `already_acked: true` | `event-closed`(409 resolved 이후), `not-found` | `fall:event:ack` |
| E-13 | `POST /fall-events/{id}/resolution` | `verdict: confirmed_fall|false_alarm`, `note`(≤500자, 생략 가능), `resident_id`(이벤트에 입소자가 없으면 `confirmed_fall`에 필수 — 생활실 매핑) · Idempotency-Key | 201 resolution(이력 행) + 이벤트 `status: resolved`(+ `resident_id` 연결) | `not-acked`(409 ack 전), `event-closed`(409 `family_notified` 이후 판정 변경), `resident-required`(422), `validation-error` | `fall:event:resolve` |
| E-14 | `GET /fall-events/stream` | SSE, `Last-Event-ID` | `text/event-stream`(시설별 채널) — `event: fall_event`, `data: {id, status, escalation_level, next_escalation_at, needs_resolution, family_notice_blocked, ...}`; `event: facility_state`(채널·시설 오프라인 배너); `SSE_HEARTBEAT` 코멘트 | — | `fall:event:read` |
| E-15 | `GET /fall-events/{id}/notifications` | — | 200 `data[] {recipient_role, channel, attempt, sent_at, result, provider_message_id}` | `not-found` | `fall:event:read` |
| E-16 | `GET /family/{token}` | `Accept: text/html` 또는 JSON | 200 `{event_time, room, response_summary, responder_role, facility_phone}` — 이름 없음 | `link-expired`(410), `link-invalid`(404) | `fall:family:view`(열람 토큰) |
| E-17 | `GET /residents` · `POST /residents` | `name`, `room_id`, `bed_id`, `consent {signed_at, signer}` · Idempotency-Key | 200 목록 / 201 | `validation-error`, `bed-occupied`(409) | `fall:resident:manage` |
| E-18 | `GET /residents/{id}` · `PATCH /residents/{id}` | 부분 갱신 | 200 | `not-found` | `fall:resident:manage` |
| E-19 | `POST /residents/{id}/discharge` | `discharged_at` · Idempotency-Key | 200 (소프트삭제, 파기 예약 `EVIDENCE_RETENTION`) | `already-discharged`(409) | `fall:resident:manage` |
| E-20 | `POST /residents/{id}/guardians` | `name`, `phone`(E.164), `notify: bool`, `relation` | 201 | `validation-error` | `fall:resident:manage` |
| E-21 | `PATCH /residents/{id}/guardians/{gid}` · `DELETE …` | `notify`, `phone` | 200 / 204 | `not-found` | `fall:resident:manage` |
| E-22 | `GET /rooms` · `POST /rooms` · `POST /rooms/{id}/beds` | `name`, `floor` / `label` | 200 / 201 | `validation-error`, `duplicate-name`(409) | `fall:resident:manage` |
| E-23 | `GET /staff` · `POST /staff` · `PATCH /staff/{id}` | `name`, `email`, `role`, `phone` | 200 / 201 | `duplicate-email`(409) | `fall:staff:manage` |
| E-24 | `GET /shifts` · `PUT /shifts` | `[{staff_id, room_ids[], weekdays[], start, end}]` 전체 치환 | 200 | `validation-error`(시간대 겹침은 허용, 빈 배열 경고 필드) | `fall:staff:manage` |
| E-25 | `GET /facility` · `PATCH /facility` | `escalation_contact_user_id`(`facility_admin` 사용자, 전화 필수 — FR-029), `name`, `phone` | 200 | `validation-error`, `contact-not-admin`(422) | `fall:facility:manage` |
| E-26 | `GET /stats/false-alarms` | `?from&to&group_by=device|room` | 200 `data[] {key, resolved, false_alarm, rate}` | — | `fall:stats:read` |
| E-27 | `GET /channels/status` | — | 200 `data[] {channel: push|alimtalk|sms, state: ok|degraded|down, since, last_error_code}` | — | `fall:event:read` |
| E-28 | `GET /audit-log` | `?from&to&entity&cursor` | 200 `data[] {at, actor_role, actor_id, action, entity, entity_id, ip}` | — | `fall:audit:read` |
| E-29 | `PUT /me/push-token` | `platform`, `token` | 204 | `validation-error` | `fall:me:push-token` |
| E-30 | `GET /healthz` | — | 200 `{db, broker, worker_last_run_at, canary_last_ok_at}` / 503 — `worker_last_run_at`이 `WORKER_DEADMAN` 초과이거나 카나리 `CANARY_FAIL_STREAK` 연속 실패면 503 (외부 업타임 모니터가 AL-01·02를 감지) | — | 공개 |
| E-31 | `POST /me/push-receipts` | `[{notification_id, received_at}]` — 앱이 푸시 수신 즉시 전송, 오프라인이면 큐잉 후 일괄 | 204 — `notifications.push_ack_at` 기록 | `validation-error` | `fall:me:push-token` |
| E-32 | `GET /ack-links/{token}` · `POST /ack-links/{token}` | 에스컬레이션 SMS의 1회용 ack 토큰 | GET: 버튼 1개 페이지(이벤트 요약, 이름 없음) / POST: E-12와 동일 처리, `acked_by` = 토큰의 사용자, 토큰 `used_at` 기록 | `link-expired`(410), `link-invalid`(404, 재사용 포함) | 공개(토큰, `ACK_LINK_TTL`, `FAMILY_LINK_RATE_LIMIT`) |

### MQTT (디바이스 → 브로커, mTLS, CN = device_id)
| ID | 토픽 (QoS) | 페이로드 | 규칙 |
|---|---|---|---|
| M-01 | `fallguard/{device_id}/fall` (QoS 1) | `{device_event_id: uuid-v4 또는 "{boot_id}:{seq}", detected_at: ISO8601, confidence: 0..1, delayed: bool, vendor: {…원문}}` | `device_event_id`는 재부팅 후에도 유일(A-7). `(device_id, device_event_id)` 멱등 — 같은 키·다른 `detected_at`은 새 감지 + 관리자 경고. 클라이언트 `clean_session=false`, `client_id=device_id`; 브로커 `persistence true`; 서버는 NestJS 트랜스포트 대신 mqtt.js 직접 구독 + `manualAck`로 **DB 커밋 후 ack**(최소 1회). ACL `pattern write fallguard/%u/#`. `DEVICE_RATE_LIMIT`(DB 카운터, `delayed=true` 제외) 초과 시 드롭 + 관리자 알림. **카나리**: `devices.is_canary=true`면 `fall_detections`만 저장하고 이벤트·알림을 만들지 않으며 E-26 집계에서 제외, E-30 `canary_last_ok_at` 갱신 (SC-009). **미매핑**(현재 placement 없음): detections 저장 + 관리자 알림 `unmapped_device` 1건 + ack, 이벤트 없음 (EC-A5) |
| M-02 | `fallguard/{device_id}/heartbeat` (QoS 0) | `{at, fw_version, rssi}` | `HEARTBEAT_INTERVAL`마다. `devices.last_seen_at` 갱신만, 행 저장 없음 |
| M-03 | `fallguard/{device_id}/cmd` (QoS 1, 서버→디바이스) | `{cmd: ping|rotate_cert, nonce}` | P2 자리(OTA FR-024). MVP는 `ping`만 |

### 내부 잡 (pg-boss — 엔드포인트 아님, 커버리지 매핑용)
| ID | 잡 | 트리거 | 규칙 |
|---|---|---|---|
| J-01 | `escalate(event_id, level)` | 이벤트 INSERT·라우팅과 **같은 트랜잭션**에서 level = 초기 `escalation_level`+1(근무조 미배정이면 2)을 `ACK_TIMEOUT` 지연 예약(pg-boss `send` on tx), `next_escalation_at` 기록 | singleton `event_id:level`, `job_id`를 `fall_events.escalation_job_id`에 저장. 실행 시 `UPDATE fall_events SET escalation_level=$level WHERE id=$id AND status IN ('detected','notified') AND escalation_level=$level-1`(CAS, 행 잠금) — 0행이면 no-op. level < `ESCALATION_LEVELS`−1 이면 다음 예약 |
| J-02 | `offline-scan` | 매 `OFFLINE_SCAN_INTERVAL` | `last_seen_at < now() - HEARTBEAT_TIMEOUT` → offline + 관리자 알림(전이 시, `FLAP_SUPPRESS_WINDOW` 안 1건). 아울러 `detected/notified`로 `escalation_level` 진행 없이 `ACK_TIMEOUT` 이상 머문 스테일 이벤트(예약 잡 없음)를 찾아 J-01(level+1)을 즉시 실행(안전망). **시설 단위**: 등록 디바이스 ≥1이고 online 0이 `FACILITY_OFFLINE_DELAY` 지속 → 시설 `offline` + 관리자 알림 1건(`facility_offline`) + AL-04, 디바이스별 알림 억제; 1대 복귀 시 `online` + 알림 1건 (FR-028) |
| J-03 | `notify-send(notification_id)` | notifications 행 생성 시 | 채널 어댑터 호출, 실패 시 지수 백오프 재시도 `NOTIFY_RETRY_MAX`, 소진 시 폴백 행 생성 (`CHANNEL_FALLBACK_DELAY`) |
| J-04 | `family-notice(event_id)` | resolution `confirmed_fall` | 통지 대상 guardians → 링크 토큰 → J-03. `FAMILY_NOTICE_LATENCY_MAX` 초과 시 채널 배너 |
| J-05 | `retention-purge` | 매일 03:00 KST | `discharged_at + EVIDENCE_RETENTION` 경과 residents·guardians 개인정보 익명화 (`retention` 롤) |
| J-06 | `channel-health` | 매 60초 | 5분 실패율 ≥ `CHANNEL_DEGRADED_FAIL_RATE` → `degraded`, 연속 실패 ≥ `CHANNEL_DOWN_STREAK` → `down`, 회복 시 `ok`; 전이 시 관리자 알림 1건(`channel_state`) + SSE `facility_state` 배너 (FR-022) |
| J-07 | `push-delivery-check(notification_id)` | push 발송 수락 시 `CHANNEL_FALLBACK_DELAY` 지연 예약 | `push_ack_at IS NULL`이면 SMS 폴백 행 생성 → J-03 (FR-006·FR-026) |
| J-08 | `resolution-reminder(event_id)` | ack 시 `RESOLUTION_REMINDER_DELAY` 지연 예약 | 아직 `acked`면 관리자 알림 1건(`resolution_reminder`) + SSE `needs_resolution` 플래그 (FR-027) |
| J-09 | `ops-alarm` | 매 60초 | AL-01·03~09 조건을 DB·`df`에서 평가해 운영자 채널(SMS·메신저 웹훅)로 상태 전이 시 1건 발송. AL-02(워커 데드맨)는 워커가 자신을 감시할 수 없으므로 외부 업타임 모니터가 E-30 503으로 감지 |

## OpenAPI 스케치 (핵심 3개만)
```yaml
openapi: 3.1.0
info: { title: FallGuard API, version: 1.0.0 }
paths:
  /fall-events/{id}/ack:
    post:
      parameters:
        - { name: Idempotency-Key, in: header, required: true, schema: { type: string, format: uuid } }
      responses:
        "200":
          content: { application/json: { schema: { $ref: "#/components/schemas/AckResult" } } }
        "409": { $ref: "#/components/responses/Problem" }
  /fall-events/{id}/resolution:
    post:
      requestBody:
        content:
          application/json:
            schema:
              type: object
              required: [verdict]
              properties:
                verdict: { type: string, enum: [confirmed_fall, false_alarm] }
                note: { type: string, maxLength: 500 }
      responses:
        "201": { description: resolution created }
        "409": { $ref: "#/components/responses/Problem" }
  /family/{token}:
    get:
      security: []
      responses:
        "200":
          content:
            application/json:
              schema:
                type: object
                properties:
                  event_time: { type: string, format: date-time }
                  room: { type: string }
                  response_summary: { type: string }
                  responder_role: { type: string, enum: [staff, facility_admin] }
                  facility_phone: { type: string }
        "410": { $ref: "#/components/responses/Problem" }
components:
  schemas:
    AckResult:
      type: object
      properties:
        status: { type: string, enum: [acked] }
        acked_at: { type: string, format: date-time }
        already_acked: { type: boolean }
    Problem:
      type: object
      properties:
        type: { type: string, format: uri }
        title: { type: string }
        status: { type: integer }
        detail: { type: string }
        instance: { type: string }
        errors: { type: array, items: { type: object, properties: { field: { type: string }, code: { type: string } } } }
  responses:
    Problem:
      content: { application/problem+json: { schema: { $ref: "#/components/schemas/Problem" } } }
```

## ERD
```mermaid
erDiagram
  facilities ||--o{ users : has
  facilities ||--o{ rooms : has
  rooms ||--o{ beds : has
  facilities ||--o{ residents : has
  residents ||--o{ guardians : has
  beds ||--o| residents : occupies
  facilities ||--o{ devices : owns
  devices ||--o{ device_placements : history
  users ||--o{ shifts : works
  users ||--o{ refresh_sessions : has
  users ||--o{ push_tokens : has
  residents ||--o{ fall_events : subject
  devices ||--o{ fall_detections : emits
  fall_events ||--o{ fall_detections : merges
  devices ||--o{ fall_events : source
  fall_events ||--o{ notifications : sends
  fall_events ||--o{ resolutions : judged
  fall_events ||--o{ family_links : issues
  fall_events ||--o{ ack_links : issues
  users ||--o{ ack_links : receives
  facilities ||--o{ audit_log : records
  facilities ||--o{ idempotency_keys : stores

  facilities { uuid id PK; text name; text phone; uuid escalation_contact_user_id FK; text status "online|offline"; timestamptz offline_since; timestamptz created_at }
  users { uuid id PK; uuid facility_id FK; text email UK; text password_hash; text role "facility_admin|staff"; text name; text phone; timestamptz deleted_at "soft" }
  rooms { uuid id PK; uuid facility_id FK; text name; int floor }
  beds { uuid id PK; uuid room_id FK; text label }
  residents { uuid id PK; uuid facility_id FK; text name; uuid bed_id FK; timestamptz consent_signed_at; text consent_signer; timestamptz discharged_at "soft"; timestamptz purged_at }
  guardians { uuid id PK; uuid resident_id FK; text name; text phone; text relation; bool notify; timestamptz deleted_at "soft" }
  devices { uuid id PK; uuid facility_id FK; text label; text vendor; text status "claiming|online|offline|revoked"; text cert_serial; timestamptz last_seen_at; text fw_version; bool is_canary }
  device_placements { uuid id PK; uuid device_id FK; uuid bed_id FK "nullable"; uuid room_id FK "nullable"; timestamptz from_at; timestamptz to_at "nullable=current" }
  shifts { uuid id PK; uuid facility_id FK; uuid user_id FK; uuid room_id FK; smallint weekday; time start_t; time end_t }
  fall_detections { uuid id PK; uuid facility_id FK; uuid device_id FK; text device_event_id; uuid event_id FK; timestamptz detected_at; timestamptz received_at; bool delayed; jsonb vendor_payload "개인정보 없음" }
  fall_events { uuid id PK; uuid facility_id FK; uuid device_id FK; uuid resident_id FK "nullable"; uuid room_id FK; text status; timestamptz detected_at "첫 감지"; timestamptz received_at; int redetect_count; bool delayed; timestamptz notified_at; timestamptz acked_at; uuid acked_by FK; timestamptz resolved_at; timestamptz family_notified_at; smallint escalation_level; text escalation_job_id; timestamptz next_escalation_at }
  ack_links { uuid id PK; uuid event_id FK; uuid user_id FK; text token_hash; timestamptz expires_at; timestamptz used_at }
  notifications { uuid id PK; uuid facility_id FK; uuid event_id FK "nullable"; text kind; text recipient_kind "user|guardian|escalation_contact"; uuid recipient_id; text channel "push|alimtalk|sms"; smallint attempt; text state "pending|sent|delivered|failed"; text provider_message_id; text error_code; timestamptz created_at; timestamptz sent_at; timestamptz push_ack_at }
  resolutions { uuid id PK; uuid event_id FK; text verdict "confirmed_fall|false_alarm"; text note; uuid created_by FK; timestamptz created_at; bool is_current }
  family_links { uuid id PK; uuid event_id FK; uuid guardian_id FK; text token_hash; timestamptz expires_at; int view_count; timestamptz last_viewed_at }
  audit_log { bigint id PK; uuid facility_id; timestamptz at; text actor_kind; uuid actor_id; text action; text entity; uuid entity_id; jsonb payload "개인정보 없음"; inet ip }
  idempotency_keys { uuid facility_id; text key; text request_hash; int status; jsonb response; timestamptz created_at }
  refresh_sessions { uuid id PK; uuid user_id FK; text token_hash; timestamptz expires_at; timestamptz revoked_at }
  push_tokens { uuid user_id FK; text platform; text token; timestamptz updated_at }
```

### DDL 스케치 (제약이 설계인 테이블만)
```sql
-- 원시 감지: 멱등 수신·증거 보존 (모든 M-01 메시지 1행, 절대 병합·삭제 안 함)
CREATE TABLE fall_detections (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  facility_id uuid NOT NULL REFERENCES facilities(id),
  device_id uuid NOT NULL REFERENCES devices(id),
  device_event_id text NOT NULL,
  event_id uuid NOT NULL,                       -- 병합된 fall_events
  detected_at timestamptz NOT NULL,
  received_at timestamptz NOT NULL DEFAULT now(),
  delayed bool NOT NULL DEFAULT false,          -- received_at - detected_at > DEDUP_WINDOW
  vendor_payload jsonb NOT NULL DEFAULT '{}',   -- 개인정보 없음
  UNIQUE (device_id, device_event_id)           -- 같은 키·다른 detected_at은 앱이 새 키로 저장 + 경고
);
-- 이벤트: 병합 단위 + 상태 열거. 첫 에스컬레이션 잡은 INSERT와 같은 트랜잭션에서 예약 (J-01)
CREATE TABLE fall_events (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  facility_id uuid NOT NULL REFERENCES facilities(id),
  device_id uuid NOT NULL REFERENCES devices(id),
  resident_id uuid REFERENCES residents(id),
  room_id uuid NOT NULL REFERENCES rooms(id),
  status text NOT NULL CHECK (status IN ('detected','notified','acked','resolved','family_notified')),
  detected_at timestamptz NOT NULL,             -- 첫 감지
  received_at timestamptz NOT NULL DEFAULT now(),
  redetect_count int NOT NULL DEFAULT 0,
  delayed bool NOT NULL DEFAULT false,
  escalation_level smallint NOT NULL DEFAULT 0 CHECK (escalation_level BETWEEN 0 AND 2),
  escalation_job_id text,
  next_escalation_at timestamptz,               -- 콘솔·앱 카운트다운의 단일 출처 (GATE M10)
  notified_at timestamptz, acked_at timestamptz, acked_by uuid REFERENCES users(id),
  resolved_at timestamptz, family_notified_at timestamptz
);
ALTER TABLE fall_detections ADD FOREIGN KEY (event_id) REFERENCES fall_events(id);
CREATE INDEX ON fall_events (facility_id, status, received_at DESC);
-- 활성(미해결) 이벤트는 디바이스당 1개: dedup 병합의 DB 보증 (경합 시 두 번째 INSERT가 실패 → 재감지 +1 경로로)
CREATE UNIQUE INDEX fall_events_one_active_per_device
  ON fall_events (device_id) WHERE status IN ('detected','notified','acked');

-- 알림 시도: 이중 발송 방지 (TM-26)
CREATE TABLE notifications (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  facility_id uuid NOT NULL,
  event_id uuid REFERENCES fall_events(id),        -- NULL = 이벤트 없는 관리자 알림 (GATE H5)
  kind text NOT NULL CHECK (kind IN ('fall_alert','escalation','ack_echo','family_notice','device_offline','facility_offline','facility_online','channel_state','shift_unassigned','resolution_reminder','unmapped_device')),
  recipient_kind text NOT NULL, recipient_id uuid NOT NULL,
  channel text NOT NULL CHECK (channel IN ('push','alimtalk','sms')),
  attempt smallint NOT NULL,
  state text NOT NULL CHECK (state IN ('pending','sent','delivered','failed')),
  provider_message_id text, error_code text,
  created_at timestamptz NOT NULL DEFAULT now(), sent_at timestamptz, push_ack_at timestamptz,
  UNIQUE (event_id, recipient_kind, recipient_id, channel, attempt)
);

-- 판정 이력: 현재 판정은 이벤트당 1행
CREATE TABLE resolutions (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  event_id uuid NOT NULL REFERENCES fall_events(id),
  verdict text NOT NULL CHECK (verdict IN ('confirmed_fall','false_alarm')),
  note text CHECK (char_length(note) <= 500),
  created_by uuid NOT NULL REFERENCES users(id),
  created_at timestamptz NOT NULL DEFAULT now(),
  is_current bool NOT NULL DEFAULT true
);
CREATE UNIQUE INDEX resolutions_one_current ON resolutions (event_id) WHERE is_current;

-- 감사 로그: append-only를 권한 + 트리거로 (TM-21, SC-011)
CREATE TABLE audit_log (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  facility_id uuid NOT NULL, at timestamptz NOT NULL DEFAULT now(),
  actor_kind text NOT NULL, actor_id uuid, action text NOT NULL,
  entity text NOT NULL, entity_id uuid, payload jsonb NOT NULL DEFAULT '{}', ip inet
);
CREATE INDEX ON audit_log (facility_id, at DESC);
REVOKE UPDATE, DELETE, TRUNCATE ON audit_log FROM app_rw;
CREATE FUNCTION audit_log_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'audit_log is append-only'; END $$;
CREATE TRIGGER audit_log_no_change BEFORE UPDATE OR DELETE ON audit_log
  FOR EACH ROW EXECUTE FUNCTION audit_log_immutable();

-- 디바이스 배치 이력: 현재 배치는 디바이스당 1행
CREATE UNIQUE INDEX device_placements_current ON device_placements (device_id) WHERE to_at IS NULL;
-- 침상 점유: 활성 입소자는 침상당 1명
CREATE UNIQUE INDEX residents_one_per_bed ON residents (bed_id) WHERE discharged_at IS NULL AND bed_id IS NOT NULL;

-- 테넌시: RLS를 MVP부터 (A4 검토 M9). API 요청·워커 잡·SSE 허브는 트랜잭션마다 SET LOCAL app.facility_id
-- 대상: fall_detections, fall_events, notifications, resolutions, family_links, residents, guardians, devices, device_placements, shifts, users, audit_log
ALTER TABLE fall_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE fall_events FORCE ROW LEVEL SECURITY;   -- 테이블 소유자도 정책 적용
CREATE POLICY tenant_isolation ON fall_events
  USING (facility_id = current_setting('app.facility_id', true)::uuid);
-- 시설 횡단 잡(J-02·J-05·J-06·J-09)은 시설 목록을 먼저 읽고 시설별 트랜잭션으로 반복한다 (컨텍스트 없는 세션은 0행)

-- 컨텍스트 이전 조회(부트스트랩)는 BYPASSRLS 롤이 소유한 SECURITY DEFINER 함수 4개로만 (GATE H3)
CREATE ROLE bootstrap NOLOGIN BYPASSRLS;
CREATE FUNCTION auth_lookup_user(p_email text)
  RETURNS TABLE(id uuid, facility_id uuid, password_hash text, role text)
  SECURITY DEFINER SET search_path = public LANGUAGE sql AS $$
  SELECT id, facility_id, password_hash, role FROM users WHERE email = p_email AND deleted_at IS NULL $$;
ALTER FUNCTION auth_lookup_user(text) OWNER TO bootstrap;
GRANT EXECUTE ON FUNCTION auth_lookup_user(text) TO app_rw;
-- 같은 형식으로: device_lookup(p_device_id uuid) → facility_id·status·is_canary·현재 placement
--               claim_lookup(p_code text)     → device_id·facility_id·expires_at (1회용)
--               token_lookup(p_kind text, p_hash text) → event_id·facility_id·expires_at·used_at (family_links·ack_links)
-- 함수는 단일 행·최소 컬럼만 반환하고, 호출 직후 앱이 SET LOCAL app.facility_id로 컨텍스트를 세운다
```

## 데이터 규칙
- 시각: 전부 `timestamptz`, API는 UTC ISO 8601. SLA·정렬 기준은 `received_at`(서버). `detected_at`은 디바이스 참고값.
- 식별자: 외부 노출 ID는 `uuid`(열거 방지). `audit_log`만 `bigint identity`(순서·용량). 볼륨이 작아(`MAX_EVENTS_PER_DAY`) UUID 인덱스 국소성은 무시.
- 개인정보 위치: `residents.name`·`guardians.phone`만. `fall_events`·`notifications`·`audit_log.payload`에는 ID만 (04 프라이버시).
- 소프트삭제: `users.deleted_at`, `residents.discharged_at`, `guardians.deleted_at`. 파기: J-05가 `EVIDENCE_RETENTION` 경과 시 이름·전화를 `'[purged]'`로 익명화, `purged_at` 기록. 이벤트·알림·감사 기록(개인정보 없음)도 `EVIDENCE_RETENTION` 경과 후 파기 대상이나 구현은 P2(파일럿 기간 내 미도달) — 04 데이터 저장과 동일 정책.
- 보존: `audit_log`·`fall_events`·`notifications` = `EVIDENCE_RETENTION`. `idempotency_keys` 24시간. `family_links` = `FAMILY_LINK_TTL` + 30일(열람 로그 보존).
- 전화번호: E.164 저장, 응답 마스킹(`010-****-1234`). 로그에는 뒤 4자리만 (TM-27).
- DB 롤: `app_rw`(DML, audit_log는 INSERT/SELECT만), `migrator`(DDL), `retention`(J-05 전용 UPDATE on residents/guardians). 세 롤 모두 RLS 적용(`FORCE`), `BYPASSRLS` 없음.
- 테넌트 컨텍스트: HTTP는 토큰의 `facility_id`, 워커는 잡 페이로드의 `facility_id`, SSE 허브는 연결 시 시설별 채널 구독. 컨텍스트 없이 실행된 쿼리는 RLS로 0행.

## 커버리지 매핑 — P0·P1 요구사항 → 담당 엔드포인트/이벤트
| FR-ID | 우선순위 | 담당 |
|---|---|---|
| FR-001 | P0 | E-05, E-06 |
| FR-002 | P0 | E-07 (`device_placements` 이력) |
| FR-003 | P0 | M-01 → IngestModule (`fall_detections` UNIQUE, 수동 ack — DB 커밋 후, `received_at`) |
| FR-004 | P0 | M-01 처리 + `fall_events_one_active_per_device` 부분 유니크 + `redetect_count`·`delayed` |
| FR-005 | P0 | RoutingModule(E-24 shifts) → J-03(push, E-29 토큰) |
| FR-006 | P0 | J-03 재시도·폴백 + J-07 전달 확인 폴백, E-15 조회 |
| FR-007 | P0 | E-12 |
| FR-008 | P0 | J-01 (+ J-02 스테일 안전망) |
| FR-009 | P0 | E-13 (`resolutions` 이력, `event-closed`, `resident_id` 연결) |
| FR-010 | P0 | J-04 → J-03(alimtalk→sms), E-15; 입소자 미확정 시 보류 + E-11/E-14 `family_notice_blocked` 배너 |
| FR-011 | P1 | E-16 |
| FR-012 | P0 | M-02 + J-02 + E-08 |
| FR-013 | P0 | E-14 (SSE) + E-10 |
| FR-014 | P0 | E-17, E-18, E-20, E-21, E-22 |
| FR-015 | P0 | E-23, E-24, E-25 |
| FR-016 | P1 | E-11 (`timeline`), E-15 |
| FR-017 | P1 | E-26 |
| FR-018 | P0 | 전 엔드포인트 공통 규약(테넌시·스코프), E-01~E-03, RLS 정책 + 부트스트랩 함수 4개(DDL) |
| FR-019 | P0 | `audit_log` DDL(권한·트리거), E-28 |
| FR-020 | P1 | E-19 + J-05 |
| FR-021 | P2 | E-04 |
| FR-022 | P1 | J-06 + E-27 |
| FR-023 | P1 | 엔드포인트 없음 — 정적 문구 자산(앱·콘솔·알림톡 템플릿). 06 문구 검사 시나리오로 검증 |
| FR-024 | P2 | M-03 자리, `devices.fw_version` |
| FR-025 | P2 | NotifyModule 채널 어댑터 인터페이스(`push|alimtalk|sms|voice`), J-03 |
| FR-026 | P0 | E-31 + `notifications.push_ack_at` + J-07 |
| FR-027 | P1 | J-08 + E-14 `needs_resolution` |
| FR-028 | P0 | J-02 시설 단위 + `facilities.status` + E-14 `facility_state` + E-30 |
| FR-029 | P0 | E-25 `escalation_contact_user_id` + E-32 + `ack_links` |

매핑 0건 P0/P1 요구사항: 없음 (`check_package.py` C2로 검증).
