# LOD workspace

Dark Ages 관련 원본 저장소, 현행 분석 문서, 이후 모바일 전환 작업을 함께 관리하는 상위 작업공간이다.

## 구조

- `sources/`: GitHub 원본을 소유자별 Git submodule로 고정한다.
- `docs/current-system-analysis/`: 확인된 현행 구조와 모바일 전환 판단을 기록한다.
- `WORKFLOW.md`: GitHub + Graphite 작업 규칙을 정의한다.

## 처음 받기

```powershell
git clone --recurse-submodules <이 작업공간의 GitHub URL>
cd LOD
.\scripts\gt.ps1 init --trunk main
```

이미 작업공간만 받은 경우에는 다음 명령으로 원본 저장소를 내려받는다.

```powershell
git submodule update --init --recursive
```

이 작업공간은 관리자 권한 없이 설치한 PortableGit을 사용하므로 Graphite 명령은
`.\scripts\gt.ps1 <command>` 형태로 실행한다.

## 분석 문서

[현행 프로젝트 분석서](docs/current-system-analysis/README.md)
