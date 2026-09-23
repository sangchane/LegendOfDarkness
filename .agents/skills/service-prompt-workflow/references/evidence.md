# 근거 매핑 (Evidence Map)

이 워크플로우의 모든 규칙·단계는 아래 1차 출처에서 왔다. "인기 있어서"가 아니라
**여러 권위 출처가 수렴**할 때만 규칙으로 승격했다(solution-planner ETHOS와 동일 기준).

## 출처와 권위

| 출처 | 유형 | 권위 신호 | 제공한 것 |
|---|---|---|---|
| **gstack** (garrytan/gstack) | 오픈소스 하네스 | Garry Tan(YC CEO), ~120k★, MIT | 스프린트 모델, 페르소나 phased skill, hard gate, forcing question, completion enum, 핸드오프 아티팩트, 라우터, 품질 게이트, ETHOS 프리앰블, User Sovereignty |
| **Anthropic — Prompt engineering** | 1차 벤더 문서 | 대상 모델 제작사 | 명시성, 이유 제공, 예시(few-shot), XML 구조화, 역할, CoT, "하지마 대신 해라" |
| **Anthropic — Claude Code best practices** | 1차 도구 문서 | 대상 도구 제작사 | Explore→Plan→Code→Commit, 루프 닫기(증거), TDD Writer/Reviewer, CLAUDE.md, 인터뷰→SPEC.md, 컨텍스트 위생, 적대적 리뷰 |
| **Anthropic — Building effective agents / Context engineering** | 1차 엔지니어링 | 널리 인용 | 단순함 우선, 워크플로우 5패턴(chaining/routing/parallel/orchestrator/evaluator-optimizer), just-in-time 컨텍스트, 서브에이전트 |
| **OpenAI — Prompt engineering guide** | 1차 벤더 문서 | 업계 표준 레퍼런스 | 6전략(명확한 지시·참조 텍스트·작업 분할·생각할 시간·외부도구·체계적 테스트) |
| **GitHub spec-kit** | 오픈소스 | GitHub 공식, ~118k★ | Spec-Driven Development: constitution→specify→clarify→plan→tasks→analyze→implement, "명세가 진실원", test-first Article III, 작은 증분 |
| **Harper Reed — LLM codegen workflow** | 실무자 워크플로우 | Simon Willison 등 인용, 사실상 커뮤니티 표준 | idea honing(한 번에 한 질문)→prompt_plan.md+todo.md→실행 |
| **promptingguide.ai** (dair-ai) | 기법 색인 | ~76k★ | 기법 카탈로그(zero/few-shot, CoT, self-consistency, ReAct, RAG, reflexion) |
| **MengTo/Skills** | 스킬 라이브러리 | Design+Code 창립자, ~1.1k★ | SKILL.md 포맷(Use When/Workflow/Guardrails/Avoid), "prompts are assets", 프론트 slop 체크리스트, design dial |
| **f/awesome-chatgpt-prompts** | 프롬프트 모음 | ~165k★ | 역할/페르소나 프롬프트 대중화 |
| **DietrichGebert/ponytail** | 오픈소스 스킬·플러그인 | 121.6k★ (2026-09-03), MIT, v4.9.0, 자체 에이전틱 벤치마크(LOC −54%·비용 −20%·시간 −27%·안전 100%) | BUILD `<ladder>` 결정 사다리 7단, REVIEW의 과잉설계 전용 패스(`ponytail-review` 형식), "검증 하나는 남긴다" |
| **obra/superpowers** | 오픈소스 스킬 플러그인 | 280.8k★ (2026-09-03), MIT, v6.3.0 설치 2026-09-07 | PLAN 이후 실행 엔진: writing-plans(2~5분 작업·자리표시자 금지), executing-plans / subagent-driven-development, test-driven-development, systematic-debugging, verification-before-completion, requesting/receiving-code-review, finishing-a-development-branch. 이 스킬의 4)~8) 템플릿은 미설치 시 대체용으로 강등 |
| **affaan-m/everything-claude-code** (ecc) | 스킬 마켓플레이스 | 246.4k★ (2026-09-03), 설치 v1.10.0 | `skill-routing.md`의 단계별 1순위 스킬(product-lens·blueprint·tdd-workflow·verification-loop·git-workflow 등) |
| **Anthropic — Skill authoring best practices / agentskills.io** | 1차 벤더 문서·오픈 스펙 | 대상 도구 제작사 | `metadata`·`argument-hint` 프론트매터, 시간 민감 수치는 근거 파일로, `evals/evals.json` 형식 |
| **AGENTS.md 표준** | 오픈 규약 | Linux Foundation, >20k repos | 저장소 지침 파일 규약(CLAUDE.md 동종) |
| **Anthropic `claude-api` 번들 스킬** (Claude Code 2.1.259, 모델표 캐시 2026-06-24) + Agent 도구 `model` 파라미터 | 1차 벤더 문서 · 도구 스키마 | `references/model-routing.md`: 작업 클래스→등급 표, "캐스케이드 전에 최상위 모델+낮은 effort", "완료된 작업당 비용", 캐시는 모델 단위 |

