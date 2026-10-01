# AGENTS.md — LOD

> 크로스툴 프로젝트 가이드. Claude Code는 CLAUDE.md의 `@import`로, Antigravity·Cursor는 네이티브로 읽는다.
> 이 파일은 **단일 원본** — 프로젝트 표준·네비게이션·다음-할일 진입점을 여기 한 곳에 둔다. 얇게 유지(≤ ~4KB).

## 세션 시작 — 먼저 이것부터
1. `NEXT.md`의 `NEXT-ACTION` 마커 사이 **현재-작업 블록**을 읽어라. 그게 지금 할 일이다.
2. 특정 폴더의 파일을 다룰 땐 그 폴더의 `CLAUDE.md`(스코프 규칙)를 그때 읽어라 — `mobile/` `scripts/` `data/` `docs/` `sources/`.
3. 지난 이력은 `WORKLOG.md`, 당장 안 하는 미결은 `plans/backlog.md`(항상 읽을 필요 없음).

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
- 다음 할 일(단일 출처): `NEXT.md` · 대기·미결: `plans/backlog.md` · 히스토리: `WORKLOG.md`
- **기능 39개가 서버·모바일·원작 중 어디에 있나: `docs/feature-map.md`** — 새 기능을 만들기 전에 여기부터
- **Hades 에 뭐가 이미 있나(만들 것 vs 채울 것): `docs/what-hades-already-has.md`** — 콘텐츠를 이식하기 전에 여기부터
- **원작이 어떻게 했는지 막혔을 때: `docs/where-the-answers-are.md`** (`scripts/find-in-sources.ps1`)
- 모바일 클라이언트 빌드·실행·함정: `docs/mobile-client.md` → `mobile/CLAUDE.md`
- 문서·현황판·UI 테마·작업지시서 목록: `docs/CLAUDE.md` · 볼트·자료 출처: `data/CLAUDE.md` · 생성기·서버 스크립트: `scripts/CLAUDE.md`
- 현행 분석서 `docs/current-system-analysis/README.md` · 서버 실행 `docs/run-procedure.md` · Git 규칙 `WORKFLOW.md` · 처음 받기 `README.md`

## 규약
- **운영(2026-09-25): 개발은 맥(격리 서버 시험), 플레이는 클라우드.** 고치기·시험·커밋은 맥에서 → `LOD_CLOUD_IP=161.33.43.117 scripts/ops/cloud-server.sh deploy` 로 올린다(올리면 접속 중인 사람이 끊긴다 — 직전에 알린다) → 앱이 바뀌었으면 `scripts/ops/ios-build.sh install`. **캐릭터의 기준은 클라우드**(`… backup` 으로 받아 로컬에서 재현). 아이폰 앱 주소는 늘 클라우드 — 맥 서버(`com.lod.gameserver`)는 꺼 두고 필요할 때만 켠다
- 구현·검증·리뷰·커밋 절차는 아래 dev 작업 규칙(등급별 단계)을 따른다. 같은 파일을 고치는 작업은 동시에 여러 에이전트에 나누지 않는다.
- **자료 출처 우선순위**: Hades 자기 자료 → 원작 아카이브 → 참고 저장소 16개 → 서버팩(3개 모두 일치할 때만). 위쪽을 팩으로 덮지 않는다. 예외: 기술·마법 이펙트 번호·속도는 노바. 자세한 것 `data/CLAUDE.md`.
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
