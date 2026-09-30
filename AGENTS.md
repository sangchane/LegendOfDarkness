# AGENTS.md — LOD

> 크로스툴 프로젝트 가이드. Claude Code는 CLAUDE.md의 `@import`로, Antigravity·Cursor는 네이티브로 읽는다.
> 이 파일은 **단일 원본** — 프로젝트 표준·네비게이션·다음-할일 진입점을 여기 한 곳에 둔다. 얇게 유지(≤ ~4KB).

## 세션 시작 — 먼저 이것부터
1. `NEXT.md`의 `NEXT-ACTION` 마커 사이 **현재-작업 블록**을 읽어라. 그게 지금 할 일이다.
2. 특정 폴더의 파일을 다룰 땐 그 폴더의 `CLAUDE.md`(스코프 규칙)를 그때 읽어라.
3. 지난 이력이 필요하면 `WORKLOG.md`를 본다(여기 항상 읽을 필요는 없음).

## 코딩 규율 — Karpathy guidelines
- **코딩 전 사고:** 추측하지 말고 가정·불확실성·트레이드오프를 먼저 드러낸다.
- **단순함 우선:** 요청을 해결하는 최소 코드만 작성하고, 보너스 기능·추상화·투기적 확장은 넣지 않는다.
- **외과적 변경:** 요청과 직접 관련된 코드만 바꾸며, 인접 코드의 무관한 리팩터링·정리는 하지 않는다.
- **목표 주도 실행:** 모호한 요청을 확인 가능한 성공 조건으로 바꾸고, 각 단계마다 가장 작은 검증을 남긴다.

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
- **2023 기술·마법표(일반·어빌리티 539행): `docs/skill-spell-2023.md`** — 사용자 제공 워크북의 별도 계보; Hades 613개 표와 섞지 않는다
- **세계가 굴러가는 식(피해·방어·성장): `python3 scripts/build-formula-vault.py` → `data/formula-vault/`** (Obsidian). **수치는 표에 없고 식이 코드에 박혀 있다** — 근거 줄과 **돌려 보고 재 본 값**까지 함께(방어가 피해를 늘린다·기술이 휘두를 때마다 오른다·괴물 템플릿의 체력이 버려진다 …)
- **원작 아카이브에 뭐가 들었나: `python3 scripts/build-archive-vault.py` → `data/archives-vault/`** (Obsidian). `.dat` 11개 안의 읽을 수 있는 표 503개. **팩을 뒤지기 전에 여기부터**
- **5.99 서버·클라이언트 실행 파일 역어셈블(식·평타 동작·장착 규칙·그리는 순서, 주소 근거): `docs/disassembly.md`** (`data/disassembly/findings.json` → 그래프)
- **기술·마법·이펙트·사운드 노바 정리 2차(안티그래비티 작업지시서): `docs/abilities-nova-work-order.md`** — 사용자 결정 D1~D9
- 노바 클라이언트 이펙트 그림 가져오기(윈도우 세션 작업지시서): `docs/nova-client-work-order.md` · `.dat` 목록·비교 `scripts/dat-manifest.py`
- 원작 우드랜드 확인(윈도우 세션 작업지시서): `docs/woodland-origin-work-order.md` — 지금 서버의 우드랜드는 5.99 팩이 새로 만든 판
- 포테의숲(1~6존 · 보스존 개인 던전 · 수오미 건물 문 — 5.99 map_create 사본은 서버 `Systems/Instances`): `docs/pote-forest.md`
- **서버팩 자료 — 방법·공통 규칙: `docs/server-pack-data.md`** (원작 자료와 별개)
  - 팩별 내용: `docs/server-packs/5.99-server.md` · `docs/server-packs/honden-community.md` · `docs/server-packs/novaonline.md`
  - **팩 3개와 Hades 를 나란히 놓고 본 것: `docs/pack-comparison.md`** — 무엇을 팩에서 가져오고 무엇을 가져오면 안 되는가 (`python3 scripts/compare-packs.py`)
- 화면 배치(세로·가로): `docs/mobile-test-v1-wireframes.md` · 눌러볼 화면: `docs/index.html` (그림은 `docs/ui/assets/`)
- **`docs/index.html` = 「지금 무엇이 되나」** — 기능 39개 구현/미구현 · 괴물 스펙·드랍·경험치 · 기술 연출 · 워프 연결 · **원작과 달라진 것**. 숫자는 생성기가 뽑고, 낡으면 화면이 스스로 말한다 (`python3 scripts/build-data-freshness.py`)
- **UI 테마 — 원작 4.51(5.01 이전) 을 쓴다: `docs/original-ui-451.md`** (원작 UI 92장 `docs/ui/original-451/` · 눌러볼 시안 `docs/ui/mockups-451/index.html`). **화면을 만들거나 고치면 여기 규칙표부터** — 돌 두 가지·글자 색·치수가 거기 있다
  - 볼트로 묻기: `data/ui-vault/`(Obsidian) · 그래프 `python scripts/build-ui-graph.py` → `graphify query "..." --graph data/original-ui/graph/graph.json`. 단일 출처는 `data/original-ui/451.json`
