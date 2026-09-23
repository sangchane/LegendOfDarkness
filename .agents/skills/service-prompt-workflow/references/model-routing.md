# 작업 클래스별 모델 라우팅 (PLAN · BUILD · VERIFY · REVIEW)

"구현 기능에 맞는 모델"을 고르는 정적 표. 스킬 프론트매터 `model:`은 턴 전체를 바꾸는 거친 수단이라 쓰지 않고, **tasks.md의 작업을 서브에이전트에
위임할 때 `model` 파라미터**(Claude Code Agent 도구: `haiku` · `sonnet` · `opus` · `fable`)로 고른다.
등급표·가격·근거는 `service-autopilot/references/model-routing.md`와 같다(원본은 번들 `claude-api` 스킬, 캐시 2026-06-24).

## 작업 클래스 → 모델

| 작업 클래스 | tasks.md에서 읽히는 신호 | 모델 | 왜 |
|---|---|---|---|
| **판단 집약** | `INV-*` 불변식 구현, 동시성(락·트랜잭션·EXCLUDE 제약), 인증·권한·암호·세션, 데이터 손실 가능한 마이그레이션, 알고리즘, 새 아키텍처 경계·포트 | `opus` (세션이 더 높으면 세션) | 틀리면 재작업·데이터 손실. 완료된 작업당 비용이 가장 낮은 선택 |
| **패턴 반복** | CRUD 엔드포인트, DTO·스키마·zod, 폼·리스트·그리드 화면, RED 테스트 작성(시나리오 → 코드), 설정·compose·CI 파일, 문서 | `sonnet` | 명세(SPEC·05·06)가 정확하면 등급 차이가 결과 차이를 만들지 않는다 |
| **기계적** | 리네임, 포맷, 로그·에러 문구 일괄(해요체), 주석, 의존성 버전 올림, 파일 이동 | `haiku` | 판단 0 |
| **VERIFY 실행** | 테스트·빌드·린트 실행, 스크린샷 수집, 종료코드 보고 | `haiku` | 명령 실행과 결과 보고. 해석은 메인이 한다 |
| **REVIEW 정확성** | diff + 수용 기준 | 세션 등급 이상, fresh context (`fable` 세션이면 `fable`) | 생성 모델과 같거나 높은 등급이어야 잡는다 |
| **REVIEW 복잡도** (`ponytail:ponytail-review`) | diff | `sonnet` | 삭제 후보 나열 |
| **santa-method 2인** (돈·안전·법) | diff | 세션 등급 + `opus` (opus 세션이면 `opus` + `sonnet`) | 모델을 달리해 맹점을 공유하지 않게 |

## 규칙

1. **PLAN에서 태그한다.** tasks.md의 각 작업에 `model: opus|sonnet|haiku`를 붙인다. 태그가 없으면 `sonnet`.
   한 작업이 두 클래스에 걸치면 높은 등급. 태그 근거가 애매하면 클래스 신호를 한 줄 적는다.
2. **UI 작업은 `sonnet` 이상.** `frontend-design-taste` 하드룰(토큰·상태 4종·룩업맵) 준수가 필요하다.
3. **위임할 때 `model`을 명시**하고, 서브에이전트에는 SPEC·해당 작업·검증 명령만 준다(컨텍스트 위생 — ETHOS 9).
4. **승급 규칙.** VERIFY에서 같은 작업이 2회 실패하면 한 등급 올려 재시도. 그래도 실패하면 모델 탓이 아니라
   명세 탓이다 — SPEC으로 되돌아간다.
5. **캐시.** 프롬프트 캐시는 모델 단위다. 같은 등급 작업을 묶어서 연속 위임하고, 작업마다 등급을 번갈아 바꾸지 않는다.
6. **완료된 작업당 비용**으로 판단한다. 싼 모델이 재작업을 만들면 싼 게 아니다.
   근거: Anthropic 비용 최적화 지침 — 캐스케이드 전에 "최상위 모델 + 낮은 effort"를 먼저 재라, 최신 모델의 낮은 effort가
   이전 세대 높은 effort와 같거나 낫다 (`claude-api` 스킬 Thinking & Effort 절). Claude Code 서브에이전트는 effort를 따로 못 주므로 등급으로만 나눈다.
7. **기록.** decision-log 스킬 사용 줄에 모델을 병기한다: `[5 BUILD] FR-003 (opus 서브에이전트) — …`.

## service-autopilot 핸드오프와의 연결

autopilot `08-readiness-report.md`의 `<model_hints>` 블록이 오면 PLAN에서 그대로 `model:` 태그의 초기값으로 쓴다.
힌트가 없으면 위 표로 직접 분류한다.
