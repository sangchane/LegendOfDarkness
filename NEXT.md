<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[다음]** PRD `docs/mobile-test-v1-prd.md` 미결정 항목 확정: **D-001 모바일 엔진**, **D-004 기준 기기**. Phase 0이 끝나 근거는 다 모였다 — 서버·데이터·규칙은 그대로 쓰고 클라이언트만 새로 만드는 것이 기준선(`WORKLOG.md` 2026-09-08)
- **[검토]** `src/Hades.Client`(C# 30개 파일)는 패킷·암호화·로그인만 있고 렌더링 코드가 없다. 모바일 클라이언트의 통신 계층 베이스로 쓸 수 있는지 판단 필요 → spike이므로 `superpowers:brainstorming`
- **[막힘/픽스처]** 원본 실행으로 확인: Mundane(NPC) 템플릿 **0개**, Item 3 · Monster 3 · Spell 0 · Skill 1. PRD의 NPC 상호작용·드롭 수용기준을 검증하려면 픽스처를 먼저 만들어야 한다
- **[재현 방법]** Hades 실행은 `docs/run-procedure.md` 10절 A단계(실행 결과 반영 완료). 서버·클라이언트 사본은 `tmp/hades-run/`(gitignore), 관리자 계정 `wren` 생성됨
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
