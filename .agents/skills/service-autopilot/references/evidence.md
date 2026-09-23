# 근거 매핑 — 무엇을 어디서 이식했고, 무엇을 왜 바꿨나

조사일: 2026-07-09 (웹 실측 — 스타 수는 GitHub API, 프로토콜은 원본 명령 프롬프트 파일 확인).
규칙: 여러 권위 출처가 수렴하거나 1차 출처(원문)가 확인된 것만 채택.

## 프레임워크 실측 (2026-07-09 기준, 2026-09-03 GitHub API 재확인 → 화살표 뒤 값)

| 프레임워크 | 스타 | 활성 | 이 스킬이 가져온 것 |
|---|---|---|---|
| [github/spec-kit](https://github.com/github/spec-kit) | 118.9k → 133.1k | 매우 활발 | /clarify 질문 프로토콜(5문항 상한·Impact×Uncertainty·객관식+추천), 9카테고리 택소노미, /analyze 교차검사(커버리지 0건 탐지), P1-only-MVP 스토리 원칙, "체크리스트는 요구사항의 유닛테스트" |
| [geekan/MetaGPT](https://github.com/geekan/MetaGPT) | 69.3k → 70.2k | 정체 (마지막 push 2026-01-21) | PRD 노드(목표≤3 직교·스토리 3~5·P0/P1/P2 풀), 시스템설계 노드(구현접근·classDiagram·sequenceDiagram), 태스크의 OpenAPI 산출, "Anything UNCLEAR" 필드 개념 |
| [bmad-code-org/BMAD-METHOD](https://github.com/bmad-code-org/BMAD-METHOD) | 50.3k → 52.6k | 매우 활발(v6) | fresh-context 적대적 리뷰("문제를 반드시 찾아라" + 거짓양성 필터 + 2회 수확체감), PASS/CONCERNS/FAIL 준비도 게이트, "각 문서가 다음 페이즈의 컨텍스트" |
| [buildermethods/agent-os](https://github.com/buildermethods/agent-os) | 5.0k | 보통 | 표준(컨벤션)을 스펙에 주입하는 3층 컨텍스트 관점 (→ 핸드오프 시 CLAUDE.md/BASE와 연결) |
| [ruvnet/claude-flow](https://github.com/ruvnet/claude-flow) | 63.6k | 매우 활발 | (채택 없음 — 메타 오케스트레이션이라 스코프 밖. SPARC 5단계는 spec-kit과 중복) |

**5개 전부 위협모델링·IaC·관측성 전용 단계 부재** → A4 위협모델·A7 OPS-DESIGN이 이 스킬의
고유 확장. 근거는 프레임워크가 아니라 실무 표준(아래)에서 직접 가져왔다.

## 실무 표준 이식

| 스킬 요소 | 출처 (원문 확인) |
|---|---|
| A4 문서 골격(Context/Goals·Non-goals/설계=트레이드오프/검토한 대안/Cross-cutting) | [Design Docs at Google](https://www.industrialempathy.com/posts/design-docs-at-google/) |
| A4 위협모델 4질문 프레임 | [Threat Modeling Manifesto](https://www.threatmodelingmanifesto.org/) |
| A4 STRIDE 6범주·대응 4택(Mitigate/Eliminate/Transfer/Accept)·trust boundary 절차 | [OWASP Threat Modeling Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Threat_Modeling_Cheat_Sheet.html) |
| A5 에러 포맷 Problem JSON·스택트레이스 금지·URL 버저닝 회피·커서 페이지네이션·스코프 규약 | [Zalando RESTful API Guidelines](https://opensource.zalando.com/restful-api-guidelines/) (#176/#177/#115/#160/#225) + [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) |
| A5 멱등성(클라 생성 키·≥24h 보관·최초 응답 재생) | [Stripe Idempotent Requests](https://docs.stripe.com/api/idempotent_requests) |
| A6 Given/When/Then 정의·AC는 개발 전 존재 | [Cucumber Gherkin Reference](https://cucumber.io/docs/gherkin/reference) |
| A6 피라미드·"가능한 한 아래층으로"·contract test 레이어 | [Fowler — Practical Test Pyramid](https://martinfowler.com/articles/practical-test-pyramid.html) |
| A7 SLI 유형별 표준·SLO 규칙(적게·100% 금지)·4 골든 시그널·"모든 알람은 조치 가능" | [Google SRE Book — SLO](https://sre.google/sre-book/service-level-objectives/) · [Monitoring](https://sre.google/sre-book/monitoring-distributed-systems/) |
| 블라인드스팟 P1(IoT) — SD 마모·전원·watchdog 15초 함정·OTA A/B·RTC 부재·X.509 신원 | [Mender — Pi in production](https://mender.io/blog/raspberry-pi-in-production) · [dzombak SD wear](https://www.dzombak.com/blog/2021/11/reducing-sd-card-wear-on-a-raspberry-pi-or-armbian-device/) · [AWS IoT Lens](https://docs.aws.amazon.com/wellarchitected/latest/iot-lens/) (IOTSEC 1·2, OTA·프로비저닝 원문) |
| 도메인 Lens 개념(도메인별 체크리스트가 공식 패턴이라는 근거) | [AWS Well-Architected Lenses](https://docs.aws.amazon.com/wellarchitected/latest/userguide/lenses.html) (IoT·SaaS·Serverless·ML 등 실존 + Custom Lens 공식 지원) |
| eval/ 전체(pairwise+스왑·binary 6차원·시드 10·해석 임계) | eval/PROTOCOL.md의 출처 절 참조 (MT-Bench·Anthropic·Hamel·ISO 29148) |

## 스킬 라우팅·형식 정렬 근거 (2026-09-03 추가)

| 출처 | 실측 (2026-09-03, GitHub API) | 이 스킬이 가져온 것 |
|---|---|---|
| [Anthropic — Skill authoring best practices](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices) · [anthropics/skills](https://github.com/anthropics/skills) skill-creator · [agentskills.io 스펙](https://agentskills.io/specification) | 173.3k★ | `metadata`(version/updated)·`argument-hint` 프론트매터, 본문에서 시간 민감 수치 제거(근거 파일로 이동), 참조 1단계 깊이, `evals/evals.json` 형식, description에 사용 시점 포함 |
| [Claude Code — Skills](https://code.claude.com/docs/en/skills) | 2.1.259 문서 | `when_to_use`·`context: fork`·`paths` 등 확장 필드 확인. `context: fork`는 미채택(아래 의도적 변경 6) |
| [affaan-m/everything-claude-code](https://github.com/affaan-m/everything-claude-code) (ecc) | 246.4k★, 설치 v1.10.0 (2026-04-09) | `references/skill-routing.md`의 1순위 스킬 대부분 (research-ops·product-lens·api-design·tdd-workflow·deployment-patterns·santa-method 등). 설치본이 5개월 구버전이므로 갱신 시 라우팅 재검사 |
| [DietrichGebert/ponytail](https://github.com/DietrichGebert/ponytail) | 121.6k★, MIT, v4.9.0, 마지막 push 2026-08-07 | 구현 단계(service-prompt-workflow BUILD·REVIEW)의 결정 사다리. 자체 벤치마크: 12개 기능 과제·n=4·Haiku 4.5에서 LOC −54%, 토큰 −22%, 비용 −20%, 시간 −27%, 안전 100% (`benchmarks/results/2026-06-18-agentic.md`) |
| [obra/superpowers](https://github.com/obra/superpowers) | 280.8k★, 2026-09-07 설치 v6.3.0 (공식 마켓) | **강도 lite/full**은 brainstorming의 spike/bounded/architectural 3등급 분류에서 착안. 구현 단계(PLAN 이후)는 service-prompt-workflow가 이 플러그인의 writing-plans·executing-plans·TDD·verification·code-review 스킬로 넘긴다. 설계 단계는 이 스킬이 담당(경계: `~/.claude/CLAUDE.md`) |
| 2026-09 스모크 런 (`eval/runs/run-20260903-smoke/RESULT.md`) | 자체 실측 | 규칙 9(개정 전파·상수 표), 규칙 10(`scripts/check_package.py`), A7 착수 자산, A3 화면 스케치, 예산·재개·비용 기록 규칙 — 전부 이 런의 GATE·judge가 잡은 결함에서 나옴 |
| Anthropic `claude-api` 번들 스킬 (Claude Code 2.1.259, 모델표 캐시 2026-06-24) + Claude Code Agent 도구 `model` 파라미터 | 1차 벤더 문서 · 도구 스키마 | `references/model-routing.md`: 등급표(모델 id·가격), "캐스케이드 전에 최상위 모델+낮은 effort를 먼저 재라", "완료된 작업당 비용", 캐시는 모델 단위 → 등급 전환 지점을 3곳으로 제한 |

## 실전 실측 (2026-09-08, 한삼국 전략 full 런 → 다른 세션에서 구현 착수)

| 맹점 | 스킬 원인 | 반영 (v1.4.0) |
|---|---|---|
| 엔진(Godot) 답이 하루 만에 웹·아이폰으로 뒤집힘 | 해법(엔진)을 물었지 사용 맥락을 안 물음, 추천이 앵커링 | 공통 축 11 사용 맥락, 환경 질문은 맥락 먼저·되돌림 비용 표기 |
| "한국 삼국시대"를 4~7C로 못 박음 | A0가 해석을 조용히 확정 | A0 해석 목록 + 최종 보고 첫 절 + 질문 1칸 예약 |
| 24도시 미세 관리·실시간 전투가 폰 세션에 과함 | 코어 루프 가정이 맥락과 미대조 | P7 코어 루프 결정 수·자동위임 기본값 |
| 와이어프레임 먼저 → 폐기 | 산출물 순서 원칙 없음 | P7 산출물 순서(엔진→시뮬→그레이박스), 와이어프레임은 버릴 스케치 |
| 전투·징병 상수 전부 창작 | RECON이 계산식·매뉴얼 확보를 안 물음, 규칙 1이 구현 상수에 미적용 | A1 참조 제품 규칙 상세, A5 `source` 필드, 임의값 비율 |
| 인물 120명 목표 vs 사료 16명 | 미확인 조사 위에 상수 | A1 데이터 공급 표본 10건, C7(미확인 상수는 SC 금지) |
| 08 없이 세션 이동 → RESUME.md 발명 | 재개 포인터가 GATE 끝에만 | 단계마다 NEXT.md, 대규모 개정은 REVISIONS.md + 배지(C8) |
| full이 과함(탐색 단계) | 강도 표에 미확정 조건 없음 | spike 강도 |

## 의도적 변경 (출처와 다르게 한 것 — 이유 명시)

1. **질문 배치 1회** vs spec-kit의 "한 번에 정확히 1문항씩 순차": spec-kit은 대화형 CLI라 순차가
   자연스럽지만, 이 스킬의 1순위 요구는 **개입 최소화**(사용자 결정)다. 총량 5문항 상한·객관식·
   추천 기본값·즉시 반영은 그대로 유지하고 제시만 배치로 바꿨다. 트레이드오프: 앞 답변이 뒤 질문을
   바꾸는 연쇄는 포기 — 대신 충돌 시 GATE가 잡는다.
2. **MetaGPT 경쟁 분석(5~7개)·쿼드런트 차트 미채택**: RECON의 유사 솔루션 3~5개로 충분하고,
   쿼드런트는 장식성 높음(범위 절제 차원과 충돌). 필요 시 RECON에서 선택 적용.
3. **BMAD advanced elicitation(사용자가 렌즈 지정) 미채택**: 개입을 늘리는 방향이라 목적과 반대.
   같은 효과(다각 검토)는 GATE의 적대적 검토가 자동으로 수행.
5. **라우팅은 정적 표 + 스캔 스크립트, 동적 스캔 아님**: 매 단계 200개 넘는 description을 훑으면 토큰 비용과
   선택 흔들림이 크다. 표는 사람이 근거를 달아 유지하고, `_tools/skill_catalog.py`가 설치 여부·미배정만 기계적으로 검사한다.
6. **`context: fork` 미채택**: GATE·REVIEW의 fresh-context 검토는 Agent 도구 서브에이전트로 이미 구현돼 있고,
   파이프라인 내부 단계를 별도 스킬로 쪼개야만 `context: fork`를 쓸 수 있어 파일 수만 는다.
4. **OpenAPI 전문 생성 안 함**: MetaGPT는 태스크 단계에서 Full OpenAPI 3.0을 뽑지만, Google
   디자인독 원칙("형식 정의 전문 복붙 금지, 스케치만")과 충돌. 설계 단계는 스케치, 전문은 구현
   단계(service-prompt-workflow BUILD)로 미룸.

## 규칙의 실측 유래 (SKILL.md 본문에서 옮김, 2026-09-08)

| 규칙·항목 | 유래 사건 |
|---|---|
| 규칙 9 개정 전파·상수 표 | 2026-09 스모크 S4: 03 v1.1이 04~07에 미전파돼 세션 상한이 4·8·16 세 값으로 공존 |
| 규칙 10 스크립트 검증 | 2026-09 스모크 S4·S8: "누락 0"·"미결정 0건" 자기 선언이 거짓 (FR-026·SC-009·013 누락, 전화 수신자 미정) |
| A3 화면 스케치 · A7 착수 자산 | 2026-09 스모크 S1: 쉬운 시드에서 실행가능성 항목(화면 목업·디렉터리·.env 스케치) 부재로 패배 |
| spike 강도 | 2026-09 한삼국: 법·하드웨어 신호로 full을 돌렸지만 탐색 단계였고 엔진·범위가 하루 만에 뒤집힘 |
| 강도 실측 토큰 | S1 lite 20만 (검색 5·스킬 호출 11·도구 호출 48·서브에이전트 0·문서 74KB). S4 full 약 70만 (메인 37.5만 + 조사 sonnet 11.6만 + 검토 5만 + GATE 16.7만, 3.5시간) |
| 5시간 사용량 창 환산 (2026-09-08 트랜스크립트 실측) | 한도가 걸린 창 8개의 소비량은 API 환산 약 180~300달러. S4 full + 판정 6회 = 창 1개. 그 창 비용의 약 8할이 Opus 판정관·GATE의 캐시 재읽기 |

## 2026-09-08 프롬프트 감사 (Fable 5.1 맞춤)

기준: Anthropic 번들 `claude-api` 스킬 `shared/prompt-audit.md`·`shared/model-migration.md` "Migrating to Claude Fable 5.1"
("이전 모델용 스킬은 너무 처방적이라 품질을 떨어뜨린다. 각본 뺀 버전과 A/B 하라. 맥락·검증 지시·경계는 남긴다").
- 뺀 것: "절대 규칙"·"반드시"·"필독" 같은 압력 어조, 규칙 안의 사건 서술(→ 위 표), 템플릿 안의 교육 문장(테스트 피라미드·SRE 원칙·출처 목록),
  실행 절차 1~3의 파이프라인 표 중복, prompt-workflow ETHOS 1·2·3·5·7·9·10, FRAME 템플릿의 "YOU MUST 표기" 지시.
- 남긴 것: 택소노미, 라우팅·모델 표, 질문 프로토콜, check_package, 형식 계약(ID·버전 줄·상수 표 열), GATE fresh-context 검토, 예산·재개.
- 훅: ponytail 서브에이전트 주입을 구현 타입으로 한정(`PONYTAIL_SUBAGENT_MATCHER`), 검토·판정은 `fresh-reviewer` 타입. 죽은 suggest-compact 훅 제거.
- A/B 결과(`eval/runs/run-20260908-trim/RESULT.md`): 1라운드 1.4.1이 4패 2무 → 패치 라운드를 같게 준 재대결은 종합 tie, 1.4.1이 완전성·근거성 승, 범위 절제 패.
  각본 제거는 무해로 확정. 결정 변수는 GATE 뒤 패치 라운드 유무. 남은 문제는 A0 해석의 범위 팽창.
- 1.4.3(2026-09-09): 실행 절차 2에 "CONCERNS + HIGH ≥1 → 패치 1회 후 마감", A0·질문 프로토콜에 "확장 해석은 축소 기본값 + 질문 1칸".
  근거는 위 A/B 두 라운드(같은 문서가 패치 유무로 4패↔무승부, 범위 절제는 두 judge 4회 전부 확장 해석을 감점).
- 2026-09-09 추가 반영(Anthropic Fable 5.1 가이드): SKILL·fresh-reviewer `effort: high`(긴 산출물은 xhigh보다 high), model-routing effort 규칙,
  GATE 검토관에 "수량 주장은 확인한 것만". 실측 사실: 서브에이전트 컨텍스트 상한 20만 토큰(재개 시 58만 토큰 대화가 압축됨), 한도 리셋 명령은 이 계정에서 대상 아님.

## 이 파일의 용도

스킬 규칙·템플릿을 바꾸려면: ① 바꿀 항목의 출처를 여기서 확인 ② 새 근거(수렴 출처)를 확보
③ eval/ 동결 시드로 회귀 평가 ④ 이 표와 버전을 갱신. 근거 없는 변경 금지.
