# 괴물은 어떻게 움직이고 싸우나 — 값이 어디에 있고 무엇을 바꾸나

- 기준일: 2026-09-11
- 왜 있나: 이동 패턴·공격 방식·선공 여부가 **세 곳에 나뉘어** 있다. 값은 템플릿 JSON, 이름표는
  `EnumReference.txt`, **판정은 `src/` 가 아니라 `database/server/scripts/` 의 Roslyn 스크립트**에
  있다. 드랍표와 같은 함정이다(`where-the-answers-are.md` 4.7절) — `src/` 만 뒤지면 못 찾는다.
- 그림 프레임은 여기가 아니다: [`original-sprite-animation.md`](original-sprite-animation.md) 4절.

---

## 1. 세 곳

| 무엇 | 어디 |
|---|---|
| 괴물 한 마리의 값 | `database/server/templates/monsters/<지역>/<이름>.json` |
| 숫자 → 이름표 | `database/server/EnumReference.txt` |
| 값을 상태로 바꾸는 곳 | `database/server/scripts/Creations/monsters.cs` |
| 매 순간의 판단 | `database/server/scripts/Monsters/CommonMonster.cs` (템플릿의 `ScriptName`) |
| 칸 정의 | `src/Hades.Server.Base/Templates/MonsterTemplate.cs` |

`scripts/Monsters/` 에는 `CommonMonster` 말고 `CommonPet` · `TowerDefense` · `TrainingDummy` 도 있다.
어느 것을 쓸지는 템플릿의 `ScriptName` 이 고른다. 지금 있는 괴물은 전부 `"Common Monster"` 다.

---

## 2. 선공 · 비선공 — `MoodType`

```
MoodQualifer   Idle:1  Aggressive:2  Unpredicable:4  Neutral:8  VeryAggressive:16
```

깃발(flags)이고, 스폰할 때 **한 번** `Aggressive` 참/거짓으로 접힌다
(`Creations/monsters.cs:162`):

| `MoodType` | 스폰 시 결과 |
|---|---|
| `Aggressive`(2) 깃발이 있으면 | **선공** |
| 아니고 `Unpredicable`(4) 이면 | **동전 던지기 한 번** — 50%보다 크면 선공. 그 마리는 죽을 때까지 그대로다 |
| 그 밖에 전부 | 비선공 |

**주의할 것 셋.**

- **`VeryAggressive`(16) 는 아무 일도 하지 않는다.** 16에는 2 깃발이 없어 `HasFlag(Aggressive)` 가
  거짓이고, 그대로 마지막 `else` 로 떨어져 **비선공**이 된다. `Idle`(1) `Neutral`(8) 도 같다 —
  실제로 뜻이 있는 값은 **2와 4뿐**이다.
- **`Unpredicable` 은 순간순간 변덕이 아니다.** 스폰 때 한 번 던지고 끝이다.
- **비선공은 "안 싸운다"가 아니라 "먼저 걸지 않는다"이다.** 맞으면 그 자리에서 선공으로 바뀐다
  (`CommonMonster.cs:102`, `OnDamaged`). 되돌아가는 길은 없다.

선공이 목표를 고르는 곳은 `CommonMonster.cs:340` — **사거리 안에서 가장 가까운 사람**이다.

---

## 3. 이동 — `PathQualifer`

```
PathQualifer   Wander:1  Fixed:2  Patrol:3
```

스폰할 때 `WalkEnabled` 로 접힌다(`Creations/monsters.cs:155`):

| 값 | `WalkEnabled` | 목표가 없을 때 하는 일 |
|---|---|---|
| `Wander`(1) | 참 | 아무 데나 (`Monster.Wander()`) |
| `Fixed`(2) | **거짓** | 서 있다 |
| `Patrol`(3) | 참 | `Waypoints` 를 돈다. 목록이 비었거나 없으면 그냥 `Wander()` (`CommonMonster.cs:388`) |

**`Fixed` 는 비선공일 때만 지켜진다.** 선공인 놈이 목표를 찾으면 `UpdateTarget` 이
`WalkEnabled = Target != null` 로 덮어쓴다(`CommonMonster.cs:348`). 즉 **선공 + Fixed 는 사람을
보는 순간 쫓아온다.** 서 있기를 바란다면 비선공이어야 한다.

목표가 있을 때는 `Walk()`(`CommonMonster.cs:352`)가 이렇게 한다:

1. **옆칸이면** — 바라보고 있으면 때리고, 아니면 방향만 돌린다(그 턴은 안 때린다).
2. **멀면** — `WalkTo(목표)`. 길이 막히면 `Wander()`.

빠르기는 **걸음 사이의 밀리초**다 — 작을수록 빠르다. 평소는 `MovementSpeed`, **목표가 생기면
`EngagedWalkingSpeed`** 로 바뀐다(`CommonMonster.cs:229`).

| | 평소 | 쫓을 때 |
|---|---|---|
| 말벌 | 1000 | 1000 (그대로) |
| 거미 | 1000 | **3000** (세 배 느려진다) |
| Minion | 500 | **1000** (두 배 느려진다) |

**셋 다 쫓을 때 더 빨라지지 않는다.** 둘은 오히려 느려진다 — 도망치면 못 따라온다는 뜻이다.
원작이 그랬는지는 확인하지 않았다. 이 값은 템플릿이 정하는 것이라 고치기는 쉽다.

---

## 4. 공격 — 시계 셋이 따로 돈다

`HandleMonsterState`(`CommonMonster.cs:227`)가 매 틱 세 시계를 굴린다. **서로 독립이다** —
때리면서 주문을 걸 수 있다.

