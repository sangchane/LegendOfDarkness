<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/P0-00]** `docs/hades-p0-stabilization-plan.md`에 따라 원본 Hades를 수정하지 않는 격리 harness와 7.18 로그인→맵 입장 golden fixture를 TDD로 만든다. 실제 계정·비밀번호와 실행 중 서버 데이터는 사용하지 않는다
- **[다음/P0-10]** S0 gate가 통과하면 Hades fork에서 패킷 검증·예외 격리부터 작은 Graphite stack으로 수정한다. 실제 모바일 클라이언트 연결은 S1 전체 gate 전까지 금지한다
- **[대기/UI·Mac]** UI greybox는 와이어프레임 마지막 4개 결정을 승인한 뒤 진행한다. iOS export·서명·실기기 검증은 같은 커밋을 Mac에서 여는 후속 gate로 유지한다
- **[대기/Mac]** 같은 커밋을 Mac에서 Godot 4.6 .NET + .NET 9로 열어 macOS 실행 후 Xcode export·arm64 iPhone 실기기 설치를 확인한다. iOS C# 지원은 experimental이며 서명 Team ID와 기준 iPhone/OS는 그때 지정한다
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
