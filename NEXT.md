<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[참고]** 격리 시험을 돌리기 전에 `database/server/aislings/` 를 비운다. 시험이 그 폴더가 비어 있기를 요구하는데, 손으로 만든 계정과 서버가 만드는 `.backup` 이 거기 쌓인다
- **[다음/도구]** **`map` 명령은 눈 깃발을 따르지 않는다.** 바닥을 언제나 `TILEA` 로 그린다 — 서버가 `MapFlags.SnowTileset`(128)로 눈 타일셋을 지시하는데(`dark-ages-ts/packages/network/src/entities/map-flags.ts`) 도구는 그것을 보지 않는다. `tiles` 는 이제 `눈|snow` 로 고를 수 있다
- **[다음/자산]** **벽 타일은 `TILEA*` 에 없다.** `TILEAS` 의 S 는 눈(snow)이다 — 맵 에디터가 `snow ? TILEAS : TILEA` 로 고른다. 벽·구조물이 어느 파일에 있는지는 아직 모른다

<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
