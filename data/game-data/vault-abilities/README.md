# 원작 기술·마법 — 무엇을 배워야 무엇을 배우나

`data/game-data/abilities.json` 에서 났다. **기술 275 · 마법 338**.
Hades 의 `database/server/metafile/SClass1~5` 가 원본이고, 팩(5.99)의 153개와는 **다른 계보**다.

- 선행이 없는 것(맨 처음 배우는 것): **211개**
- 무언가를 여는 것: **345개**
- 선행으로 불리는데 이 표에 없는 이름: **0개** — 없음

직업별:

| 직업 | 기술 | 마법 |
|---|---|---|
| 전사 | 62 | 4 |
| 도적 | 51 | 48 |
| 마법사 | 12 | 143 |
| 사제 | 26 | 100 |
| 수도사 | 124 | 43 |

아이콘 번호는 여기 없다. `data/archives-vault` 의 `Legend — skill.tbl` 을 본다.

`python3 scripts/build-ability-vault.py` 로 다시 만든다.
