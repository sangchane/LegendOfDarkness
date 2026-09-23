# 준비도 리포트 — PiCam Watch (GATE)

작성: 2026-09-03 · 대상: 00~07 v1.2 + decision-log · 검토 방식: `ecc:santa-method`(독립 리뷰어 2인, 동일 루브릭, 컨텍스트 격리) → 패치 1회 → fresh-context 리뷰어 1인 재검토(예산 제약으로 2인 규칙 부분 준수)

## 판정: **CONCERNS**

사유: 1차(B·C) FAIL 지적은 전부 반영 또는 근거 있는 거짓 양성으로 처리되어 CRITICAL 0건(커버리지 FR 29·SC 15·E 16 전수 대조, 레지스터 36항목, 계수 P0 17/P1 9/P2 3 검산 통과). 그러나 2차(D)에서 HIGH 7건이 남았고 그중 4건(Tailscale P1/P2 모순, E-11 재부팅 보정 무효, 프리셋 recall로 INV-9 우회, 자격증명 재입력 경로 부재)은 **결정 수정 없이는 MVP 착수 불가**다. 단계 재실행은 불필요하며 A3·A4·A5·A7 항목 패치로 해소 가능 → 핸드오프 SPEC 단계의 **선행 조건**으로 이관한다(예산 제약으로 3차 패치 루프는 돌리지 않음).

