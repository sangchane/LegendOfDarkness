# 모바일 UI 테마 — 원작 4.51 을 쓴다

사용자가 고른 것은 **5.01 이전 판**이고, 우리가 가진 그 판은 [[판/4.51|4.51]] 하나다.
시안 셋을 그려 안C 를 먼저 만들었으나, 폰 화면에서 돌 무늬가 어수선해 **안A · 색만 빌린다** 로 바꿨다(사용자, 2026-09-18).

**화면을 만들거나 고치기 전에 규칙부터 읽는다.** 눌러볼 시안: `docs/ui/mockups-451/index.html`

## 규칙

| 규칙 | 말 |
|---|---|
| ~~[[규칙/돌두가지|돌두가지]]~~ | **버렸다** — 무늬를 안 쓴다(2026-09-18) |
| ~~[[규칙/글자색은돌이정한다|글자색은돌이정한다]]~~ | **버렸다** — 음각은 돌이 있어야 성립한다 |
| [[규칙/돌을안쓰는곳|돌을안쓰는곳]] | **전부** 평평하게. 강조색 `#c8783c` 하나로 확정 단추·공격 버튼·열린 탭만 구분한다 |
| [[규칙/글꼴|글꼴]] | 제목줄·확정 단추만 세리프, 나머지 산세리프, 숫자는 고정폭 |
| [[규칙/아이콘은납작하게|아이콘은납작하게]] | 체력·마력 아이콘은 납작한 원 18px, 그림 파일 없음 |
| [[규칙/체력을두번안그린다|체력을두번안그린다]] | 머리 위 = 백분율 막대(맞을 때만), 위 판 = 정확한 숫자(늘) |
| [[규칙/배치는안바꾼다|배치는안바꾼다]] | 지금 클라이언트가 잡아 둔 자리를 그대로 쓴다 |

## 색

| 이름 | 값 | 무엇 |
|---|---|---|
| [[색/hp|hp]] | `#c8783c` | 체력 |
| [[색/mp|mp]] | `#5a6fa8` | 마력 |
| [[색/title|title]] | `#abab9f` | 어두운 돌 위 제목 |
| [[색/engrave|engrave]] | `#100f0b` | 밝은 돌 위 음각 |
| [[색/inner|inner]] | `#0f0f0f` | 창 속(96% 불투명) |
| [[색/cell|cell]] | `#1f1f24` | 격자 칸 |
| [[색/cell-edge|cell-edge]] | `#303036` | 칸 테두리 |
| [[색/muted|muted]] | `#97978b` | 흐린 글자 |

치수: [[치수/치수|치수]]

## 손댈 코드

| 파일 | 무엇 | 상태 |
|---|---|---|
| [[구현대상/Greybox.cs|mobile/client/src/Greybox.cs]] | 자리표 회색 팔레트. 테마로 갈아끼운다 | 아직 |
| [[구현대상/GameScreen.cs|mobile/client/src/GameScreen.cs]] | 위 판에서 긴 체력·마력 막대를 뺀다 | 아직 |
| [[구현대상/GearGrid.cs|mobile/client/src/GearGrid.cs]] | 18자리 고리에 테마 적용 | 아직 |
| [[구현대상/PackPanel.cs|mobile/client/src/PackPanel.cs]] | 소지품 격자에 테마 적용 | 아직 |
| [[구현대상/TalkPanel.cs|mobile/client/src/TalkPanel.cs]] | NPC·상점 창에 테마 적용 | 아직 |
| [[구현대상/ChatPanel.cs|mobile/client/src/ChatPanel.cs]] | 대화 창에 테마 적용 | 아직 |
| [[구현대상/LoginScreen.cs|mobile/client/src/LoginScreen.cs]] | **보류** — 사용자가 시안을 무르다(2026-09-18) | 보류 |
| [[구현대상/HealthBar.cs|mobile/client/src/HealthBar.cs]] | 머리 위 막대 — 이미 있다. 색만 테마에 맞춘다 | 있음 |
| [[구현대상/StatusRow.cs|mobile/client/src/StatusRow.cs]] | 상태 아이콘 줄 — 이미 있다 | 있음 |

## 원작 자료

- 화면 **92장** (`docs/ui/original-451/`) — 쓰임까지 확인된 것 63장, 나머지는 모양만 봤다
- 재질 3 · 아카이브 4 · 판 3
- 글꼴: `han00.fnt`(한글 비트맵 글꼴) · `han01.fnt`(한글 비트맵 글꼴(다른 굵기)) · `eng00.fnt`(영문 비트맵 글꼴) · `eng01.fnt`(영문 비트맵 글꼴(다른 굵기))

## 함정

- dat-extract 는 net8 빌드인데 런타임은 net9 뿐이라 DOTNET_ROLL_FORWARD=Major 가 필요하다
- EPF 는 팔레트를 제 안에 안 지고 있다 — UI 는 legend.pal 이다
- 12열로 그리면 프레임이 하나뿐인 창도 12칸 판에 그려진다. 바탕색 #141720 기준으로 잘라낸다

---

단일 출처는 `data/original-ui/451.json` 이다. 이 볼트는 그것을 옮겨 적은 것이므로,
새로 알게 된 것은 **JSON 에 적고** `python scripts/build-ui-vault.py` 로 다시 만든다.
그래프는 `python scripts/build-ui-graph.py`.