- 서버 안정화 계획과 결과: `docs/hades-p0-stabilization-plan.md`
- 스코프 규칙(온디맨드): `sources/CLAUDE.md`
- 현행 프로젝트 분석서: `docs/current-system-analysis/README.md` (01~09)
- 서버 실행 절차·체크리스트: `docs/run-procedure.md`
- Git/Graphite 작업 규칙: `WORKFLOW.md` · 처음 받기: `README.md`

## 규약
- **운영(2026-09-25): 개발은 맥(격리 서버 시험), 플레이는 클라우드.** 고치기·시험·커밋은 맥에서 → `LOD_CLOUD_IP=161.33.43.117 scripts/cloud-server.sh deploy` 로 올린다(올리면 접속 중인 사람이 끊긴다 — 직전에 알린다) → 앱이 바뀌었으면 `scripts/ios-build.sh install`. **캐릭터의 기준은 클라우드**(`… backup` 으로 받아 로컬에서 재현). 아이폰 앱 주소는 늘 클라우드 — 맥 서버(`com.lod.gameserver`)는 꺼 두고 필요할 때만 켠다
- 구현·검증·리뷰·커밋 요청에는 `$service-prompt-workflow`가 자동 적용된다. 사용자 요청·`NEXT.md` 현재 블록·관련 변경 파일·실패 이력을 기준으로 내장 모델 라우팅을 적용하며, 같은 파일을 고치는 작업은 동시에 분산하지 않는다.
- **볼트는 저장소에 들어 있다 — 생성기를 돌리지 않고 그냥 Obsidian 으로 연다.** `data/truth-vault/`(자료 출처) · `data/formula-vault/`(식) · `data/archives-vault/`(아카이브 503표) · `data/game-data/vault-abilities/`(원작 기술·마법 613) · `data/ui-vault/`(원작 4.51 UI 와 테마 규칙) · `data/drop-vault/`(사냥터·괴물·아이템·골드식 — `graphify query "..." --graph data/drop-vault/graph/graph.json`). 낡았으면 해당 생성기로 다시 만든다. 팩 볼트(`data/server-packs/vault/`, 43MB)만 무시 목록에 있다
- **자료가 어디서 오나 (갈래별 셈): `python3 scripts/build-truth-vault.py` → `data/truth-vault/`** (Obsidian). **`database/assets/MetaFiles/` 를 먼저 본다** — 아이템 2,110·기술마법 613·퀘스트 38 이 거기서 나온다. 손으로 적은 표는 틀렸었다
- **자료 출처 우선순위** (위에서부터 찾고, 있으면 아래를 보지 않는다):
  1. **Hades 자기 자료** — `database/server/` · `database/assets/MetaFiles/` · `database/server/metafile/`(+`more`)
  2. **원작 아카이브** — `ItemInfo0~11`(아이템 6,199) · `SClass1~5`(기술·마법 613) · `SEvent1~7`(퀘스트 38) · `.dat` 11개
  3. **참고 저장소 16개** — 원작을 관찰해 사람이 적은 것. `ETDA/BotCore/Shared/Collections.cs` 에 기술·마법의 직업과 **쿨다운**(`sCooldown` — 요구레벨이 아니다)이, `SleepHunter4/data/*.xml` 에 같은 성격의 표가 있다 (`docs/where-the-answers-are.md`)
  4. **서버팩** — **3개(5.99 · 혼든 · Novaonline)가 모두 일치할 때만** 후보. 불일치하면 **버린다**(사람에게 묻지 않는다)
  위쪽을 팩으로 **덮지 않는다.** 갈래별 셈: `python3 scripts/build-truth-vault.py`
  - **예외 — 기술·마법 이펙트(그림) 번호는 노바 것을 쓴다.** 사용자 결정 2026-09-26: 「노바 것이 원작 이펙트다」. 이펙트 속도도 노바(2026-09-27). 노바에 이펙트가 없으면 5.99 번호를 둔다(허공답보 68). 동작·소리는 그대로. `python3 scripts/build-nova-effects.py --쓰기`
- `sources/` 아래는 외부 원본 submodule. 직접 push 금지. 수정이 필요하면 fork 뒤 submodule 포인터만 갱신한다 (`WORKFLOW.md`).
- 저장소 전체 읽기 스윕 금지. 구조는 분석서로 파악하고, 코드는 작업에 필요한 파일만 연다.
- 원본에 비밀값이 들어 있다(경로만 기록): `sources/wren11/da/credentials.conf`, `sources/FallenDev/Decipher` 안의 Sentry DSN. 복사·재사용·커밋 금지.
- 커밋은 Conventional Commits, 브랜치는 `docs/…` `feature/…` `fix/…` (`WORKFLOW.md`).

