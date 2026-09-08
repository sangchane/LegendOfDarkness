<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[다음]** PRD `docs/mobile-test-v1-prd.md` **D-001 모바일 엔진** 결정 (그다음 D-004 기준 기기). 판단 재료는 아래에 모아 뒀다 — 분석을 다시 하지 말고 여기서 이어간다
  - 결정을 가르는 축은 **C# 재사용 가능 여부**다. 재사용할 자산이 전부 C#이기 때문: `Hades.Client.Base`(netstandard2.0, `.dat`/맵/팔레트), `Hades.Server.Base/Security/SecurityProvider.cs`(암호화), 서버 핸들러 52종(프로토콜 정본)
  - 후보 A **Godot 4 + C#**: 무료(MIT), 앱 크기 작음, 2D 타일에 강함, **사용자가 Godot 경험 있음**. 리스크 — Godot의 C# Android 내보내기가 이 용도에 충분한지 **미확인(확인 필요)**. 부족하면 GDScript로 가고 위 C# 두 덩어리(암호화 237줄 + `.dat` 읽기 10파일)를 포팅하는 대안
  - 후보 B **Unity**: C# 네이티브, Android 성숙. 리스크 — 요금제, 앱 크기, 사용자 숙련도 불명
  - 공통 전제: 레거시 TCP 소켓 직접 구현(D-002), 가로 고정 Android 폰(D-003), 30fps(NFR-005) — 렌더링 부하 자체는 두 엔진 다 여유
- **[결론]** `src/Hades.Client` 재사용은 **불가**(검토 완료, `WORKLOG.md` 2026-09-09). 모바일 클라이언트는 ① 프로토콜 사양 = 서버 핸들러(로그인 11종 + 월드 41종) ② 암호화 = `Hades.Server.Base/Security/SecurityProvider.cs` ③ `.dat` 읽기 = `Hades.Client.Base`(netstandard2.0, 이식 가능) 로 간다
- **[막힘/픽스처]** 원본 실행으로 확인: Mundane(NPC) 템플릿 **0개**, Item 3 · Monster 3 · Spell 0 · Skill 1. PRD의 NPC 상호작용·드롭 수용기준을 검증하려면 픽스처를 먼저 만들어야 한다. 재현은 `docs/run-procedure.md` 10절 A단계(사본은 `tmp/hades-run/`, 관리자 계정 `wren` 생성됨)
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