## 13개 공통분모 원칙 → ETHOS 매핑

수렴 출처가 3개 이상인 것만 규칙으로 승격했다. 0.4.1(2026-09-08)부터 SKILL.md에는 8·4·6만 ETHOS 1·2·3으로 남는다.
나머지는 Fable 5.1에서 학습된 기본값이거나(1·2·3·5·9) superpowers·ponytail이 실행 시점에 주입한다(7·10).
Anthropic 번들 `claude-api` 스킬의 prompt-audit 기준("모델이 이미 아는 것은 지시가 아니라 비용")에 따라 뺐다. 아래 번호는 원래 번호다.

1. 명시·구체 — Anthropic, OpenAI, Claude Code, Brex
2. 이유(why) — Anthropic, OpenAI, Claude Code
3. 탐색/계획과 구현 분리 — Claude Code, spec-kit, Harper Reed, OpenAI
4. 명세·계획 파일화 — spec-kit, gstack, Harper Reed, Claude Code
5. 작게 쪼개 개별 검증 — OpenAI, spec-kit, prompt chaining, Harper Reed
6. 루프 닫기(증거) — Claude Code, OpenAI, evaluator-optimizer, spec-kit
7. 테스트 우선 — Claude Code, spec-kit Article III
8. 사용자 주권(한 번에 하나) — gstack, Claude Code
9. 컨텍스트 위생 — Claude Code, Context engineering
10. 단순함 우선 — Building effective agents, spec-kit

+ 구조화(XML/구분자), 역할/페르소나, 예시(few-shot), 사고 유도(CoT), 근거+독립 리뷰는
프롬프트 작성 기법으로 각 단계 템플릿에 반영.

## Anthropic Fable 5.1 공식 지침 반영 (2026-09-09)

출처: Claude Code 번들 `claude-api` 스킬 `shared/model-migration.md` "Migrating to Claude Fable 5.1" 두 절.
- 반영: BUILD 템플릿에 scope/test-coverage 지침(요청 밖 수정·테스트 증식 억제). catch-up WORKLOG에 압축 요약 보존 항목.
- 반영 안 함(하네스가 이미 적용): 진행 상황 알림, 독립 도구 호출 묶음, 승인 작업 끝까지(autonomy), 대화 기록 추가 전용 — Claude Code 시스템 프롬프트가 같은 문장을 넣는다. 스킬에 다시 쓰면 중복.
- 보류(autopilot 실행 중이라 나중에): 긴 산출물은 effort high(`effort:` 프론트매터), 검토관·서브에이전트 effort 표, 진척 주장은 도구 결과로 대조.

## 주의(변동 사항)

- Anthropic의 고전 "프리필(assistant 턴 미리 채우기)" 기법은 **Claude 4.6+에서 미지원**.
  대체: 시스템 프롬프트 직접 지시 / 구조화 출력 / 도구 호출. 템플릿은 프리필을 쓰지 않는다.
- 스타 수·버전은 조사 시점(2026-07, ponytail·ecc·Anthropic 행은 2026-09-03) 값이며 규칙의 근거는 "수렴"이지 "인기"가 아니다.
