# 자료 지도 — 무엇을 어디서 찾나

사용자(2026-10-09): 「어둠템관련해서 스펙 정리한것 엔피시나 기타 게임데이터 graphify 랑 볼트로 정리한 데이터끼리 연결고리 정리해서
작업할때 계속 같은 내용 찾거나 맥락 이해 못 하는 일 없도록 해 활용을 못 하는것 같아」.

## 먼저 한 줄

```bash
scripts/ask.sh <이름>          # 아이템·NPC·사냥터·괴물·서클·기술 — 볼트 노트 + 게임 그래프를 한 번에
scripts/ask.sh --코드 <이름>   # 서버·앱·참고 저장소 코드 그래프(graphify-out)
```

게임 자료를 고르거나 견주거나 고치기 전에(드랍 후보 · 상점 물목 · 서클 나눔 · NPC 자리 · 장비 수치) **템플릿 JSON 을 바로 뒤지지 말고 이것부터**.
노트는 생성기가 서버 자료에서 매번 다시 만든다 — 낡았으면 생성기를 돌린다(맨 아래 표).

## 게임 볼트 `data/game-vault/` (Obsidian) · 게임 그래프 `data/game-vault/graph/graph.json`

| 노트 | 무엇이 들었나 | 이어진 것(링크·간선) |
|---|---|---|
| `아이템/<이름>.md` | 갈래·값·레벨 · **수치 — 어둠템 대 서버**(같음·어긋남·없음) | 떨구는 괴물·사냥터 · 파는 NPC |
| `NPC/<이름>@<맵>.md` | 자리 · 역할(상점·은행·제작·전직·기술 …) · 하는 일 한 줄 · 물목 · 가르치는 기술 · 파는 서클 폭 | 서 있는 맵 · 파는 아이템 · 가르치는 기술 |
| `사냥터/<맵>-<이름>.md` | 괴물·경험치·추정 레벨 · 입장 레벨 · **서클** | 괴물 · 그 서클 |
| `괴물/<이름>@<맵>.md` | 경험치·골드·드랍 목록과 실제 확률 | 사냥터 · 아이템 |
| `서클/서클<n>.md` | 레벨 띠 · 그 서클 사냥터 · 상점(NPC) · 장비 수 | 사냥터 · NPC |
| `식/` | 드랍·골드·경험치 깎기 식(서버 코드 근거 줄) | 괴물·아이템 |

그래프 노드 이름표에는 **레벨·수치·역할·서클이 실려 있다** — `graphify query` 한 번에 이어진 것까지 보인다
(예: 「윙부츠 레벨1 방어력-1 … · 어둠템과 같음」 → 파는 NPC → 그 NPC 가 선 맵).

## 무엇의 정본이 어디인가

| 묻는 것 | 정본(자료) | 서버를 맞추는 생성기 · 지키는 시험 |
|---|---|---|
| 장비 수치·레벨·직업 | **어둠템** `docs/items/어둠템#1~5.xlsx` → `data/game-data/items-original-sheets.json` | `build-gear-from-original.py` · `tests/test_item_specs_follow_sheet.py` |
| 아이템 값(가격) | 서클 상한(사냥 10시간 금화) `docs/item-prices-by-circle.md` | `build-price-cap.py` |
| 드랍 목록·확률 | 서버 괴물 `Drops`·아이템 `DropRate` + `Formulas/monsterexp.cs` | `build-gear-drops` → `build-drop-variety` → `build-potion-by-level` → `build-drop-cap` |
| 상점 물목(서클별) | 서버 mundanes | `build-novice-gear-shops` · `build-town-gear-shops` → `build-circle-gear-shops` · `build-operator-shop`(운영자 전용) |
| NPC 역할·하는 일 | 서버 mundanes + 5.99 NPC 대본(`scripts/Pack599/Npcs`) | `scripts/lib/_npcs.py` → 앱 `guide.txt`(`build-client-guide.py`) · 게임 볼트 |
| 사냥터 레벨·서클 | 워프 레벨문 · 월드맵 · `eco-grounds.txt` | `build-hunting-ground-rules.py` · `build-eco-grounds.py` |
| 원작 기술·마법 613 | `data/game-data/abilities.json` · 볼트 `data/game-data/vault-abilities/` | `build-ability-vault.py` |
| 피해·방어·성장 식 | 서버 코드(볼트 `data/formula-vault/`) | `build-formula-vault.py` |
| 자료 출처 우선순위 | `data/CLAUDE.md` · 볼트 `data/truth-vault/` | `build-truth-vault.py` |
| 원작 아카이브 표 503 | 볼트 `data/archives-vault/` | `build-archive-vault.py` |
| 원작 UI 테마 | `data/original-ui/451.json` · 볼트 `data/ui-vault/` · 그래프 `data/original-ui/graph/` | `build-ui-graph.py` |
| 기능 39개가 어디 있나 | `docs/feature-map.md` | — |
| 원작이 어떻게 했나 | `docs/where-the-answers-are.md` · `scripts/find-in-sources.ps1` | — |
| 사용자 결정 | `NEXT.md` · `WORKLOG.md` · `autopilot/*/SPEC.md` | — |

## 다시 만들기

```bash
python3 scripts/gen/vault/build-game-vault.py --그래프   # 게임 볼트 + 그래프(그래프는 무시 목록 — 없으면 ask.sh 가 만든다)
python3 scripts/gen/vault/build-data-freshness.py         # 현황판 숫자가 낡았나
```
