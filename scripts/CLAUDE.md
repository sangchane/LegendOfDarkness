# scripts/ — 스코프 작업 지침

이 폴더의 파일을 다룰 때만 로드된다(온디맨드). 공통 행동규칙은 루트 `CLAUDE.md`,
프로젝트 표준·네비게이션은 `AGENTS.md`, 다음-할일은 `NEXT.md`를 따른다.

## 목표
자료 생성기(`build-*.py`)·서버 운영 스크립트(`lod-server.sh`·`cloud-server.sh`·`ios-build.sh`)·검색 도구.

## 소유 경로
`scripts/` 전부. 생성기가 쓰는 곳은 각 스크립트 머리 주석을 본다(서버 템플릿·`data/`·`docs/*-data.js`).

## 핵심 관례
- **큰 파일을 고칠 땐 먼저 `plans/split-when-touched.md`** — 그 파일을 어떻게 나눌지 적어 둔 지도. 고치는 작업 앞에 해당 줄 하나만 나눈다(나누기 커밋 따로).
- 생성기는 더하고 고치기만 한다 — 요청 없이 지우지 않는다. 대부분 `--쓰기` 를 줘야 파일을 쓴다(없으면 미리보기).
- 드랍 생성기 순서: `build-gear-drops` → `build-drop-variety` → **`build-drop-cap`**(마지막).
- `cloud-server.sh deploy` 는 빌드하지 않고 맥의 `Staging/net9.0` 을 올린다 — 먼저 `dotnet build …/Lorule.GameServer.csproj`. 올리면 접속자가 끊긴다(직전에 알린다).
- 현황판 숫자가 낡았는지: `python3 scripts/build-data-freshness.py`.

- **서버를 켤 때 포트를 확인한다** — `lod-server.sh` 가 2610·2615 가 열릴 때까지 기다리고, 안 열리면
  기록을 보여 주고 실패로 끝난다. "켰습니다" 만 믿고 헤매던 일이 있었다.

### 맥에서 서버 띄우기

```bash
cd sources/wren11/Dark-Ages-Private-Server/Staging/net9.0
DOTNET_ROOT=$PWD/../../../../../.tools/dotnet-9.0.317 dotnet Lorule.GameServer.dll
```
**빌드하면 `LoruleConfig.json` 이 덮어써진다** — 절대경로 셋과 `ServerIP` 를 다시 고쳐야 한다
(`docs/run-procedure.md` 0.5·0.6 절). 하네스 배경 작업으로 띄우면 메모리 압박에 잘리므로 `nohup … & disown`.

## 검증
- 생성기: `--쓰기` 없이 먼저 돌려 바뀔 것을 본다 → 쓴 뒤 `git diff --stat`.
- 서버 쪽을 바꿨으면 `dotnet test tests/hades-characterization/Hades.Characterization.Tests.csproj`.
