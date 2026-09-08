<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[결정/D-001]** Windows/Android 구현 기준으로 **Godot 4.6 + C#을 채택**한다. Windows 빌드·실행, Android APK export·서명·에뮬레이터 설치·C# 실행·화면 렌더링을 통과했다. 애플리케이션 소스에는 Windows 전용 의존성이 없고 macOS에서 이어갈 iOS preset 뼈대도 포함했다. 최종 iOS 적합성은 아래 Mac gate에서 확정한다
- **[다음/Windows]** 첫 기능 구현 전 Hades 7.18 로그인 프로토콜 계약과 격리 테스트 fixture를 만든다. 게임 구현 범위는 `docs/mobile-test-v1-prd.md`의 단일 흐름을 넘기지 않는다
- **[대기/Mac]** 같은 커밋을 Mac에서 Godot 4.6 .NET + .NET 9로 열어 macOS 실행 후 Xcode export·arm64 iPhone 실기기 설치를 확인한다. iOS C# 지원은 experimental이며 서명 Team ID와 기준 iPhone/OS는 그때 지정한다
- **[결론]** `src/Hades.Client` 재사용은 **불가**(검토 완료, `WORKLOG.md` 2026-09-09). 모바일 클라이언트는 ① 프로토콜 사양 = 서버 핸들러(로그인 11종 + 월드 41종) ② 암호화 = `Hades.Server.Base/Security/SecurityProvider.cs` ③ `.dat` 읽기 = `Hades.Client.Base`(netstandard2.0, 이식 가능) 로 간다
- **[막힘/픽스처]** 원본 실행으로 확인: Mundane(NPC) 템플릿 **0개**, Item 3 · Monster 3 · Spell 0 · Skill 1. PRD의 NPC 상호작용·드롭 수용기준을 검증하려면 픽스처를 먼저 만들어야 한다. 재현은 `docs/run-procedure.md` 10절 A단계(사본은 `tmp/hades-run/`, 관리자 계정 `wren` 생성됨)
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
