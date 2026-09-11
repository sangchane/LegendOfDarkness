# 원작 자료 — 기술·마법·퀘스트·아이템이 어디에 있고 무엇에 걸리나

- 기준일: 2026-09-11
- 만드는 법: `./scripts/build-game-data.ps1`
- 단일 출처: `data/game-data/*.json` (커밋됨) · 노트는 거기서 나므로 커밋하지 않는다

---

## 1. 어디서 나오나

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
# → data/game-data/vault/  (노트 2,964장, 링크 5,053개)
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
| `index.md` | 1 | 들어가는 곳 |

**노트 2,964장 · 링크 5,053개 · 끊긴 링크 0개.**

**노트는 커밋하지 않는다**(`.gitignore`). JSON 이 단일 출처이고 노트는 거기서 언제든 다시 난다.

### graphify 를 그냥 돌리지 않은 이유

`graphify data/game-data` 는 **JSON 파일 네 개**를 본다. 613개 능력은 그 안의 줄이라 노드가 되지
않는다. 그리고 여기서는 LLM 이 관계를 짐작할 이유가 없다 — 선행 관계가 자료에 적혀 있다. 그래서
노트를 바로 쓴다. 만들어진 노트(2,964장)를 graphify 에 넣으면 커뮤니티 탐지는 따로 얹을 수 있다.

---

## 5. 아직 안 이은 것

- **아이템 ↔ 괴물** — 무엇이 무엇을 떨구는지는 이 자료에 없다. 서버가 정한다(`where-the-answers-are.md` 4.5절).
- **기술·마법 ↔ 배우는 곳** — `SClass` 의 남은 칸에 있을 수 있으나 확인하지 못했다.
- **NPC ↔ 파는 물건** — 스크립트가 아니라 `templates/mundanes/` 가 정하는데 **그 폴더가 비어 있다.**
  NPC 인스턴스를 만들기 전에는 이을 것이 없다.
- **기술 ↔ 가르치는 NPC** — `SkillTemplate.NpcKey` 가 정한다. 기술 템플릿이 하나뿐이고 그 칸이 비어 있다.
- **퀘스트 깃발 ↔ 원작 퀘스트** — 스크립트의 `sunup_quest` 같은 깃발 이름과 `SEvent` 의 제목은 서로 모른다.
- **맵 ↔ 워프** — `templates/warps/` 4개와 `worldmaps/temuair.json`. 다음 차례.
