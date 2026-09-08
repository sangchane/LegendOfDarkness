# GitHub + Graphite 작업 규칙

## 관리 원칙

1. 이 루트 저장소는 분석 문서, 의사결정 기록, 통합 계획, submodule 기준 커밋을 관리한다.
2. `sources/`의 각 원본 저장소는 독립 Git 이력을 유지한다.
3. 외부 원본에 직접 push하지 않는다. 실제 수정이 필요하면 해당 저장소를 사용자 GitHub 계정으로 fork한 뒤 submodule의 `origin`을 fork로, 원본을 `upstream`으로 둔다.
4. 한 PR에는 한 가지 목적만 담고 Conventional Commits 형식을 사용한다.
5. Graphite의 trunk는 `main`이며, 작은 브랜치를 쌓아 stacked PR로 제출한다.

## 일반 작업 흐름

```powershell
gt sync
gt create --message "docs: describe current architecture"
# 파일 수정 및 검증
gt modify --commit
gt submit --stack
```

실제 명령 옵션은 설치된 `gt --help`를 우선한다.

## submodule 변경 흐름

1. 대상 `sources/<owner>/<repo>`에서 별도 브랜치를 만든다.
2. 해당 하위 저장소에서 코드 변경·테스트·PR을 완료한다.
3. 루트 저장소에서는 검증된 새 커밋을 가리키도록 submodule 포인터만 변경한다.
4. 루트 PR에 하위 저장소 PR 링크와 테스트 결과를 함께 기록한다.

## 브랜치와 커밋 이름

- 문서: `docs/<topic>` / `docs: ...`
- 기능: `feature/<topic>` / `feat(<scope>): ...`
- 수정: `fix/<topic>` / `fix(<scope>): ...`
- 구조 개선: `refactor/<topic>` / `refactor(<scope>): ...`
- 실험: `experiment/<topic>`

## 완료 기준

- 변경된 저장소의 빌드·테스트·정적 검사가 통과한다.
- 보안 관련 입력·인증·비밀정보를 검토한다.
- 루트 분석/결정 문서와 submodule 커밋이 실제 구현 상태와 일치한다.
- PR에는 변경 이유, 영향 범위, 검증 방법, 관련 하위 PR을 기록한다.

