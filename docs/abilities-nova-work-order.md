# 작업지시서 — 기술·마법·이펙트·사운드 (노바 기준 정리 2차)

- 작성: 2026-09-27 (맥 세션, Claude Code)
- 받는 쪽: **안티그래비티(Antigravity) 세션** — 맥이든 윈도우든 이 저장소를 받은 곳. `AGENTS.md` 를 먼저 읽는다.
- 브랜치: `feature/nova-abilities-2` (지금 `test/hades-characterization` 에서 딴다). 끝나면 **푸시만** 한다.
- **하지 않는 것**: 클라우드 올리기(`scripts/cloud-server.sh deploy`)·앱 설치(`scripts/ios-build.sh install`) — 맥 세션이 받아서 한다.
  `sources/` 아래 원본 submodule 은 직접 push 하지 않는다. 단 서버 포크 `sources/wren11/Dark-Ages-Private-Server`(origin = `sangchane/Dark-Ages-Private-Server`)는
  우리 포크라 커밋·푸시하고 상위 저장소에 포인터를 올린다(`WORKFLOW.md`).

## 0. 지금까지 된 것 (2026-09-26~27, 이미 클라우드 반영)

- 기술·마법 **아이콘** = 노바 팩 `이미지` 번호 우선(`scripts/fill-ability-icons.py`).
- **이펙트 번호** = 노바 번호(원작 = 200 이하 옛 그림 — `docs/nova-client-work-order.md` 결과 절). 노바에 이펙트가 없으면 5.99 번호를 둔다(허공답보 68).
  생성기 `scripts/build-nova-effects.py` — 바꾼 줄 끝에 `// 노바 이펙트(5.99: …)` 로 원래 값.
- **자동 습득 표** = 노바 `db/script/스킬배우기.txt` 1차 스킬상인 + 전직 첫 기술(`scripts/build-auto-learn.py` →
  서버 `src/Hades.Server.Base/Types/AutoLearnTable.cs` · 앱 `mobile/client/assets/world/auto-learn.txt`).
  노바 목록에 없는 5.99 기술 31개는 `AutoLearnTable.Withdrawn` 으로 로그인·레벨업 때 **치운다**(`AutoLearn.cs`).
- 노바 기술 6개를 새로 옮김(`build-pack-abilities.py` 의 `FROM_NOVA`): 두번찌르기·마레네라·엑스마레나·디베노모·벨라르모·수페라벨라르모.

## 1. 사용자 결정 (2026-09-27, 원작을 기억하는 사람의 말 — 그대로 따른다)

| # | 결정 | 사용자 말 |
|---|---|---|
| D1 | **치운 것 중 원작에 있던 기술·마법은 되살린다**: 양의신권·백보신권·소수신공(무도가) · 일루메나(무도가·성직자) · 피닉스모드(전사) · 콘푸지오·딜루메니(마법사) — "등" 이라 했으니 **나머지 치운 목록도 원작 근거를 찾아 다시 가린다** | "원작에도 있는 기술, 마법이야" |
| D2 | **이펙트 속도는 전부 노바 값** — 쿠로토도 노바 75(지금 117 `KEEP_SPEED` 는 없앤다), 무도가 템플릿 기술도 노바 값 | "노바 기준에 맞춰둬" · "쿠로토 속도 노바 값으로" |
| D3 | **스크립트가 없는 기술은 다른 팩·참고 저장소에서 같은 아이콘이나 이름의 자료를 찾아보고 정한다**: 벨라르모·수페라벨라르모(방어력 명령 `belra` 없음), 통배권 | "다른 팩에도 같은 아이콘이나 이름으로 자료 없는지 찾아보고 결정" |
| D4 | **디베노모는 보류**(독이 우리 서버에 없음 — 표에는 두되 효과 작업 안 함, 문서에 "보류") | "같은 이유로 보류" |
| D5 | **통배권 아이콘 = 일음지와 같은 모양, 색만 붉은 계열** — D3 대로 찾고, 아이콘 번호도 그 근거로 | "일음지랑 같은 아이콘인데 색상만 붉은 계열" |
| D6 | **투핸드어택은 막는다** — 기술이 아니라 전사 무기 휘두르는 동작 자체다. 자동 습득 표에서 빼고 사범·운영자 외에는 배울 수 없게 | "전사용 무기 휘두르는 모션 자체야, 막아" |
| D7 | **정권은 막는다**(무도가 전직 첫 기술이지만 주지 않는다 — 지금처럼 운영자 명령으로만) | "정권 막아" |
| D8 | **새 무도가는 이형환위·단각·쿠로토를 배운 채 시작**(붕각은 빼고 노바대로 41레벨에 배운다) | "우리는 이형, 단각, 쿠로토를 배운채 시작" |
| D9 | 그 밖에는 **노바대로**(목록·레벨) | "노바대로인데 1번에 언급한 내용을 참고해" |

