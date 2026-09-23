# Decision Log — PiCam Watch

형식: `#n [단계] 결정 — 이유 / 버린 대안 — 이유`. 스킬 사용 기록은 `[단계] 스킬 — 무엇이 달라졌나` 한 줄.
실행: 2026-09-03 · 오토파일럿 모드(질문 무응답 → 추천안 채택)

## 스킬 사용 기록 (skill-routing 준수)
- [A0] 스킬 없음 — 라우팅 표대로 정규화는 모델만으로 수행
- [A1] ecc:research-ops — 출처를 [사실]/[2차]/[추론]으로 분리 표기, 확인일 강제 (01-recon 전 항목)
- [A1] ecc:search-first — Adopt/Extend/Build 판정 프레임 적용 → Extend/Compose(go2rtc + onvif-zeep + 얇은 API). ecc:market-research는 단계당 2개 상한으로 미호출(경쟁 비교는 research-ops 아래서 수행)
- [A2] ecc:product-lens — Mode 1 진단 7문을 register 상단에 추가, "제어 = 법 제25조⑤ 충돌" 발견으로 감사로그를 P0 승격
- [A3] ecc:product-capability — "제약·불변식" 절(INV-1~7, 상태 전이)을 PRD에 추가, 요구사항을 EARS 문장으로 통일
- [A3] frontend-design-taste — 관제/대시보드 프로파일 dial(DENSITY 8 / MOTION 2 / VARIANCE 3) 고정, 빈/로딩/에러/stale 4상태를 FR-025·UI 방향에 강제
- [A4] ecc:architecture-decision-records — "검토한 대안" 표를 ADR(Context/Decision/Alternatives/Consequences) 형식으로 decision-log #10~#16에 흡수(별도 docs/adr 미생성)
- [A4] ecc:security-review — 시크릿·입력 검증·쿠키 속성·레이트리밋·에러 노출 체크리스트를 STRIDE ③ 대책 표에 반영(HttpOnly/SameSite, Pydantic 범위 검증, 로그 마스킹)
- [A4] ecc:architect 서브에이전트 — 04 독립 검토(링버퍼·ICE 도달성·WAL·STRIDE 누락) → 결과는 GATE 전 반영(아래 #24~)
- [A5] ecc:api-design — 자원 명명(복수형 kebab-case)·상태코드(201+Location, 202, 409/422/429)·레이트리밋 헤더·커서 페이지네이션 형식 적용. URL 버저닝 권고는 미채택(#17)
- [A5] ecc:postgres-patterns — 복합 인덱스 순서(등치→범위)·부분 인덱스·소프트삭제 관례를 SQLite로 번안, append-only는 트리거로
- [A6] ecc:tdd-workflow — RED 게이트·AAA·독립 테스트·목킹 패턴 적용, 80% 일률 목표는 리스크 기반으로 대체(#18)
- [A6] ecc:e2e-testing — POM 4개·auto-wait·--repeat-each flaky 판별·quarantine·아티팩트 정책을 E2E 절에 적용
- [A7] ecc:deployment-patterns — 파이프라인 단계·헬스체크 2단(/health, /health/details)·롤백 체크리스트·준비도 체크리스트 번안. ecc:docker-patterns는 단계당 2개 상한으로 미호출
- [A7] ecc:dashboard-builder — 운영자 질문 4개(건강한가/어디가 막혔나/무엇이 변했나/무엇을 해야 하나)로 패널 최소 세트 구성, 허영 패널 제외
- [GATE] ecc:santa-method — 법(개인정보보호법 제25조) 도메인이므로 독립 리뷰어 2명(B·C, 동일 루브릭, 컨텍스트 격리) 모두 통과 규칙 적용. fresh-context 서브에이전트 2개를 Agent 도구로 병렬 실행, 거짓 양성은 반영 전 필터링. 1차 판정 FAIL·FAIL → 패치 1회 → 2차는 예산 제약으로 fresh-context 리뷰어 1명(D)만 재검토

## 결정
- #1 [A0] 서비스 유형을 IoT·엣지(주)+관제(부)로 분류 — Pi가 서버이자 엣지. 대안 "웹 SaaS"는 단일 운영자·셀프호스팅이라 기각
- #2 [A1] 스트림 게이트웨이 go2rtc 채택 — ONVIF 입력·WebRTC 출력·트랜스코딩 없음·ARM 바이너리(MIT, 14.1k★, 2026-09-03 확인) / MediaMTX 기각 — ONVIF 디스커버리 부재(녹화 내장은 장점, 클립 생성은 ffmpeg 세그먼트로 대체)
- #3 [A1] Frigate 통째 채택 기각 — 객체감지·가속기(≈$130)가 시드 범위 밖, 설정 부담. 향후 확장 경로로만 기록
- #4 [A2] Q1 무응답 → 사업장 시나리오 전제(보수적) — 안내판 정보·보존기간·PTZ 감사로그를 P0. 대안 "자택 전용"은 이전 시 재작업
- #5 [A2] Q2 무응답 → ONVIF Profile S 1~4대 전제 — 벤더 어댑터 없음. 대안 "벤더 전용 혼합"은 PTZ 역공학으로 범위 폭발
- #6 [A2] Q3 무응답 → LAN + Tailscale — 공개 포트 0, TURN 불필요. 대안 "공개 HTTPS"는 TURN·공격면 증가
- #7 [A2] Q4 무응답 → 이벤트 클립만 NVMe, 30일 보존 — 45GB/30일 추정. 대안 "24/7"은 2.6TB/30일로 NAS 필수
- #8 [A2] Q5 무응답 → 카메라 내장 모션(ONVIF Events) — 추가 HW 0. 대안 "Pi 객체인식"은 Hailo + Frigate 전환
- #9 [A2] 트랜스코딩 금지 원칙 — Pi 5 H.264 HW 인코더 없음(01-recon 사실). 그리드는 카메라 서브스트림 사용
- #10 [A4] 얇은 자체 서비스(go2rtc+FastAPI+onvif-zeep) — ADR: Context=법정 보존·감사·역할이 기존 NVR에 없음 / Decision=스트림은 사고 제어·보존은 만든다 / Alternatives=Frigate 통째(객체감지·가속기 범위 밖), MediaMTX(ONVIF 디스커버리 없음) / Consequences=+운영 데몬 3개로 단순, −객체감지 없음(향후 Frigate 이전 경로 유지)
- #11 [A4] Python FastAPI 백엔드 — python-onvif-zeep 동일 런타임, ffmpeg 서브프로세스 관리 단순 / Node·Go 기각 — 1인 개발 학습 비용, ONVIF 라이브러리 성숙도
- #12 [A4] SQLite WAL(synchronous=NORMAL) — 단일 노드, 백업=파일 복사 / PostgreSQL 기각 — 데몬 추가·다중 노드는 non-goal
- #13 [A4] 전 10초 확보 = tmpfs 링버퍼(ffmpeg -c copy, 10s×3 wrap) — NVMe 상시 쓰기 회피 / NVMe 상시 세그먼트 기각 — SD·NVMe 마모, 재부팅 소실은 허용 가능(이벤트 클립만 법정 대상)
- #14 [A4] WebRTC 시그널링은 API가 WHEP 프록시 — go2rtc localhost 무인증 특성(2026-09-03 확인), 스코프·동시 시청자 제한·감사가 우리 쪽에 있어야 함 / go2rtc basic auth 직접 노출 기각
- #15 [A4] RTSP 평문(TB3)은 Accept — 카메라 RTSPS 미지원이 일반적, 카메라 VLAN 분리·Pi 인바운드 차단으로 완화. 클립 저장 암호화도 Accept(물리 보안 범위, 성능·복구 복잡도)
- #16 [A4] 컨테이너 격리: 비루트·read_only·no-new-privileges, ffmpeg는 별도 recorder 컨테이너 — 좀비·메모리 격리 / 단일 컨테이너 기각
- #17 [A5] 버저닝: URL 버저닝 미채택(Accept 미디어타입, 기본 v1) — stage-templates(Zalando #115)가 ecc:api-design의 /api/v1/ 권고보다 우선. 단일 테넌트·번들 SPA라 실익도 낮음
- #18 [A6] 커버리지: ecc:tdd-workflow "80%+ 일률" 대신 리스크 기반 — 인가·감사·PTZ 워치독·보존·시크릿 100%, 스트림 80%, UI는 상태 스냅샷만
- #19 [A6] 카메라 의존 테스트를 mock 카메라(CI)와 실카메라 HW rig(주 1회)로 이원화 — 벤더 비표준 ONVIF는 mock으로 재현 불가(정성 근거)
- #20 [A7] Docker data-root=/data/docker + SD 루트만 overlay read-only — overlayfs 루트에서 Docker 기동 실패 사례(2026-09-03 확인) / 전체 read-only 기각
- #21 [A7] OTA = 다이제스트 고정 이미지 + Owner 수동 승인 + previous.env 롤백 — 단일 기기·현장 접근 가능 / A/B 파티션은 P2로 이월
- #22 [A7] 클립은 백업 제외(단일 사본 정책) — 백업 사본이 보존기간 통제를 어렵게 함(법 제25조⑦). DB·config만 일 1회 + 분기 복원 리허설
- #23 [A7] SLO는 5개만, 보존 준수만 100%(법정) — SRE "가능한 한 적게, 100% 금지" 원칙의 명시적 예외로 기록

## GATE 반영 결정 (v1.2)
- [GATE] fresh-context 서브에이전트(Reviewer B·C) 판정 FAIL 2건 수신 → 타당성 필터링 후 1회 패치. 거짓 양성 처리: B#13(c)/C#14(c) 443 바인딩은 cap 의존이라 8443+리다이렉트로 우회(지적 수용), C#3 go2rtc.yaml 평문은 런타임 등록으로 해소, C#5 다운로드 "법 위반"은 아니나 #22 논리 일관성 위해 Owner 전용 채택, C#13 Pi 5 RTC는 배터리 BOM으로 수용
- [GATE] 2차 검토: fresh-context 서브에이전트 1명(리뷰어 D) — 예산 제약으로 santa-method 2인 규칙 대신 1인 재검토(부분 준수, 08에 명시)
- #24 [A4] go2rtc 스트림을 런타임 API로 메모리 등록, 설정 파일에 비밀 0 — INV-5를 설정 파일·백업까지 확장 / yaml 기재(0600) 기각 — 백업·물리 탈취 시 평문 (GATE C#3)
- #25 [A3] 오디오 기능 Eliminate(FR-019 P0, INV-6) — 사업장 전제에서 녹음 금지는 예외 없음 / "경고 후 Owner 활성" 기각 — 코드 경로가 남음 (GATE B#9·C#4)
- #26 [A3/A5] 안내판 필드 촬영범위·촬영시간 추가(FR-011, CAM-3 필수) — 시행령 §24 기재사항 (GATE B#8·C#6)
- #27 [A3/A5] 클립 다운로드 Owner 전용 스코프 `cam:clip:download`, Viewer는 재생만 — #22(백업 사본이 보존 통제를 깨뜨림) 논리와 일관 (GATE C#5)
- #28 [A3/A4] PTZ 허용 범위 FR-027(P1): GetStatus 범위 검사 + 홈 복귀 — 법 제25조⑤ "다른 곳을 비추는 행위"를 기술적으로 제한 / 감사로그만 기각 — 사후 입증뿐 (GATE B#8·C#5)
- #29 [A3/A7] 알림 = 앱 배너(P1) + 웹훅 1개·heartbeat(P2, FR-028) — 채널 다중화 회피, Pi 다운은 heartbeat 부재로 수신처가 감지(정직 기재) / 이메일·텔레그램 개별 연동 기각 (GATE B#11·C#2)
- #30 [A3/A5] 사용자 관리 FR-029(P1) ↔ USR-1~4 매핑 — PRD 부재 해소 (GATE C#15)
- #31 [A5] 엔드포인트 ID 접두사 재부여(AUTH/CAM/STR/PTZ/EVT/CLIP/SET/AUD/USR/HLT) — 03 E-/G-/SC-, 04 STRIDE S/T/R/I/D/E, 02 P1~P6와 충돌 제거 (GATE B#15·C#8)
- #32 [A5] MSE WebSocket 인증 = 쿠키 + Origin 허용 목록 — 브라우저 WebSocket은 커스텀 헤더 불가 / X-Requested-With 기각 (GATE B#3)
- #33 [A5] 감사로그 1년 아카이브 = 트리거 DROP→DELETE→재생성 + 앵커 행, Owner 승인·연 1회 — SQLite에 트리거 비활성화가 없어 "우회 없이"는 불가능, 절차를 유일 경로로 문서화 (GATE B#7·C#11)
- #34 [A5] E-11 보정 규칙 수치화: 단조시계 오프셋으로 created_at·expires_at 재계산, 미동기 90일 지속 시 파기 — "보수적(늦게)" 모호성 제거 (GATE B#14·C#13)
- #35 [A3/A5] 보존 상한 90일·파기 방식(unlink+fsync+fstrim)을 Assumed로 명시 — 법정 수치·방법 아님, 가이드라인 해석 (GATE C#2d·B#14)
- #36 [A7] API 8443 바인딩 + nftables 443 리다이렉트 — 비루트 1024 미만 바인딩은 cap/sysctl 의존 / cap_add 기각 — no-new-privileges와 상호작용 불확실 (GATE B#13·C#14c)
- #37 [A7] 호스트 systemd 헬스 타이머(30s×3=90s)가 api 재시작 — Docker healthcheck는 재시작하지 않음 / autoheal 컨테이너 기각 — 의존 추가 (GATE B#13·C#14d)
- #38 [A7] 백업: 외부 USB 필수 BOM + age 암호화 + 복구 키 1회 표시, RPO는 USB 정상 시 24h·미장착 시 복구 불능으로 정직 기재 — 동일 NVMe 백업은 고장에 무력, 평문 반출 금지 (GATE C#14a·C#3)
- #39 [A7] tailscaled statedir·chrony drift를 /data로, RTC 배터리 BOM — 오버레이 루트에서 상태 소실 방지 (GATE C#14b·C#13)
- #40 [A4] STRIDE "해당없음" 6셀 근거 기입, TB5 백업 매체·웹훅 경계 추가, TB3-E 파서 격리(XXE), VLAN 조건(Pi 서브인터페이스), api↔recorder IPC = SQLite 폴링 명시, StreamHealth 폴러로 오프라인 판정 주체 단일화 (GATE B#10·B#12·C#10·C#12)
- #41 [A6] SC-009 24h 소크 + 운영 30일 SLI, SC-001/004 HW rig 행, E-2 차단은 OS 방화벽(page.route는 HTTP만), 계수 재검산 P0 17·P1 9 (GATE B#2·C#1·C#15)
- #42 [A3] SC-002를 P2 게이트로 분리(US-5 출시 시) — 완료 신호에서 제외 (GATE C#15)
