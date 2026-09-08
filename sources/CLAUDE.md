# sources/ — 스코프 작업 지침

이 폴더의 파일을 다룰 때만 로드된다(온디맨드). 공통 행동규칙은 루트 `CLAUDE.md`,
프로젝트 표준·네비게이션은 `AGENTS.md`, 다음-할일은 `NEXT.md`를 따른다.

## 목표
외부 원본 저장소 17개를 Git submodule로 고정해 둔 읽기 전용 참조 영역.

## 소유 경로
- `sources/<owner>/<repo>/` 전부 **읽기 전용 입력**. 루트 저장소는 submodule 포인터만 소유한다.
- 실제 수정은 fork → 하위 저장소 브랜치 → 루트에서 포인터 갱신 (`WORKFLOW.md` "submodule 변경 흐름").

## 핵심 관례
- 전체 읽기 스윕 금지. 위치는 `docs/current-system-analysis/`로 찾고 필요한 파일만 연다.
- 빌드 산출물(bin/obj/node_modules, SQLite 파일)은 하위 저장소 안에 두고 루트에 올리지 않는다.
- 원본에 든 비밀값(`wren11/da/credentials.conf`, `FallenDev/Decipher`의 Sentry DSN)은 복사·재사용 금지.

## 검증
- `git submodule status` 로 포인터가 의도한 커밋인지 확인한다.
- 하위 저장소 빌드·실행은 `docs/run-procedure.md` 체크리스트를 따른다.