## 2. 할 일 (위에서부터, 각 단계 끝에 검증)

### T1. 원작에 있던 기술 되살리기 (D1)
- `AutoLearnTable.Withdrawn`(31개)을 하나씩 **원작 근거**로 다시 가린다. 근거 우선순위는 `AGENTS.md` 의 자료 출처 우선순위 그대로:
  원작 아카이브 SClass(`data/game-data/abilities.json`, 볼트 `data/game-data/vault-abilities/`) → 참고 저장소(`ETDA/BotCore/Shared/Collections.cs`,
  `SleepHunter4/data/*.xml` — `docs/where-the-answers-are.md`, `scripts/find-in-sources.ps1`) → 팩. 한글 이름 ↔ 영문 원작 이름 짝은 `docs/game-data.md`·
  `docs/skill-spell-2023.md`(2023 표, 별도 계보) 를 참고.
- 사용자가 이름을 댄 7개(양의신권·백보신권·소수신공·일루메나·피닉스모드·콘푸지오·딜루메니)는 **무조건 되살린다.** 배우는 **직업·레벨**은 원작 근거로,
  근거가 없으면 5.99 팩 레벨을 쓰고 보고에 "5.99 레벨" 로 적는다.
- 나머지 24개는 원작 근거가 있으면 되살리고, 없으면 치운 채로 둔다 — 표로 보고(이름 · 근거 · 결정).
- 되살린 것은 `Withdrawn` 에서 빼고 `build-auto-learn.py` 입력에 더한다(생성기로 — 손 편집 금지). 이미 치워진 캐릭터는 로그인 때 다시 채워진다(`AutoLearn.Catchup`).
- 검증: `AutoLearnTests` 에 "되살린 것은 치우지 않고 레벨이 되면 채운다" 시험을 더해 통과.

### T2. 이펙트 속도 전부 노바 (D2)
- `scripts/build-nova-effects.py` 의 `KEEP_SPEED = {"쿠로토"}` 를 없애 쿠로토 속도를 노바 75 로(몸동작 90 과의 짝은 `docs/martial-artist-skill-presentation.md` 에 남긴다).
- 무도가 템플릿 기술은 속도 칸이 없고 `database/server/scripts/Skills/…` 의 `MonkStrike`(Clobber.cs·beagsuainia.cs 근처)가 **100 을 박아** 보낸다.
  템플릿에 속도 칸(예: `TargetAnimationSpeed`)을 더하고 `MonkStrike` 가 그것을 읽게 한 뒤, 생성기가 노바 값을 적게 한다:
  구양신공·달마신공·마구때리기·붕신선각·연천단각·파천각 75 · 단각·붕각·선풍각 69 (`build-nova-effects.py` 가 보기로만 보여 주는 목록).
- 검증: `MonkPackAbilityTests`·`KurotoTests`(있는 이름으로)·`MonkLevelTenSkill*` 통과, 0x29 의 속도 칸을 보는 시험 하나.

### T3. 스크립트 없는 기술 찾기 (D3·D5)
- 대상: **벨라르모·수페라벨라르모**(방어력 명령 `belra`), **통배권**(노바 정의는 `SKILL_통배권` 인데 스크립트는 `통배권1` 뿐, 5.99 에 없음).
- 찾을 곳: 서버팩 3개(`data/server-packs/5.99-server`·`honden-community`·`novaonline`, 정규화본 `data/server-packs/extracted/`) — **이름**과 **아이콘 번호**로.
  통배권은 **일음지 아이콘(33)과 같은 모양의 붉은 판**을 기술 시트(`mobile/client/assets/ability/skill.png`, 16칸·35×35)에서 찾아 번호를 정한다.
  참고 저장소 16개(`scripts/find-in-sources.ps1`)도.
- 찾으면: 그 스크립트를 옮기는 길(`build-pack-abilities.py`)로 만들고, 없는 서버 명령(`belra` 등)은 `database/server/scripts/Pack599/Pack599.cs` 명령 처리에 더한다
  (방어력 올리기 = 기존 버프 구조 재사용). 못 찾으면 **만들지 않고** 보고(무엇을 어디서 찾았고 없었는지).
