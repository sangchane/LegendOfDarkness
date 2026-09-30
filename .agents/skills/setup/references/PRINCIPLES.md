# 원칙과 근거 — 왜 이렇게 세팅하나

> SKILL.md 절차의 "왜"만 모았다. 필요할 때만 읽는다(항상 로드 아님).

## 1. Progressive Disclosure / Just-in-time (핵심 원칙)
항상 읽히는 컨텍스트는 최소로, 상세는 실행 시점에 도구로 그때 로드한다. 처음엔 파일경로·요약 같은
가벼운 식별자만 유지 → 필요할 때 read/grep으로 꺼낸다. Claude Code가 채택한 방식.
- [Anthropic — Effective context engineering](https://www.anthropic.com/engineering/effective-context-engineering-for-ai-agents)
- [Anthropic — Agent Skills](https://www.anthropic.com/engineering/equipping-agents-for-the-real-world-with-agent-skills)

## 2. 층 나누기 (휘발성 기준)
- **항상-로드(얇게):** 루트 CLAUDE.md(행동규칙) + AGENTS.md(프로젝트 표준·네비). 합 ≤ ~8KB 목표.
- **온디맨드:** `<폴더>/CLAUDE.md` — Claude Code는 그 폴더 파일을 만질 때만 로드(공식 문서 명시: 토큰 낭비 방지).
- **주입 1회:** SessionStart 훅이 NEXT.md 마커 블록만 세션 시작에 넣는다.
- [Claude Code — memory & 중첩 CLAUDE.md 온디맨드 로딩](https://code.claude.com/docs/en/memory)

## 3. AGENTS.md = 크로스툴 단일 원본
툴마다 규칙 파일을 따로 두면 드리프트가 난다. AGENTS.md 한 곳을 원본으로 두고 각 툴 네이티브 파일은
얇은 어댑터로 가리킨다.
- Claude Code: `CLAUDE.md` 가 `@AGENTS.md` import (공식 지원, Windows·recursive depth 5).
- Antigravity: v1.20.3(2026-03-05)부터 `AGENTS.md` 네이티브 로드. `GEMINI.md`가 있으면 AGENTS.md보다
  **우선**하므로, GEMINI.md는 포인터만 두어 가로채기를 막는다.
- Cursor: `AGENTS.md` 네이티브 / `.cursor/rules` globs(파일 매칭 시만 로드).
- [agents.md 오픈표준(6만+ repos, Linux Foundation)](https://github.com/agentsmd/agents.md)
- [Antigravity AGENTS.md/GEMINI.md 우선순위](https://agentpedia.codes/blog/user-rules)
- [Cursor Rules](https://cursor.com/docs/rules)

## 4. 단일 출처 — 히스토리와 다음-할일 분리
"다음 할 일"은 NEXT.md 마커 블록 한 곳에만. 완료 이력은 WORKLOG.md에만 쌓는다. 둘을 섞으면 NEXT.md가
히스토리로 비대해진다(실제 사례: 한 프로젝트 NEXT.md가 230KB까지 부풂 → 매 세션 통째 주입되는 역효과).
Memory Bank 패턴(세션 시작 상태 파일 읽기 + 계층적 lazy 로딩 ~70% 토큰 절감)과 같은 사상.
- [vanzan01/cursor-memory-bank (hierarchical/lazy rule loading)](https://github.com/vanzan01/cursor-memory-bank)
- [Cline Memory Bank](https://cline.bot/blog/memory-bank-how-to-make-cline-an-ai-agent-that-never-forgets)

## 5. 비파괴·가역 (스킬 안전 규율)
기존 CLAUDE.md는 손으로 다듬은 자산이다. 바꾸기 전 diff를 보여주고 승인받으며, clean 트리에서
git 체크포인트 후 적용해 `git revert` 한 번으로 되돌린다. 시크릿 내용은 컨텍스트 파일에 넣지 않는다.

## 6. 스킬 자체도 얇게
SKILL.md 본문은 로드되면 매 턴 토큰 비용이다 → 절차만 얇게, 이 근거 문서와 템플릿은 분리(필요 시 로드).
- [Claude Code — Skill authoring best practices](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices)

## 설계 단일 출처
이 스킬의 전체 기획(PRD·아키텍처·계약·테스트·운영·적대적 게이트)은 첫 적용 프로젝트의
`autopilot/context-continuity-kit/` (00~08 + decision-log)에 있다.
