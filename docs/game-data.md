# 원작 자료 — 기술·마법·퀘스트·아이템이 어디에 있고 무엇에 걸리나

- 기준일: 2026-09-11
- 만드는 법: `./scripts/build-game-data.ps1`
- 단일 출처: `data/game-data/*.json` (커밋됨) · 노트는 거기서 나므로 커밋하지 않는다

---

## 1. 어디서 나오나

> **계열: Hades (7.18).** 이 문서의 수치는 전부 Hades 저장소에서 나온 것이다. `data/server-packs/`
> 의 배포 팩(5.99·혼든)은 한글로 된 **다른 계보의 다른 표**이므로 여기 수치로 설명하면 안 된다.
> `data/README.md` 참고.

`database/server/metafile/` — **`.dat` 아카이브가 아니라 서버 데이터베이스 폴더**다
(`where-the-answers-are.md` 4.6절). 각각 zlib 한 덩어리이고, 풀면 "줄마다 이름과 값 몇 개"인 표다.

| 원본 | 무엇 | 뽑은 것 |
|---|---|---|
| `SClass1`~`SClass5` | 직업 다섯의 기술·마법 | `abilities.json` — **613개** (기술 275 · 마법 338) |
| `SEvent1`~`SEvent7` | 퀘스트 | `quests.json` — **38개** |
| `ItemInfo8`~`ItemInfo11` | 아이템 | `items.json` — **2,110개**, 종류 37가지 |
| `NPCIllust` | NPC 이름 → 초상 파일 | `npc-portraits.json` — **170명** |

그리고 원작이 아닌 것 하나 — 서버가 짜 둔 NPC 행동:

| 원본 | 무엇 | 뽑은 것 |
|---|---|---|
| `database/server/scripts/Mundanes/*.cs` | NPC 스크립트 24개 | `npc-scripts.json` |
| `database/server/areas/*.json` | 맵 4개 | `areas.json` |
| `templates/warps/*.json` | 맵 사이의 문 4개 | `warps.json` |
| `templates/worldmaps/temuair.json` | 월드맵과 그 위의 문 2개 | `worldmaps.json` |
| `templates/monsters/**/*.json` | 괴물이 어느 맵에 나오나 | `monster-spawns.json` |

이건 **원작의 자료**다. 서버가 실제로 띄우는 것(`templates/`)과 다르다 — 그쪽은 괴물 3, 아이템 3,
마법 0으로 거의 비어 있다(`monster-behaviour.md` 7절).

---

## 2. 간선은 자료 안에 이미 있다

짐작할 것이 없다는 것이 이 자료의 좋은 점이다.

### 기술·마법의 선행 관계 — 403개

`SClass` 한 줄의 **넷째 칸**이 선행 조건이다.

```
Assail    1/0/0 | 1/223/10 | 3/3/3/3/3 | 0/0      | 0/0     ← 선행 없음
Assault   2/0/0 | 1/303/10 | 5/3/3/4/9 | Assail/1 | 0/0     ← Assail 1단계 필요
Rescue    2/0/0 | 13/143/54 | 8/4/4/4/5 | Assail/10 | 0/0   ← Assail 10단계 필요
```

613개 중 **403개에 선행이 있고**, 그 이름이 목록에 없는 것은 **단 하나**다. 표가 스스로 닫혀 있다는
뜻이라 그래프로 그대로 옮길 수 있다.

같은 이름이 여러 직업에 나온다 — `Assail` 은 다섯 직업이 다 배운다. 노트는 이름마다 한 장이고
`classes:` 에 배우는 직업을 모아 적는다.

### 퀘스트 → 사람

퀘스트 요약문에 이름이 그대로 적혀 있다: *"Talk to Alleen in Piet, or Aud in Abel, …"*.
`NPCIllust` 의 170명과 이름이 맞으면 링크를 건다.

### NPC 스크립트 → 아이템·마법