- 검증: 찾은 기술마다 격리 서버 시험 하나(쓰면 효과·이펙트·소리가 나간다).

### T4. 막기 (D4·D6·D7)
- **투핸드어택**: 자동 습득 표에서 뺀다. 이미 배운 캐릭터에서 치울지 — 전사 평타 동작과 겹치므로 **치운다**(`Withdrawn` 에 더함). 사범 메뉴에 있으면 그 갈래를 막는다.
- **정권**: 지금처럼 운영자 명령으로만. 표·사범에 없는지 확인.
- **디베노모**: 표에는 두되 "효과 보류" 를 문서(`docs/martial-artist-skill-presentation.md` 가 아니라 성직자 쪽 문서나 `docs/feature-map.md`)에 적는다.
- 검증: `AutoLearnTests`·`Pack599TeacherTests` 통과.

### T5. 새 무도가 시작 기술 (D8)
- `src/Hades.Server.Base/Network/Login/LoginServer.cs:214` 부근("A new Monk starts with exactly 이형환위 · 붕각 · 단각 and the spell 쿠로토")과
  `database/server/scripts/Mundanes/ClassChooser.cs:174` 부근에서 **붕각을 뺀다** → 이형환위 · 단각 · 쿠로토.
- 검증: 새 캐릭터 생성 시험(무도가) 기대값을 고치고 통과.

### T6. 밀레스 사범 (열린 질문 — 바꾸지 말고 보고만)
- 5.99 밀레스 사범 20명(`database/server/scripts/Pack599/Npcs`)이 아직 5.99 전용 기술을 가르친다(목록은 `AutoLearnTable.cs` 주석). T1 뒤에도 남는 5.99 전용
  메뉴를 표로 보고한다 — 막을지는 사용자가 정한다.

## 3. 사운드
- 이번 결정에 소리는 없다. T3 에서 새 기술을 만들면 소리 번호는 **그 기술을 찾은 팩의 값**을 쓰고, 노바에 같은 기술이 있으면 노바 값을 쓴다
  (소리 파일은 `mobile/client/assets/` 아래 — `docs/mobile-client.md` 의 소리 절). 번호만 적고, 소리 그림·파일을 새로 뽑아야 하면 보고.

## 4. 공통 규칙

- 자료는 **생성기로** 바꾼다(`--쓰기` 없으면 보기만). 생성기는 **더하고 고치기만** — 요청 없는 삭제 금지, `git stash`·`git checkout` 으로 되돌리기 금지.
  `scripts/build-monk-skills.py` 는 다시 돌리면 손본 템플릿 값(단각 Cooldown 4 · 이형환위 7 · 일음지 TargetAnimation)을 되돌린다 — 돌리지 말거나 그 규칙을 먼저 생성기에 옮긴다.
- 서버 C# 을 고치면 시험 전에 빌드: `dotnet build sources/wren11/Dark-Ages-Private-Server/src/Lorule.GameServer/Lorule.GameServer.csproj -c Debug`
  (격리 서버 시험은 그 `Staging/net9.0` 을 쓴다. `src/Hades.sln` 은 맥에서 윈도우 도구 때문에 실패).
- 시험: `dotnet test tests/hades-characterization --filter "FullyQualifiedName~AutoLearn|FullyQualifiedName~Pack599|FullyQualifiedName~Monk|FullyQualifiedName~Kuroto|FullyQualifiedName~Login"`,
  앱 알맹이 `dotnet test mobile/tests/Lod.Mobile.Core.Tests`. 끝에 서버 시험 전체 한 번(≈40분).
- 앱 자료가 바뀌면(`auto-learn.txt`, 아이콘) `python3 scripts/build-ability-icon-picker.py` 도 다시.
- 이 저장소에서 그냥 `git status` 는 submodule 을 훑다 멈춘다 — `git status --ignore-submodules=all` 또는 `git -C sources/<소유자>/<저장소> status`.
- 보고·커밋 메시지는 한국어. 커밋은 Conventional Commits.

## 5. 끝나면

1. 이 파일 끝에 「결과」 절: T1 표(되살림/그대로), T2 속도 목록, T3 찾은 것·못 찾은 것, T4·T5 확인, T6 표, 시험 결과.
2. 서버 포크 커밋·푸시 → 상위 저장소에 포인터와 함께 커밋 → `feature/nova-abilities-2` 푸시.
3. 맥 세션이 받아 서버 시험 전체 → 클라우드 올리기 → 앱 설치.