<!-- dev:start -->
# dev 작업 규칙

## 작업 원칙
- 요청한 범위대로 한다. 요청이 잘못됐거나 더 나은 방법이 있으면 한 문장으로 말하고, 조용히 좁히거나 넓히거나 바꾸지 않은 채 요청대로 진행한다.
- 사소한 판단은 스스로 하고 가정은 한 줄로 밝힌다. 해석에 따라 결과물이 크게 달라질 때만 묻는다.
- 문제를 푸는 최소 코드를 쓴다. 요청 밖 기능, 한 번 쓰는 추상화, 요청하지 않은 설정화는 넣지 않는다.
- 기존 코드를 고칠 때 요청과 무관한 코드·주석·포맷은 건드리지 않고 기존 스타일을 따른다. 내 변경으로 안 쓰게 된 것만 치우고, 원래 있던 죽은 코드와 요청 밖 버그는 끝에 알려만 준다.
- 사용자의 코드·결론·결정을 바꾸는 오류만 짧게 바로잡고, 영향 없는 사소한 실수는 고치고 넘어간다.
- 할 수 있는 다음 단계가 남았으면 예고만 하고 멈추지 않고 진행한다. 사용자 없이는 진행할 수 없거나, 위험하거나 되돌리기 어려운 작업일 때만 멈춘다.

## 이어가기
- 세션을 시작하면 프로젝트 루트 `NEXT.md`의 `NEXT-ACTION` 블록을 확인하고, 있으면 그 등급·스킬·단계에서 이어간다. "다음 진행해", "이어서"는 그 블록의 다음 할 일이다.
- M·L 작업은 단계를 마치거나 멈출 때 그 블록을 덮어쓴다(등급 · 스킬 · 단계 · 다음 할 일 1~3줄 · 산출물 경로). S 작업은 쓰지 않는다. 히스토리는 `WORKLOG.md`에만.

## 규모 판정
새 요청을 받으면 응답 첫 줄에 `등급: S|M|L — 이유 한 구절`을 쓴다. 이어가는 요청이면 NEXT.md의 등급을 따른다.
- **S**: 변경을 한 문장으로 설명할 수 있고 되돌리기 쉽다. 스킬을 부르지 않고 바로 고친 뒤 검증 명령을 한 번 돌린다.
- **M**: 기능 하나, 여러 파일, 게임·도구 프로토타입. `build` M 경로(1쪽 SPEC → 구현 → 검증 → 리뷰 1회).
- **L 신규**: 새 서비스(사용자와 데이터가 있는 제품을 처음 만드는 것 — 기능 수와 상관없이), 모르는 도메인, 되돌리기 어려운 설계 결정. `design`으로 설계한 뒤 `build` L 경로. 작은 서비스는 design이 lite·spike로 줄인다. 게임·도구 프로토타입은 M이다.
- **L 변경**: 기존 저장소에서 돈·인증·개인정보·데이터 마이그레이션을 건드리는 변경. design 없이 `build` L 경로(SPEC + 보안 리뷰).
- 버그는 규모와 상관없이 `build`의 디버깅 분기(재현 → 원인 → 수정)로 가고, 위험 모듈이면 리뷰를 1회 더한다.
- 기존 프로젝트가 막혔거나 지지부진하다는 요청("막혔어", "살려줘", "진도가 안 나가")은 `build`의 재정비 분기(실행해서 진단 → 원인을 버그·회귀·결정 공백·범위 팽창으로 분류 → 경로 확인 1회 → 해당 경로)로 간다. 첫 줄은 `등급: 재정비 — 진단 후 결정`이다.
- 새 서비스가 아닌데 애매하면 한 등급 낮게 시작하고, 진행 중 L 신호가 보이면 그때 올리며 한 줄로 알린다. 웹 UI가 있으면 `ui`를 함께 쓴다.

## 모델·effort·위임
- 모델과 reasoning effort는 사용자가 `/model`로 고른다. 나는 바꾸지 못한다. 모델 기본 effort로 시작하고, L 작업만 한 단계 올리는 게 맞다. L 작업을 기본보다 낮게 하고 있을 때만 `/model`로 올리라고 한 줄로 권한다.
- 스킬은 `$ui`, `$setup`으로 부르거나 요청에 맞으면 스스로 쓴다.
- `build`·`design`은 이 도구에 스킬로 없다. 그 등급의 괄호 안 단계를 직접 수행하고, design은 요구사항·아키텍처·API·테스트 설계를 문서로 먼저 쓰는 단계로 한다. 스킬 본문의 `dev:X`는 스킬 `X`다. 본문이 없는 다른 이름(`superpowers:*`, `ponytail:*`, `ecc:*`, `reviewer`·`deep`·`quick` 에이전트)을 가리키면, 그 이름이 뜻하는 단계를 직접 수행한다. 리뷰는 구현을 마친 뒤 diff만 다시 읽는 별도 단계로 한다.
<!-- dev:end -->
