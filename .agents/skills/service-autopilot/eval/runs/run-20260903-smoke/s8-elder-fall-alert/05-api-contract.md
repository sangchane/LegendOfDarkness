# API 계약 & 데이터 스키마 — FallGuard-Care (요양시설 낙상 감지·알림)

> 근거 (A5 진입 사전조사, 검색 0회 — 스킬·템플릿 근거 재사용)
> - **정량** — Stripe 멱등성 키는 서버가 24시간 보관 후 동일 응답 재생 ([Stripe idempotent requests](https://docs.stripe.com/api/idempotent_requests), 2026-09-03) → 확인/확정 POST는 오프라인 큐 재전송 시 중복 없이 처리돼야 하므로 키 보관을 **72시간**으로 늘린다(단절 최대 허용치 가정)
> - **정성** — "License change done on a minor release" 류 벤더 의존 불만(04 근거)과 같은 맥락에서, 센서 벤더 메시지 포맷은 계약에 넣지 않고 정규화 스키마(DetectionSignal)만 계약으로 고정
> - **사용자 영향** — 직원 앱은 LAN이든 클라우드든 **같은 요청 형식**으로 확인·확정을 보낸다(게이트웨이 로컬 API = 클라우드 API 부분집합). 사용자는 연결 상태를 의식하지 않는다 (L-07 테슬러)

스킬 적용 기록: `ecc:api-design` — 상태코드 표·커서 페이지네이션·레이트리밋 헤더 채택, **URL 경로 버저닝(`/api/v1`)은 채택하지 않음**(stage-templates·Zalando #115 우선, 미디어타입 버저닝), 에러 봉투도 스킬의 `{error:{code}}` 대신 **RFC 9457**. `ecc:postgres-patterns` — `timestamptz`·`text`·BRIN·부분 인덱스·RLS `(SELECT …)` 래핑 채택, **ID는 bigint 대신 UUIDv7**(게이트웨이가 오프라인에서 생성해야 하므로 시퀀스 불가 — decision-log #D-020).

## 규약 (전 엔드포인트 공통)

- **에러 포맷**: RFC 9457 `application/problem+json` — `type`(URI, 예 `https://fallguard.example/problems/invalid-transition`)·`title`·`status`·`detail`·`instance` + 확장 `errors[]`(필드 오류). 스택트레이스·SQL 노출 금지 (Zalando #176·#177).
- **버저닝**: `Accept: application/vnd.fallguard+json; version=1`. URL에 버전 없음 (Zalando #115). 스펙 파일 semver(`openapi.yaml` 1.0.0). 비호환 변경만 version 증가, 동시 지원 최대 2개, `Sunset` 헤더로 6개월 예고.
- **페이지네이션**: 커서 기반 `?cursor=&limit=`(기본 50, 최대 200), 응답 `meta.next_cursor`·`meta.has_next` (Zalando #160). 정렬 `sort=-detected_at` 형식.
- **멱등성**: 부작용 있는 POST(`ack`·`resolve`·`amend`·`retry`·`claim`)는 `Idempotency-Key`(UUID, 클라이언트 생성) 필수. 서버 보관 **72h**, 같은 키+같은 본문 → 최초 응답 재생(200/201 그대로), 같은 키+다른 본문 → `409 idempotency-key-reuse`.
- **네이밍·권한**: 경로 kebab-case 복수 명사, 필드 `snake_case`, 시각 UTC ISO 8601(`2026-09-03T14:05:07.123Z`). 권한 스코프 `<모듈>:<자원>:<행위>` (Zalando #225 동형). 역할→스코프 매핑은 서버 고정, 요청 본문의 role 무시.
- **인증**: 직원 앱·관리 웹 = OIDC 호환 자체 발급 액세스 토큰(12h) + 리프레시(30d, 기기 바인딩). 게이트웨이 = mTLS 클라이언트 인증서(MQTT·REST 공통). 관리자 2단계(OTP).
- **레이트리밋**: 직원·관리자 토큰당 300/min, 로그인 10/min/IP, 게이트웨이 REST 60/min, MQTT 시설당 100 msg/s. 초과 시 `429` + `Retry-After` + `X-RateLimit-*` 헤더.
- **응답 봉투**: 단일 `{ "data": … }`, 목록 `{ "data": [...], "meta": {...} }`.
- **상태코드**: 200 조회/전이, 201 생성(+`Location`), 204 삭제, 400 형식 오류, 401/403, 404(타 시설 자원도 404 — 존재 누설 방지), 409 상태 충돌, 422 의미 오류, 429, 500(상세 없음), 503(+`Retry-After`).

## 엔드포인트 표 (클라우드 REST)

| ID | 메서드 경로 | 요청(핵심 필드) | 응답 | 주요 에러(RFC 9457 type) | 권한 스코프 |
|---|---|---|---|---|---|
| E-01 | `POST /auth/sessions` | `login_id`, `password`, `device_id`, `otp?` | 201 `{access_token, refresh_token, expires_in, role}` | `invalid-credentials`(401), `otp-required`(401), `rate-limited`(429) | 공개 |
| E-02 | `POST /auth/sessions/refresh` | `refresh_token`, `device_id` | 200 토큰 | `token-revoked`(401) | 공개 |
| E-03 | `DELETE /auth/sessions/current` | — | 204 | — | any |
| E-04 | `PUT /staff-devices/{device_id}` | `fcm_token`, `platform`, `app_version` | 200 | `unsupported-app-version`(426) | `staff:devices:write` |
| E-10 | `GET /fall-events` | `status?`, `zone_id?`, `from?`, `to?`, `outcome?`, `cursor`, `limit`, `sort` | 200 목록(FallEvent 요약) | `invalid-cursor`(400) | `events:fall-events:read` |
| E-11 | `GET /fall-events/{id}` | — | 200 FallEvent(상태·시각·행위자·통보 요약) | `not-found`(404) | `events:fall-events:read` |
| E-12 | `POST /fall-events/{id}/ack` | `acked_at`(단말 시각), `device_id` + `Idempotency-Key` | 200 FallEvent(ACKNOWLEDGED) / 200 `already-acked-by`(첫 확인자 정보, 에러 아님) | `invalid-transition`(409: 이미 종결), `not-found`(404) | `events:fall-events:ack` |
| E-13 | `POST /fall-events/{id}/resolve` | `outcome`(`CONFIRMED`\|`FALSE_POSITIVE`), `injury?`(`none`\|`minor`\|`major`), `note?`(≤500자), `resident_id`(**CONFIRMED면 필수** — 구역 배정 수급자가 1명이면 서버 자동 채움, 2명 이상이면 클라이언트가 선택), `device_at` + `Idempotency-Key` | 200 FallEvent(`resolved_at` = 게이트웨이 RTC 수신 시각) | `invalid-transition`(409: ACK 전), `resident-required`(422: 다인실 미지정), `validation-failed`(422) | `events:fall-events:resolve` |
| E-14 | `POST /fall-events/{id}/amend` | `outcome`(`FALSE_POSITIVE`\|`CONFIRMED` — 양방향, 각 1회), `resident_id`(CONFIRMED로 정정 시 필수), `reason` + `Idempotency-Key` | 200 FallEvent | `amend-window-closed`(409: 5분 초과), `amend-already-used`(409), `invalid-transition`(409) | `events:fall-events:resolve` |
| E-17 | `POST /fall-events/{id}/displayed` | `device_id`, `displayed_at`(단말 시각) | 204 | `not-found` | `events:fall-events:read` |
| E-18 | `POST /fall-events` | `origin`=`manual`, `zone_id`, `resident_id`, `found_at`, `note?` + `Idempotency-Key` | 201 FallEvent(status=ACKNOWLEDGED, acked_by=요청자) + `Location` | `validation-failed`(422), `zone-not-found`(404) | `events:fall-events:create-manual` |
| E-15 | `GET /fall-events/{id}/transitions` | — | 200 Transition[] (seq·상태·행위자·시각·hash) | `not-found` | `events:fall-events:read` |
| E-16 | `GET /fall-events/{id}/incident-report` | `Accept: application/pdf` | 200 PDF | `report-not-available`(409: CONFIRMED 아님) | `reports:incident:read` |
| E-20 | `GET /fall-events/{id}/notifications` | — | 200 Notification[](채널·수신자 마스킹·상태·시각·provider_id) | `not-found` | `notify:receipts:read` |
| E-21 | `POST /notifications/{id}/retry` | `channel?` + `Idempotency-Key` | 202 | `already-delivered`(409) | `notify:receipts:retry` |
| E-30 | `GET /facilities/current` · `PATCH /facilities/current` | `escalation_policy{zone_timeout_s:60 [30~120], all_timeout_s:120 [60~300], repeat_every_s:30 [15~60], repeat_max:3 [1~5], phone_targets:[{role:"nurse", on_duty_only:true},{role:"facility_admin", on_duty_only:false}] [1~4개], phone_step_s:30 [15~60]}`, `merge_window_s:30 [10~60]`, `amend_window_s:300 [60~600]` — 범위 밖·비활성화(0) 불가 | 200 | `validation-failed`(422: 범위 밖), `phone-target-missing-number`(422: 대상 역할 직원 중 `phone_enc` 없는 계정 존재) | `facility:settings:read/write` |
| E-31 | `GET/POST /zones` · `PATCH/DELETE /zones/{id}` | `name`, `kind`(`bedroom`\|`bathroom`\|`corridor`\|`common`), `floor` | 200/201/204 | `zone-has-sensors`(409 on delete) | `facility:zones:*` |
| E-32 | `GET/POST /sensors` · `PATCH /sensors/{id}` | `sensor_id`(MAC), `zone_id`, `install_height_cm`, `sensitivity` | 200/201 | `duplicate-sensor`(409) | `facility:sensors:*` |
| E-33 | `GET/POST /residents` · `PATCH /residents/{id}` · `POST /residents/{id}/discharge` | `name`, `zone_id`, `bed_label?`, `consent_signed_at` | 200/201 | `consent-missing`(422) | `facility:residents:*` |
| E-34 | `GET/POST /residents/{id}/guardians` · `PATCH/DELETE /guardians/{id}` | `phone`(E.164), `relation_label`("어머님"), `notify_consent`(bool), `consent_at` | 200/201/204 | `invalid-phone`(422) | `facility:guardians:*` |
| E-35 | `GET/POST /staff` · `PATCH /staff/{id}` · `PUT /staff/{id}/zone-assignments` | `name`, `role`(`caregiver`\|`nurse`\|`facility_admin`\|`hq_operator`), `login_id`, `phone`(E.164, `nurse`·`facility_admin`은 필수), `zone_ids[]` | 200/201 | `role-not-allowed`(403), `phone-required-for-role`(422) | `facility:staff:*` |
| E-36 | `GET/PUT /shifts` | `[{staff_id, zone_ids[], starts_at, ends_at}]` (주간 표) | 200 | `shift-overlap`(422), `roster-gap-warning`(200+`meta.warnings`) | `facility:shifts:*` |
| E-40 | `GET /reports/monthly` | `month=2026-09`, `Accept: text/csv`\|`application/pdf` | 200 | `not-ready`(409: 당월) | `reports:monthly:read` |
| E-41 | `GET /metrics/summary` | `from`, `to` | 200 `{events, median_ack_s, p95_display_s, false_positive_rate, notify_success_rate}` | — | `reports:metrics:read` |
| E-50 | `POST /gateways/claim` | `claim_token`, `csr`, `hw_id` + `Idempotency-Key` | 201 `{gateway_id, certificate, mqtt_endpoint, facility_id}` | `claim-token-invalid`(401), `claim-token-used`(409) | 공개(클레임 토큰) |
| E-51 | `GET /gateways/current/config` | `If-None-Match` | 200 `{roster, residents, zones, sensors, policy, etag}` / 304 | — | mTLS 게이트웨이 |
| E-52 | `GET /gateways` · `GET /gateways/{id}` | — | 200 `{last_seen_at, outbox_depth, fw_version, sensors_online/total}` | — | `facility:gateways:read` |
| E-60 | `GET /audit-log` | `from`, `to`, `actor?`, `cursor` | 200 AuditRecord[] | — | `audit:log:read` |
| E-61 | `POST /audit-log/verify` | `from`, `to` | 200 `{verified: bool, broken_at?: seq}` | — | `audit:log:verify` |
| E-70 | `GET /health` | — | 200 `{status, db, mqtt, notify_providers}` | 503 | 공개(내부망) |

### 게이트웨이 로컬 API (LAN, 직원 앱 전용 — 클라우드 계약의 부분집합)

| ID | 메서드 경로 (`https://gw.<facility>.local`) | 동일 계약 | 비고 |
|---|---|---|---|
| L-11 | `GET /fall-events/{id}` | = E-11 | 로컬 SQLite 기준 |
| L-12 | `POST /fall-events/{id}/ack` | = E-12 | 게이트웨이가 진실원. 클라우드로 전이 재생 |
| L-13 | `POST /fall-events/{id}/resolve` | = E-13 | 상동 |
| L-14 | `POST /fall-events/{id}/amend` | = E-14 | 상동 |
| L-17 | `POST /fall-events/{id}/displayed` | = E-17 | LAN 경로 우선 |
| L-18 | `POST /fall-events` | = E-18 (수동 등록) | 게이트웨이가 생성·전이 |
| L-90 | `WS /staff` | 서버→앱: `alert`·`update`·`siren`·`gateway-status`·`ping` / **앱→서버: `displayed{event_id, device_id, displayed_at}`·`pong`** | **게이트웨이 발급 기기 자격증명(30일)** 으로 인증 — 클라우드 토큰과 무관, 오프라인 검증. `displayed`는 게이트웨이가 `transitions.payload.displayed[]`(device_id·displayed_at·received_at)로 기록·미러 → SC-001·SLI 원천 |
| L-05 | `POST /device-credentials` | 요청 `{device_id, cloud_access_token}` → 201 `{credential, expires_at}` | 최초 1회·갱신 시 클라우드 토큰으로 신원 확인 후 게이트웨이가 서명 발급. 폐기 목록은 E-51 config로 동기화 |

앱 규칙: LAN 우선 → 실패 시 클라우드(E-1x, 클라우드는 `transition-proposal` 커맨드로 게이트웨이에 위임, 게이트웨이 무응답 5초면 **202**) → 둘 다 실패 시 로컬 큐(같은 `Idempotency-Key`로 재전송). 진실원은 항상 게이트웨이.

### MQTT 토픽 계약 (게이트웨이 ↔ 클라우드)

| 토픽 | 방향 | QoS | 페이로드 |
|---|---|---|---|
| `fg/{facility_id}/transitions` | GW→Cloud | 1 | `Transition` (아래 스키마), `seq` 단조 증가, 클라우드는 `(gateway_id, seq)` 중복 제거 |
| `fg/{facility_id}/telemetry` | GW→Cloud | 0 | `{outbox_depth, sensors_online, watchdog_ok, fw_version, disk_write_mb_24h, ts}` 60초 |
| `fg/{facility_id}/sensors/{sensor_id}/heartbeat` | GW→Cloud | 0 | `{rssi, fw, ts}` 60초 |
| `fg/{facility_id}/commands` | Cloud→GW | 1 | `{type: "config-updated"\|"siren-off"\|"resync"\|"transition-proposal"\|"revoke-device", etag?, proposal?: {event_id, kind: ack\|resolve\|amend, actor_id, device_id, at, payload, idempotency_key}}` — 게이트웨이는 proposal을 적용한 결과를 `transitions`로 응답 |

클라우드는 `(gateway_id, seq)` 로 게이트웨이 해시체인 연속성을 검증하고(`prev_hash` 불일치 → `audit_log` 경고 + 관리자 알림), 텔레메트리 180초 미수신 시 `gateway-silent` 를 발행한다(FR-009).

ACL: 게이트웨이 인증서 CN = `gateway_id`, 발행·구독은 자기 `facility_id` 프리픽스만.

## OpenAPI 스케치 (핵심 2개만)

```yaml
openapi: 3.1.0
info: { title: FallGuard-Care API, version: 1.0.0 }
paths:
  /fall-events/{id}/ack:
    post:
      parameters:
        - { name: id, in: path, required: true, schema: { type: string, format: uuid } }
        - { name: Idempotency-Key, in: header, required: true, schema: { type: string, format: uuid } }
      requestBody:
        content:
          application/json:
            schema:
              type: object
              required: [acked_at, device_id]
              properties:
                acked_at: { type: string, format: date-time }
                device_id: { type: string }
      responses:
        "200":
          content:
            application/vnd.fallguard+json; version=1:
              schema: { $ref: "#/components/schemas/FallEvent" }
        "409":
          content:
            application/problem+json:
              schema: { $ref: "#/components/schemas/Problem" }
  /fall-events/{id}/resolve:
    post:
      requestBody:
        content:
          application/json:
            schema:
              type: object
              required: [outcome, resolved_at]
              properties:
                outcome: { type: string, enum: [CONFIRMED, FALSE_POSITIVE] }
                injury: { type: string, enum: [none, minor, major] }
                note: { type: string, maxLength: 500 }
                resident_id: { type: string, format: uuid }
                resolved_at: { type: string, format: date-time }
      responses:
        "200": { description: FallEvent }
        "409": { description: invalid-transition }
components:
  schemas:
    FallEvent:
      type: object
      required: [id, facility_id, zone_id, status, detected_at]
      properties:
        id: { type: string, format: uuid }
        facility_id: { type: string, format: uuid }
        zone_id: { type: string, format: uuid }
        status: { type: string, enum: [DETECTED, ACKNOWLEDGED, CONFIRMED, FALSE_POSITIVE] }
        detected_at: { type: string, format: date-time }
        acked_at: { type: string, format: date-time, nullable: true }
        acked_by: { type: string, format: uuid, nullable: true }
        resolved_at: { type: string, format: date-time, nullable: true }
        outcome: { type: string, nullable: true }
        injury: { type: string, nullable: true }
        resident_id: { type: string, format: uuid, nullable: true }
        escalation_level: { type: integer, enum: [0, 1, 2] }
        signal_ids: { type: array, items: { type: string, format: uuid } }
    Transition:
      type: object
      required: [transition_id, event_id, seq, from_status, to_status, actor_type, at, prev_hash, hash]
      properties:
        transition_id: { type: string, format: uuid }
        event_id: { type: string, format: uuid }
        seq: { type: integer }
        from_status: { type: string, nullable: true }
        to_status: { type: string, enum: [DETECTED, ACKNOWLEDGED, CONFIRMED, FALSE_POSITIVE, ESCALATED_ALL, ESCALATED_PHONE] }
        actor_type: { type: string, enum: [gateway, staff, system] }
        actor_id: { type: string, nullable: true }
        device_id: { type: string, nullable: true }
        at: { type: string, format: date-time, description: "권위 시각 — 게이트웨이 RTC 기준 수신/발생 시각. ack_at·resolved_at·사고보고서·SC-002는 이 값" }
        received_at: { type: string, format: date-time, description: "클라우드 수신 시각 — FR-006 10분 SLO 기산점" }
        payload: { type: object, description: "device_at(단말 시각, 보조)·displayed[]·reminder 등" }
        prev_hash: { type: string }
        hash: { type: string, description: "sha256(prev_hash + canonical_json(without hash))" }
    Problem:
      type: object
      properties:
        type: { type: string, format: uri }
        title: { type: string }
        status: { type: integer }
        detail: { type: string }
        instance: { type: string }
        errors: { type: array, items: { type: object, properties: { field: {type: string}, message: {type: string} } } }
```

`ESCALATED_ALL`/`ESCALATED_PHONE`은 FallEvent의 `status`가 아니라 **전이 기록·`escalation_level`** 로만 표현한다(INV-02의 상태 집합은 4개 유지).

## ERD

```mermaid
erDiagram
  facilities ||--o{ zones : has
  facilities ||--o{ staff : employs
  facilities ||--o{ residents : houses
  facilities ||--o{ gateways : owns
  facilities ||--o{ fall_events : records
  zones ||--o{ sensors : contains
  zones ||--o{ residents : assigned
  residents ||--o{ guardians : has
  staff ||--o{ staff_devices : registers
  staff ||--o{ shifts : works
  fall_events ||--o{ event_transitions : "append-only"
  fall_events ||--o{ notifications : triggers
  fall_events }o--o| residents : "resolved for"
  fall_events ||--o{ detection_signals : merged_from
  facilities ||--o{ audit_log : writes

  facilities { uuid id PK; text name; jsonb escalation_policy; int merge_window_s; int amend_window_s; timestamptz created_at }
  zones { uuid id PK; uuid facility_id FK; text name; text kind; int floor; timestamptz deleted_at "soft" }
  sensors { uuid id PK; uuid facility_id FK; uuid zone_id FK; text hw_id UK; int install_height_cm; int sensitivity; timestamptz last_heartbeat_at; timestamptz deleted_at "soft" }
  gateways { uuid id PK; uuid facility_id FK; text hw_id UK; text cert_serial; text fw_version; timestamptz last_seen_at; int outbox_depth; timestamptz revoked_at }
  residents { uuid id PK; uuid facility_id FK; uuid zone_id FK; text name "enc"; text bed_label; timestamptz consent_signed_at; timestamptz discharged_at "soft"; timestamptz purge_after }
  guardians { uuid id PK; uuid resident_id FK; text phone_enc "pgcrypto"; text relation_label; bool notify_consent; timestamptz consent_at; timestamptz deleted_at "soft" }
  staff { uuid id PK; uuid facility_id FK; text login_id UK; text password_hash "argon2id"; text role; text name; text phone_enc "pgcrypto, nurse/facility_admin 필수"; uuid[] zone_ids; bool otp_enabled; timestamptz deleted_at "soft" }
  staff_devices { text device_id PK; uuid staff_id FK; text fcm_token; text platform; text app_version; timestamptz last_seen_at }
  shifts { uuid id PK; uuid facility_id FK; uuid staff_id FK; uuid[] zone_ids; timestamptz starts_at; timestamptz ends_at }
  fall_events { uuid id PK "uuidv7"; uuid facility_id FK; uuid zone_id FK; text origin "sensor|manual"; text status; timestamptz detected_at; timestamptz acked_at "GW RTC"; uuid acked_by FK; timestamptz resolved_at "GW RTC"; text outcome; text injury; text note; uuid resident_id FK "CONFIRMED면 NOT NULL (CHECK)"; int escalation_level; int amend_count; uuid gateway_id FK }
  event_transitions { uuid transition_id PK; uuid event_id FK; uuid facility_id; uuid gateway_id FK; bigint seq "per gateway"; text from_status; text to_status; text actor_type; text actor_id; text device_id; timestamptz at; timestamptz received_at; bool outage_deferred; jsonb payload; text prev_hash; text hash }
  device_credentials { text device_id PK; uuid staff_id FK; uuid gateway_id FK; text credential_hash; timestamptz issued_at; timestamptz expires_at; timestamptz revoked_at }
  detection_signals { uuid id PK; uuid facility_id; uuid event_id FK; uuid sensor_id FK; text kind; real confidence; timestamptz at "raw 벤더 페이로드는 게이트웨이 30일 보관, 클라우드 미저장" }
  notifications { uuid id PK; uuid facility_id; uuid event_id FK; text channel "fcm|lan|alimtalk|sms|voice"; text recipient_ref; text status "queued|sent|delivered|failed"; text provider_msg_id; timestamptz queued_at; timestamptz sent_at; timestamptz delivered_at; text failure_reason; int attempt }
  audit_log { bigint seq PK; uuid facility_id; text actor_type; text actor_id; text action; text target_type; uuid target_id; jsonb diff; timestamptz at; text prev_hash; text hash }
  idempotency_keys { text key PK; uuid facility_id; text request_hash; int status_code; jsonb response; timestamptz expires_at }
```

### 스키마 규칙 (ecc:postgres-patterns 적용)

- 전 테이블 `facility_id` + `ENABLE ROW LEVEL SECURITY`, 정책 `USING (facility_id = (SELECT current_setting('app.facility_id')::uuid))`. 본사 운영자 역할은 별도 `BYPASSRLS` 없는 읽기 전용 뷰로만.
- `event_transitions`·`audit_log`·`notifications`: INSERT만 허용(`REVOKE UPDATE, DELETE`), 트리거로 `hash = sha256(prev_hash || canonical_json)` 검증. **해시체인은 writer 단위** — `event_transitions`는 `(gateway_id, seq)` UNIQUE + 게이트웨이별 단조 seq(게이트웨이 체인의 미러), `audit_log`는 클라우드 자체 체인(`seq`). 두 체인을 섞지 않는다. `notifications.status` 갱신은 UPDATE 대신 상태별 행 INSERT(`attempt` 증가)로 표현.
- 인덱스: `fall_events (facility_id, status, detected_at DESC)`, 부분 인덱스 `WHERE status IN ('DETECTED','ACKNOWLEDGED')`(미결 큐), `event_transitions USING brin (at)`, `detection_signals USING brin (at)`, `notifications (event_id)`, `shifts (facility_id, starts_at, ends_at)`, `guardians (resident_id) WHERE deleted_at IS NULL`.
- 파티셔닝: `detection_signals` 월 단위 RANGE(`at`) + 30일 드롭, `event_transitions`·`audit_log` 연 단위 RANGE(3년 보관 후 드롭 아님 — 법적 요청 시까지 아카이브 후 드롭).
- 타입: 시각 `timestamptz`, 문자열 `text`, 플래그 `boolean`, ID `uuid`(v7, 앱/게이트웨이 생성), 전화 `phone_enc`(pgcrypto `pgp_sym_encrypt`, 키는 KMS/환경변수).
- 설정: `statement_timeout 30s`, `idle_in_transaction_session_timeout 30s`, `pg_stat_statements` 활성.

## 데이터 규칙

- 시각: UTC ISO 8601 저장·전송, 표시만 KST. 게이트웨이 `at`(RTC)과 클라우드 `received_at` 둘 다 보존.
- 식별자: UUIDv7(시간 정렬, 오프라인 생성). `hw_id`(MAC/시리얼)는 별도 UK.
- 금액: 해당 없음(과금 non-goal).
- 보존(03 NFR-005 상수 표 참조): `detection_signals` 클라우드 30일(메타만) · `fall_events`/`event_transitions`/`notifications`/`audit_log` **3년** · `idempotency_keys` 72h · 퇴소 수급자·보호자는 `purge_after`(퇴소 + 3년) 경과 시 파기 배치.
- 권위 시각: `event_transitions.at` = 게이트웨이 RTC(법적·측정용). `payload.device_at`은 보조. `received_at` = 클라우드 수신(통보 SLO 기산점).
- 소프트삭제: zones·sensors·residents·guardians·staff만. fall_events·transitions·notifications·audit_log는 삭제 불가.
- 마스킹: 로그·E-20 응답의 전화번호는 `010-****-1234`.

## 커버리지 매핑 (FR ↔ 엔드포인트/이벤트) — P0·P1 매핑 0건 = 결함

| FR-ID | 우선순위 | 담당 엔드포인트 / 이벤트 |
|---|---|---|
| FR-001 병합 | P0 | 게이트웨이 EventEngine 내부 + `fg/*/transitions`(DETECTED, `signal_ids`) · E-30 `merge_window_s` |
| FR-002 직원 알림 이중 경로 | P0 | L-90 WS `alert` + FCM(E-04 등록 토큰) via NotificationService |
| FR-003 확인 | P0 | L-12 / E-12 · L-90 `update` |
| FR-004 에스컬레이션 | P0 | 게이트웨이 타이머 → transitions `ESCALATED_ALL`/`ESCALATED_PHONE` → NotificationService voice(Twilio, `phone_targets` 순서·`staff.phone_enc`) · E-30 `escalation_policy` · E-35 `phone` |
| FR-005 결과 입력·리마인더·resident 필수 | P0 | L-13 / E-13(`resident-required` 422) · 리마인더는 system transition `payload.reminder` + FCM |
| FR-020 수동 등록 | P1 | L-18 / E-18 (`origin=manual`) |
| FR-021 표시 시각 보고 | P0 | L-90 `displayed` · L-17 / E-17 · `transitions.payload.displayed[]` · E-41 `p95_display_s` |
| FR-006 보호자 통보·폴백·정정 | P0 | CONFIRMED 전이 → notifications(alimtalk→sms) · E-14 amend · E-20 · E-21 |
| FR-007 오프라인 지속·동기화 | P0 | L-1x 로컬 API · outbox → `fg/*/transitions` QoS1 재생 · E-52 `outbox_depth` · 관리 웹 stale 배지 |
| FR-008 감사·해시체인·RTC | P0 | `event_transitions`·`audit_log` 스키마 · E-15 · E-60 · E-61 |
| FR-009 장비 알람·감지 중단 | P1 | `fg/*/sensors/*/heartbeat` · `fg/*/telemetry` → 관리자 FCM(channel=device) · 텔레메트리 180초 미수신 → `gateway-silent` → 전 직원 FCM + 관리자 · E-52 · L-90 `gateway-status` |
| FR-002 오프라인 자격증명·클라우드 경유 ACK | P0 | L-05 device-credentials · `commands.transition-proposal` · E-12 202 규칙 |
| FR-010 근무표 | P1 | E-35 zone-assignments · E-36 shifts · E-51 config 스냅샷 |
| FR-011 이력·리포트 | P1 | E-10 · E-41 · E-40 |
| FR-012 사고보고서 PDF | P1 | E-16 |
| FR-013 수급자·보호자·동의·파기 | P1 | E-33 · E-34 · `purge_after` 배치 |
| FR-014 테넌시·권한 | P1 | RLS + 스코프 표 전체 · 404 규칙 |
| FR-015 프로비저닝·OTA | P1 | E-50 claim · E-51 config · Mender(외부 계약, 07) |
| FR-016 4상태 UI | P1 | E-52·E-70 상태 필드 + L-90 `ping`(stale 판정 15분) |
| FR-017 다시설 대시보드 | P2 | (2단계) 본사 뷰 — 미설계, P2이므로 결함 아님 |
| FR-018 너스콜 릴레이 | P2 | (2단계) 게이트웨이 GPIO — 미설계 |
| FR-019 iOS Critical Alerts | P2 | (2단계) E-04 `platform=ios` 확장 |

P0·P1 21건(FR-001~016·020·021) 중 미매핑 **0건**. SC ↔ 엔드포인트: SC-001은 E-17/L-90 `displayed`→E-41, SC-002는 E-41(`at` 기준), SC-005는 E-20/E-41, SC-006은 E-52 outbox, SC-007은 E-20(channel=voice), SC-008은 E-61.
