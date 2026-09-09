<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/P0-02]** 합성 계정 로그인 뒤 저장된 `aislings/<이름>.json`의 정상 상태를 최소 fixture와 의미 비교기로 고정한다. 시간·임의값은 비교에서 제외하고, 비교기가 실제 차이를 잡아내는지 확인한다. 여기까지가 S0 gate
- **[다음/S1]** S0 gate 통과 후 Hades fork를 만들어 패킷 검증·예외 격리부터 작은 Graphite stack으로 고친다. 하드코딩 상수 설정화(P0-17)도 그 안에 있다
- **[대기/UI·Mac]** UI greybox는 와이어프레임 마지막 4개 결정을 승인한 뒤 진행한다. iOS export·서명·실기기 검증은 같은 커밋을 Mac에서 여는 후속 gate로 유지한다
- **[대기/Mac]** 같은 커밋을 Mac에서 Godot 4.6 .NET + .NET 9로 열어 macOS 실행 후 Xcode export·arm64 iPhone 실기기 설치를 확인한다. iOS C# 지원은 experimental이며 서명 Team ID와 기준 iPhone/OS는 그때 지정한다
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
