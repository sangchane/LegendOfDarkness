# docs/ — 스코프 작업 지침

이 폴더의 파일을 다룰 때만 로드된다(온디맨드). 공통 행동규칙은 루트 `CLAUDE.md`,
프로젝트 표준·네비게이션은 `AGENTS.md`, 다음-할일은 `NEXT.md`를 따른다.

## 목표
설계·근거 문서, 작업지시서, 현황판(`index.html` 과 `*-data.js`).

## 소유 경로
`docs/` 전부. `*-data.js` 는 생성기 산출물 — 손으로 고치지 않는다(`scripts/CLAUDE.md`).

## 핵심 관례 — 무엇이 어디에
- **기능 39개가 서버·모바일·원작 중 어디에 있나(패킷 골격 포함): `docs/feature-map.md`** — 새 기능을 만들기 전에 여기부터
- **원작이 어떻게 했는지 막혔을 때 — 어디를 보나: `docs/where-the-answers-are.md`** (`scripts/find-in-sources.ps1` 한 줄로 참고 저장소 16개 검색)
- **괴물이 어떻게 움직이고 싸우나(선공·이동·공격): `docs/monster-behaviour.md`**
- **원작 기술·마법·퀘스트·아이템(선행 관계 그래프): `docs/game-data.md`** (`data/game-data/*.json`, Obsidian 노트 2,969장 — 맵·워프·NPC까지)
- **Hades 에 뭐가 이미 있나(만들 것 vs 채울 것): `docs/what-hades-already-has.md`** — 콘텐츠를 이식하기 전에 **여기부터**. 그래프·볼트에 묻는 법도 여기 있다
- **2023 기술·마법표(일반·어빌리티 539행): `docs/skill-spell-2023.md`** — 사용자 제공 워크북의 별도 계보; Hades 613개 표와 섞지 않는다
- **5.99 서버·클라이언트 실행 파일 역어셈블(식·평타 동작·장착 규칙·그리는 순서, 주소 근거): `docs/disassembly.md`** (`data/disassembly/findings.json` → 그래프)
- **기술·마법·이펙트·사운드 노바 정리 2차(안티그래비티 작업지시서): `docs/abilities-nova-work-order.md`** — 사용자 결정 D1~D9
- 노바 클라이언트 이펙트 그림 가져오기(윈도우 세션 작업지시서): `docs/nova-client-work-order.md` · `.dat` 목록·비교 `scripts/dat-manifest.py`
- 원작 우드랜드 확인(윈도우 세션 작업지시서): `docs/woodland-origin-work-order.md` — 지금 서버의 우드랜드는 5.99 팩이 새로 만든 판
- 포테의숲(1~6존 · 보스존 개인 던전 · 수오미 건물 문 — 5.99 map_create 사본은 서버 `Systems/Instances`): `docs/pote-forest.md`
- **서버팩 자료 — 방법·공통 규칙: `docs/server-pack-data.md`** (원작 자료와 별개)
  - 팩별 내용: `docs/server-packs/5.99-server.md` · `docs/server-packs/honden-community.md` · `docs/server-packs/novaonline.md`
  - **팩 3개와 Hades 를 나란히 놓고 본 것: `docs/pack-comparison.md`** — 무엇을 팩에서 가져오고 무엇을 가져오면 안 되는가 (`python3 scripts/compare-packs.py`)
- **`docs/index.html` = 「지금 무엇이 되나」** — 기능 39개 구현/미구현 · 괴물 스펙·드랍·경험치 · 기술 연출 · 워프 연결 · **원작과 달라진 것**. 숫자는 생성기가 뽑고, 낡으면 화면이 스스로 말한다 (`python3 scripts/build-data-freshness.py`)
- **UI 테마 — 원작 4.51(5.01 이전) 을 쓴다: `docs/original-ui-451.md`** (원작 UI 92장 `docs/ui/original-451/` · 눌러볼 시안 `docs/ui/mockups-451/index.html`). **화면을 만들거나 고치면 여기 규칙표부터** — 돌 두 가지·글자 색·치수가 거기 있다
- 서버 안정화 계획과 결과: `docs/hades-p0-stabilization-plan.md`
- 화면에 「왜 낡았나」 같은 잡일은 넣지 않는다 — 생성기 출력으로.

## 검증
- 현황판을 바꿨으면 `python3 scripts/build-data-freshness.py` 뒤 헤드리스로 `docs/index.html` 을 한 장 찍어 본다(보이는 브라우저 금지).
