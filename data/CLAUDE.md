# data/ — 스코프 작업 지침

이 폴더의 파일을 다룰 때만 로드된다(온디맨드). 공통 행동규칙은 루트 `CLAUDE.md`,
프로젝트 표준·네비게이션은 `AGENTS.md`, 다음-할일은 `NEXT.md`를 따른다.

## 목표
볼트(Obsidian)·자료 표·그래프. 대부분 `scripts/build-*.py` 가 만든다 — 손으로 고치지 말고 생성기를 고친다.

## 소유 경로
`data/*-vault/` · `data/game-data/` · `data/original-ui/` · `data/disassembly/` 등. `data/server-packs/vault/`(43MB)만 무시 목록.

## 핵심 관례
- **볼트는 저장소에 들어 있다 — 생성기를 돌리지 않고 그냥 Obsidian 으로 연다.** `data/truth-vault/`(자료 출처) · `data/formula-vault/`(식) · `data/archives-vault/`(아카이브 503표) · `data/game-data/vault-abilities/`(원작 기술·마법 613) · `data/ui-vault/`(원작 4.51 UI 와 테마 규칙) · `data/drop-vault/`(사냥터·괴물·아이템·골드식 — `graphify query "..." --graph data/drop-vault/graph/graph.json`). 낡았으면 해당 생성기로 다시 만든다. 팩 볼트(`data/server-packs/vault/`, 43MB)만 무시 목록에 있다
- **자료가 어디서 오나 (갈래별 셈): `python3 scripts/build-truth-vault.py` → `data/truth-vault/`** (Obsidian). **`database/assets/MetaFiles/` 를 먼저 본다** — 아이템 2,110·기술마법 613·퀘스트 38 이 거기서 나온다. 손으로 적은 표는 틀렸었다
- **자료 출처 우선순위** (위에서부터 찾고, 있으면 아래를 보지 않는다):
  1. **Hades 자기 자료** — `database/server/` · `database/assets/MetaFiles/` · `database/server/metafile/`(+`more`)
  2. **원작 아카이브** — `ItemInfo0~11`(아이템 6,199) · `SClass1~5`(기술·마법 613) · `SEvent1~7`(퀘스트 38) · `.dat` 11개
  3. **참고 저장소 16개** — 원작을 관찰해 사람이 적은 것. `ETDA/BotCore/Shared/Collections.cs` 에 기술·마법의 직업과 **쿨다운**(`sCooldown` — 요구레벨이 아니다)이, `SleepHunter4/data/*.xml` 에 같은 성격의 표가 있다 (`docs/where-the-answers-are.md`)
  4. **서버팩** — **3개(5.99 · 혼든 · Novaonline)가 모두 일치할 때만** 후보. 불일치하면 **버린다**(사람에게 묻지 않는다)
  위쪽을 팩으로 **덮지 않는다.** 갈래별 셈: `python3 scripts/build-truth-vault.py`
  - **예외 — 기술·마법 이펙트(그림) 번호는 노바 것을 쓴다.** 사용자 결정 2026-09-26: 「노바 것이 원작 이펙트다」. 이펙트 속도도 노바(2026-09-27). 노바에 이펙트가 없으면 5.99 번호를 둔다(허공답보 68). 동작·소리는 그대로. `python3 scripts/build-nova-effects.py --쓰기`
- **원작 기술·마법 613개(선행 관계 포함): `python3 scripts/build-ability-vault.py` → `data/game-data/vault-abilities/`** (Obsidian). 팩의 153개와 다른 계보
- **세계가 굴러가는 식(피해·방어·성장): `python3 scripts/build-formula-vault.py` → `data/formula-vault/`** (Obsidian). **수치는 표에 없고 식이 코드에 박혀 있다** — 근거 줄과 **돌려 보고 재 본 값**까지 함께(방어가 피해를 늘린다·기술이 휘두를 때마다 오른다·괴물 템플릿의 체력이 버려진다 …)
- **원작 아카이브에 뭐가 들었나: `python3 scripts/build-archive-vault.py` → `data/archives-vault/`** (Obsidian). `.dat` 11개 안의 읽을 수 있는 표 503개. **팩을 뒤지기 전에 여기부터**
  - 볼트로 묻기: `data/ui-vault/`(Obsidian) · 그래프 `python scripts/build-ui-graph.py` → `graphify query "..." --graph data/original-ui/graph/graph.json`. 단일 출처는 `data/original-ui/451.json`

## 검증
- 다시 만든 뒤 `git diff --stat data/` 로 바뀐 범위를 본다. 그래프는 `graphify query "..." --graph <그래프>` 로 한 번 물어본다.
