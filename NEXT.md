<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/화면]** **Godot에 원작 그림을 올린다.** 로그인·월드 입장은 붙었다(`mobile/src/Lod.Mobile.Core`, 시험 19+26개 통과). 이제 회색 상자를 실제 스프라이트로 바꾼다 — 바닥은 `tools/dat-extract`의 `map`, 사람은 `pose`, 괴물은 `mpf`로 뽑는다. **방향 규칙은 `docs/original-sprite-animation.md`를 따른다**(등·앞 두 벌 + 서·남 좌우 뒤집기, 뒤집는 축은 발 위치, 방패 z순서 교체)
- **[다음/통신]** 월드에 들어간 뒤의 패킷 — 이동·시야·대화. `WorldSession`이 연결과 암호 매개변수를 들고 있으므로 `HadesCipher.EncodeSecured`/`DecodeSecured`로 바로 주고받을 수 있다
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
