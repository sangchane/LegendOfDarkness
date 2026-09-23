# Decision Log — FallGuard-Care (요양시설 낙상 감지·알림)

형식: `#D-nnn [단계] 결정 — 이유 / 버린 대안(왜)`. ADR 스킬이 요구하는 별도 파일은 이 로그에 흡수한다(skill-routing 충돌 규칙).
실행 모드: 오토파일럿(무응답). 세션 한도(429)로 1회 중단 후 02까지 저장된 산출물을 재개 입력으로 사용(2026-09-03).

## 단계별 스킬 사용 기록

- [A0] 스킬 없음 — 라우팅 표대로 정규화는 모델만으로 수행
- [A1] ecc:research-ops — [사실]/[추론]/[추천] 경계 표기와 확인일(2026-09-03) 병기를 01-recon 전 항목에 강제
- [A1] ecc:market-research — 경쟁 5종을 "실제 기능 범위"로만 기술하고 §6 미확인·다운사이드 절을 추가
- [A1-스택] ecc:search-first — Adopt/Extend/Build 판정 열을 스택 표에 도입, "우리가 만드는 것 4가지"로 범위 축소 (EMQX BSL → Mosquitto 채택 근거 포함)
- [A2] ecc:product-lens — Mode 1 Product Diagnostic 7문항을 register §0에 실행, 질문 승격의 Impact 기준(안티골·MVP)을 고정
- [A3] ecc:product-capability — CAPABILITY/CONSTRAINTS(불변식)/상태 전이 계약을 PRD에 흡수, 요구사항 풀에 INV-nnn 불변식 열 추가
- [A3-UI] frontend-design-taste — 직원 앱 dial(DENSITY 3·MOTION 2·VARIANCE 2), 관리 웹 dial(7·2·3) 지정, 빈/로딩/에러/stale 상태 요구를 FR로 승격
- [A4] ecc:architecture-decision-records — "검토한 대안" 표를 Nygard ADR 형식(장점·단점·왜 아닌가)으로 작성, 별도 docs/adr 대신 이 로그 #D-013~#D-019에 흡수
- [A4] ecc:security-review — 체크리스트(시크릿·입력검증·SQLi·인가·XSS/CSRF·레이트리밋·로그 노출)를 STRIDE ③ 대책 표에 항목별로 매핑, TB5 관리 웹 행과 "시크릿 Eliminate" 행 추가
- [A4-검토자] ecc:architect 서브에이전트 — 독립 리뷰 8건(CRITICAL 1·HIGH 4·MEDIUM 3) 전부 타당 판정 후 반영: 게이트웨이 SPOF degraded 모드·예비기, Doze 대책(포그라운드 서비스), 오프라인 기기 자격증명, 해시체인 writer 분리, 재생 시 종국 상태 통보, transition-proposal 커맨드, 대안 표 정정, 클라우드 모놀리스 축소
- [A5] ecc:api-design — 상태코드 표·커서 페이지네이션·X-RateLimit 헤더·스코프 체크리스트 채택; URL 경로 버저닝은 거부(Zalando #115 미디어타입), 에러 봉투는 RFC 9457로 대체
- [A5] ecc:postgres-patterns — timestamptz/text/BRIN/부분 인덱스/RLS `(SELECT …)` 래핑/statement_timeout 채택; ID는 bigint 대신 UUIDv7(오프라인 생성)
- [A6] ecc:tdd-workflow — RED 게이트·AAA·독립 테스트·가짜 시계 채택; 일률 80% 커버리지 거부 → 리스크 기반(EventEngine·AuditLog 100%, UI 60%)
- [A6] ecc:e2e-testing — POM·자동 대기·waitForResponse·quarantine·repeat-each·trace/video 채택; 네이티브 Android는 Maestro로 동일 원칙 적용
- [A7] ecc:deployment-patterns — 파이프라인 단계·상세 헬스체크·zod 환경검증·롤백/준비도 체크리스트 채택; 전략은 클라우드 블루-그린 + 게이트웨이 Mender A/B로 특정
- [A7] ecc:docker-patterns — 클라우드 compose 하드닝(read_only·cap_drop·no-new-privileges·healthcheck·env_file) 채택; 게이트웨이는 컨테이너 대신 systemd 네이티브
- [GATE] ecc:santa-method — 독립 리뷰어 2명(B·C) 동시 기동·동일 루브릭·양측 통과 규칙을 적용하려 했으나 두 서브에이전트 모두 세션 한도(429)로 결과 없이 종료 → 코디네이터 예산 지시에 따라 fresh-context 서브에이전트 **1명 1회**로 대체(santa 양측 통과 규칙 미충족, 08에 명시)
- [GATE] Agent(general-purpose) 적대적 검토 1회 — 발견 10건(CRITICAL 1·HIGH 7·MEDIUM 2), 판정 FAIL(A3 재실행 권고) → 타당성 필터 후 9건 반영·1건 부분 반영, 재검토 루프 없이 **패치 1회**로 종료, 최종 판정 CONCERNS

## 결정

- #D-001 [A0] 서비스 유형을 "복합(IoT·엣지+AI+관제+모바일+웹 SaaS)"으로 분류, 프로파일 P1·P2·P3·P4·P5 + 임시 PX-헬스케어 안전알림 로드 — 입력에 센서·알림·다수 수신자가 동시에 있음 / 단일 프로파일(웹 SaaS만)은 엣지·알림 함정을 놓침
- #D-002 [A1] 제품 1차 가치를 "낙상 예측"이 아닌 "바닥 체류 시간 단축"으로 정의 — long lie ≥1h가 6개월 사망률·재입원과 직결(BMC Geriatrics 2022), SafelyYou −29.6분 실증 / 예측 중심은 의료기기 경계 진입·근거 부족
- #D-003 [A1] MQTT 브로커 = Eclipse Mosquitto — 시설당 단일 노드로 충분, EPL/EDL / EMQX 5.9+는 BSL 1.1 "타사 임베디드 제공 제한"이 시설 임베드 배포 모델과 충돌 가능
- #D-004 [A1→A2 Q1] 감지 센서 = 60GHz mmWave 레이더 전용(MVP) — 침실 CCTV 전원 동의 회피·화장실 설치 가능·$25 파일럿·AGPL 회피 / 카메라 전용·하이브리드는 동의 절차와 비전 파이프라인이 MVP를 2배로 키움
- #D-005 [A1] 백엔드 = NestJS + PostgreSQL 16, TimescaleDB 보류 — 시설당 이벤트 <1만/일, 관계형으로 충분(YAGNI) / Timescale은 볼륨 근거 없이 도입하면 운영 복잡도만 증가
- #D-006 [A2 Q1] 엣지 게이트웨이 = Raspberry Pi 5급(레이더 전용이므로 GPU 불필요) — Jetson은 비전 2단계에서 재검토
- #D-007 [A2 Q2] 배포 형상 = 하이브리드(시설 게이트웨이 + 클라우드) — 인터넷 단절에도 직원 알림 경로 유지가 안전 서비스의 전제 / 완전 클라우드는 단절=알림 중단, 완전 온프레는 알림톡·푸시 불가
- #D-008 [A2 Q3] 보호자 통보 = 직원 확정 후에만 — 현행 절차(기관장 판단 후 연락)와 동일, 오탐 통보 차단 / 즉시 자동은 오탐률 10~30% 환경에서 신뢰 붕괴
- #D-009 [A2 Q4] 에스컬레이션 = 담당자 60초 → 전 직원 +120초 → 간호사·시설장 전화 — Nobi 3분56초 벤치마크 내 사람 도달 / 5분 단일 단계는 야간 1인 근무에 과다
- #D-010 [A2 Q5] 규제 포지션 = "낙상 안전 알림 장치"(비의료기기 전제), 출시 전 식약처 해당 여부 질의를 착수 조건으로 — 진단·치료·위험도 산출 문구 금지 / 위험 예측 포함은 인허가 경로로 6~12개월 지연
- #D-011 [A2] 너스콜 연동은 MVP 제외(P2 릴레이 접점) — 국내 표준 프로토콜 부재, 시설별 상이 / 포함 시 현장 연동 일정이 파일럿을 지배
- #D-012 [A2] 직원 앱 Android 우선, iOS는 Critical Alerts entitlement 승인 후 — Apple 승인 필요(건강·안전 용도), Android는 setBypassDnd로 즉시 가능
- #D-013 [A4] 상태기계 진실원 = 게이트웨이, 클라우드는 미러·통보·이력 — 단절 중 안전 경로가 1급(INV-04) / "클라우드 진실원 + 게이트웨이 store-and-forward"는 단절 중 두 번째 상태기계가 생겨 충돌 규칙이 필요
- #D-014 [A4] 직원 알림 = LAN WebSocket + FCM 고우선순위 이중 발송 + 앱 상시 포그라운드 서비스·배터리 최적화 제외·full-screen intent — FCM 일반 우선순위는 Doze 중 지연, LAN 소켓도 앱 프로세스 생존이 전제 / FCM 단일 경로는 단절·Doze에 취약
- #D-015 [A4] 게이트웨이 SPOF 대책 = watchdog(MTTR ≤ 60s) + 클라우드 gateway-silent 180초 → "감지 중단" 알림 + 예비 게이트웨이 콜드 스탠바이(MTTR ≤ 4h) — 센서는 게이트웨이와만 통신하므로 클라우드가 감지를 대체할 수 없음 / 핫 스탠바이 이중화는 파일럿 규모에 과잉
- #D-016 [A4] 직원 단말 LAN 인증 = 게이트웨이 발급 장기 기기 자격증명(30일, 오프라인 검증), 클라우드 토큰(12h)과 분리 — 단절 > 12h 또는 단절 중 교대에도 LAN 경로 유지 / 클라우드 토큰만 쓰면 INV-04 위반
- #D-017 [A4] 감사 해시체인은 writer 단위(게이트웨이 `gateway_id`+seq, 클라우드 `audit_log.seq`), 클라우드가 미러 시 게이트웨이 체인 검증 — 두 writer를 한 체인에 섞으면 지연 재생 시 분기 / outbox 상한은 outbox에만, transitions는 append-only 유지
- #D-018 [A4] 재생(replay) 통보는 종국 상태 기준(CONFIRMED→정정이 함께 오면 억제), FR-006 10분 기산점 = 클라우드 received_at, 단절분은 outage_deferred 지표 — 순서대로 통보하면 정정된 오탐이 보호자에게 나감
- #D-019 [A4] 클라우드 = NestJS 모듈러 모놀리스 1개 + Mosquitto + PostgreSQL(파일럿), 클라우드측 백업 에스컬레이션 타이머 제거, 클라우드 경유 ACK는 transition-proposal 커맨드로 게이트웨이에 위임 — 3인·1시설에 서비스 3개는 과설계, 이중 전화 발신 위험 제거 / 마이크로서비스 분리는 다시설 진입 시
- #D-020 [A5] ID = UUIDv7(앱·게이트웨이 생성) — 오프라인에서 ID를 만들어야 하므로 DB 시퀀스 불가 / bigint(postgres-patterns 권장)는 온라인 전제
- #D-021 [A5] 버저닝 = 미디어타입(`Accept: …; version=1`), 에러 = RFC 9457 — stage-templates·Zalando 우선 / api-design의 `/api/v1`·`{error:{code}}`는 채택 안 함
- #D-022 [A5] 멱등성 키 보관 72h(Stripe 24h보다 김) — 단절 최대 허용치 동안 오프라인 큐 재전송이 중복 처리되지 않아야 함
- #D-023 [A6] 직원 앱 E2E = Maestro, 관리 웹 = Playwright, 센서 정확도 = HIL 실측 프로토콜(연기 낙상 50·일상 200) — 네이티브 Android는 Playwright 대상 아님, 감지 정확도는 단위 테스트로 대체 불가
- #D-024 [A6] 커버리지 = 리스크 기반(EventEngine·AuditLog 분기 100%, NotificationService 95%, UI 60%) — INV-01~04가 걸린 코드는 전수, 안전 경로 아닌 화면은 최소 / 일률 80%는 위험도를 반영하지 못함
- #D-025 [A7] 게이트웨이 = systemd 네이티브(컨테이너 아님), 클라우드 = Docker Compose 블루-그린, 게이트웨이 = Mender A/B — HW watchdog·RTC·GPIO·루트FS OTA가 컨테이너 레이어와 충돌, RPi 자원 절약 / 컨테이너는 클라우드에서만 가치
- #D-026 [A7] SLO 4개(알림 경로 99.5%·지연 p95 10s·감지 가동률 99.0%·통보 정확성 99.0%), 알람 9개 각 런북 1:1 — SRE "가능한 한 적게·100% 금지·조치 가능" / 오탐률은 SLO가 아닌 품질 대시보드 지표
- #D-027 [GATE→A3/A4] 전화 에스컬레이션 = `escalation_policy.phone_targets`(기본: 근무 중 간호사 → 시설관리자), `staff.phone_enc` 필수, 음성 벤더 = Twilio Programmable Voice(한국어 TTS, [문서](https://www.twilio.com/docs/voice), 발신번호 사전등록) — P0 최후 경로의 수신자·번호·벤더가 미정의였음(GATE #1) / 국내 음성 API는 파일럿 중 재평가(**Assumed — 벤더 실검증 미완**)
- #D-028 [GATE→A3/A5] 권위 시각 = 게이트웨이 RTC 수신 시각(`transitions.at`), 단말 시각은 `payload.device_at` 보조 — 02(서버 우선)와 03(단말 우선)이 상충했고 단말 시계는 검증 불가(GATE #7)
- #D-029 [GATE→A3/A5] INV-01 재정의(낙상 통보=CONFIRMED만, 정정 통보=선행 발송 시만), INV-02 정정 양방향(5분·각 1회) + 수동 이벤트 등록 FR-020 — 오탐 오종결 시 실제 낙상을 되돌릴 수 없고 미탐을 기록할 수 없었음(GATE #3·#4)
- #D-030 [GATE→A3/A5] CONFIRMED는 `resident_id` 필수(1인실 자동, 다인실 선택, 미지정 422), 통보는 해당 수급자 보호자만 — 다인실에서 통보 대상이 미정의(GATE #8)
- #D-031 [GATE→A3/A5/A7] `displayed` 보고 계약(L-90·E-17, FR-021) 추가, 클라우드 `/health` 외부 모니터 P1 알람 추가, 보존·RPO/RTO·stale·토픽 프리픽스를 03 NFR-005 단일 상수 표로 통일(감사 3년, RPO 15분/RTO 1h, `fg/`) — SLI 원천 부재·무페이지 장애·수치 드리프트(GATE #2·#6·#9)
- #D-032 [GATE] E-30 시설별 설정은 유지하되 범위 고정·비활성화 불가, E-32 sensitivity 변경은 허용(평가셋 재실행 경고), US-5를 P1로 승격 — GATE #10 부분 반영: 설정 자체를 코드 상수로 박는 안은 시설별 근무 체계 차이(야간 1인 vs 다인)를 수용 못 해 기각
