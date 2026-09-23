# Decision Log — PiCam Hub
버전: v1.0 · 런: run-20260907-smoke / s4-rpi-ip-camera · 시작 2026-09-07 10:20 KST · 모드: 오토파일럿(질문 금지 → 추천안 자동 채택)

형식: `#n [단계] 결정 — 이유 / 버린 대안(왜)`. 스킬·서브에이전트 사용 기록은 `[단계] 스킬 (모델) — 무엇이 달라졌나`.

## 결정

- #1 [A0] 강도 **full** — 신호 표 3항목 해당(하드웨어/엣지 · 민감정보(영상) · 외부 연동 2개 이상: 카메라 ONVIF/RTSP + 알림 채널). 버린 대안: lite(1인 프로토타입 문구 없음, 엣지 신호만으로도 full 확정).
- #2 [A0] 서비스 유형 복합(IoT·엣지 주 + 관제 인접) → 프로파일 P1 전항목 + P3 3항목(실시간성·알람 폭주·이력 증가). 버린 대안: P1 단독(실시간 모니터링 UI가 관제 성격이라 알람 폭주·실시간성 정의가 필요).
- #3 [A0] 가칭 "PiCam Hub" — 라즈베리파이가 카메라들의 허브라는 위치를 이름에 고정. 산출물 slug `s4-rpi-ip-camera`(런 지정).
- #4 [A1] GATE 검토관 인원 — 1명(fresh, 세션 등급 fable). 영상 개인정보는 "민감정보" 신호이나 서비스의 주 도메인이 돈·안전·법이 아니므로 santa 2인 조건 미해당으로 판정. 버린 대안: santa 2인(개인정보보호법 §25 준수는 A2·A4에서 항목으로 처리하면 충분, 비용 +20만 토큰).

## 스킬·서브에이전트 사용 기록

