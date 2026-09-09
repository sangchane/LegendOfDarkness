<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/P0-12]** 게임 입장 티켓의 단일 소비와 인증 순서. `Format10Handler`가 redirect 검증보다 `EnterGame()`을 먼저 부르고, redirect 상태가 사용자명 목록에 의존한다. 같은 티켓을 동시에 쓰는 두 연결 중 하나만 성공해야 한다
- **[다음/P0-12·P0-13]** 입장 티켓 단일 소비와 인증 순서, 그 뒤 연결 수명·동시성. **P0-13에 handshake 시간 제한**을 포함한다 — 잘린 본문을 보낸 연결은 형태로 판정할 수 없어 시간 제한으로만 정리된다
- **[대기/UI·Mac]** UI greybox는 와이어프레임 마지막 4개 결정을 승인한 뒤 진행한다. iOS export·서명·실기기 검증은 같은 커밋을 Mac에서 여는 후속 gate로 유지한다
- **[대기/Mac]** 같은 커밋을 Mac에서 Godot 4.6 .NET + .NET 9로 열어 macOS 실행 후 Xcode export·arm64 iPhone 실기기 설치를 확인한다. iOS C# 지원은 experimental이며 서명 Team ID와 기준 iPhone/OS는 그때 지정한다
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
