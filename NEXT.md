<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/이식]** **콘텐츠 이식 — `plans/server-pack-content-port.md` 2판.** 배경은 `docs/what-hades-already-has.md` 부터(Hades 는 규칙이 있고 내용이 없다). **0단계(맵 521개)는 통과했다** — `data/map-source/5.99-server/` 에 8.45MB 로 들어와 있다. 다음은 **1(적재기) · 2(명령·상점결합 읽기) · 6(아이템 989)** 셋 중 하나. 6이 가장 빨리 게임 안에서 보이고, 1이 나머지 전부의 토대다

- **[주의/자료]** **팩의 `너비`·`높이` 를 믿지 마라.** 맵 807개 선언 중 12개가 파일 크기와 어긋난다. `lod3713.map` 은 실제 50×50 이라 `해안가 3` 이 맞고 `집털 1-1~1-4` 가 틀렸다. **파일이 정답이다.** 대조표 `plans/5.99-맵선언검증.tsv`, 판정은 계획서 A 절. 바로 넣을 수 있는 맵은 **796**

- **[참고]** 그래프·볼트에 **먼저 물어라.** 한국어 문장 말고 **기호**로 — `graphify explain "shop1"`. 팩 자료는 `data/server-packs/vault/<팩>/`(Obsidian). `graphify-out/` 은 추적 안 되는 로컬 산출물이라 `built_at_commit` 이 HEAD 와 벌어졌으면 다시 만든다

<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
