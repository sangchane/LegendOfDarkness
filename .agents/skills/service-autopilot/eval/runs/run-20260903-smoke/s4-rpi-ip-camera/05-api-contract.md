# API 계약 & 데이터 스키마 — PiCam Watch

> 근거 (A5 진입 사전조사, 검색 1회, 2026-09-03)
> - **정량**: WHEP는 POST(SDP offer) → 201 + `Location`(세션 URL) → DELETE로 종료, 4개 동사(POST/DELETE/PATCH/OPTIONS)·2개 URL. 2026-05 기준 draft-ietf-wish-whep-03, 아직 RFC 아님 ([IETF datatracker](https://datatracker.ietf.org/doc/draft-ietf-wish-whep/) · [wish-wg 저장소](https://github.com/wish-wg/webrtc-http-egress-protocol)) → 스트림 세션 엔드포인트는 WHEP 형태를 그대로 따르되 "draft 준수"로 표기.
> - **정성**: go2rtc API는 localhost 요청을 기본 통과시키고 카메라 URL(비밀번호 포함)을 API로 조회할 수 있다 ([go2rtc API 문서](https://go2rtc.org/internal/api/), A4 확인) → 우리 API는 go2rtc 응답을 **그대로 전달하지 않고** SDP만 추출해 돌려주며, 스트림은 런타임 API로 메모리에만 등록한다.
> - **사용자 영향**: 시청자는 API를 모른다. 유일하게 체감하는 계약은 "재생이 5초 안에 시작되고, 안 되면 이유가 보인다"(E-2·L-06) — WHEP 실패 응답의 `detail`이 그대로 타일 에러 문구가 된다(해요체, T-09).

스킬: `ecc:api-design`(자원 명명·상태코드·레이트리밋 헤더·페이지네이션 형식 적용. **URL 버저닝 권고는 stage-templates(Zalando #115)가 우선하여 미채택** — decision-log #17) · `ecc:postgres-patterns`(복합 인덱스 순서·부분 인덱스·append-only를 SQLite로 번안)
개정: v1.1 — 스트림 토큰 제거, 세션 2단 한도, 열람·반출 감사, 설정 토큰, 해시 체인. **v1.2 (2026-09-03, GATE 반영)** — 엔드포인트 ID 접두사 재부여(엣지케이스 E-·목표 G-·STRIDE S-와 충돌 제거), WebSocket 인증을 쿠키+Origin으로, 오디오 필드 제거, 촬영범위·시간 필드, 다운로드 Owner 스코프, PTZ 범위·웹훅·사용자 관리 FR 매핑, 감사로그 아카이브 절차 정정.

## 규약 (전 엔드포인트 공통)
- **에러 포맷**: RFC 9457 `application/problem+json` — `type`(`urn:picam:problem:<code>`)·`title`·`status`·`detail`(사용자 문구, 해요체)·`instance`. 스택트레이스·SQL·내부 경로 금지. 검증 실패는 422 + `errors[]{field,code}`.
- **버저닝**: `Accept: application/vnd.picam.v1+json`(생략 시 v1). URL 버저닝 없음. OpenAPI 파일은 semver(`1.2.0`).
- **페이지네이션**: 커서 — `?cursor=<opaque>&limit=20(≤100)`, 응답 `{data:[…], meta:{next_cursor, has_next}}`. 정렬은 `-created_at` 고정(ULID 역순 = 시간 역순).
- **멱등성**: 부작용 있는 POST(카메라 등록·프리셋 생성·사용자 생성)는 `Idempotency-Key`(클라 ULID) 필수, 서버 24h 보관, 재시도 시 최초 응답 재생. PTZ `stop`은 본질적으로 멱등. `moves`는 750ms 갱신 루프의 일부라 키 불필요(1s 자기 종료).
- **인증**: 세션 쿠키 `picam_session`(HttpOnly·Secure·SameSite=Lax, 12h/유휴 1h) **단일 수단**. REST 상태 변경·WHEP 요청은 `X-Requested-With: PiCam` 헤더 필수(CSRF). **WebSocket(MSE)은 브라우저가 커스텀 헤더를 못 보내므로 쿠키 + `Origin` 헤더가 허용 origin(MagicDNS·LAN self-signed 2개)과 일치할 때만 업그레이드**. origin별 세션 분리.
- **권한 스코프** `cam:<자원>:<행위>`: Viewer = `cam:camera:read cam:stream:read cam:event:read cam:clip:read`; Owner = Viewer + `cam:camera:write cam:ptz:write cam:preset:write cam:clip:download cam:clip:delete cam:audit:read cam:settings:write cam:user:write`.
- **세션 한도(INV-8)**: 사용자 세션당 동시 스트림(WHEP+MSE) ≤ 4, 전역 ≤ 16(고정, 설정 불가). 초과 시 409 `viewer-limit`. 활성 세션은 go2rtc 소비자 수와 5초 주기로 동기화(유령 세션 정리).
- **레이트리밋**: `X-RateLimit-Limit/Remaining/Reset` 헤더, 초과 시 429 + `Retry-After`. PTZ 5/s/user, 로그인 10/10min/IP, 기타 100/min/user.
- **네이밍**: 경로 kebab-case 복수형, JSON snake_case, 시각 UTC ISO 8601(`2026-09-03T01:23:45Z`), ID ULID(26자). 문서 ID 접두사: `AUTH- CAM- STR- PTZ- EVT- CLIP- SET- AUD- USR- HLT-`(03의 E-/G-/SC-/FR-, 04의 STRIDE S/T/R/I/D/E, 02의 P1~P6와 충돌 없음).

## 엔드포인트 표
| ID | 메서드 경로 | 요청(핵심 필드) | 응답 | 주요 에러(problem code) | 스코프 |
|---|---|---|---|---|---|
| AUTH-1 | `POST /auth/setup` | setup_token(콘솔 표시 1회용), username, password(≥12자) | 201 Owner 생성 + 세션 + `recovery_key`(백업 복구 키, 1회 표시), 마법사 라우트 제거 | 409 `setup-done`, 401 `setup-token-invalid`(5회 실패 시 423 `setup-locked`) | 없음(첫 부팅만) |
| AUTH-2 | `POST /auth/login` | username, password | 200 `{user{id,role}}` + Set-Cookie | 401 `invalid-credentials`, 429 | 없음 |
| AUTH-3 | `POST /auth/logout` | — | 204 | — | 세션 |
| AUTH-4 | `GET /auth/me` | — | 200 user + scopes | 401 | 세션 |
| CAM-1 | `POST /camera-discoveries` | timeout_s(≤10) | 200 `data[]{xaddr, model, manufacturer}` | 504 `discovery-timeout` | `cam:camera:write` |
| CAM-2 | `GET /cameras` | cursor, limit | 200 목록 `{id,name,status,ptz_fault,ptz_supported,profiles[]{name,codec,bitrate_kbps,gop_s},record_profile,ptz_limits}` | — | `cam:camera:read` |
| CAM-3 | `POST /cameras` (Idempotency-Key) | name, onvif_xaddr 또는 rtsp_url, username, password, purpose, location, coverage_area, operating_hours, manager{name,phone}, retention_days(1~90, 기본 30) | 201 + `Location`; 본문에 검증 결과(profiles, record_profile, max_rtsp_sessions, warnings[]) | 422, 502 `camera-unreachable`, 415 `codec-unsupported`(H.264 없음, INV-1/E-12), 409 `duplicate-camera` | `cam:camera:write` |
| CAM-4 | `GET /cameras/{id}` | — | 200 상세(자격증명 비포함, INV-5) | 404 | `cam:camera:read` |
| CAM-5 | `PATCH /cameras/{id}` | name, purpose, location, coverage_area, operating_hours, manager, retention_days, ptz_limits{pan_min,pan_max,tilt_min,tilt_max,zoom_max}, home_preset_token, enabled | 200 | 422, 404 | `cam:camera:write` |
| CAM-6 | `DELETE /cameras/{id}` | — | 204 (소프트삭제, 클립은 보존기간까지 유지) | 404 | `cam:camera:write` |
| CAM-7 | `GET /cameras/{id}/capabilities` | — | 200 `{ptz{continuous,presets,max_presets,position_status}, profiles[], events, max_rtsp_sessions}` | 404 | `cam:camera:read` |
| CAM-8 | `GET /cameras/{id}/signage` | — | 200 `{text}` 안내판 문구(FR-011 5항목) | 404 | `cam:camera:read` |
| STR-1 | `POST /cameras/{id}/whep?profile=sub\|main` | `application/sdp` offer, `X-Requested-With` | 201 `application/sdp` answer + `Location: /whep-sessions/{sid}`; 감사 `stream.open` | 409 `viewer-limit`(INV-8/E-3), 503 `camera-offline`, 406, 415 `profile-not-playable`(메인 H.265 → sub 안내) | `cam:stream:read` |
| STR-2 | `DELETE /whep-sessions/{sid}` | — | 204; 감사 `stream.close`(duration) | 404 | `cam:stream:read` |
| STR-3 | `GET /cameras/{id}/mse` (WebSocket) | 쿠키 + `Origin` 허용 목록 | 101 → fMP4 조각(go2rtc /api/ws 프록시); 감사 `stream.open/close` | 401, 403 `origin-not-allowed`, 409, 503 | `cam:stream:read` |
| PTZ-1 | `POST /cameras/{id}/ptz/moves` | pan, tilt, zoom ∈ [-1,1], timeout_ms(≤1000, 기본 1000) | 202 `{move_id, position?}` (클라이언트 750ms 주기 갱신; 서버는 GetStatus로 범위 검사) | 403, 404, 409 `ptz-unsupported`, 409 `ptz-fault`, 409 `ptz-out-of-range`(E-15, Stop 전송 후), 429, 502 `camera-error` | `cam:ptz:write` |
| PTZ-2 | `POST /cameras/{id}/ptz/stop` | — | 202 | 403, 404, 502 | `cam:ptz:write` |
| PTZ-3 | `GET /cameras/{id}/presets` | — | 200 `data[]{token,name}` | 404 | `cam:camera:read` |
| PTZ-4 | `POST /cameras/{id}/presets` (Idempotency-Key) | name | 201 + Location | 409 `preset-limit`, 409 `ptz-out-of-range`(현재 위치가 범위 밖), 502 | `cam:preset:write` |
| PTZ-5 | `DELETE /cameras/{id}/presets/{token}` | — | 204 | 404 | `cam:preset:write` |
| PTZ-6 | `POST /cameras/{id}/presets/{token}/recalls` | — | 202 | 404 `preset-not-found`(E-7), 409 `ptz-fault`, 502 | `cam:ptz:write` |
| EVT-1 | `GET /events` | camera_id, from, to, cursor, limit | 200 `data[]{id,camera_id,started_at,end_at,status,clip_id}` | 422 | `cam:event:read` |
| EVT-2 | `GET /events/{id}` | — | 200 상세 | 404 | `cam:event:read` |
| CLIP-1 | `GET /clips/{id}` | — | 200 `{id,event_id,duration_s,size_bytes,sha256,expires_at,status}` | 404, 410 `clip-purged` | `cam:clip:read` |
| CLIP-2 | `GET /clips/{id}/content?intent=view` | `Range` | 200/206 `video/mp4`; 감사 `clip.view`(첫 Range) | 404, 410 | `cam:clip:read` |
| CLIP-3 | `GET /clips/{id}/content?intent=download` | — | 200 `Content-Disposition: attachment`; 감사 `clip.download` | 403(Viewer, E-16), 404, 410 | `cam:clip:download` |
| CLIP-4 | `DELETE /clips/{id}` | — | 204 (즉시 파기 + 감사 `clip.delete`) | 404, 410 | `cam:clip:delete` |
| SET-1 | `GET /settings` | — | 200 `{default_retention_days, max_sessions_global:16, max_streams_per_user:4, webhook_url_set, ntp_synced, backup{last_ok_at,target}}` | — | `cam:camera:read` |
| SET-2 | `PATCH /settings` | default_retention_days(1~90), webhook_url(https, 1개, null로 해제) | 200 | 422 | `cam:settings:write` |
| SET-3 | `POST /settings/webhook-tests` | — | 202 (테스트 알람 1건 발송) | 409 `webhook-not-set`, 502 `webhook-failed` | `cam:settings:write` |
| AUD-1 | `GET /audit-logs` | actor_id, action, from, to, cursor, limit | 200 `data[]{id,at,actor_id,action,target,result,detail,row_hash}` | 403 | `cam:audit:read` |
| AUD-2 | `GET /audit-logs/verification` | from, to | 200 `{ok, checked_rows, first_broken_id}` 해시 체인 검증 | 403 | `cam:audit:read` |
| USR-1 | `GET /users` | — | 200 | — | `cam:user:write` |
| USR-2 | `POST /users` (Idempotency-Key) | username, password, role(owner\|viewer) | 201 | 409 `duplicate-username`, 422 | `cam:user:write` |
| USR-3 | `DELETE /users/{id}` | — | 204 | 404, 409 `last-owner` | `cam:user:write` |
| USR-4 | `PUT /users/{id}/password` | current_password(본인), new_password | 204 | 401, 422 | 본인 또는 `cam:user:write` |
| HLT-1 | `GET /health` | — | 200 `{status: ok\|degraded}` (최소 정보) | 503 | 없음 |
| HLT-2 | `GET /health/details` | — | 200 `{cameras[]{id,status,ptz_fault,last_frame_at,rtsp_sessions}, go2rtc, sessions{active,global_max}, disk{used_pct}, ring_buffer{bytes,max}, ntp_synced, backup{last_ok_at}, boot_reconcile{orphans_fixed,hash_mismatches}}` | — | `cam:audit:read` |
| HLT-3 | `GET /health/rollup` | hours(≤24) | 200 15분 롤업 시계열(가용성·지연·PTZ 성공률·클립 성공률·디스크) | — | `cam:audit:read` |

감사로그 기록 액션(FR-010): `auth.setup`, `auth.login.ok/fail`, `auth.logout`, `camera.create/update/delete`, `stream.open`, `stream.close`, `ptz.move`, `ptz.stop`, `ptz.fault`, `ptz.denied-range`, `preset.create/delete/recall`, `clip.create`(system), `clip.view`, `clip.download`, `clip.delete`, `clip.auto-purge`(system), `clip.hash-mismatch`(system), `boot.reconcile`(system), `settings.update`, `user.create/delete`, `authz.denied`(E-6·E-16), `audit.archive`(system), `ops.restore-drill`, `backup.ok/fail`(system).

## OpenAPI 스케치 (핵심 3개)
```yaml
openapi: 3.1.0
info: {title: PiCam Watch API, version: 1.2.0}
paths:
  /cameras:
    post:
      parameters: [{name: Idempotency-Key, in: header, required: true, schema: {type: string, pattern: '^[0-9A-HJKMNP-TV-Z]{26}$'}}]
      requestBody:
        content:
          application/json:
            schema:
              type: object
              required: [name, username, password, purpose, location, coverage_area, operating_hours, manager]
              properties:
                name: {type: string, maxLength: 60}
                onvif_xaddr: {type: string, format: uri}
                rtsp_url: {type: string, format: uri}
                username: {type: string}
                password: {type: string, writeOnly: true}
                purpose: {type: string, maxLength: 200}
                location: {type: string, maxLength: 200}
                coverage_area: {type: string, maxLength: 200}      # 촬영 범위 (시행령 §24)
                operating_hours: {type: string, maxLength: 60}     # 촬영 시간, 예 "24시간" / "09:00-18:00"
                manager: {type: object, required: [name, phone], properties: {name: {type: string}, phone: {type: string}}}
                retention_days: {type: integer, minimum: 1, maximum: 90, default: 30}
      responses:
        '201':
          description: created (검증 결과 포함)
          headers: {Location: {schema: {type: string}}}
          content:
            application/json:
              schema:
                type: object
                properties:
                  id: {type: string}
                  profiles: {type: array, items: {type: object, properties: {name: {type: string, enum: [main, sub]}, codec: {type: string}, bitrate_kbps: {type: integer}, gop_s: {type: number}}}}
                  record_profile: {type: string, enum: [main, sub]}
                  max_rtsp_sessions: {type: integer}
                  warnings: {type: array, items: {type: string}}   # gop-over-4s, main-h265-not-playable, bitrate-over-4mbps
        '415': {$ref: '#/components/responses/Problem'}   # codec-unsupported
        '502': {$ref: '#/components/responses/Problem'}   # camera-unreachable
  /cameras/{id}/whep:
    post:
      summary: WHEP (draft-ietf-wish-whep) 시청 세션 생성 — 세션 쿠키 인증
      parameters:
        - {name: profile, in: query, schema: {type: string, enum: [sub, main], default: sub}}
        - {name: X-Requested-With, in: header, required: true, schema: {type: string, enum: [PiCam]}}
      requestBody: {content: {application/sdp: {schema: {type: string}}}}
      responses:
        '201': {description: SDP answer, headers: {Location: {schema: {type: string}}}, content: {application/sdp: {schema: {type: string}}}}
        '409': {$ref: '#/components/responses/Problem'}   # viewer-limit (per-user 4 / global 16)
        '503': {$ref: '#/components/responses/Problem'}   # camera-offline
  /cameras/{id}/ptz/moves:
    post:
      requestBody:
        content:
          application/json:
            schema:
              type: object
              properties:
                pan: {type: number, minimum: -1, maximum: 1, default: 0}
                tilt: {type: number, minimum: -1, maximum: 1, default: 0}
                zoom: {type: number, minimum: -1, maximum: 1, default: 0}
                timeout_ms: {type: integer, minimum: 100, maximum: 1000, default: 1000}
      responses:
        '202': {description: accepted, content: {application/json: {schema: {type: object, properties: {move_id: {type: string}, position: {type: object, properties: {pan: {type: number}, tilt: {type: number}, zoom: {type: number}}}}}}}}
        '403': {$ref: '#/components/responses/Problem'}   # authz.denied → 감사로그
        '409': {$ref: '#/components/responses/Problem'}   # ptz-unsupported | ptz-fault | ptz-out-of-range
components:
  responses:
    Problem:
      description: RFC 9457
      content:
        application/problem+json:
          schema:
            type: object
            required: [type, title, status]
            properties:
              type: {type: string, format: uri}
              title: {type: string}
              status: {type: integer}
              detail: {type: string}
              instance: {type: string}
              errors: {type: array, items: {type: object, properties: {field: {type: string}, code: {type: string}}}}
```

## ERD
```mermaid
erDiagram
  USER ||--o{ SESSION : has
  USER ||--o{ AUDIT_LOG : acts
  CAMERA ||--|| CAMERA_CREDENTIAL : secures
  CAMERA ||--o{ PRESET : caches
  CAMERA ||--o{ EVENT : emits
  EVENT ||--o| CLIP : produces
  CAMERA ||--o{ CLIP : stores
  USER {
    text id PK "ULID"
    text username UK
    text password_hash "argon2id"
    text role "owner|viewer"
    text created_at
    text deleted_at "소프트삭제"
  }
  SESSION {
    text id PK
    text user_id FK
    text origin "magicdns|lan"
    text expires_at
    text last_seen_at
  }
  CAMERA {
    text id PK
    text name
    text onvif_xaddr
    text rtsp_main_url "비밀번호 제외"
    text rtsp_sub_url
    text profiles_json "codec/bitrate/gop per profile"
    text record_profile "main|sub"
    int max_rtsp_sessions
    int ptz_supported
    int ptz_fault
    text ptz_limits_json "pan/tilt/zoom 한계, null=전 범위"
    text home_preset_token
    text capabilities_json
    text purpose "법 25조④"
    text location
    text coverage_area "촬영 범위"
    text operating_hours "촬영 시간"
    text manager_name
    text manager_phone
    int retention_days "1~90"
    int enabled
    text status "online|offline|disabled"
    text created_at
    text deleted_at
  }
  CAMERA_CREDENTIAL {
    text camera_id PK FK
    blob username_enc "AES-256-GCM"
    blob password_enc
    blob nonce
  }
  PRESET {
    text camera_id FK
    text token "ONVIF PresetToken"
    text name
    text created_at
  }
  EVENT {
    text id PK
    text camera_id FK
    text started_at
    text end_at "마지막 모션+20s, 상한 +120s"
    text source "onvif_motion|manual"
    text status "recording|stored|failed"
    int time_unsynced "E-11"
    real mono_offset_s "미동기 시 단조시계 경과"
  }
  CLIP {
    text id PK
    text event_id FK
    text camera_id FK
    text path "clips/{camera_id}/{id}.mp4"
    int duration_s
    int size_bytes
    text sha256
    text created_at
    text expires_at
    text status "stored|expired|purged"
    text purged_at
  }
  AUDIT_LOG {
    text id PK
    text at
    text actor_id "nullable(system)"
    text action
    text target
    text result "ok|denied|fault"
    text detail_json
    text prev_hash
    text row_hash "sha256(prev_hash || row)"
  }
  IDEMPOTENCY_KEY {
    text key PK
    text user_id
    text request_hash
    int status
    text response_body
    text created_at
  }
  SETTINGS {
    text key PK
    text value "default_retention_days, webhook_url(암호화), recovery_key_hash"
  }
  METRICS_ROLLUP {
    text bucket_at PK "15분"
    text metric PK
    real value
  }
```

인덱스·제약(postgres-patterns 번안, SQLite):
- `event(camera_id, started_at DESC)` 복합(등치 → 범위 순서) · `event(status) WHERE status='recording'` 부분 인덱스(recorder 폴링·boot reconcile) · `clip(status, expires_at)` 부분 인덱스 `WHERE status='stored'`(파기 잡 전용) · `audit_log(at DESC)`, `audit_log(actor_id, at DESC)` · `session(expires_at)` · `user(username) WHERE deleted_at IS NULL` 유니크 부분 인덱스 · `metrics_rollup(bucket_at)`
- `CREATE TRIGGER audit_log_immutable BEFORE UPDATE ON audit_log BEGIN SELECT RAISE(ABORT,'append-only'); END;` + DELETE 동일 (INV-3).
- **감사로그 1년 아카이브(INV-3의 유일한 예외 절차)**: SQLite는 트리거 비활성화가 없으므로, 연 1회 Owner 승인(설정 화면 확인) 하에 전용 잡이 ① 대상 행을 암호화 아카이브 파일(`/data/backups/audit-YYYY.age`)로 내보내고 해시 체인 검증 ② `BEGIN IMMEDIATE` 후 트리거 2개 DROP → `DELETE WHERE at < now-1y` → 트리거 재생성 ③ `audit.archive` 행(삭제 건수·마지막 row_hash·아카이브 파일 해시)을 앵커로 추가해 체인 연속성 유지. 이 절차 외 트리거 조작은 없다(코드 리뷰 게이트).
- 커넥션 PRAGMA: 일반 `journal_mode=WAL; synchronous=NORMAL; foreign_keys=ON; busy_timeout=5000`; **감사 커넥션 `synchronous=FULL`**.

## 데이터 규칙
- 시각: UTC ISO 8601 `Z` 고정, SQLite `text`. NTP 미동기 중 생성된 이벤트는 `time_unsynced=1` + `mono_offset_s`(부팅 후 단조시계 경과). **동기 시 보정**: `created_at := synced_now − (mono_now − mono_offset_s)`, `expires_at` 재계산, `time_unsynced=0`. 미동기가 단조시계 기준 90일 지속되면 파기(E-11).
- 식별자: ULID(시간순 정렬, 커서로 재사용). 프리셋은 카메라의 `PresetToken`을 그대로.
- 자격증명: `camera_credential`에만, AES-256-GCM(master.key), 응답·로그·go2rtc API·설정 파일·백업 평문 비노출(INV-5). go2rtc 스트림은 API가 기동 시 런타임 API(`PUT /api/streams`)로 메모리 등록.
- 보존: `clip.expires_at = created_at + camera.retention_days`; 파기 = `unlink` + `fsync(dir)` + `status=purged` + 감사로그, 일 1회 `fstrim /data`(SSD TRIM으로 일반 복구 도구 복구 불가 — **Assumed**: 물리 포렌식 잔여는 Accept). 이벤트 메타는 클립 파기 후 90일 더 유지. 감사로그 1년(아카이브 절차 위). 세션 만료 후 7일 정리. 롤업 24h.
- 소프트삭제: user·camera만(`deleted_at`). 클립은 물리 파기(법정 요구).
- 크기: 클립 단일 최대 130s / 200MB(상한 도달 시 종료), 링버퍼 tmpfs 128MB, 이벤트 큐 상한 1,000.
- 오디오 관련 컬럼·키 없음(INV-6, SC-013).

## 커버리지 매핑 (P0·P1 요구사항 → 담당 엔드포인트/이벤트)
| FR | 우선순위 | 담당 |
|---|---|---|
| FR-001 | P0 | CAM-1 |
| FR-002 | P0 | CAM-3 (검증: 연결·프로파일 코덱·GOP·비트레이트·PTZ·세션 상한, 415 거부), CAM-7 |
| FR-003 | P0 | CAM-3/CAM-4 응답 스키마(비밀번호 writeOnly), CAMERA_CREDENTIAL 암호화, go2rtc 런타임 등록 + `local_auth`(04), 암호화 백업(07) |
| FR-004 | P0 | STR-1(profile=sub) → STR-3 폴백 |
| FR-005 | P0 | STR-1(profile=main, 415 `profile-not-playable` 시 sub) |
| FR-006 | P0 | StreamHealth 폴러(내부, 04) → CAM-2 `status`, HLT-2 `cameras[].last_frame_at` |
| FR-007 | P0 | PTZ-1(750ms 갱신), PTZ-2 |
| FR-008 | P0 | PTZ-3, PTZ-4, PTZ-5, PTZ-6 |
| FR-009 | P0 | 전 변경·다운로드 엔드포인트 스코프 열 + 403 `authz.denied` 감사 |
| FR-010 | P0 | AUD-1, AUD-2 + 감사 액션 목록 + AUDIT_LOG 트리거·해시 체인 |
| FR-011 | P0 | CAM-3 required[purpose, location, coverage_area, operating_hours, manager], CAM-5 |
| FR-012 | P0 | RetentionJob(내부, 매시) + CLIP.expires_at + CLIP-4 |
| FR-013 | P0 | EventIngest(내부) → EVENT.end_at 연장, EVT-1/EVT-2 조회 |
| FR-014 | P0 | ClipRecorder(내부, record_profile) → CLIP.sha256, CLIP-1 |
| FR-015 | P1 | EVT-1, EVT-2, CLIP-1, CLIP-2(view, Viewer 가능), CLIP-3(download, Owner) |
| FR-016 | P1 | RetentionJob 90% 정리 + HLT-2 `disk.used_pct` + `clip.auto-purge` 감사 |
| FR-017 | P0 | AUTH-2, AUTH-3, AUTH-4, SESSION |
| FR-018 | P1 | AUTH-1(setup_token, recovery_key, 423 잠금) |
| FR-019 | P0 | (부재로 충족) 오디오 필드·엔드포인트 없음 — SC-013 계약 테스트가 OpenAPI 전수 검사 |
| FR-020 | P2 | (배포, 07) — 엔드포인트 동일, SESSION.origin |
| FR-021 | P2 | CAM-8 |
| FR-022 | P1 | HLT-1, HLT-2, HLT-3 |
| FR-023 | P1 | 레이트리밋 규약(429) |
| FR-024 | P0 | PTZ-2 워치독(내부) + 409 `ptz-fault` + `ptz.fault` 감사 + CAM-2 `ptz_fault` |
| FR-025 | P1 | STR-1/STR-3 클라이언트 측 프레임 타이머(SPA) + HLT-2 `last_frame_at` |
| FR-026 | P1 | BootReconcile(내부) + HLT-2 `boot_reconcile` + `boot.reconcile`/`clip.hash-mismatch` 감사 |
| FR-027 | P1 | CAM-5 `ptz_limits`·`home_preset_token`, PTZ-1 409 `ptz-out-of-range` + `ptz.denied-range` 감사, PTZ-4 범위 검사, CAM-7 `position_status` |
| FR-028 | P2 | SET-2 `webhook_url`, SET-3, 내부 Notifier(07) |
| FR-029 | P1 | USR-1, USR-2, USR-3(`last-owner`), USR-4 |

P0·P1 매핑 0건: **없음** — P0 17건, P1 9건, P2 3건 전부 매핑. (계수: P0 = {001,002,003,004,005,006,007,008,009,010,011,012,013,014,017,019,024} = 17건, P1 = {015,016,018,022,023,025,026,027,029} = 9건, P2 = {020,021,028} = 3건, 합 29.)