**NPC 가 무엇을 파는지는 스크립트에 없다.** `templates/mundanes/` 의 인스턴스가 `DefaultMerchantStock`
으로 정하도록 돼 있는데 **그 폴더가 비어 있다**. 기술을 누가 가르치는지도 `SkillTemplate.NpcKey` 가
정하는데, 있는 기술 템플릿 하나(`assail.json`)의 `NpcKey` 는 `null` 이다.

그래서 이을 수 있는 것은 스크립트가 **이름으로 직접 부르는 것**뿐이다:

```csharp
ServerContext.GlobalItemTemplateCache["Eppe"]          // shop / Class Chooser
ServerContext.GlobalSpellTemplateCache["ard cradh"]    // Dean
```

24개 중 **12개가 무언가를 이름으로 부른다.** 아이템 12개, 마법 2개, 그리고 퀘스트 깃발 이름
(`sunup_quest` 같은 것 — 원작 퀘스트 제목과는 이어지지 않는다).

### 원작인가, Hades 가 만든 것인가

부른 이름이 원작 자료표에 있는지로 갈린다. 그게 이 연결의 덤이다.

| | 원작에 있음 | Hades 가 만든 것 |
|---|---|---|
| NPC (24개 중) | **3** — Aoife · Erin · Raghnall | 21 (Dean · Benson · Delta · sunup · gos …) |
| 아이템 (12개 중) | **5** — Eppe · Magic Jade Ring · Silver Earrings · Snow Secret … | 7 (Training Staff · Used Boots · rat shit …) |
| 마법 (2개 중) | **2** — ard cradh · beag cradh | 0 |

`Shagreen Boots` 가 `ItemInfo` 에 없어 Hades 것으로 갈렸던 것과 같은 방법이다
(`where-the-answers-are.md` 4.6절).

### 맵 ↔ 맵

맵은 `areas/*.json` 이 정의한다(이름·크기·음악·`.map` 파일). 넷뿐이다:

| id | 이름 | 크기 |
|---|---|---|
| 1 | Safe House | 30 x 31 |
| 2 | Refugee Camp | 70 x 70 |
| 3 | Lost Woods | 255 x 255 |
| 99999 | Hades (죽으면 가는 곳) | 25 x 25 |

문은 `templates/warps/` 다. `WarpType` 이 `Map` 이면 `To.AreaID` 로 가고, `World` 면 월드맵 화면으로
나간다(그때 `To.Location` 은 `null`). 밟는 칸이 여럿일 수 있다 — 월드맵으로 나가는 문은 아홉 칸이
한 줄로 늘어서 있다.

```
Safe House 29,14  →  Refugee Camp 20,15
Refugee Camp 17,15  →  Safe House 24,13
Refugee Camp 69,30~38 (아홉 칸)  →  월드맵
Lost Woods  →  월드맵
```

월드맵(`Temuair`)에서는 그림 위의 점을 눌러 들어간다 — `442,225` 점이 Refugee Camp 로,
`417,50` 점이 Lost Woods 로. 그림은 `database/server/fieldmaps/field00#.png` 다.

**Safe House 에서 Lost Woods 로 바로 가는 길은 없다.** Refugee Camp 를 지나 월드맵으로 나갔다
들어와야 한다.

### 괴물이 뜨지 않는다

괴물 셋 다 `AreaID` 가 **3029** 인데 `areas/` 에 그런 맵이 없다. **그래서 하나도 뜨지 않는다.**
안전 가옥이 비어 있던 이유이고, 시험할 때 `safehouse_wasp`(AreaID 1)를 손으로 만들어야 했던
이유다(`monster-behaviour.md` 7절).

---

## 3. 뜻을 모르는 칸은 이름을 붙이지 않았다

확인된 것만 이름을 준다. 나머지는 `raw` 에 원문 그대로 남긴다 — **추측해서 적으면 다음 사람이
그것을 근거로 삼는다.**

