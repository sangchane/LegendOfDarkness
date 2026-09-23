# 단계별 모델 라우팅 (A0 ~ GATE)

각 단계를 **어느 등급의 모델이 수행할지** 정하는 정적 표. `skill-routing.md`가 "어떤 전문 스킬을 부르나"를 정하듯,
이 표는 "그 일을 어느 모델에 맡기나"를 정한다.

## 전제 — 모델을 바꾸는 유일한 수단

메인 세션은 사용자가 고른 모델(`/model`)로 돈다. 스킬 프론트매터 `model:`·`effort:`로 **그 턴 전체**의 모델을 바꿀 수는 있지만
(Claude Code 확장 필드, 다음 프롬프트에서 원복), 파이프라인 한 번이 한 턴 안에서 다 돌기 때문에 단계별로 달리할 수 없고
세션 모델보다 낮은 등급을 강제할 이유도 없어 채택하지 않는다. 단계별로 모델을 달리하는 수단은 **서브에이전트의 `model` 파라미터**다
(Claude Code Agent 도구: `haiku` · `sonnet` · `opus` · `fable` 별칭. 별칭이 가리키는 실제 버전은 Claude Code 릴리스에 따라 바뀐다 —
`claude --version`과 `/model`로 확인). 따라서 "모델 선택" = "그 단계를 어느 등급 서브에이전트에 위임하나"이며,
위임하지 않는 단계는 세션 모델로 돈다.

## 등급표

가격·모델 id는 번들 `claude-api` 스킬의 캐시(2026-06-24)에서 옮겼다. 변동 수치이므로 갱신 시
Anthropic 가격 페이지(`claude-api` 스킬 `shared/live-sources.md`의 Pricing 행)로 재확인한다.

| 등급(별칭) | 모델 id (확인 2026-09-04) | 입력 / 출력 $ per 1M tok | 이 파이프라인에서 맡기는 일 |
|---|---|---|---|
| `haiku` | `claude-haiku-4-5` | 1 / 5 | 판단이 없는 정형 작업 — 형식 변환, 목록 채우기, 명령 실행·결과 보고 |
| `sonnet` | `claude-sonnet-5` | 2 / 10 | 근거 수집·긴 원문 읽기·템플릿 충실도가 높은 초안 |
| `opus` | `claude-opus-5` | 5 / 25 | 설계 판단·트레이드오프·적대적 검토. eval judge(생성과 다른 모델) |
| `fable` | `claude-fable-5-1` | 미확인 — 캐시(2026-06-24)에 없음, 가격 페이지로 확인 | 최상위 등급(Opus 위, 2026-09-01 출시). 세션이 Fable이면 GATE·독립 검토도 이 등급 |
| (세션 모델) | 사용자가 고른 것 | — | 기본값. 위임하지 않는 단계 전부 |

## 라우팅 표

| 단계 | 실행 위치 | 모델 | 왜 |
|---|---|---|---|
| A0 SEED | 메인 | 세션 | 10줄 정규화에 서브에이전트 왕복이 더 비싸다 |
| A1 RECON (조사) | 서브에이전트 1명 | `sonnet` | 검색 10~15회 + 긴 원문 읽기 = 토큰 대량, 요구되는 판단은 "출처가 있나·교차 확인됐나"뿐. 메인 컨텍스트를 아낀다. 결과는 `01-recon.md` 초안으로 받고 메인이 fit 판정을 덧붙인다 |
| A1 스택 후보 fit | 메인 | 세션 | 사용자 제약(팀·환경·규모·예산) 매칭은 판단 |
| A2 INTERROGATE | 메인 | 세션 | 질문 5개 선정(Impact×Uncertainty)이 파이프라인에서 가장 비싼 결정. 낮추지 않는다 |
| A3 PRD · A4 ARCHITECT · A5 CONTRACT | 메인 | 세션 | 문서 간 ID·용어·정책 숫자 정합이 생명. 위임하면 드리프트가 생기고 GATE HIGH의 다수가 그 드리프트다 |
| A3~A7 진입 사전조사 (검색 ≤3회) | 메인 (기본) | 세션 | 3줄이면 왕복 비용이 더 크다. 검색이 3회를 넘게 생기면 그 조사만 `sonnet`에 위임 |
| A4 독립 검토자 (`ecc:architect`) | 서브에이전트 | 세션 등급 이상 (`fable` 세션이면 `fable`, `opus` 세션이면 `opus`) | 독립 판단은 생성 모델보다 낮은 등급이 잡지 못한다 |
| A6 TEST-DESIGN · A7 OPS-DESIGN | 메인 (기본) | 세션 | 템플릿 충실도가 높은 단계. **컨텍스트가 60%를 넘었으면** 03~05를 입력으로 주고 `sonnet` 서브에이전트에 초안을 맡기고, 메인이 ID·slug·정책 숫자 정합만 검사한다 |
| GATE 검토관 | 서브에이전트 (fresh context, `fresh-reviewer` 타입) | 세션 등급 이상 (`fable` 세션이면 `fable`) | 정밀 교차 검토. 생성 모델보다 낮은 등급은 생성 모델의 맹점을 잡지 못하므로 등급이 같거나 높아야 한다 |
| GATE 2차 (`ecc:santa-method`, 돈·안전·법) | 서브에이전트 2명 | 세션 등급 + `opus` (opus 세션이면 `opus` + `sonnet`) | 독립성 — 모델을 달리해 같은 맹점을 공유하지 않게. 둘 다 생성 모델보다 두 등급 아래로 내려가지 않는다 |
| eval judge (`eval/PROTOCOL.md`) | 다른 계열 모델 | — | self-preference 방어. PROTOCOL.md 규칙 그대로 |

