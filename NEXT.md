<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[참고]** 격리 시험을 돌리기 전에 `database/server/aislings/` 를 비운다. 시험이 그 폴더가 비어 있기를 요구하는데, 손으로 만든 계정과 서버가 만드는 `.backup` 이 거기 쌓인다
- **[다음/자산]** **`TILEAS` 의 팔레트 매핑을 못 찾았다.** 칸 자체는 문제없다 — `TILEAS.BMP` 는 `TILEA` 와 같은 56×27 고정 칸이고 정확히 2,805칸이다(`TILEA` 는 19,243칸). "고정 크기가 아니다"라는 이전 기록은 틀렸다. `dat-extract tiles … snow` 로 뽑을 수 있게 했고 칸은 깨끗한 마름모로 나온다. 다만 `mpt*.tbl` 은 `TILEA` 번호 기준이라 `TILEAS` 에 그대로 대면 색이 어긋나는 줄이 나온다. `seo.dat` 에 `TILEAS` 전용 표는 안 보인다(`gndani.tbl`·`gndattr.tbl` 이 후보)
- **[다음/자산]** **벽 타일은 `TILEA*` 에 없다.** `TILEAS` 의 S 는 눈(snow)이다 — 맵 에디터가 `snow ? TILEAS : TILEA` 로 고른다. 벽·구조물이 어느 파일에 있는지는 아직 모른다

<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
