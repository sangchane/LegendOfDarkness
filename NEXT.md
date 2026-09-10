<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/자산]** 캐릭터 스프라이트와 벽 타일을 원본 `.dat`에서 추출한다. `tools/dat-extract`로 지면 타일·맵 바닥은 이미 그려진다. 벽은 `TILEAS.BMP`가 고정 크기가 아니라 별도 해석이 필요하고, 캐릭터는 어느 아카이브에 있는지 아직 확정 못 했다. **`LOD_`의 복원본은 쓰지 않는다** — 저장소 `.dat`이 설치 클라이언트와 바이트 단위로 같다
- **[다음/P0-14~P0-17]** 서버 안정화 계속 — 송신 큐, 원자적 저장, 기본 전투 정확성, 하드코딩 상수 설정화. 화면 작업과 병행 가능하다
- **[대기/Mac]** 같은 커밋을 Mac에서 Godot 4.6 .NET + .NET 9로 열어 macOS 실행 후 Xcode export·arm64 iPhone 실기기 설치를 확인한다. iOS C# 지원은 experimental이며 서명 Team ID와 기준 iPhone/OS는 그때 지정한다
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
