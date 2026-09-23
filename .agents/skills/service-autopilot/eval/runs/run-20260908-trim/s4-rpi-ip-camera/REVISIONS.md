# REVISIONS — PiCam Watch

## R1 — A4 독립 검토(fresh-reviewer, fable, 2026-09-08) 반영 · 적용 2026-09-09 01:52
- 사용자 원문: 없음 (검토관 지적 16건, 타당성 필터링 결과 전부 채택 — 거짓 양성 0)
- 결정:
  1. Event 파기는 행 DELETE + 파일 삭제(일 1회)로 INV-1 준수. 월 파티션 DROP은 AuditLog·전 행 파기 후 정리용만 (#1)
  2. TURN 릴레이 경로는 서브스트림 고정, 전역 릴레이 상한 TURN_RELAY_MAX_MBPS, VPS 회선 VPS_LINE_MBPS 상수화 (#2)
  3. FR-021 카메라 연결 감시·전이 책임을 agent에 명시 (#3)
  4. api 내부 ca 모듈 + 기기 인증서 갱신 FR-030 (DEVICE_CERT_VALID_DAYS · DEVICE_CERT_RENEW_BEFORE_DAYS) (#4)
  5. 클립 온디맨드: 이벤트당 singleflight, 임시파일→원자적 rename, 캐시 CLIP_CACHE_TTL_MIN(활성 토큰 시 연장), 202 폴링 (#5)
  6. CMD_TIMEOUT_SEC · webrtc.offer는 session_id 멱등 · 세션 종료 시 webrtc.close · trickle ICE (#6)
  7. Pi 측 TURN 자격증명은 기기 단위 회전(DEVICE_TURN_CRED_TTL_HOURS), 브라우저는 세션별 TURN_CRED_TTL_MIN, 라이브 세션 상한 LIVE_SESSION_MAX_MIN (#7)
  8. 링버퍼 tmpfs 상한 RINGBUF_TMPFS_MB · 세그먼트 RINGBUF_SEGMENT_SEC (#8)
  9. STRIDE ③ 행 추가: B6-R, B6-E, B5-D 배포 폭주 (#9)
  10. 04의 숫자 직접 기입 제거 → MQTT_MSG_MAX_KB · OTA_RETRY_MAX · SITES_MAX 참조 (#10)
  11. 시퀀스 참가자를 구조도 이름과 일치(api(mqtt-bridge) · Camera · 웹 푸시 서비스), 외부 노드를 구조도에 추가 (#11)
  12. THUMB_MAX_KB 상수화 + 썸네일 디스크 산정식 (#12) · 골든 시그널 문구 (#13) · `clock_synced=false`로 플래그 통일 (#14)
  13. MQTT 토픽 프리픽스를 `devices/{device_id}/`로 바꿔 mosquitto 정적 ACL `%u` 패턴 적용 (#15)
  14. 컨테이너 레지스트리는 외부(GHCR + cosign)로 — VPS 컴포넌트에서 제거 (#16)
- 영향: 03 v1.0→v1.1 (상수 14개·FR-030·SC-015·엣지 17·18·FR-003/006·INV-3), 04 v1.0→v1.1, 05 v1.0→v1.1. 전부 같은 턴에 반영 완료 — 적용 대기 문서 없음.

## R2 — A7 운영 상수 추가 · 적용 2026-09-09
- 사용자 원문: 없음 (규칙 9 — 07의 SLO·백업 주기·RTO/RPO·로그 상한 숫자를 03 상수 표에만 두기 위해)
- 결정: 03 상수 표에 운영 상수 17개 추가(SLO_WINDOW_DAYS · SLO_LIVE_SUCCESS_PCT · SLO_DEVICE_ONLINE_PCT · SLO_PUSH_SUCCESS_PCT · ALARM_WARN_PCT · HEALTHCHECK_INTERVAL_SEC · HEALTHCHECK_FAIL_MAX · PI_LOG_RAM_MB · JOURNAL_MAX_MB · LOG_RETENTION_DAYS · BACKUP_INTERVAL_HOURS · BACKUP_RETENTION_DAYS · RESTORE_DRILL_INTERVAL_DAYS · RTO_SERVER_MIN · RPO_SERVER_HOURS · RTO_DEVICE_MIN · ALERT_OPS_DEDUP_MIN). 근거: log2ram README(2건)·blindspot P2(1건)·설계 결정 DL-018(14건).
- 영향: 03 v1.1→v1.2. 04·05·06은 내용 영향 없음 — 머리의 기준 버전만 v1.2로 갱신. 07 v1.0(기준 03 v1.2) 신규. 같은 턴에 반영 완료 — 적용 대기 문서 없음.

## R3 — GATE 검토(독립 검토관, fable, 2026-09-09 06:25) 즉시 반영분 · 적용 2026-09-09 06:25
- 사용자 원문: 없음 (검토관 30건 중 기계적으로 닫히는 2건만 이 턴에 반영, 나머지는 08 핸드오프 선행 조건)
- 결정:
  1. 웹 프론트엔드(React+Tailwind+Zustand PWA)·Caddy를 01 fit 표에 근거와 기각 대안을 붙여 추가 (CRITICAL #1 — 근거 없는 추천 해소)
  2. 04 시퀀스 1의 명령 토픽을 `devices/{device_id}/cmd/…`로 정정 — R1 #15 전파 마감 (HIGH #2) → 04 v1.2
- 영향: 01(표 1행), 04(v1.2). 03·05·06·07 영향 없음(기준 03 v1.2 유지).

## R4 — GATE 검토 패치 라운드(재실행 없음) · 적용 2026-09-09
- 사용자 원문: 없음 (GATE 검토관 30건 중 R3 미반영 28건을 타당성 필터링 — 27건 반영, #28은 가드 상수의 의도된 여유라 정보로 유지. 정책 결정 6건은 단순한 쪽으로 자율 결정 → decision-log DL-020~028)
- 결정:
  1. Docker data-root·log2ram 동기화 대상을 USB SSD로 — firstrun이 설정 후 Overlay FS (#3, DL-020)
  2. 릴레이 판정: 모든 세션 sub 시작, 클라 `ice_state`·`first_frame` WS 보고, main = 직결 세션을 참조하는 신규 세션(`direct_session_id`), relay-cap·main 릴레이는 `ice_state` 시점 종료, `stream_effective` 제거, go2rtc 8555 LAN 바인딩 (#4·#10·#19, DL-021)
  3. 상수 추가 PI_BOOT_MAX_SEC · AGENT_RECOVER_MAX_SEC · FRAME_DROP_MAX_PCT · ALARM_EVAL_WINDOW_MIN · OFFLINE_ESCALATE_MIN · VPS_DISK_MIN_GB, EVENTS_PER_CAM_DAY_MAX 500→120, 03·04·06·07의 직접 기입 숫자 제거 (#5·#6·#7·#20, DL-022·023)
  4. Pi 재설치 = SSD 인증서 재사용 (#14, DL-024) · 백업: pg_dump event 제외 + thumbs rsync --delete 미러 (#15, DL-025) · 이벤트 capability 없는 카메라 = 등록 + 경고 + 엣지 19 (#18, DL-026)
  5. YAGNI: Event 파티션·CAMERA.password_enc·E44 제거, INV-7 유지 (#22, DL-027)
  6. 계약 공백: E51 POST /releases(CI 토큰), `.env` 키 8개, FR-018 요약 규칙(07), 용어 "운영 담당", 권한 문구 FR-008·028, E11 mTLS 예외 (#11·#12·#17·#21·#26·#27, DL-028)
  7. 전파·정정: 캐시 TTL(#8), D01 경로·slug(#9), D03 목록(#13), `events` status 필드·purge.report 응답(#16), 02 집계(#23), 06 제목(#24), 목표 표 SC-005·015(#25), 큐 길이 알람 + caddy 노드(#29), DL-014 26건(#30) (DL-029)
- 영향: 02(집계 2줄), 03 v1.2→v1.3, 04 v1.2→v1.3, 05 v1.1→v1.2, 06 v1.0→v1.1, 07 v1.0→v1.1. 전부 같은 턴에 반영 완료 — 적용 대기 문서 없음. 판정은 CONCERNS 유지(재검토 없음).
