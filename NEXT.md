<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/P0-10]** fork `fix/hades-network-boundary`에서 악성 프레임을 보낸 **연결 자체를 정리**한다. 서버가 죽던 문제는 고쳤지만 지금은 그 연결의 socket이 닫히지 않은 채 서비스만 멈춘다. 완료 조건은 "해당 연결만 종료하고 서버와 정상 연결은 계속 동작"
- **[다음/P0-11]** 사용자명·저장 경로 검증(`..`·구분자·절대경로 거부). 그 뒤 P0-12 입장 티켓, P0-13 연결 수명, P0-17 하드코딩 설정화
- **[대기/UI·Mac]** UI greybox는 와이어프레임 마지막 4개 결정을 승인한 뒤 진행한다. iOS export·서명·실기기 검증은 같은 커밋을 Mac에서 여는 후속 gate로 유지한다
- **[대기/Mac]** 같은 커밋을 Mac에서 Godot 4.6 .NET + .NET 9로 열어 macOS 실행 후 Xcode export·arm64 iPhone 실기기 설치를 확인한다. iOS C# 지원은 experimental이며 서명 Team ID와 기준 iPhone/OS는 그때 지정한다
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
