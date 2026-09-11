# `data/` — 어느 계열의 수치인지 먼저 본다

여기에는 **서로 다른 계보의 게임 자료**가 들어 있다. 생김새가 비슷해서 섞기 쉬운데, 섞이면
"원작은 이랬다" 는 말이 어느 원작인지 알 수 없게 된다. 인용할 때는 폴더 이름까지 함께 적는다.

| 폴더 | 계열 | 무엇에서 나왔나 | 글자 |
|---|---|---|---|
| `game-data/` | **Hades (7.18)** | `sources/wren11/Dark-Ages-Private-Server` 의 `database/server/metafile/` | 영문 |
| `legend-tables/` | **Hades (7.18)** | 같은 저장소의 `database/archives/legend/Legend.dat` | 영문 |
| `server-packs/<팩>/` | **배포 팩** (5.99 · 혼든) | 각 팩의 zip 안 텍스트 (CP949 → UTF-8) | 한글 |

`ClientVersion` 은 `sources/wren11/Dark-Ages-Private-Server/src/Lorule.Config/LoruleConfig.json` 에
`718` 로 박혀 있다 — 그것이 Hades 계열의 기준이다.

## 얼마나 다른가

같은 "기술 표" 라도 이만큼 다르다:

```
game-data/abilities.json      "Assail",  statCosts [3,3,3,3,3]      613개, 영문
server-packs/5.99-server/…    이름 기본공격 · 이미지 1 · 레벨 100    605줄, 한글
```

같은 항목의 다른 표기가 아니라 **다른 계보의 다른 표**다. 한쪽 수치로 다른 쪽을 설명하면 안 된다.

## 규칙

- 수치를 문서·커밋에 옮길 때 **어느 폴더에서 왔는지 적는다.** "원작은 X다" 만 쓰면 나중에 못 가린다.
- 팩에서 뽑은 산출물은 **팩마다 따로** 둔다 — `server-packs/extracted/<팩>/`, `server-packs/vault/<팩>/`.
  `build-server-pack-vault.py` 가 그 밖으로 못 쓰게 단언으로 막아 두었다.
- Hades 계열 산출물은 `game-data/` 안에서 끝낸다. 팩 자료를 그리로 합치지 않는다.