| 시계 | 간격 | 켜지는 조건 | 하는 일 |
|---|---|---|---|
| `BashTimer` | `AttackSpeed` | `BashEnabled` — 목표가 옆칸이고 바라볼 때 | `Bash()` |
| `CastTimer` | `CastSpeed` | `CastEnabled` — 목표가 있을 때 | `CastSpell()` |
| `WalkTimer` | `MovementSpeed` → `EngagedWalkingSpeed` | `WalkEnabled` — 3절 | `Walk()` |

- **`Bash()`**(`:140`) — 앞칸에 뭔가 있어야 하고, 안 바라보고 있으면 **돌기만 하고 끝난다**.
  실제 기술은 템플릿의 `SkillScripts` 다. 지금 쓰이는 것은 `"Assail"`(평타) 하나뿐이다.
- **`CastSpell()`**(`:194`) — 목표가 사거리 안이어야 한다. `SpellScripts` 중 하나를 무작위로 고르되
  **`LoruleConfig.json` 의 `MonsterSpellSuccessRate` 확률로만** 성공한다. 그 뒤 `DefaultSpell` 이
  따로 한 번 더 나간다. 지금 있는 괴물에는 주문이 하나도 없다.

기술·주문 이름은 `ServerContext.GlobalSkillTemplateCache` / `GlobalSpellTemplateCache` 에서 찾는다 —
**이름이 거기 없으면 조용히 무시된다**(`LoadSkillScript` 가 통째로 `try/catch` 다).

---

## 5. 목표를 놓는 때

`UpdateTarget`(`:315`)이 매 틱 본다. 아래 중 하나면 놓는다:

- 목표가 죽었다 · 체력이 0이다
- 목표가 **숨었다**(`Invisible`)
- 목표가 **사거리 밖으로 나갔다**(`WithinRangeOf`)
- 목표가 괴물인데 근처에 사람이 있다 (사람을 우선한다)

놓으면 `BashEnabled` · `CastEnabled` 가 꺼진다(`ClearTarget`, `:219`).

---

## 6. 어디에 몇 마리 나오나

| 칸 | 뜻 |
|---|---|
| `SpawnType` | `Random`(2) 은 맵 아무 데나, `Defined`(4) 는 `DefinedX/Y` 에 고정 |
| `AreaID` | 어느 맵인가 |
| `SpawnMax` · `SpawnRate` · `SpawnSize` | 최대 마릿수 · 간격 · 한 번에 몇 |
| `SpawnOnlyOnActiveMaps` | 사람이 있는 맵에만 |
| `ImageVarience` | 0보다 크면 `Image` ~ `Image+Varience` 중 무작위로 그림을 고른다 |
| `Grow` · `IgnoreCollision` · `UpdateMapWide` | 성장 · 충돌 무시 · 맵 전체에 알림 |

`Level` 로 능력치가 자동으로 붙고(`monsters.cs`), `BonusMr = 10 * (Level / 20)` 이되 설정의
`BaseMR` 에서 잘린다.

---

## 7. 지금 있는 것 — 저렙 사냥터 셋뿐

**이 저장소에는 괴물 템플릿이 셋밖에 없다**(`tmp/hades-run` 에 손으로 만든 `safehouse_wasp` 가 하나
더 있다). 셋 다 **저렙 사냥터 몬스터**라 비선공인 것이 정상이다 — 원작에서도 레벨이 올라가면 선공
몬스터가 많아진다. **셋을 보고 서버 전체를 판단하면 안 된다.**

움직이는 기계(`scripts/`)와 그 기계에 넣을 내용(`templates/`)의 양이 크게 다르다:

| | 개수 |
|---|---|
| `scripts/Spells` · `Skills` · `Mundanes` … | **115개** — 기술·마법·NPC 행동이 다 짜여 있다 |
| `templates/monsters` · `items` · `spells` … | **17개** — 괴물 3, 아이템 3, 마법 0 |

즉 **규칙은 다 있는데 넣어 둔 내용이 거의 없는 상태**다. 원작의 자료표는 따로 있다 —
`database/server/metafile/` 에 기술·마법 641줄, 퀘스트 361줄, 아이템 2,110줄
(`where-the-answers-are.md` 4.6절).

| 이름 | `Image` | 기분 | 이동 | 기술 | 타격 / 시전 |
|---|---|---|---|---|---|
| `bees` 말벌 | 16385 → MNS001 | `Unpredicable`(4) — 동전 던지기 | `Wander` | 없음 | 1000 / 8000 |
| `Spider 5s` 거미 | 16437 → MNS053 | **`Neutral`(8) → 비선공** | `Wander` | `Assail` | 1000 / 2500 |
| `Minion` | 0x40C5 → MNS197 | `Unpredicable`(4) | `Wander` | 없음 | 1000 / 2000 |

셋 다 저렙다운 값이다 — 둘은 동전 던지기, 거미는 비선공. **선공 괴물을 만들 때는 `MoodType` 을
반드시 `2`로 적는다.** `16`(VeryAggressive)을 적으면 2절 때문에 오히려 비선공이 된다.

주문을 하나도 안 가졌는데 `CastSpeed` 는 다 적혀 있다. 시계는 도는데 걸 것이 없는 셈이다 —
`templates/spells` 가 비어 있는 것과 같은 이야기다.

`Spider 5s` 의 JSON 은 목록 끝에 쉼표가 남아 있고 셋째는 `Image` 를 `0x40C5` 로 적어 두었다.
서버의 파서는 둘 다 봐주지만 엄격한 파서는 거부한다 — `build-client-assets.ps1` 이 JSON 으로
읽지 않고 `Image` 줄만 집는 이유다.
