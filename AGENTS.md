# AGENTS.md — LOD

> 크로스툴 프로젝트 가이드. Claude Code는 CLAUDE.md의 `@import`로, Antigravity·Cursor는 네이티브로 읽는다.
> 이 파일은 **단일 원본** — 프로젝트 표준·네비게이션·다음-할일 진입점을 여기 한 곳에 둔다. 얇게 유지(≤ ~4KB).

## 세션 시작 — 먼저 이것부터
1. `NEXT.md`의 `NEXT-ACTION` 마커 사이 **현재-작업 블록**을 읽어라. 그게 지금 할 일이다.
2. 특정 폴더의 파일을 다룰 땐 그 폴더의 `CLAUDE.md`(스코프 규칙)를 그때 읽어라.
3. 지난 이력이 필요하면 `WORKLOG.md`를 본다(여기 항상 읽을 필요는 없음).

## 프로젝트
Dark Ages(어둠의 전설) 계열 오픈소스 16개를 Git submodule로 유지하는 상위 작업공간. 최초 검토한 17개 중 `DungMunkey/Dark-Ages`는 모바일 MMORPG 기준선에서 제외해 2026-09-08 제거했다.
현행 구조를 분석해 두고, 이후 모바일 게임으로 전환하는 작업을 여기서 관리한다.
지금 단계는 **분석·실행 검증**이며 원본 소스는 수정하지 않는다.
형제 폴더 `D:\_personal\LOD_`는 원본 클라이언트 7.41·.dat 아카이브·추출 리소스를 가진 **자료 쪽**(게임 로직 없음)이고,
이 폴더는 **게임 로직 쪽**이다. 최종적으로 둘을 합쳐 모바일 전환의 베이스 자료로 모은다.

