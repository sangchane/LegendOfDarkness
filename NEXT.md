<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[지금/이식]** **콘텐츠 이식 — `plans/server-pack-content-port.md` 2판.** 배경은 `docs/what-hades-already-has.md` 부터 읽는다(Hades 는 규칙이 있고 내용이 없다). **0단계가 관문**: Windows PC 의 `5.99 서버팩.zip` 에서 맵 파일 **521개·8.4MB** 를 꺼낸다. 목록은 `plans/5.99-필요한-맵파일.tsv`(경로·기대바이트). 이게 없으면 3·4·5·7·8·9 를 못 한다. **0 없이 지금 시작할 수 있는 것: 1(적재기) · 2(명령·상점결합 읽기) · 6(아이템 989)**

- **[참고]** 그래프·볼트에 **먼저 물어라.** 한국어 문장 말고 **기호**로 — `graphify explain "shop1"`. 팩 자료는 `data/server-packs/vault/<팩>/`(Obsidian). `graphify-out/` 은 추적 안 되는 로컬 산출물이라 `built_at_commit` 이 HEAD 와 벌어졌으면 다시 만든다

- **[보류/자산]** **눈 맵은 지금 확인할 방법이 없다.** 참고 저장소 16개 어디에도 눈 맵을 그리는 구현이 없고(`dark-ages-ts` 는 `MapFlags.SnowTileset=128` 을 **선언만** 한다), 이 서버의 맵 넷은 모두 눈 비트가 꺼져 있다. 맞춰 볼 정답도 시험할 맵도 없다

<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
