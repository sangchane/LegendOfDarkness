# 준비도 리포트 — PiCam Hub
버전: v1.0 · 기준 03 v1.3 (04 v1.3 · 05 v1.2 · 06 v1.2 · 07 v1.1) · GATE 2026-09-07 13:44

## 판정: **CONCERNS**
- GATE 1차(fresh-context 서브에이전트, `fable`): **FAIL** — 15건(CRITICAL 2 · HIGH 11 · MEDIUM 2).
- 타당성 필터 후 **15건 전부 타당**(거짓 양성 0, "확인 필요" 표기 2건은 설계 변경으로 무관화). 패치 1회 적용(03 v1.3·04 v1.3·05 v1.2·06 v1.2·07 v1.1·02 3줄).
- **평가 런 예산 이탈(명시)**: SKILL.md는 FAIL 시 해당 단계 재실행 후 재검토 1회를 허용하나, 이 런은 코디네이터 지시로 **재검토 없이 패치 1회로 마감**한다. 따라서 재검토를 거친 PASS를 주장하지 않고 CONCERNS로 마감하며, 잔여 지적은 아래 "핸드오프 선행 조건"으로 넘긴다. 돈·안전·법 도메인 santa 2인 검토도 미적용(00·decision-log #4: 주 도메인이 돈·안전·법이 아님 + 예산).

## check_package.py (패치 후 재실행 2026-09-07 13:44)
```
## CRITICAL (0)
## HIGH (0)
## INFO (3)
- C2 FR 총 26 (P0 12 · P1 8 · P2 6), 05 참조 26
- C3 SC 총 10, 06 참조 10
- C5 축 행 33, 마킹 33, 질문 5
```
(패치 전 13:40 실행도 CRITICAL 0 · HIGH 0 — 스크립트는 형식 매핑만 세므로 GATE 검토관이 실질 공백을 찾았다.)

## GATE 발견 → 처리
| # | 심각도 | 지적 (문서:섹션) | 타당성 | 처리 |
|---|---|---|---|---|
| 1 | CRITICAL | 04·02·03·06·07 — docker healthcheck는 unhealthy 표시만, 재시작 안 함 → 자가복구 경로 무효 | 타당 | hub-api **내부 감시 태스크**가 프로브 정체(PROBE_STALE_FACTOR×HEALTH_PROBE_INTERVAL_S) 시 `exit(1)` → `restart: unless-stopped`. healthcheck는 관측용으로 강등. 02·03·04·06(T-058)·07 전파 |
| 2 | CRITICAL | 06·05·07·04 — 03 상수가 값으로 재기술(단일 출처 위반) | 타당 | 06 T-001/002/010/021/036/037/038/041/043/059를 상수명·파생식(`±1`)으로, 05 detail은 `{상수}` 템플릿, 04 산식화, 07 compose는 `# = 상수명` 주석 + `render.sh` 치환 명시 |
| 3 | HIGH | 07 — healthcheck가 8080을 치지만 CMD는 8443만 | 타당 | 8443 TLS 단일 리슨, healthcheck는 인증서 검증 없이 8443 |
| 4 | HIGH | 05·06 — 오디오 제외 옵션 3표기, "착수 시 확정"은 TBD 우회 | 타당 | go2rtc 소스 옵션에 **의존하지 않는** 설계로 변경: E-18 프록시가 SDP `m=audio`·MSE 오디오 코덱 제거 + 플레이어 `media=video`. 03 FR-018·05 E-18/J-04·06 T-015 통일 |
| 5 | HIGH | 03 미결정 "0건"이나 실측·라이선스 미결 | 타당 | 03에 **검증 대기 가정 4건** 절 신설(값은 결정됨, 착수 첫 작업에서 검증). 02 축 10 Clear→Assumed |
| 6 | HIGH | 07 — docker json-file 로그가 SD로 감 | 타당 | docker `data-root: /data/docker`(SSD). 02·04·07 전파 |
| 7 | HIGH | 03·05 — degraded·unknown 미정의 | 타당 | 03 FR-009에 4상태 정의(RTSP 실패=offline, ONVIF만 실패=degraded, 첫 프로브 전=unknown). 06 T-060·061 추가 |
| 8 | HIGH | 03·05 — 그룹 알림 1건↔이벤트 4건이 스키마로 불가 | 타당 | ERD `alert_event` 조인 + `alert.primary_event_id`, E-27 `event_ids[]` |
| 9 | HIGH | 03·07·06 — ALERT_DELIVERY_MAX_S 기산점 3종·SC-003 lab 행 없음 | 타당 | 기산점 = `event.ts`로 통일, 전원차단 기준은 합계(OFFLINE_DETECT_MAX_S + ALERT_DELIVERY_MAX_S). US-4·G3·SC-003·07 SLI 갱신, lab T-062 추가 |
| 10 | HIGH | 06 T-004 403 slug 부재 · FR-019 항목 수 불일치 | 타당 | `csrf-header-required` 403 추가, 체크리스트 4항목(E-31/E-32/T-043) |
| 11 | HIGH | 04 A7-I "해당없음" 자기모순 · A4-S RTSP Digest 해시 유출 | 타당 | A7-I를 Accept(사유)로 + 비밀 회전 절차(07). RTSP 프로브를 **무인증 DESCRIBE**(401도 생존)로 바꿔 해시 유출 자체를 제거 |
| 12 | HIGH | 03·04·06 — PTZ keepalive 감사 600행/분 | 타당 | 감사는 누름·뗌·프리셋 단위, keepalive 제외 |
| 13 | HIGH | 05·07 — 03 밖 신규 숫자 근거 없음 | 타당 | 03 상수 표에 **운영 기본값** 하위 표 신설(근거 "관행·근거 없음" 명시), 05·07은 이름으로 참조 |
| 14 | MEDIUM | 스테일 텍스트 4곳 | 타당 | 04 A5-E·SD 마모 행, decision-log #16 주석, 07 docs/ 정정 |
| 15 | MEDIUM | T-044 빈 행·T-053/054 시뮬레이터 부재·07 SLI 결함·stale 타일 스냅샷 의존·PICAM_ENV 메커니즘·offline-images·SUB_STREAM_MAX 강제 불가·ERD P2 잔존 | 타당(부분) | T-044 삭제, sim-rtsp H.265 변형·sim-ntfy TLS 명시, SLI 재정의(프로브 성공률), stale=플레이어 마지막 프레임, 초과 뷰어=상태 목록, constants_override 메커니즘, offline-images 삭제, SUB_STREAM_MAX 권장값 표기. ERD의 clip·telegram·health_rollup은 P2 표기 유지(삭제하면 P2 착수 시 재설계) |

## 잔여 리스크 (재검토 미실시 — 핸드오프에서 확인)
1. 패치가 새 드리프트를 만들지 않았는지 사람이 재확인하지 않았다 — check_package는 통과했으나 실질 정합(특히 03 FR-009 4상태 ↔ 04 상태 기계 ↔ 06 T-024/060/061)은 SPEC 작성자가 대조한다.
2. 03 "운영 기본값" 14개는 근거가 약하다(관행). 운영 1개월 후 재설정 전제.
3. 검증 대기 가정 4건(03) — 첫 작업 1·3에서 해소. 실패 시 03 개정 → 하류 전파.
4. 위협모델 Accept 3건(RTSP 평문·기기 물리 탈취·SD 비밀 평문)은 "소규모 사업장 1인 운영" 전제에서만 타당. 전제가 바뀌면 04 재검토 조건 발동.

## 핵심 결정 5줄
1. **Extend**: go2rtc 채택 + 등록→라이브→제어→상태→알림 얇은 층만 자체 구현. 객체 감지는 Frigate 영역(non-goal).
2. Pi 5 4GB + USB SSD, 카메라 ≤ MAX_CAMERAS(4), H.264 sub-stream 라이브, 허브 트랜스코딩 없음(INV-4).
3. hub-api·go2rtc 둘 다 host 네트워크, go2rtc는 localhost API + WebRTC 8555만, 시그널링은 hub-api 프록시(오디오 필터 포함), 원격은 Tailscale.
4. 소규모 사업장(공개 장소) 법 기준 내장: 보관 상한 RETENTION_MAX_DAYS(30) 하드코딩, 오디오 없음, 운영 체크리스트.
5. 자가 복구 = 내부 감시 exit(1) + restart + HW watchdog; 알림은 ntfy + 로컬 큐 + 데드맨 하트비트.

## 질문 → 가정 채택 목록 (오토파일럿, 전부 무응답)
Q1 장소=소규모 사업장 기준 · Q2 HW=Pi 5 4GB+SSD · Q3 녹화=P1 스냅샷/P2 클립 · Q4 원격=Tailscale · Q5 카메라 ≤4·H.264 sub. 사용자가 답을 바꾸면 02 "반영 기록"의 전파 경로대로 03 개정.

## 구현 핸드오프 (service-prompt-workflow SPEC 입력)
/service-prompt-workflow 로 다음을 실행:
<inputs>eval/runs/run-20260907-smoke/s4-rpi-ip-camera/03-prd.md (요구사항·불변식·상수 표·검증 대기 가정), 05-api-contract.md (엔드포인트·문제 유형·ERD·커버리지), 08-readiness-report.md (이 문서 — 선행 조건·첫 작업 3개)</inputs>
<references>04-architecture.md, 06-test-design.md, 07-ops-design.md — 필요할 때만 읽는다 (07 "착수 자산" 절은 첫 작업 시 읽는다)</references>
<preconditions>
1. 08 "잔여 리스크" 1을 SPEC 작성 중 대조 (FR-009 4상태 ↔ 04 상태 기계 ↔ 06 T-024/060/061)
2. 첫 작업 1에서 03 "검증 대기 가정" 4건 해소 — 실패 시 03 개정 후 05·06·07 `기준 03 v` 갱신 (규칙 9)
3. 실기기 lab(T-045·048·062)은 첫 작업 3에서 — MAX_VIEW_SESSIONS·MSE_MAX_STREAMS·HUB_API_MEM_LIMIT_MB 검증
</preconditions>
<first_task>SPEC.md 작성 — 위 문서를 진실원으로, 낯선 구현자 실행 가능 수준(≥7/10). 상수는 `picam/constants.py`에 03과 같은 이름으로</first_task>
<then>superpowers 설치 시 `superpowers:writing-plans` → 07 "첫 작업 3개"(워킹 스켈레톤: ①등록→스트림→그리드 ②헬스→알림→복구 ③PTZ+lab)부터. brainstorming은 생략 — 이 프롬프트를 붙여 넣은 것이 설계 승인이다.</then>
UI 포함 — BUILD·REVIEW에서 frontend-design-taste dial=관제/대시보드(DENSITY 8 · MOTION 2 · VARIANCE 3), 상태 4종(빈/로딩/에러/stale) 필수, 폰트·JS 로컬 서빙(CDN 0)
<model_hints>
opus: FR-001(세션·IP 잠금·CSRF), FR-005/E-18(WS 프록시 + SDP/MSE 오디오 필터 + 뷰어/MSE 상한), FR-007(PTZ 스로틀·hold·Timeout), FR-009/J-01(병렬 프로브·4상태 기계·내부 감시 exit(1)·reconcile), FR-010/J-02(dedup·그룹핑 alert_event·큐), INV-3(AES-GCM·로그 마스킹), INV-6(감사 트리거), 05 DDL
sonnet: FR-002~004·008 CRUD, FR-011 스냅샷, FR-013·019 설정 화면, FR-012 SSE, FR-015 RetentionJob, 06 RED 시나리오·시뮬레이터(sim-camera/sim-rtsp/sim-ntfy), 07 compose·Dockerfile·install.sh·render.sh, 그리드/상세/설정 템플릿
haiku: 해요체 문구 일괄, 문제 유형 detail 템플릿, 로그 필드명, README, `.env.example` 주석
</model_hints>  (분류 기준: references/model-routing.md 하단 표)
