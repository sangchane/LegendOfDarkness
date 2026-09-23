# A4 독립 검토 결과 — 04-architecture.md (PiCam Watch) · fresh-reviewer(fable) · 2026-09-08 15:0x

(생성 에이전트가 한도로 끊겨 받지 못한 검토관 회신을 메인이 그대로 저장한 것. 재실행하지 말고 이 파일을 읽어 타당성 필터링 후 반영한다.)

대상: `04-architecture.md` · 기준: 같은 폴더의 `03-prd.md`, `01-recon.md`(fit 표·정량 근거), `02-blindspot-register.md`(질문 배치)

## 발견 목록 (심각도순)

**HIGH**
1. 04:데이터 저장 L192 — Event를 "월 파티션 DROP"으로 파기하면 최대 약 1개월 초과 보존 → INV-1(CLIP_RETENTION_DAYS + PURGE_MAX_DELAY_HOURS 내 소멸)·FR-014 위반. 법 준수 목표 G3 직결. 제안: Event는 일 파티션 또는 행 DELETE, 월 파티션은 AuditLog만.
2. 04:ADR-5·관측성 — TURN 릴레이 대역을 계산하지 않았고 03에 VPS 회선 상수가 없어 대조 자체가 불가. 계산: 동시 세션 = SITES_MAX×VIEWER_MAX = 50. sub: 50×SUB_STREAM_BITRATE_KBPS ≈ 25.6Mbps/방향. main: 50×MAIN_STREAM_BITRATE_MBPS = 250Mbps/방향(릴레이는 수신+송신이라 NIC 500Mbps) — 저가 VPS 100Mbps~1Gbps 포트를 넘거나 포화. Q1(스마트폰·LTE=CGNAT)이라 릴레이 비율은 높다. 제안: 03에 VPS_LINE_MBPS·TURN_RELAY_MAX_MBPS 추가, 릴레이 경로는 sub 강제 또는 전역 릴레이 세션 상한, ADR-5 알람 임계를 그 상수로.
3. 04 전체 — FR-021(P0: 카메라 RTSP/ONVIF 끊김 시 CAM_RECONNECT_BACKOFF 재접속·끊김/복구 이벤트)이 컴포넌트 책임·시퀀스·저장 어디에도 없음. go2rtc 내부 재접속과 agent의 Camera 상태 전이(connected⇄disconnected)·이벤트 생성 책임이 미분리. 제안: agent 책임 줄에 추가하고 PullPoint 구독 갱신 실패도 같은 경로로.
4. 04:③ B2 / FR-001 — 기기 X.509 발급 주체("내부 CA")가 컴포넌트 구조도에 없고, claimed→approved 승인 흐름·인증서 만료/갱신 정책이 없음(만료 = 영구 offline → G2 위반). 제안: api 내 CA 모듈(또는 step-ca) 명시 + 갱신 주기 상수.

**MEDIUM**
5. 04:ADR-3·시나리오 3 L188 — (a) 같은 event_id 동시 요청 → clip.upload 2회, 부분 파일이 Range로 서빙되는 경쟁. (b) 캐시 삭제가 STREAM_TOKEN_TTL_SEC에 묶여 MANUAL_REC_MAX_MIN 클립(길이 = TTL)은 재생 중 삭제 가능. (c) GET이 업로드 완료를 동기로 기다림(수동 녹화 클립 ≈ MANUAL_REC_MAX_MIN×MAIN_STREAM_BITRATE_MBPS ÷ UPLINK_MIN_MBPS ≈ 수십 초 → HTTP 타임아웃). 제안: singleflight + 임시파일→atomic rename, 활성 토큰 refcount, 202 + 상태 폴링/WS.
6. 04:ADR-2·시나리오 1 — 명령 응답 타임아웃 상수 없음(03에도 없음), QoS1 중복 전달 시 webrtc.offer 멱등(session_id) 미명시, 세션 종료(L134) 시 agent/go2rtc PeerConnection 정리 명령이 없어 ICE 타임아웃에 의존(Pi 자원 누수), trickle 여부 미명시(LIVE_FIRST_FRAME_P95 예산에 영향). 제안: 03에 CMD_TIMEOUT_SEC, `webrtc.close` 명령 추가.
7. 04:구현 접근 L54 — go2rtc `ice_servers`는 설정 파일 항목이라 세션별 단기 자격증명 주입 API가 없음(가능성). 또 coturn `use-auth-secret`은 만료 후 Refresh가 실패해 STREAM_TOKEN_TTL_SEC를 넘는 라이브 세션이 끊김. 제안: Pi 측은 기기별 장기(회전) 자격증명·브라우저만 단기, 또는 세션 중 재발급 + restartIce. R-1 실증 항목에 명시.
8. 04:데이터 저장 L195 — 링버퍼 tmpfs 상한 미정의(size= 마운트 옵션·상수 없음; 기본 tmpfs = RAM 50%, 세그먼트 정리 실패 시 OOM). 산정 ≈ CAMERAS_PER_SITE_MAX×MAIN_STREAM_BITRATE_MBPS×(CLIP_PRE_SEC+여유)로 작지만 상한을 명시해야 함. 세그먼트 "2초"도 직접 기입.
9. 04:③ 대응 표 누락 셀 — B6-R(Admin 설정 변경·삭제 부인: B1 부인 행은 시청 행위만), B6-E(서버 컨테이너 탈출: B5 행은 Pi 전용), B5-D "배포 폭주"(롤백 루프만 대응). 제안: 행 3개 추가.
10. 04 정합성 — 숫자 직접 기입: ADR-2 "64KB"(③ B2 동일), ADR-3 "12MB@30Mbps≈3초"(UPLINK_MIN_MBPS·MAIN_STREAM_BITRATE_MBPS×(CLIP_PRE_SEC+CLIP_POST_SEC)로 표기해야), ADR-4 "사이트 ≤10"(SITES_MAX), R-2 "30·365" 값 재기재, ③ B5 "재시도 최대 1회". 제안: 03에 MQTT_MSG_MAX_KB·RINGBUF_SEGMENT_SEC·OTA_RETRY_MAX 추가 후 이름만 참조.
11. 04 구조도 vs 시퀀스 — 구조도는 MQ(mqtt-bridge)---BRK이고 API는 BRK와 미연결인데 시퀀스 3개는 모두 api↔mosquitto 직접; ws-hub·mqtt-bridge가 시퀀스에 한 번도 등장하지 않음. `Camera(ONVIF)`·`웹 푸시`는 구조도에 없는 참가자(컨텍스트도 이름 "IP Camera (ONVIF/RTSP H.264)"·"웹 푸시 서비스"와도 불일치). 제안: 외부 참가자를 구조도에 점선 노드로, 시퀀스에 mqtt-bridge 표기.