| 자료 | 이름 붙인 것 | `raw` 로만 남긴 것 |
|---|---|---|
| 기술·마법 | 이름 · 기술/마법 · 직업 · 선행(이름+단계) | 첫째·둘째·다섯째 칸. 능력치 다섯은 값은 알지만 **어느 자리가 어느 능력치인지 모른다** |
| 아이템 | 이름 · 레벨 · 무게 · 종류 · 설명 | 둘째 칸 |
| 퀘스트 | 원문 칸 이름 그대로(`title` `sum` `reward` `result` …) | — |

레벨과 무게는 설명글과 맞춰 확인했다 — `Fine Dirk … 4 | 0 | 4 | Dagger | All Lev4, Wt 4` 에서
첫째가 `Lev4`, 셋째가 `Wt 4` 와 맞는다.

---

## 4. 노트 — Obsidian 으로 바로 열린다

```powershell
./scripts/build-game-data.ps1
# → data/game-data/vault/  (노트 2,969장, 링크 5,057개)
```

Obsidian 에서 `data/game-data/vault` 를 vault 로 열면 된다. 그래프 화면이 곧 선행 관계 지도다.

| 폴더 | 장수 | 무엇 |
|---|---|---|
| `abilities/` | 613 | 선행과 "이것이 열어 주는 것"이 양쪽으로 걸려 있다 |
| `items/` | 2,110 | 한 장씩. NPC·퀘스트가 이름으로 부르므로 한 장씩 있어야 링크가 이어진다 |
| `items/_kinds/` | 37 | 종류마다 목차 한 장 |
| `npcs/` | 170 | 이름과 초상 파일 |
| `npc-scripts/` | 24 | 부르는 아이템·마법, 퀘스트 깃발, 원작인지 여부 |
| `quests/` | 38 | 요약·보상·나오는 사람 |
| `maps/` | 4 + 1 | 나가는 문·들어오는 문·나오는 괴물. 한 장은 "없는 맵" |
| `index.md` | 1 | 들어가는 곳 |

**노트 2,969장 · 링크 5,057개 · 끊긴 링크 0개.**

**노트는 커밋하지 않는다**(`.gitignore`). JSON 이 단일 출처이고 노트는 거기서 언제든 다시 난다.

### graphify 를 그냥 돌리지 않은 이유

`graphify data/game-data` 는 **JSON 파일 네 개**를 본다. 613개 능력은 그 안의 줄이라 노드가 되지
않는다. 그리고 여기서는 LLM 이 관계를 짐작할 이유가 없다 — 선행 관계가 자료에 적혀 있다. 그래서
노트를 바로 쓴다. 만들어진 노트(2,969장)를 graphify 에 넣으면 커뮤니티 탐지는 따로 얹을 수 있다.

---

## 5. 아직 안 이은 것

- **아이템 ↔ 괴물** — 무엇이 무엇을 떨구는지는 이 자료에 없다. 서버가 정한다(`where-the-answers-are.md` 4.5절).
- **기술·마법 ↔ 배우는 곳** — `SClass` 의 남은 칸에 있을 수 있으나 확인하지 못했다.
- **NPC ↔ 파는 물건** — 스크립트가 아니라 `templates/mundanes/` 가 정하는데 **그 폴더가 비어 있다.**
  NPC 인스턴스를 만들기 전에는 이을 것이 없다.
- **기술 ↔ 가르치는 NPC** — `SkillTemplate.NpcKey` 가 정한다. 기술 템플릿이 하나뿐이고 그 칸이 비어 있다.
- **퀘스트 깃발 ↔ 원작 퀘스트** — 스크립트의 `sunup_quest` 같은 깃발 이름과 `SEvent` 의 제목은 서로 모른다.
- **맵 ↔ NPC** — NPC 인스턴스가 없으니 어느 맵에 누가 서 있는지도 없다.
- **괴물 ↔ 맵** — 이어 놓기는 했으나 가리키는 맵(3029)이 없어 실제로는 끊겨 있다.
