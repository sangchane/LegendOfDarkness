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
- **원작이 어떻게 했는지 막혔을 때 — 어디를 보나: `docs/where-the-answers-are.md`** (`scripts/find-in-sources.ps1` 한 줄로 참고 저장소 16개 검색)
- 원작 스프라이트 방향·프레임 구간: `docs/original-sprite-animation.md`
- **괴물이 어떻게 움직이고 싸우나(선공·이동·공격): `docs/monster-behaviour.md`**
- **원작 기술·마법·퀘스트·아이템(선행 관계 그래프): `docs/game-data.md`** (`data/game-data/*.json`, Obsidian 노트 2,969장 — 맵·워프·NPC까지)
- **서버팩(5.99·혼든) db 자료 — 팩별 JSON·Obsidian·그래프: `docs/server-pack-data.md`** (원작 자료와 별개)
- 화면 배치(세로·가로): `docs/mobile-test-v1-wireframes.md` · 눌러볼 화면: `docs/index.html` (그림은 `docs/ui/assets/`)
- 서버 안정화 계획과 결과: `docs/hades-p0-stabilization-plan.md`
- 스코프 규칙(온디맨드): `sources/CLAUDE.md`
- 현행 프로젝트 분석서: `docs/current-system-analysis/README.md` (01~09)
- 서버 실행 절차·체크리스트: `docs/run-procedure.md`
- Git/Graphite 작업 규칙: `WORKFLOW.md` · 처음 받기: `README.md`

## 규약
- `sources/` 아래는 외부 원본 submodule. 직접 push 금지. 수정이 필요하면 fork 뒤 submodule 포인터만 갱신한다 (`WORKFLOW.md`).
- 저장소 전체 읽기 스윕 금지. 구조는 분석서로 파악하고, 코드는 작업에 필요한 파일만 연다.
- 원본에 비밀값이 들어 있다(경로만 기록): `sources/wren11/da/credentials.conf`, `sources/FallenDev/Decipher` 안의 Sentry DSN. 복사·재사용·커밋 금지.
- 커밋은 Conventional Commits, 브랜치는 `docs/…` `feature/…` `fix/…` (`WORKFLOW.md`).