## 스택
- 루트: Markdown 문서 + PowerShell 래퍼(`scripts/gt.ps1`), Git submodule, Graphite stacked PR
- 서버 기준선: C#/.NET 5 — Hades/Lorule (`sources/wren11/Dark-Ages-Private-Server`)
- 모바일 클라이언트: C#/.NET 9 — 알맹이 `mobile/src/Lod.Mobile.Core`(엔진 없이 시험됨), 화면 `mobile/client`(Godot 4.6 + C#)
- 웹 실험: TypeScript/Bun + Phaser/Svelte — Medenia (`sources/FallenDev/dark-ages-ts`)
- 나머지 저장소(C# 도구, C++ 후킹 도구 등): `docs/current-system-analysis/01-repository-inventory.md`

## 네비게이션 (무엇이 어디에)
- 다음 할 일(단일 출처): `NEXT.md`
- 히스토리/핸드오프 로그: `WORKLOG.md`
- **모바일 클라이언트 — 빌드·실행·인자·함정: `docs/mobile-client.md`** (클라이언트를 만지면 여기부터)
- **기능 39개가 서버·모바일·원작 중 어디에 있나(패킷 골격 포함): `docs/feature-map.md`** — 새 기능을 만들기 전에 여기부터
- **원작이 어떻게 했는지 막혔을 때 — 어디를 보나: `docs/where-the-answers-are.md`** (`scripts/find-in-sources.ps1` 한 줄로 참고 저장소 16개 검색)
- 원작 스프라이트 방향·프레임 구간: `docs/original-sprite-animation.md`
- 무도가 1~10 기술 모션·이펙트·사운드 근거와 구현 계약: `docs/martial-artist-skill-presentation.md`
- **괴물이 어떻게 움직이고 싸우나(선공·이동·공격): `docs/monster-behaviour.md`**
- **원작 기술·마법·퀘스트·아이템(선행 관계 그래프): `docs/game-data.md`** (`data/game-data/*.json`, Obsidian 노트 2,969장 — 맵·워프·NPC까지)
- **Hades 에 뭐가 이미 있나(만들 것 vs 채울 것): `docs/what-hades-already-has.md`** — 콘텐츠를 이식하기 전에 **여기부터**. 그래프·볼트에 묻는 법도 여기 있다
- **원작 기술·마법 613개(선행 관계 포함): `python3 scripts/build-ability-vault.py` → `data/game-data/vault-abilities/`** (Obsidian). 팩의 153개와 다른 계보
- **세계가 굴러가는 식(피해·방어·성장): `python3 scripts/build-formula-vault.py` → `data/formula-vault/`** (Obsidian). **수치는 표에 없고 식이 코드에 박혀 있다** — 근거 줄과 **돌려 보고 재 본 값**까지 함께(방어가 피해를 늘린다·기술이 휘두를 때마다 오른다·괴물 템플릿의 체력이 버려진다 …)
- **원작 아카이브에 뭐가 들었나: `python3 scripts/build-archive-vault.py` → `data/archives-vault/`** (Obsidian). `.dat` 11개 안의 읽을 수 있는 표 503개. **팩을 뒤지기 전에 여기부터**
- **5.99 서버·클라이언트 실행 파일 역어셈블(식·평타 동작·장착 규칙·그리는 순서, 주소 근거): `docs/disassembly.md`** (`data/disassembly/findings.json` → 그래프)
- 원작 우드랜드 확인(윈도우 세션 작업지시서): `docs/woodland-origin-work-order.md` — 지금 서버의 우드랜드는 5.99 팩이 새로 만든 판
- 포테의숲(1~6존 · 보스존 개인 던전 · 수오미 건물 문 — 5.99 map_create 사본은 서버 `Systems/Instances`): `docs/pote-forest.md`
- **서버팩 자료 — 방법·공통 규칙: `docs/server-pack-data.md`** (원작 자료와 별개)
  - 팩별 내용: `docs/server-packs/5.99-server.md` · `docs/server-packs/honden-community.md` · `docs/server-packs/novaonline.md`
  - **팩 3개와 Hades 를 나란히 놓고 본 것: `docs/pack-comparison.md`** — 무엇을 팩에서 가져오고 무엇을 가져오면 안 되는가 (`python3 scripts/compare-packs.py`)
- 화면 배치(세로·가로): `docs/mobile-test-v1-wireframes.md` · 눌러볼 화면: `docs/index.html` (그림은 `docs/ui/assets/`)
- **UI 테마 — 원작 4.51(5.01 이전) 을 쓴다: `docs/original-ui-451.md`** (원작 UI 92장 `docs/ui/original-451/` · 눌러볼 시안 `docs/ui/mockups-451/index.html`). **화면을 만들거나 고치면 여기 규칙표부터** — 돌 두 가지·글자 색·치수가 거기 있다
  - 볼트로 묻기: `data/ui-vault/`(Obsidian) · 그래프 `python scripts/build-ui-graph.py` → `graphify query "..." --graph data/original-ui/graph/graph.json`. 단일 출처는 `data/original-ui/451.json`
- 서버 안정화 계획과 결과: `docs/hades-p0-stabilization-plan.md`
- 스코프 규칙(온디맨드): `sources/CLAUDE.md`
- 현행 프로젝트 분석서: `docs/current-system-analysis/README.md` (01~09)
- 서버 실행 절차·체크리스트: `docs/run-procedure.md`
- Git/Graphite 작업 규칙: `WORKFLOW.md` · 처음 받기: `README.md`

## 규약
- 구현·검증·리뷰·커밋 요청에는 `$service-prompt-workflow`가 자동 적용된다. 사용자 요청·`NEXT.md` 현재 블록·관련 변경 파일·실패 이력을 기준으로 내장 모델 라우팅을 적용하며, 같은 파일을 고치는 작업은 동시에 분산하지 않는다.
- **볼트는 저장소에 들어 있다 — 생성기를 돌리지 않고 그냥 Obsidian 으로 연다.** `data/truth-vault/`(자료 출처) · `data/formula-vault/`(식) · `data/archives-vault/`(아카이브 503표) · `data/game-data/vault-abilities/`(원작 기술·마법 613) · `data/ui-vault/`(원작 4.51 UI 와 테마 규칙). 낡았으면 해당 생성기로 다시 만든다. 팩 볼트(`data/server-packs/vault/`, 43MB)만 무시 목록에 있다
- **자료가 어디서 오나 (갈래별 셈): `python3 scripts/build-truth-vault.py` → `data/truth-vault/`** (Obsidian). **`database/assets/MetaFiles/` 를 먼저 본다** — 아이템 2,110·기술마법 613·퀘스트 38 이 거기서 나온다. 손으로 적은 표는 틀렸었다
- **자료 출처 우선순위** (위에서부터 찾고, 있으면 아래를 보지 않는다):
  1. **Hades 자기 자료** — `database/server/` · `database/assets/MetaFiles/` · `database/server/metafile/`(+`more`)
  2. **원작 아카이브** — `ItemInfo0~11`(아이템 6,199) · `SClass1~5`(기술·마법 613) · `SEvent1~7`(퀘스트 38) · `.dat` 11개
  3. **참고 저장소 16개** — 원작을 관찰해 사람이 적은 것. `ETDA/BotCore/Shared/Collections.cs` 에 기술·마법의 직업과 **쿨다운**(`sCooldown` — 요구레벨이 아니다)이, `SleepHunter4/data/*.xml` 에 같은 성격의 표가 있다 (`docs/where-the-answers-are.md`)
  4. **서버팩** — **3개(5.99 · 혼든 · Novaonline)가 모두 일치할 때만** 후보. 불일치하면 **버린다**(사람에게 묻지 않는다)
  위쪽을 팩으로 **덮지 않는다.** 갈래별 셈: `python3 scripts/build-truth-vault.py`
- `sources/` 아래는 외부 원본 submodule. 직접 push 금지. 수정이 필요하면 fork 뒤 submodule 포인터만 갱신한다 (`WORKFLOW.md`).
- 저장소 전체 읽기 스윕 금지. 구조는 분석서로 파악하고, 코드는 작업에 필요한 파일만 연다.
- 원본에 비밀값이 들어 있다(경로만 기록): `sources/wren11/da/credentials.conf`, `sources/FallenDev/Decipher` 안의 Sentry DSN. 복사·재사용·커밋 금지.
- 커밋은 Conventional Commits, 브랜치는 `docs/…` `feature/…` `fix/…` (`WORKFLOW.md`).
