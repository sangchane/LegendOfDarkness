<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[결정/D-001]** 조건부 **Godot 4.6 + C#**. 공식 문서상 C# Android·iOS 내보내기는 모두 가능하지만 experimental이다. Android는 .NET 9 이상, iOS는 macOS+Xcode가 필요
- **[다음]** 게임 구현 전에 빈 C# 프로젝트로 Android와 iPhone 실기기 export smoke test. 둘 다 통과하면 Godot 확정, 막히면 Unity로 전환. GDScript+C# 이중 포팅은 복잡도가 커 1차 대안에서 제외
- **[대기/D-004]** 기준 Android·iPhone 모델/OS와 iOS 빌드용 Mac/Xcode 보유 여부 확인
- **[결론]** `src/Hades.Client` 재사용은 **불가**(검토 완료, `WORKLOG.md` 2026-09-09). 모바일 클라이언트는 ① 프로토콜 사양 = 서버 핸들러(로그인 11종 + 월드 41종) ② 암호화 = `Hades.Server.Base/Security/SecurityProvider.cs` ③ `.dat` 읽기 = `Hades.Client.Base`(netstandard2.0, 이식 가능) 로 간다
- **[막힘/픽스처]** 원본 실행으로 확인: Mundane(NPC) 템플릿 **0개**, Item 3 · Monster 3 · Spell 0 · Skill 1. PRD의 NPC 상호작용·드롭 수용기준을 검증하려면 픽스처를 먼저 만들어야 한다. 재현은 `docs/run-procedure.md` 10절 A단계(사본은 `tmp/hades-run/`, 관리자 계정 `wren` 생성됨)
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
