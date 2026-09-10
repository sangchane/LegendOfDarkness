<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/통신]** **주변 사람과 괴물을 서버에서 받는다.** 지금은 내 위치만 서버가 정한다 — 다른 배우는 우리가 놓은 자리에 서 있다. 서버가 보내는 `0x07`(물체 추가)·`0x0C`(누군가 걸음)·`0x33`(사람 표시)을 `WorldClient.PumpAsync`에 붙이면 된다. 방향 규칙은 `docs/original-sprite-animation.md`
- **[다음/P0-14~P0-17]** 서버 안정화 마무리 — 송신 큐, 원자적 저장, 기본 전투 정확성, 하드코딩 상수 설정화
- **[다음/자산]** 벽 타일(`TILEAS.BMP`는 고정 크기 아님)과 아이템 아이콘. 지면·맵·캐릭터·괴물·사물은 끝났다. **`LOD_`의 복원본은 쓰지 않는다**
- **[다음/화면]** 대상 고르기·전투·인벤토리를 Godot에 올린다. 배치는 시안(`docs/ui/`)에 이미 있다
- **[대기/Mac]** 같은 커밋을 Mac에서 Godot 4.6 .NET + .NET 9로 열어 macOS 실행 후 Xcode export·arm64 iPhone 실기기 설치를 확인한다. iOS C# 지원은 experimental이며 서명 Team ID와 기준 iPhone/OS는 그때 지정한다

<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