**LOW**
12. 04:데이터 저장 — 썸네일 디스크 산정 없음: EVENTS_PER_CAM_DAY_MAX×CAMERAS_PER_SITE_MAX×SITES_MAX = 20,000/일 × CLIP_RETENTION_DAYS ≈ 60만 파일, 썸네일 크기 상수 없음.
13. 04:관측성 L286 — "4 골든 시그널"에 5개 열거.
14. 04:L180 `clock_synced` vs 03 엣지 12 `clock_unsynced` — 플래그 이름 불일치.
15. 04:③ B2 — INV-3 토픽 `sites/{site}/devices/{cn}/#`는 mosquitto 정적 ACL 패턴(%u)으로 site를 못 넣음 → 승인 시 ACL 생성·reload 또는 dynsec 플러그인 필요. 미명시.
16. YAGNI — 컨테이너 레지스트리를 VPS 내부 컴포넌트로 둠(≤SITES_MAX 규모면 GHCR 등 외부 + cosign이 운영 부담 적음). Prometheus는 1~2인 팀에 경계선이나 07 위임으로 수용.

## 확인한 항목 (문제없음 + 근거)
- ADR-1~7 모두 대안·기각 이유·단점(결과) 보유 — 04:검토한 대안 표 7행 전부 "결과" 열에 손실이 적혀 있음.
- 04가 쓰는 상수 이름 18개(LIVE_LATENCY_P95…OFFLINE_QUEUE_MAX·TURN_RELAY_COST) 전부 03 상수 표에 존재. 03에 없는 이름 0건.
- FR/INV/SC/US/엣지 참조(FR-003·004·006·007·010·011·012·017·022·027, INV-1~7, SC-001, US-1~3, 엣지 16)가 03과 ID·의미 일치. 상태명(offline·clip_ready·done·rolled_back)도 03 상태·전이와 일치.
- STRIDE ② 6경계×6범주 36셀 전부 기입, "해당없음" 7건 모두 사유 있음(B2-R·B3-R·B4-T/R/E·B6-S).
- 02 질문 배치 Q1~Q5 채택안이 04에 반영됨: sub 기본(L119)·VPS 경유·USB SSD 클립·ONVIF H.264·앱 계층 A/B + R-3 Accept.
- 01 fit 표 선택 10항목(go2rtc·Pi 5·Python/onvif-zeep-async·MQTT·WebRTC+coturn·FastAPI·PostgreSQL 16·SQLite+SSD·Overlay FS·앱 A/B) ↔ 04 스택 일치.
- P0 FR 커버 확인: FR-002(agent 책임)·003/004/006(시나리오 1)·007/010(시나리오 2)·011/012/017(시나리오 3)·013(L188)·014(purge-job)·020(mqtt-bridge LWT)·022(L58·outbox)·025/026(③ B1).
- 단일 VPS의 TURN 외 병목 없음: PostgreSQL 이벤트 ≤ 60만/월, mosquitto 연결 ≤ SITES_MAX, clipcache 동시 ≤ 50×약 12.5MB.
- 시나리오 1 내부 참가자 6개(UI·api·mosquitto·agent·go2rtc·coturn)는 구조도 이름 그대로.

## 종합 판정: CONCERNS
사유: 핵심 구조(패스스루·MQTT 제어면·TURN 폴백·온디맨드 클립)는 01·02·03과 정합하고 대안 검토도 갖췄으나, HIGH 4건이 각각 불변식 위반(#1), 규모 대조 불가(#2), P0 미커버(#3), 운영 신뢰성 공백(#4)이라 그대로 05로 넘기면 안 된다. 재작성이 아닌 보강으로 해결 가능 — 수정 필수 섹션: 데이터 저장(파티션·tmpfs 상한), ADR-5+관측성(회선 상수), 컴포넌트 책임(FR-021·CA), ③ 대응 표(3행), 03 상수 표(신규 상수 5개 추가 후 04 숫자 제거).
