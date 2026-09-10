<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/통신]** **모바일 클라이언트를 서버에 붙인다.** `mobile/tests/Lod.Mobile.Core.Tests`에 시험 3종(프레임 부호기·CP949 글자·7.18 로그인 절차)이 **이미 쓰여 있는데 `mobile/src/Lod.Mobile.Core`에 구현이 없다**(이전 세션 잔재, 커밋 안 됨). 구현해서 통과시키고 Godot 클라이언트가 로그인 → 캐릭터 확인 → 월드 입장까지 가게 한다. 서버를 굳혀 온 이유가 이것이다
- **[다음/화면]** Godot에 원작 그림을 올린다. 배치(세로·가로)는 시안과 greybox 양쪽에 이미 들어갔고, 남은 것은 회색 상자를 실제 스프라이트로 바꾸는 것 — 방향 규칙은 `docs/original-sprite-animation.md`를 따른다. 위 항목과 합치면 "서버가 시키는 대로 내 캐릭터가 걷는" 화면이 된다
- **[다음/P0-14~P0-17]** 서버 안정화 마무리 — 송신 큐, 원자적 저장, 기본 전투 정확성, 하드코딩 상수 설정화
- **[다음/자산]** 벽 타일(`TILEAS.BMP`는 고정 크기 아님)과 아이템 아이콘. 지면·맵·캐릭터·괴물·사물은 끝났다. **`LOD_`의 복원본은 쓰지 않는다**
- **[대기/Mac]** 같은 커밋을 Mac에서 Godot 4.6 .NET + .NET 9로 열어 macOS 실행 후 Xcode export·arm64 iPhone 실기기 설치를 확인한다. iOS C# 지원은 experimental이며 서명 Team ID와 기준 iPhone/OS는 그때 지정한다

<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
