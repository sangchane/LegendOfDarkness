# scripts/ — 스코프 작업 지침

이 폴더의 파일을 다룰 때만 로드된다(온디맨드). 공통 행동규칙은 루트 `CLAUDE.md`,
프로젝트 표준·네비게이션은 `AGENTS.md`, 다음-할일은 `NEXT.md`를 따른다.

## 목표
자료 생성기(`gen/*/build-*.py`)·서버 운영 스크립트(`ops/` — `lod-server.sh`·`cloud-server.sh`·`ios-build.sh`)·검색 도구.

- `lib/` — 생성기가 같이 쓰는 코드(밑줄 이름 모듈: `_paths` ROOT · `_io` 읽기 · `_dotnet` · `_git` · `_graphify` · `_drops` …).
  새 생성기는 `ROOT`·`read` 를 복사하지 말고 `from lib._paths import ROOT` 로 쓴다. 다른 생성기의 함수가 필요하면
  importlib 으로 그 스크립트를 읽지 말고 그 부분을 `lib/` 으로 옮긴다.
- `ops/` — 서버·배포·앱 빌드(`lod-server.sh` `cloud-server.sh` `cloud-dashboard.sh` `ios-build.sh` `check-server-config.sh`
  `stop-hades.ps1` `ability-ops-service.py`, 설정 틀 `server-config/`).
- `gen/<도메인>/` — 파이썬 생성기(2026-10-02 옮김): `ability/` 기술·마법 · `client/` 앱 에셋 · `items/` 아이템·드랍·상점 ·
  `world/` 맵·워프·괴물·NPC · `pack/` 5.99 서버팩·역어셈블 · `vault/` 볼트·현황판. 새 생성기는 맞는 도메인에 두고
  머리의 `_sys.path.insert(… parents[2])` 두 줄을 복사한다(그래야 `lib/`·`graphify_runtime` 을 찾는다).
  여러 생성기가 같이 읽는 `graphify_runtime.py` `ability_name_consensus.py` `script_warps.py` 와 `.ps1`·`.sh` 도구는 `scripts/` 에 그대로.

## 소유 경로
`scripts/` 전부. 생성기가 쓰는 곳은 각 스크립트 머리 주석을 본다(서버 템플릿·`data/`·`docs/*-data.js`).

## 핵심 관례
- **큰 파일을 고칠 땐 먼저 `plans/split-when-touched.md`** — 그 파일을 어떻게 나눌지 적어 둔 지도. 고치는 작업 앞에 해당 줄 하나만 나눈다(나누기 커밋 따로).
- 생성기는 더하고 고치기만 한다 — 요청 없이 지우지 않는다. 대부분 `--쓰기` 를 줘야 파일을 쓴다(없으면 미리보기).
- 드랍 생성기 순서: `build-gear-drops` → `build-drop-variety` → **`build-drop-cap`**(마지막).
- `ops/cloud-server.sh deploy` 는 빌드하지 않고 맥의 `Staging/net9.0` 을 올린다 — 먼저 `dotnet build …/Lorule.GameServer.csproj`. 올리면 접속자가 끊긴다(직전에 알린다).
- 현황판 숫자가 낡았는지: `python3 scripts/gen/vault/build-data-freshness.py`.

- **서버를 켤 때 포트를 확인한다** — `ops/lod-server.sh` 가 2610·2615 가 열릴 때까지 기다리고, 안 열리면
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
