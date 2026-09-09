<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/P0-01]** 7.18 golden fixture를 이어서 확장한다. 평문 구간(`S2C 0x7E → C2S 0x00 → S2C 0x00`)은 재현 완료. 남은 건 암호화 구간(0x57 / 0x02 계정 / 0x04 캐릭터 / 0x03 로그인)과 게임 포트 리다이렉트 뒤 0x10 입장이며, 암호화는 서버의 `SecurityProvider`를 정본으로 쓴다. 합성 계정만 사용한다
- **[다음/P0-02]** 저장 전후 캐릭터 JSON 정상 상태를 fixture와 의미 비교기로 고정한다. 여기까지가 S0 gate이고, 그 뒤에야 Hades fork에서 S1 수정을 시작한다
- **[대기/UI·Mac]** UI greybox는 와이어프레임 마지막 4개 결정을 승인한 뒤 진행한다. iOS export·서명·실기기 검증은 같은 커밋을 Mac에서 여는 후속 gate로 유지한다
- **[대기/Mac]** 같은 커밋을 Mac에서 Godot 4.6 .NET + .NET 9로 열어 macOS 실행 후 Xcode export·arm64 iPhone 실기기 설치를 확인한다. iOS C# 지원은 experimental이며 서명 Team ID와 기준 iPhone/OS는 그때 지정한다
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