## 검토 이력
| 회차 | 리뷰어 | 대상 버전 | 판정 | 처리 |
|---|---|---|---|---|
| 1차 | B (fresh-context) | 03 v1.1, 04~07 v1.0 | FAIL (15건) | 아래 처리표 — 반영 21·거짓 양성 4 |
| 1차 | C (fresh-context) | 03 v1.0, 04~07 v1.0 | FAIL (15건) | 동일 표에 병합 |
| 패치 | — | v1.2 (2026-09-03 19:37~19:48) | — | 03·04·05·06·07 재작성, 00·02 국소 패치, decision-log #24~#42 |
| 2차 | D (fresh-context) | v1.2 | CONCERNS (HIGH 7·MEDIUM 7·LOW 1) | 잔여 — 핸드오프 선행 조건으로 이관 |
| A4 독립 | `ecc:architect` | 03·04 v1.0 | 12건 | v1.1에 전부 반영(decision-log #24 이전 항목·04 v1.1 개정 주석) |

## 1차 검토(리뷰어 B·C) 지적 처리표

| # | 출처 | 지적 요지 | 처리 | 반영 위치 |
|---|---|---|---|---|
| B1 | B | 03 v1.1이 04~07에 미전파(코덱 거부·세션 한도·연장 규칙·SC-003) | 반영 | 04/05/06 v1.1 + 07·02 v1.2 동기화 |
| B2 / C1 | B, C | 커버리지 카운트 거짓(FR-026 누락, SC-009·001b·013·E-13/14 누락, "누락 0" 주장) | 반영 | 05 매핑 FR-026~029, 06 SC-009 소크·SC-001/004 HW rig 행, 계수 재검산 P0 17·P1 9·P2 3 |
| B3 | B | WebSocket에 커스텀 헤더 불가 | 반영 | 05 규약·STR-3: 쿠키 + `Origin` 허용 목록 |
| B4 / C7 | B, C | 04 시퀀스 경로 ↔ 05 경로 불일치 | 반영(v1.1) | 04 시나리오 1·2 = `/cameras/{id}/whep`·`/mse`·`/ptz/moves` |
| B5 / C3 | B, C | go2rtc 평문 자격증명(설정 파일·백업), 파손 문장, recorder가 1984 도달 | 반영 | 04: 런타임 API 메모리 등록·`local_auth`·basic auth, 07: go2rtc.yaml 비밀 0, age 암호화 백업 |
| B6 / C2c | B, C | 링버퍼 용량(64MB vs 80MB), 녹화 소스 미정, NVMe 산정 무효 | 반영 | 04: tmpfs 128MB·메인 ≤4Mbps·10s×4; 02 Q4 재산정(90GB/최악 390GB → 자동 정리·512GB 권장) |
| B7 / C11 | B, C | 감사 트리거 vs 1년 삭제 자기모순, 해시 체인·검증 CLI 부재 | 반영 | 05: 연 1회 트리거 DROP→DELETE→재생성 + 앵커 행(유일 경로), `prev_hash/row_hash`, AUD-2·`picam audit verify` |
| B8 / C6 | B, C | 안내판 촬영범위·촬영시간 필드 부재, PTZ 목적 범위 강제 없음 | 반영 | 03 FR-011·FR-027(INV-9), 05 CAM-3 필수·`ptz_limits`·`ptz-out-of-range`, 06 E-15/SC-014 |
| B9 / C4 | B, C | 사업장 전제에서 "경고 후 오디오 활성"은 녹음 금지와 모순 | 반영(Eliminate) | 03 INV-6·FR-019 P0, 04·05·07 오디오 필드·API·스트림 제거, SC-013 |
| B10 / C10 | B, C | STRIDE "해당없음" 6셀 무근거, 백업·알림·프로브 경계 누락, XXE, VLAN vs WS-Discovery | 반영 | 04 ②·③: 셀 근거 기입, TB5 백업·웹훅 경계, 파서 격리, Pi 서브인터페이스 조건 |
| B11 / C2a·b | B, C | 알림 채널 "선택"·외부 프로브 미설계 = 미결정 | 반영 | 03 FR-028(P2 웹훅 + heartbeat), 07 알림 표·RB-5 "Pi는 스스로 알릴 수 없음" 정직 기재 |
| B12 / C12 | B, C | api↔recorder IPC 미정의, 오프라인 판정 주체 드리프트 | 반영 | 04: SQLite 폴링(outbox) 명시, StreamHealth 폴러 |
| B13 / C14c·d | B, C | Docker healthcheck는 재시작 안 함(90s), 비루트 443 바인딩 | 반영 | 07: 호스트 `picam-health.timer`(30s×3), API 8443 + nftables 443 리다이렉트; 02 자가복구 문구 |
| B14 / C13 | B, C | "복구 불가 파기" vs unlink, E-11 "보수적" 모호, Pi 5 RTC 내장 | 반영 | 03 FR-012(unlink+fsync+fstrim, Assumed), E-11 단조시계 보정·90일 상한(05 데이터 규칙), 07 BOM RTC 배터리, 00 문구 정정 |
| B15 / C8 | B, C | 05 엔드포인트 ID가 03 E-/G-, 04 STRIDE S-, 02 P-와 충돌 | 반영 | 05 접두사 재부여(AUTH/CAM/STR/PTZ/EVT/CLIP/SET/AUD/USR/HLT), 06 참조 갱신 |
| C2d | C | 90일 상한 출처 없음 | 반영(Assumed 명시) | 03 INV-4·가정 목록, 02 신규 행 |
| C5 | C | Viewer 다운로드 vs #22 논리 모순 | 반영 | 03 FR-015, 05 CLIP-3 `cam:clip:download`(Owner), E-16 |
| C9 | C | 시청자 상한 4/8/16 혼재, SC-003 부하 조건 | 반영(v1.1) | INV-8 2단 한도 고정, SET-2에서 설정 불가, SC-003 16세션 |
| C14a·b | C | 기본 백업처가 동일 NVMe(RPO 무한), tailscaled 상태 소실 | 반영 | 07: 외부 USB 필수 BOM·RPO 정직 기재, `/data/tailscale` statedir |
| C15 | C | 06 계수 오류, E-2 `page.route`로 UDP 차단 불가, SC-002(P2) 완료 게이트, USR FR 부재 | 반영 | 06 계수·OS 방화벽, 03 SC-002 P2 게이트·FR-029 |
| B10(일부) | B | TB3-E 대책이 Pi가 먼저 연 연결의 페이로드를 못 막음 | 반영 | 04 TB3-E: zeep/lxml `resolve_entities=False`, UID 분리 |
| B5(일부) | B | "go2rtc localhost 무조건 통과" 전제 | 거짓 양성(부분) | `api.local_auth: true` 옵션 존재 — 기본값은 통과이므로 설정 명시 유지 |
| B14(일부) | B | "복구 불가"가 단순 삭제를 포함하는지 | 거짓 양성(해석) | 가이드라인 해석 차이 → Assumed로 명시하고 Accept 사유 기록 |
| C5(일부) | C | 다운로드 = 법 위반 | 거짓 양성 | 제25조⑦은 반출 금지가 아님(리뷰어 자진 표기). 채택 이유는 #22 논리 일관성 |
| C15(일부) | C | `metrics_rollup` ERD 부재 | 거짓 양성 | v1.1에서 이미 추가(05 ERD) |

## 2차 검토(리뷰어 D) 잔여 지적 — 핸드오프 선행 조건

| # | 심각도 | 지적 요지 | 타당성 | 처리 계획(SPEC 단계 선행) |
|---|---|---|---|---|
| D1 | HIGH | 03은 Tailscale/FR-020을 P2로 두는데 04·07은 MagicDNS origin·Tailscale 설치를 MVP 필수 절차로 둠 — MVP 시청 origin 모순 | 타당 | **FR-020을 P1로 승격**(Q3 Assumed가 이미 Tailscale 전제), SC-002만 P2 게이트 유지, 06 E2E 측정 origin = MagicDNS 명시 |
| D2 | HIGH(검증 권고) | go2rtc `PUT /api/streams`가 `PatchConfig()`로 yaml에 URL을 기록할 수 있어 "메모리 등록만"은 `:ro` 마운트에 우연히 의존 | 타당(검증 필요) | 구현 착수 시 go2rtc `internal/streams/api.go` 확인 → `:ro` 의존을 명시하거나 `streams:` 섹션 없는 별도 설정으로 회피, 06 FR-003 yaml grep을 회귀 게이트로 유지 |
| D3 | HIGH | E-11의 단조시계 오프셋은 재부팅 시 리셋되어 보정식 무효; 미동기 중 "건너뜀"이 retention 7일 클립을 90일 보관 → INV-4 자기모순 | 타당 | EVENT에 `boot_id` 추가, 미동기 파기는 90일 고정이 아니라 단조 기준 `retention_days` 경과로, 재부팅 후는 RTC(배터리) 값 폴백 — 03 E-11·05 데이터 규칙 수정 |
| D4 | HIGH | PTZ-6 recall·홈 복귀에 범위 검사 없음(벤더 앱 프리셋으로 INV-9 우회), FR-027 기본값이 사실상 무제한 | 타당 | PTZ-6·홈 복귀 후 GetStatus 검사(밖이면 Stop+409), 프리셋 동기화 시 범위 밖 표시, 기본값 "설정 전까지 등록 위치 ±0(잠금)" — 03 FR-027·05 PTZ-6 수정 |
| D5 | HIGH | SC-014 "초과 ≤ 5°"가 메커니즘(750ms GetStatus + 1s ContinuousMove, 속도 상한 없음)에서 도출되지 않음 | 타당 | 경계 근접 시 timeout ≤ 250ms·속도 감속을 FR-027에 명시하거나 SC-014 임계를 도출값으로 재산정 |
| D6 | HIGH | nftables "LAN 허용" vs "카메라 CIDR 차단"이 같은 세그먼트 기본 배치에서 충돌, established/related 미기재 | 타당 | 카메라 차단 규칙을 VLAN 분리 시로 조건화, 동일 세그먼트는 TB3-E Accept로 정직 기재, `ct state established,related accept` 추가 — 07·04 수정 |
| D7 | HIGH | `disabled(자격증명 3회 실패)` 전이를 다루는 FR·CAM-5 자격증명 필드·06 시나리오 부재 | 타당 | `PUT /cameras/{id}/credentials`(writeOnly) 신설 + FR-030(실패 카운터→disabled→재입력 시 online) + 06 시나리오 |
| D8 | MEDIUM | 06 FR-009 행 계수 17 vs 나열 18(HLT-2/3 포함 시 20) | 타당 | 06 문구 20으로 정정 |
| D9 | MEDIUM(검증 권고) | `_sub/_main/_rec` 3스트림 → 단일 뷰 시 카메라 세션 3개, "2세션" 주장과 불일치 | 타당(미명세) | `_rec` 소스를 `rtsp://127.0.0.1:8554/{cam}_main` 내부 alias로 고정 — 04 스트림 규약 1줄 |
| D10 | MEDIUM | decision-log #7·#12·#13 스냅샷 값이 v1.2와 드리프트 | 타당 | 각 항목에 "→ v1.x에서 변경(#nn)" 주석 |
| D11 | MEDIUM | 05 감사 액션 목록에 `ops.restart`·`audit.tamper-detected`·`user.password-change` 없음 | 타당 | 05 목록 3건 추가, `ops.restart`는 api 재기동 시 자기 기록 |
| D12 | MEDIUM | RTSP 8554 무인증(로컬 UID 열람), Tailscale 계정 탈취 위협 미기재, 마법사-T "해당없음" 약함, 해시 체인 무키 | 타당 | go2rtc `rtsp.username/password`, TB4-S 행 추가("tailnet 노드 ≠ 앱 로그인"), 마법사-T Accept, 체인 앵커 HMAC(master.key 파생) |
| D13 | MEDIUM | RB-1 명령 무인증(HLT-2), 예비 SD·NVMe 미BOM, env 롤백 vs 다이제스트 하드코딩, `_FILE` env 미지원 | 타당(일부 검증 권고) | CLI `picam health details`, BOM 예비 매체 또는 RTO "조달 후 4h", compose `${API_DIGEST}`+`env_file`, entrypoint 파일→env 주입 |
| D14 | MEDIUM | 유령 세션 정리의 sid↔go2rtc consumer 상관 미명세, HLT-1 503 조건 미정(카메라 offline을 503으로 하면 헬스 타이머 재시작 루프) | 타당 | sid별 ICE ufrag 기록·매칭 명시, HLT-1 503 = api 자체 불능(DB/go2rtc 미도달)으로 한정 |
| D15 | LOW | Assumed 계수, 감사 1년·`max-load-1`·시작 지연 3s/5s 출처, NVMe 캐시 "권장", "영상은 Pi 밖으로 안 나감" 문구, 롤업 P1 YAGNI | 부분 타당 | 감사 1년은 안전성 확보조치 기준 §8 출처화(검증), 시작 지연 5s로 단일화, 캐시 비활성 필수/Accept 확정, "클라우드 반출 0"으로 수정, HLT-3 P2 |

거짓 양성 필터: D2·D9·D13(d)·D15(a)(b)는 리뷰어 자진 표기대로 "검증 권고"로 낮춰 두되 착수 전 확인 항목으로 유지. 15건 모두 단계 재실행 없이 항목 패치로 해소 가능하며, 예산 제약(패치 1회 규칙)에 따라 v1.3 패치는 SPEC 단계로 이관한다.

## 하드 게이트 점검
| 단계 | 게이트 | 결과 |
|---|---|---|
| A0 | 유형·도메인 분류 | PASS |
| A1 | 전 항목 출처 URL·확인일 | PASS (2차 출처는 [2차] 표기, 재확인 권고 항목 명시) |
| A2 | 전 축 마킹(36항목) | PASS (D 검산 일치) |
| A3 | SC 전부 pass/fail 판정 가능, 미결정 0 | CONCERNS — SC-014 임계 도출 근거 부족(D5), 자격증명 재입력 경로 미명세(D7) |
| A4 | STRIDE 6범주 전 경계 검토 | CONCERNS — RTSP 8554 무인증·Tailscale 계정 탈취·해시 체인 무키(D12) |
| A5 | P0/P1 커버리지 100% | PASS (FR-001~029 매핑, D 검산 일치) |
| A6 | 수용기준→시나리오 누락 0 | PASS (SC 15/15, E 16/16; FR-009 행 계수 오기 D8) |
| A7 | 로그·백업·복구 "어디에·얼마나·어떻게" | PASS(경미) — RTO의 예비 매체 전제·롤백 메커니즘 정합(D13) |
| GATE | 2인 모두 통과 | 부분 준수 — 1차 2인 FAIL → 패치 → 2차 1인 CONCERNS |

## 핵심 결정 5줄
1. 스트림은 go2rtc(패스스루, H.264 필수, 트랜스코딩 금지), 제어·보존·감사는 얇은 FastAPI + SQLite — Frigate 통째 채택·MediaMTX 기각.
2. 법 제25조⑤ 대응: 오디오 기능 부재(Eliminate), PTZ 허용 범위 검사(FR-027), 조작·열람·반출 감사 해시 체인, 다운로드 Owner 전용.
3. 이벤트 클립 = tmpfs MPEG-TS 링버퍼(10s×4, 128MB) + 링 연장(마지막 모션+20s, 상한 120s), 보존 30일(상한 90일 Assumed) 자동 파기.
4. 접근 = Tailscale MagicDNS 단일 origin, 공개 포트 0, 세션 쿠키 단일 인증, 동시 세션 사용자당 4·전역 16.
5. 운영 = SD 루트 읽기 전용 + NVMe `/data`, 호스트 헬스 타이머·HW 워치독, age 암호화 백업을 외부 USB(필수 BOM)로, RB-1~7 런북.

## 질문에서 가정으로 채택된 항목 (Assumed(무응답) 5)
Q1 사업장·공용 공간 포함(보수적) · Q2 ONVIF Profile S 1~4대 · Q3 LAN + Tailscale · Q4 이벤트 클립만 NVMe 30일 · Q5 카메라 내장 모션(ONVIF Events). 추가 운영 가정: 보존 상한 90일, 파기 = unlink+fsync+fstrim, 알림 = 배너 + 웹훅(P2).

## 구현 핸드오프 (service-prompt-workflow SPEC 입력)
```
/service-prompt-workflow 로 다음을 실행:
<inputs>C:/Users/dev/.claude/skills/service-autopilot/eval/runs/run-20260903-smoke/s4-rpi-ip-camera/03-prd.md,
04-architecture.md, 05-api-contract.md, 06-test-design.md, 07-ops-design.md, decision-log.md</inputs>
<preconditions>08-readiness-report.md의 D1~D7(HIGH)을 SPEC 작성 전에 반영한다:
 D1 FR-020 → P1 승격·E2E origin 명시 / D2 go2rtc PUT /api/streams PatchConfig 확인 /
 D3 E-11 boot_id·RTC 폴백·retention_days 기준 파기 / D4 PTZ-6·홈 복귀 범위 검사·기본값 잠금 /
 D5 SC-014 임계 재도출 / D6 nftables 규칙 조건화·established 허용 / D7 자격증명 재입력 엔드포인트 + FR-030.
 D8~D15는 SPEC의 "알려진 결함" 절에 옮겨 PLAN에서 처리.</preconditions>
<first_task>SPEC.md 작성 — 위 문서를 진실원으로, 낯선 구현자 실행 가능 수준(≥7/10)</first_task>
UI 포함 → BUILD·REVIEW에서 frontend-design-taste dial = DENSITY 8 / MOTION 2 / VARIANCE 3 (관제 프로파일) 적용
```
