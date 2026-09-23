# 준비도 리포트 — RoomBook (사내 회의실 웹 예약)

검토일 2026-09-03 · 모드: 오토파일럿(무응답, Q1~Q5 추천안 Assumed) · 스모크 런(`eval/PROTOCOL.md`) — 예산 제약: GATE 검토 1회, 패치 1회, 재검토 없음

## 검토 방식
- **A4 독립 검토**: `ecc:architect` 서브에이전트 1회 (03·04 입력) → 8건 지적, 7건 반영·1건 부분 반영 (DL-008)
- **GATE 적대적 검토**: fresh-context `general-purpose` 서브에이전트 1회 (00~07 + decision-log 입력, stage-templates GATE 프롬프트) → 원판정 **FAIL** (CRITICAL 1 · HIGH 8 · MEDIUM 6 · 참고 6)
- **타당성 필터**: 거짓 양성 0건 — 모든 지적이 문서 근거를 갖는 실제 결함이었다. 패치 1회로 대응 (DL-009). `ecc:santa-method`(독립 리뷰어 2명)는 돈·안전·법 도메인이 아니라 미적용.

## GATE 발견과 처리
| # | 심각도 | 지적 (문서:섹션) | 처리 | 반영 위치 |
|---|---|---|---|---|
| 1 | CRITICAL | 04·07 — Drizzle·Node 22·Caddy·age·gitleaks·k6/autocannon·Mailpit 근거 없음, OWASP 인용 원문 없음 | **반영** — Drizzle(공식 custom-migration 문서 + EXCLUDE 미지원 이슈), Node 24 LTS(endoflife.date) URL·확인일 추가, k6 단일 확정, 나머지는 "커모디티 — 근거 생략" 명시, OWASP는 "블로그 2건 교차"로 정정 | 04 구현 접근·근거, 07 런타임 |
| 2 | HIGH | 03 INV-3·05 DDL vs 05 보존·06 TS-031 — 감사 로그 삭제 불가와 1년 파기가 동시에 참일 수 없음 | **반영** — `roombook_retention` 롤 + `RETENTION_DATABASE_URL`, INV-3 "앱 경로 불가, 파기 롤 예외" | 03·04·05·06·07 |
| 3 | HIGH | 03 FR-001·05 — 직원 계정 생성 경로 없음 | **반영** — 허용 도메인 첫 요청 시 member JIT 생성, `PATCH /api/me {name}` | 03·04 시퀀스1·05·06 TS-010·07 |
| 4 | HIGH | 03 EC-C3·06 TS-019 — 반납 직후 14:20 재예약이 15분 경계 규칙에 걸림 | **반영** — 14:15~15:00으로 정정, 14:20 요청은 422 단언 추가 | 03·06 |
| 5 | HIGH | 03 "오늘" vs 04/05 "24h" — 대안 슬롯 탐색 창 불일치 | **반영** — "요청 start_at 이후 당일 운영시간 종료까지 전방 탐색"으로 통일 | 03·04·05 |
| 6 | HIGH | 04 ④#3·05 SESSION·07 — admin 2h 세션이 장기 메일 장애 시 EP-18 우회를 무력화 | **반영** — 세션 불필요 CLI `issue-link`(VM 셸, 감사), FR-001·TS-011에 admin 2h 명시 | 03·04·06·07 |
| 7 | HIGH | 05 EP-09/10 vs 06 TS-053 — 에러 slug 드리프트 | **반영** — TS-053 두 단언 분리, EC-C6 slug 병기 | 03·06 |
| 8 | HIGH | 07 — 알람 평가·발송 주체, 지표 저장소, TLS 종단 미결정 | **반영** — `alert-check.sh` 1분 cron + `/api/health` 5분 롤링 필드, Caddy 컨테이너 확정(compose 4개) | 07·02 |
| 9 | HIGH | 02 — 하류 문서에서 생긴 Assumed 미등재 | **반영** — 7행 추가(GDPR·브라우저·중앙 로그·TLS·알림 채널·관리자 감사 범위·JIT) | 02·03 가정 목록 |
| 10 | MEDIUM | 04 vs 05 — "관리자 행위 전부 감사" 주장이 스키마와 불일치 | **반영** — "예약 관련만, admin_audit P2"로 축소 | 04·02 |
| 11 | MEDIUM | 04 STRIDE — 미대응 위협(메일 변조)·이유 없는 해당없음 5셀·평문 SMTP·스캐너 링크 선소비 | **반영** — STARTTLS Transfer 행, 5셀 이유 기재, verify 2단계(GET 확인 → POST 소비), EC-A5/TS-059 | 03·04·05·06·07 |
| 12 | MEDIUM | decision-log·02 — 스테일 결정 기록(DL-001 포트, DL-005 컨테이너 수, 이메일 3종, 20건 상한) | **반영** | decision-log·02 |
| 13 | MEDIUM | 02·04·07 — SES 폴백이 "사내 보관" 근거와 충돌 | **반영** — 처리위탁 검토를 전제조건으로 명시 | 02·04·07 |
| 14 | MEDIUM | 03 FR-015 — 퇴사자 미래 예약 처리 미정 | **반영** — `admin_cancel` "퇴사 처리" + EC-C7/TS-058 | 03·05·06 |
| 15 | MEDIUM | 05 — 규모 대비 과설계(Idempotency-Key·커서 페이지네이션·미디어타입 버저닝·스코프 체계) | **부분 반영** — 페이지네이션 EP-16·17로 축소, 스코프는 문서 표기용으로 축소. Idempotency-Key·버저닝 규약은 유지(템플릿 규약, 재시도 이중 예약 방지) | 05 |
| 참고 | LOW | PolicyValidator→DB 엣지, EP-02 공개 목록, audit→push 순서, 복원 리허설 월 1회 | **반영** | 04·05·07·02 |
| 참고 | LOW | EC 번호 순서, DL-006 위치, yarooms "검색 요약 인용" 표기 | 보류(정보 손실 없음) | — |

