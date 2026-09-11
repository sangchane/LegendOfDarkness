<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/서버]** **돈이 같은 칸에 겹쳐도 합쳐지지 않는다.** 원작은 쌓이면 금액이 합산되는데 Hades 의
  `Money.Create` 는 떨어질 때마다 새로 만든다(`Types/Money.cs`). 등급 기준(`CalcAmount`)은 서버가 정한
  대로 두기로 했으니 건드리지 않는다 — 합산만 이어 붙이면 된다
- **[다음/서버]** **전역 잠금 범위 줄이기.** 읽지 않는 클라이언트 하나가 다른 클라이언트를 30초 넘게 굶기는 것이 세 번에 한 번꼴로 재현된다(계획서 2.3절 "관측된 한계"). P0 범위 밖이라 남겨 뒀다 — 지인 테스트에서 사람이 몇 명 붙는지 보고 우선순위를 정한다
- **[다음/자산]** 벽 타일(`TILEAS.BMP`는 고정 크기 아님)
- **[다음/도구]** `tools/` 에는 시험 프로젝트가 없다. `IconPalettes.PaletteFor`(세 갈래 파싱)와 `icon` 명령의 칸 계산은 지금 눈으로만 확인했다 — 도구 시험 자리를 만들 때 여기부터 덮는다
- **[대기/Mac]** **macOS 실행도, 서명된 arm64 .ipa 도 됐다**(2026-09-11). 남은 것은 **실기기 설치 하나뿐** — iPhone 을 꽂고 `MobileSmoke.ipa` 를 올려 `MOBILE_SMOKE_OK` 를 확인한다. 절차는 `experiments/godot-csharp-mobile-smoke/README.md`, Mac 쪽 도구는 `docs/mobile-client.md` 3절

<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