## 적용 규칙

1. 위임할 때 Agent 도구에 `model`을 **명시**한다. 생략하면 세션 모델을 상속한다 — 그것도 기록한다.
2. decision-log의 스킬 사용 기록 줄에 모델을 병기한다:
   `[A1] ecc:research-ops (sonnet 서브에이전트) — …` · `[GATE] fresh-reviewer (opus, fresh) — …`
3. 세션 모델이 표의 등급보다 **낮으면**(예: `sonnet` 세션) 판단 단계 A2·A4 검토·GATE만 `opus`로 위임한다.
   세션 모델이 더 높으면(예: 최상위 모델) 표대로 — 검토관은 세션 모델을 상속시킨다.
4. 검토·판정 서브에이전트(A4 독립 검토·GATE·eval judge·REVIEW 정확성 리뷰)는 `subagent_type: fresh-reviewer`로 띄운다
   (`~/.claude/agents/fresh-reviewer.md`, 원본은 스킬 저장소 `_tools/agents/`). 결과 파일은 메인이 쓴다. ponytail 페르소나는
   구현 서브에이전트에만 주입된다 (`~/.claude/settings.json`의 env `PONYTAIL_SUBAGENT_MATCHER`).
5. **effort.** 판단·검토·문서 생성은 `high` — 세션이 xhigh여도 긴 산출물은 high가 낫다(xhigh는 초안을 thinking에서 한 번 더 써 출력 2배).
   조사·초안 위임(sonnet)은 `medium`, 기계적 작업(haiku)은 `low`. Agent 도구 호출로는 effort를 못 바꾸므로 에이전트 정의 파일의
   `effort:`로 준다(fresh-reviewer = high). 서브에이전트 컨텍스트 상한은 20만 토큰(2026-09-09 실측) — 긴 조사는 결과를 파일로 받는다.
4. 프롬프트 캐시는 모델 단위로 분리된다. 등급을 자주 바꾸면 캐시를 버린다 — 등급이 바뀌는 지점은
   위 표의 세 곳(A1 조사, A4 검토, GATE)뿐이며 단계마다 바꾸지 않는다.
5. "가장 싼 모델"이 아니라 **완료된 작업당 비용**으로 판단한다. 재작업이 생기면 싼 게 아니다.
   근거: Anthropic 비용 최적화 지침 — 다중 모델 캐스케이드 전에 "최상위 모델 + 낮은 effort"를 먼저 재라,
   최신 모델의 낮은 effort가 이전 세대 높은 effort와 같거나 낫다 (`claude-api` 스킬 Thinking & Effort 절, 캐시 2026-06-24).
   Claude Code 서브에이전트는 effort를 따로 못 주므로 이 표는 등급으로만 나눈다.
6. 위임한 산출물은 **메인이 정합 검사**(FR-ID·SC-ID·에러 slug·정책 숫자·상태값)를 하고 나서 저장한다.

## 핸드오프 모델 힌트 (08 → service-prompt-workflow)

08의 핸드오프 블록에 구현 작업 클래스별 모델 힌트를 붙인다. 표의 원본은
`service-prompt-workflow/references/model-routing.md`(BUILD·VERIFY·REVIEW). 요약:

| 작업 클래스 | 이 패키지에서의 예 | 모델 |
|---|---|---|
| 판단 집약 | INV-* 불변식(DB 제약·트랜잭션), 인증·권한, 마이그레이션, 동시성 | `opus` |
| 패턴 반복 | CRUD 엔드포인트, DTO, 폼·리스트 화면, RED 테스트 작성, 설정 | `sonnet` |
| 기계적 | 리네임, 포맷, 로그 문구, 버전 올림 | `haiku` |
| REVIEW 정확성 | diff + 기준 | `opus` (fresh) |

핸드오프 블록 형식:
```
<model_hints>
opus: FR-003(EXCLUDE 제약·23P01 처리), FR-001(매직링크·세션), 05 DDL 마이그레이션
sonnet: FR-002 그리드, FR-005~008 CRUD, 06 RED 시나리오 작성, 07 compose
haiku: 문구(해요체) 일괄, 로그 필드명, README
</model_hints>
```
