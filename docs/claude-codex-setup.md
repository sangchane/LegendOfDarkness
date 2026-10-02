# Claude 설정을 Codex에서 쓰기

확인·설치: 2026-10-03 · Codex CLI 0.160.0

## 설치 결과

| 원본 | 설치 | Codex 이름 예시 |
|---|---:|---|
| dev 1.5.4 | 5 | `$dev:build`, `$dev:design`, `$dev:ui` |
| Superpowers 6.4.1 | 15 | `$superpowers:systematic-debugging` |
| ECC 2.0.0-rc.1 | 249 | `$ecc:csharp-testing`, `$ecc:security-review` |
| 개인 스킬 | 3 | `$graphify`, `$karpathy-guidelines`, `$sk` |

272개를 `~/.agents/skills/`에서 현재 Claude 설치 원본 폴더에 심볼릭 링크로 연결했다. Codex 실제 `skills/list`가 전부 활성 상태로 읽었고 오류는 0건이다. SKILL.md뿐 아니라 상대경로 참조·스크립트·assets도 원본 폴더를 사용한다. Claude의 현재 버전 경로에 연결했으므로 플러그인 업데이트로 버전 경로가 바뀌면 링크 대상도 갱신해야 한다.

기존 Codex용 catch-up·frontend-design-taste·service-autopilot·service-prompt-workflow·solution-planner 5개는 유지했다. 프로젝트 `.agents/skills`도 유지한다. `learned`에는 `.gitkeep`만 있어 추가할 스킬이 없다. Ponytail은 기존 Codex 플러그인을 사용하며 검증 시 런타임이 4.10.1을 로드했다(Claude는 4.10.0).

## 공통 작업 흐름

- `~/.codex/AGENTS.md`: Claude dev 공통 규칙과 graphify 진입 규칙을 Codex용으로 연결했다. 사용자·프로젝트 지침이 우선하고 모델·effort 선택은 사용자가 한다.
- `~/.codex/config.toml`: `project_doc_fallback_filenames = ["CLAUDE.md"]`. AGENTS.md가 없는 폴더에서 CLAUDE.md를 지침으로 읽는다. 다른 폴더를 작업하면 루트 지침에 따라 해당 스코프 파일을 직접 읽는다. `@import`는 수동으로 참조 파일을 읽는다.
- Claude `/dev:build`는 Codex `$dev:build`. 스킬명 없이 한국어로 구현·리뷰·수정 요청을 해도 공통 라우팅 규칙으로 진행한다.
- 기존 모델·effort·권한·MCP·알림 설정은 보존했다. Claude 전역 및 이 프로젝트에 별도로 등록된 MCP 서버는 없었다.

## 활성 훅과 검증

1. 사용자 `SessionStart`: `~/.codex/hooks/claude-session-context.py`가 현재 위치에서 Git 루트까지 NEXT.md의 NEXT-ACTION 블록을 찾아 startup/resume/clear/compact 때 주입한다.
2. 프로젝트 `PreToolUse`: `.codex/hooks.json`이 기존 `tools/hooks/guard_shell.py`를 실행한다. `git status`는 차단, `git status --ignore-submodules=all`·명시적 submodule status·git diff는 허용하는 것을 확인했다. 자료 복사 안내도 같은 원본을 사용한다.
3. Ponytail의 기존 Codex용 SessionStart·UserPromptSubmit·SubagentStart 3개를 코드 검토 후 활성화했다.

Codex `/hooks`의 공식 검토·신뢰 흐름을 사용했다. 신뢰를 우회하는 플래그나 내부 신뢰 저장소 수동 편집은 사용하지 않았다. `hooks/list`로 enabled 및 trusted를 확인한다. 하위 폴더 mobile/client에서 NEXT 문맥을 찾는 샘플도 통과했다.

## 그대로 옮길 수 없는 설정

Claude JSON 전체를 Codex TOML에 복사하지 않는다. Claude 대시보드 statusLine, Anthropic 모델명·자동 모델 전환, Claude permissions의 도구 문자열, Claude 전용 에이전트 정의는 같은 런타임 설정이 아니다. Codex의 기존 상태 표시·권한 설정과 프로젝트 규칙을 사용한다. 기존 Claude deny 목록이 Codex의 강제 차단 정책으로 동일 이식됐다는 뜻은 아니다.

ECC의 Claude 세션 로그 분석·자동 학습·Stop/PostToolUse 등 자동화 훅은 이식하지 않았다. Claude transcript·도구 입력을 전제로 한 자동화이므로 스킬 설치와 구분한다. Superpowers의 Codex manifest도 훅을 비워 두고 스킬만 제공한다. Desktop `synced`의 전용 도구 스킬은 복사하지 않았으며 문서·표·PDF·프레젠테이션·브라우저 조작은 현재 Codex의 해당 플러그인을 쓴다. 원본 Artifact 안내는 Codex에 같은 도구가 없으므로 자동 훅으로 추가하지 않았다.

스킬 설치는 각 외부 API 계정이나 선택적 실행 의존성을 모두 설치·연결한다는 뜻은 아니다. 그 스킬을 실제 쓸 때 필요한 기존 도구와 의존성을 확인한다.

## 적용과 복구

스킬은 다음 턴부터 사용할 수 있다. 설정과 SessionStart 훅을 모두 새로 적용하려면 Codex에서 새 대화를 연다. 새 대화는 NEXT.md로 이어진다.

백업: `/Users/dev/.codex/backups/claude-sync-20261003-001525`. 원본 config.toml·프로젝트 AGENTS.md와 설치 목록이 들어 있다. 현재 설치 목록은 `~/.codex/claude-sync-installation.json`. 이번에 만든 링크만 목록에 따라 지우고 설정 백업을 복원하면 되돌릴 수 있다. 훅 활성 상태는 Codex `/hooks`에서 해제할 수 있다.

공식 근거: [스킬 경로·심볼릭 링크](https://learn.chatgpt.com/docs/build-skills), [CLAUDE.md fallback 지침](https://learn.chatgpt.com/docs/agent-configuration/agents-md), [훅 형식·도구 대응·검토](https://learn.chatgpt.com/docs/hooks).