## 패치 후 정합성 카운트 (자체 확인 — 재검토 미실시)
| 항목 | 결과 |
|---|---|
| P0/P1 FR → 05 엔드포인트/잡 매핑 | 15/15 (FR-001·011·015 갱신) |
| SC → 06 시나리오 | 8/8 (SC-004·005는 운영 점검 위임 명시) |
| EC → 06 시나리오 | 22/22 (EC-A5·C7 추가분 포함) |
| STRIDE 셀 | 36/36 검토, "해당없음"에 전부 이유 기재, ②→③ 미대응 위협 0 |
| 02 register 마킹 | 31/31, 미마킹 0 |
| 07 로그·백업·복구 "어디에·얼마나·어떻게" | 답변됨 (로그: stdout JSON 20MB×7 + 중앙 30일 / 백업: pg_dump 일 1회, 로컬 7일 + 오프사이트 30일 암호화, 복원 리허설 월 1회 / 복구: 시나리오 5 + RB-02 골격) |
| 미결정(03) | 0건 |
| 측정 불가 표현(03 FR/SC) | 0건 |

## 판정: **CONCERNS**
사유:
1. 검토관 원판정이 FAIL이었고, 패치는 1회 적용됐으나 **재검토를 돌리지 않았다**(스모크 런 예산). 패치가 새 드리프트를 만들지 않았는지는 자체 확인뿐이다 — 구현 SPEC 단계에서 03↔05↔06 교차 대조를 첫 작업으로 둔다.
2. YAGNI 지적 1건(#15)을 부분 반영 — Idempotency-Key·미디어타입 버저닝은 템플릿 규약을 우선했다. 구현자가 규모를 근거로 제거해도 설계는 성립한다.
3. 오토파일럿 특유의 잔여 리스크(아래)가 실제 조직 환경 확인 전까지 남는다.

## 잔여 리스크 (Assumed 무응답 + 하류 가정)
| 우선 | 가정 | 틀리면 | 확인 방법 (5분) |
|---|---|---|---|
| **1** | Q1 캘린더 플랫폼 없음 | **조직이 M365/Google Workspace를 쓰면 이 패키지는 폐기** — room mailbox/자원 캘린더 설정으로 끝난다 (01-recon Adopt) | IT에 "Outlook/Google 캘린더로 회의실 초대가 되나요?" 한 줄 질문 |
| 2 | Q2 사내 IdP 없음 → 매직링크 | IdP가 있으면 OIDC 즉시 연동이 더 낫다(FR-019 P2 → P1 승격, 매직링크·JIT 삭제) | SSO 로그인하는 사내 시스템이 있는지 |
| 3 | Q5 사내 VM·인터넷 가능 | 폐쇄망이면 매직링크 자체가 불가 → Q2 재결정 | 서버가 사내 메일 릴레이·레지스트리에 닿는지 |
| 4 | Q3 제목 전사 공개 | 임원 회의 등 민감 제목 노출 민원 → "비공개" 토글(P2) 조기 도입 | 총무에게 사례 확인 |
| 5 | Q4 노쇼 대책 P2 | 노쇼 민원이 4주 내 급증하면 FR-016 체크인·자동 해제 앞당김 | 파일럿 4주 후 총무 접수 대장 |
| 6 | 하류 7건(02 "하류 신규" 행) — GDPR 비대상·Chrome/Edge·중앙 로그·사내 CA·웹훅 메신저·관리자 감사 범위·JIT | 각각 07 대체 경로 명시됨. 사내 CA 없으면 Caddy 대신 사내 프록시에 TLS 위임 | IT 확인 |
| 7 | 팀에 TypeScript 인력 ≥1 | Python 팀이면 FastAPI+React 대안(04 검토한 대안)으로 교체 — 계약(05)·테스트(06)는 그대로 유효 | — |

## 핵심 결정 5줄
1. 독립 웹앱 구축(캘린더 미연동) — 단, M365/Google 보유 시 No-go (DL-001)
2. 이중 예약은 PostgreSQL `EXCLUDE USING gist` 제약으로 구조적 차단, 충돌 시 당일 내 대안 슬롯 제안 (INV-1, DL-008)
3. 비밀번호 없는 매직링크 + JIT 계정, 2단계 소비(GET 확인 → POST), 세션 member 8h/admin 2h (DL-002, DL-009)
4. 즉시 확정·전사 공개·승인 없음, 노쇼 대책은 조기 반납만(체크인 P2) (DL-003·004)
5. 사내 VM Compose 4컨테이너(caddy·app·worker·db), 트랜잭셔널 아웃박스, 근무시간 외 배포, 화이트보드 폴백 (DL-005·007·009)

## 구현 핸드오프 (service-prompt-workflow SPEC 입력)
/service-prompt-workflow 로 다음을 실행:
<inputs>autopilot/s1-meeting-room-booking/03-prd.md, 04-architecture.md, 05-api-contract.md, 06-test-design.md, 07-ops-design.md (+ 02-blindspot-register.md 가정 목록, decision-log.md DL-001~009)</inputs>
<first_task>SPEC.md 작성 — 위 문서를 진실원으로, 낯선 구현자 실행 가능 수준(≥7/10). 착수 전 확인 2가지: (a) 회사가 M365/Google Workspace 캘린더를 쓰는가 → 쓰면 중단·설정 경로, (b) 03↔05↔06 교차 대조(FR-ID·에러 slug·정책 숫자·상태값)로 GATE 패치 후 드리프트 0 확인</first_task>
<constraints>INV-1~5 불변, RED→GREEN 체크포인트(06 원칙), P0/P1만 MVP, FR-016~020 착수 금지</constraints>
UI 있음 → BUILD·REVIEW에서 frontend-design-taste dial = VISUAL_DENSITY 6 · MOTION_INTENSITY 3 · DESIGN_VARIANCE 3 (제품/앱 UI 프로파일), UX-01~05 기준, 해요체·T-08 CTA 라벨 적용