- [A0] 스킬 없음 (라우팅 표: 정규화는 모델만으로).
- [A1] `ecc:research-ops` (메인 로드) — 사실/추론/추천 라벨 분리·확인일 표기 규칙을 조사원 프롬프트에 강제.
- [A1] `ecc:search-first` (메인 로드) — "만들지 말고 사라": go2rtc·MediaMTX·Frigate·ONVIF 라이브러리를 Adopt/Extend/Build 매트릭스로 평가하도록 조사 항목 구성.
- [A1] general-purpose (**sonnet** 서브에이전트, 검색 ≤15회) — 01-recon.md 초안 작성. 메인이 fit 판정 덧붙임.
- #5 [A2] Q1 장소 = 소규모 사업장(공개 장소) 기준 내장 — Assumed(무응답). 엄격한 쪽을 기본값으로 두면 가정 사용자에게 손해 없음. 버린 대안: 가정 전용(사업장 전환 시 §25 위반), 장소 선택형(정책 분기 2배).
- #6 [A2] Q2 HW = Pi 5 4GB + USB SSD + 27W — Assumed(무응답). H.264 HW 디코더 부재는 패스스루 설계로 무관. 버린 대안: Pi 4(지원은 유지, 기준 아님), Zero 2 W(RAM 512MB 비지원).
- #7 [A2] Q3 녹화 = P1 스냅샷 / P2 이벤트 클립 / 24·7 non-goal — Assumed(무응답). 43~65GB/일/대. 버린 대안: 연속 녹화(범위 2배), 라이브만(알림 가치 반감).
- #8 [A2] Q4 원격 = Tailscale — Assumed(무응답). 영상 E2E·포트포워딩 없음. 버린 대안: Cloudflare Tunnel(영상 제3자 경유), LAN 전용(원격 가치 상실).
- #9 [A2] Q5 카메라 ≤4·H.264 sub-stream 라이브 — Assumed(무응답). 6Mbps 이하·단일 출처 실측의 절반 이하. 버린 대안: 8대(세션 상한 하향), 16대(NVR급 — Frigate/상용 재검토).
- #10 [A2] 전체 접근 = Extend(go2rtc 채택 + 제어·모니터링 얇은 층 자체 구현). Frigate 통째 채택은 "객체 감지가 목적"일 때만 — 03 non-goal에 경계 명시.
- #11 [A2] read-only rootfs·A/B 파티션·X.509 fleet 신원은 P1 기본값에서 축소 채택(단일 기기·1인 운영·비용>편익) — 사유를 register에 명시. 재검토 조건: 기기 2대 이상 또는 무인 원격지 설치.
- [A1] 메인 추가 검증 WebFetch 4회 — go2rtc ONVIF 소스·서버 지원 확정, Pi 5 공식 사양(HEVC 디코더만·전원 5V/5A) 확정, ntfy 자가호스트 확인(라이선스 미확인 → Assumed).
- [A2] `ecc:product-lens` (메인 로드) — register 상단 "제품 렌즈" 3줄(문제·기존 대안·성공 정의) 추가, Q3(녹화 범위)의 Impact 판단에 "알림 가치" 관점 반영.
- #12 [A3] 목표 3개 = 본다/움직인다/안다 (G1~G3 직교). 요구사항 26개(P0 12·P1 8·P2 6), 상수 표 38개를 03에만 둠. 버린 대안: 목표에 "녹화한다" 추가 — Q3에서 P2로 밀렸고 G3(안다)의 수단이라 직교 아님.
- #13 [A3] 불변식 INV-1~6 명시(MAC 유일·보관 상한·자격증명 평문 금지·트랜스코딩 금지·바인딩·감사 불변) — product-capability의 "senior memory에만 있는 제약"을 표로 끌어냄.
- #14 [A3] UI dial = 관제/대시보드(8·2·3), 폰트 로컬 서빙(CDN 0). 버린 대안: 제품/앱 UI(5·4·4) — 그리드 타일 4개+상태 배지가 밀도 8 성격.
- [A3] `ecc:product-capability` (메인 로드) — 요구사항 풀에 "불변식·제약" 표(INV-1~6) 추가, FR을 EARS 문형으로 통일.
- [A3] `frontend-design-taste` (메인 로드) — UI 방향 절에 dial·Cockpit 하드룰·상태 4종(빈/로딩/에러/stale) 강제, 화면 스케치에 stale 타일 표기.
- #15 [A4] ADR-5 시그널링 보호 = hub-api가 go2rtc WS 시그널링을 세션 검사 후 리버스 프록시, 미디어(DTLS-SRTP)는 go2rtc :8555 직결. 버린 대안: go2rtc 직접 노출(인증 우회), go2rtc basic auth(세션 이중 관리).
- #16 [A4] ADR-6 프로세스 = docker compose 3서비스 + 호스트 tailscaled/log2ram/watchdog. *(#19-12에서 2서비스 + ntfy compose profile로 변경, 둘 다 host 네트워크)* 버린 대안: bare systemd(롤백 = 파일 복사), 전부 컨테이너(TUN 권한).
- #17 [A4] 위협 A4-I RTSP·ONVIF 평문 = Accept(카메라 대부분 TLS 미지원) + 전용 VLAN 권고 체크리스트. A5-I SSD 평문 영상 = Accept(TPM 없음, 무인 부팅 키 보관 문제) + 자격증명만 암호화·키는 SD 분리. 재검토 조건 명시.
- #18 [A4] INV-4 문구 정밀화(03 v1.0 내 수정, 하류 없음): 스냅샷 폴백의 단일 키프레임 JPEG 추출만 예외로 허용 — 1순위는 카메라 GetSnapshotUri(Pi 부담 0).
- [A4] `ecc:architecture-decision-records` (메인 로드) — "검토한 대안" 절을 ADR-1~7 표(결정·대안·왜 아닌가·결과)로 구조화, 별도 docs/adr 대신 이 표+decision-log에 흡수.
- [A4] `ecc:security-review` (메인 로드) — 쿠키 HttpOnly/Secure/SameSite=Strict, CSRF 커스텀 헤더, 로그인 레이트리밋, 로그 마스킹, 컨테이너 cap_drop/non-root를 ③ 대책 표에 반영.
- [A4] `ecc:architect` (**fable** 서브에이전트, 입력 03+04만, 응답 40줄 제한) — 독립 검토. 결과는 아래 #19에 기록.
- #20 [A5] 03 v1.0→**v1.1**: 카메라 자격증명 거부 401→422 `camera-auth-rejected`, PTZ 미지원 405→409 `ptz-unsupported` (허브 세션 401·HTTP 메서드 405와 의미 충돌 제거). 04 헤더·등록 시퀀스 같은 턴에 v1.1로 전파(규칙 9). 05는 v1.1 기준으로 작성.
- #21 [A5] 버저닝 = 미디어타입(`application/vnd.picam.v1+json`), URL 버전 없음 — ecc:api-design은 `/api/v1/` 권장이나 stage-templates(Zalando #115)가 우선. 충돌 기록.
- #22 [A5] 멱등성 키는 E-06·E-14·E-23(생성 3개)만. PTZ move/stop은 "마지막 명령 승리"라 키 없음. 버린 대안: 전 POST 키 강제(PTZ 초당 10건에 키 저장은 SSD 쓰기 낭비).
- #23 [A5] 카메라 목록만 페이지네이션 예외(≤ MAX_CAMERAS). 나머지 목록은 커서. 소프트삭제 없음(카메라 하드 삭제 + CASCADE, 감사는 이름 스냅샷).
- #24 [A5] `ecc:postgres-patterns` 미호출 — 저장소가 SQLite(ADR-3)라 부적합. 인덱스·제약은 05 데이터 규칙에 직접 기술.
- [A5] `ecc:api-design` (메인 로드) — 상태코드 표(201+Location, 204, 409/422/429/507)·문제 유형 slug 표·레이트리밋 티어·커서 페이지네이션 채택. URL 버저닝 권고는 불채택(#21).
- #25 [A6] 레이어에 **lab**(실 Pi + 실 카메라 수동 체크리스트) 추가 — SC-001·002·005·006·007·008은 시뮬레이터로 못 잰다. 버린 대안: 실기기 SC를 "구현 후 확인"으로 미룸(수용기준 누락 게이트 위반).
- #26 [A6] 카메라 시뮬레이터 3종(sim-camera ONVIF 스텁·sim-rtsp ffmpeg testsrc+MediaMTX·sim-ntfy)을 테스트 인프라로 명시 — 실카메라 없이 CI 가능. MediaMTX는 여기서만(테스트 퍼블리셔) 쓰고 런타임에는 안 쓴다(ADR-1과 충돌 없음).
- #27 [A6] 커버리지 = 리스크 기반(인증·암복호·보존 100%/95%, 화면 60%) — ecc:tdd-workflow의 80% 일률 대신(skill-routing 충돌 규칙).
- [A6] `ecc:tdd-workflow` (메인 로드) — RED 게이트 문구·"테스트 격리·의미 셀렉터" 원칙 채택, 80% 일률 목표는 불채택.
- [A6] `ecc:e2e-testing` (메인 로드) — flaky 정책(타임아웃 대기 금지·repeat-each·fixme 격리), retries/trace/video 설정, 3여정 아티팩트 스크린샷 채택.
- #19 [A4 검토 결과] `ecc:architect`(fable) 판정 **FAIL** — 12건(HIGH 5·MEDIUM 6·LOW 1). 타당성 필터: 12건 전부 타당(거짓 양성 0). 반영: (1) hub-api·go2rtc 둘 다 host 네트워크, 격리는 바인딩 주소 (2) go2rtc rtsp 8554 비활성 Eliminate + T-056 (3) StreamManager.reconcile + J-01 go2rtc 대조 + T-057 (4) MSE 폴백은 허브 릴레이 명시 + MSE_MAX_STREAMS + T-059 (5) 시청 세션=뷰어(로그인 세션) 정의, WS 상한 파생 (6) PROBE_TIMEOUT_S·HUB_TEMP_MAX_C·LOGIN_RATE_PER_MIN 상수화, HEALTH_PROBE_INTERVAL_S 30→25(최악 55s ≤ 60) (7) systemd WatchdogSec 폐기 → docker healthcheck(HEALTHCHECK_INTERVAL_S) + ContinuousMove Timeout (8) AUDIT_RETENTION_DAYS 삭제(INV-6와 모순) (9) SettingsModule·AuditLog 간선·FR-018 강제 지점 (10) A4-S 프로브 기반 시리얼 검사 폐기 → 등록·IP 변경 시 대조 + 409 camera-identity-mismatch (11) 잠금을 IP 기준+지수 지연, A5-I Accept 사유 솔직화 (12) YAGNI: /metrics·헬스 롤업(P2)·ntfy 컨테이너(profiles). 결과 03 v1.2 · 04 v1.2 · 05 v1.1 · 06 v1.1 · 02 3줄 (규칙 9 전파). 재검토 없음(예산).
- [재개] 2026-09-07 13:25 — 세션 한도(429) 중단 후 재개. 00~06+decision-log 존재 확인. A4 검토 전파(위 #19)가 04/05/06에 미완이라 먼저 완료 후 A7 진행.
- #28 [A7] 배포 = Pi가 pull(`picam update`) + 헬스체크 3회 실패 시 이전 태그 자동 복귀. 버린 대안: CI가 Pi로 push(인바운드 필요), watchtower 자동 갱신(무인 업데이트가 라이브를 끊음).
- #29 [A7] 관측 = E-22 JSON + 구조화 로그 + 설정 화면 "허브 상태" 패널 1장. Grafana/Prometheus는 P2(1인 운영 YAGNI). 데드맨 하트비트(매일 09:00 ntfy)로 "허브 자체 사망"을 사람이 인지.
- #30 [A7] 백업 = DB·키·TLS·.env·go2rtc.yaml만(스냅샷·클립 제외 — 보관 상한을 백업이 넘기지 않게, INV-2). 일 1회 SSD + 주 1회 Tailnet rsync 오프사이트, 분기 복원 리허설. RPO 24h / RTO 1h.
- [A7] `ecc:deployment-patterns` (메인 로드) — 파이프라인 단계표(lint→test→contract→build→e2e→release→deploy→smoke), 헬스체크 start_period·retries, 롤백 체크리스트, 준비도 체크리스트 항목을 배포 절에 흡수.
- [A7] `ecc:docker-patterns` (메인 로드) — compose 하드닝(read_only·tmpfs·cap_drop·no-new-privileges·non-root·태그 핀), 볼륨 전략(SSD 바인드·키 ro), json-file 로그 로테이션 채택. 브리지 네트워크 격리 패턴은 host 네트워크 결정(ADR-6)으로 불채택.
- #31 [GATE] fresh-context general-purpose(**fable**) 1차 판정 **FAIL** — 15건(CRITICAL 2·HIGH 11·MEDIUM 2). 타당성 필터: 15/15 타당, 거짓 양성 0. 패치 1회: 03 v1.3 · 04 v1.3 · 05 v1.2 · 06 v1.2 · 07 v1.1 · 02 3줄 · decision-log #16 주석. check_package 재실행 CRITICAL 0/HIGH 0. **재검토 없음(코디네이터 예산 지시)** → 판정 CONCERNS로 마감, 잔여 4건을 08 핸드오프 선행 조건으로 이관. 핵심 설계 변경 3: 자가복구=내부 감시 exit(1)(docker healthcheck는 재시작 안 함), 오디오 제외=프록시 SDP/MSE 필터(go2rtc 소스 옵션 미의존), RTSP 프로브 무인증(Digest 해시 유출 제거).
- [GATE] general-purpose (fable, fresh, 입력 00~07+check_package 출력, 응답 60줄 제한) — 위 #31. santa 2인 미적용(#4 + 예산).
- [GATE] 스킬 호출 없음 — 검토는 서브에이전트, 08 작성은 메인.

## 비용 기록
강도 **full** · 검색 횟수 **31회**(A1 sonnet 조사원 WebSearch 20 + WebFetch 6, 메인 WebFetch 5: go2rtc README·ntfy·Pi 5 문서·Pi 5 제품·go2rtc raw README) · 서브에이전트 **3명**(A1 sonnet ≈115.8k tok / A4 검토 ecc:architect fable ≈50.0k / GATE general-purpose fable ≈167.2k = 서브 합계 ≈333k; 메인 세션 토큰 미계측) · 소요 시간 **10:20 → 13:50 KST (약 3.5h, 세션 한도 429 중단·재개 13:25 포함)** · 산출물 10파일 + 통합본 s4-b.md.
