# 모바일 클라이언트 — 무엇이 있고 어떻게 돌리나

- 기준일: 2026-09-10
- 대상: 이 저장소를 처음 여는 사람(또는 새 세션)이 **모바일 클라이언트를 빌드·실행·검증**하는 데 필요한 것 전부.
- 서버 자체의 실행 절차는 [`run-procedure.md`](run-procedure.md), 스프라이트 형식은
  [`original-sprite-animation.md`](original-sprite-animation.md), 화면 배치는
  [`mobile-test-v1-wireframes.md`](mobile-test-v1-wireframes.md).

---

## 1. 어디까지 됐나

로그인해서 월드에 들어가고, 서버가 말한 칸에 서고, 걸으면 서버에 알리고, 다른 사람이 나타나고 걷고 사라지는
것까지 동작한다. 그림은 전부 이 저장소의 원본 `.dat`에서 뽑은 것이다.

| | 상태 |
|---|---|
| 로그인 → 캐릭터 확인 → 월드 입장 | 됨 |
| 지도·내 위치를 서버에서 받음 | 됨 |
| 걷기(예측 + 서버 정정) | 됨 |
| 다른 사람 표시·이동·사라짐 | 됨 |
| 사람마다 다른 옷 | **됨** — 서버가 말한 몸·머리·바지·신발·방패·갑옷·도포·무기·장신구를 겹쳐 그리고, 머리·신발·바지는 말한 색으로 염색한다 |
| 괴물·상인·바닥 아이템 | **안 됨** — 이 서버의 안전 가옥에 하나도 없어 확인 불가 |
| 전투·대화·인벤토리 | **안 됨** — 시안만 있음 |

---

## 2. 무엇이 어디에

```
mobile/
  src/Lod.Mobile.Core/          엔진 없는 클라이언트 알맹이. 여기가 시험된다
    Protocol/                     프레임·CP949 글자
    Protocol/Login/               암호·7.18 로그인 절차
    Net/                          소켓 하나, 로그인 전체 절차
    World/                        월드에 들어간 뒤 — 지도·위치·다른 사람
    Art/                          방향 규칙, 칸 → 화면 좌표
  tests/Lod.Mobile.Core.Tests/  위의 시험 42개 (서버 없이 돈다)
  client/                       Godot 4.6 + C#
    src/                          화면들
    assets/                       scripts/build-client-assets.ps1 이 만든 그림
tests/hades-characterization/   격리 서버를 띄워 돌리는 시험 46개
tools/dat-extract/              원본 .dat 에서 그림·지도를 뽑는 도구
data/legend-tables/             Legend.dat 에서 뽑아 둔 규칙 표
```

**엔진에 붙지 않는 것은 전부 `Lod.Mobile.Core`에 둔다.** 그래야 Godot 없이 시험할 수 있다.
방향 규칙·좌표 계산이 거기 있는 이유다.

---

## 3. 빌드와 실행

**.NET SDK는 작업공간 안에 있다**(`PATH`에 없다). 이걸 쓴다:

```powershell
$env:DOTNET_ROOT = 'D:\_personal\LOD\.tools\dotnet-9.0.317'
$dotnet = "$env:DOTNET_ROOT\dotnet.exe"
```

### 시험

```powershell
& $dotnet test mobile/tests/Lod.Mobile.Core.Tests/Lod.Mobile.Core.Tests.csproj   # 42개, 몇 초
& $dotnet test tests/hades-characterization/Hades.Characterization.Tests.csproj  # 46개, 약 1분
```

격리 시험은 **`Staging/net5.0`의 서버 바이너리를 실행한다.** 서버 코드를 고쳤으면 먼저 빌드해야
바뀐 것을 시험한다:

```powershell
& $dotnet build sources/wren11/Dark-Ages-Private-Server/src/Hades.sln -c Debug
```

### Godot 클라이언트

```powershell
& $dotnet build mobile/client/LodClient.csproj
& '.tools/godot-4.6-mono/Godot_v4.6-stable_mono_win64/Godot_v4.6-stable_mono_win64_console.exe' --path mobile/client
```

**C#을 고쳤으면 반드시 다시 빌드한다.** Godot는 직접 실행할 때 C#을 다시 컴파일하지 않는다.

### 실행 인자 (두 줄 대시 뒤)

| 인자 | 값 | 뜻 |
|---|---|---|
| `--screen` | `login`(기본) · `game` | 어떤 화면으로 시작할지. `game`은 서버 없이 그림만 |
| `--orient` | `landscape`(기본) · `portrait` | 화면 방향 |
| `--server` | `127.0.0.1:2610`(기본) | 로그인 서버 |
| `--login` | `이름:비밀번호` | 채워 넣고 바로 접속. 손 안 대고 확인할 때 |
| `--walk` | `NESW` 같은 글자열 | 그 순서로 걷는다 |
| `--shot` | 파일 경로 | 한 장 찍고 종료 |
| `--shot-after` | 초 | 찍기 전에 기다릴 시간(접속처럼 프레임보다 느린 것) |

예 — 지역 서버에 붙어 북쪽으로 넷 걷고 찍기:

```powershell
& '<godot>' --path mobile/client -- --login wren:test1234 --walk NNNN --shot out.png --shot-after 10
```

### 그림 다시 뽑기

```powershell
& $dotnet build tools/dat-extract/DatExtract.csproj
& ./scripts/build-client-assets.ps1
```

`dat-extract`는 **net8.0이라 시스템 dotnet으로 실행한다**(`C:/Program Files/dotnet/dotnet.exe`).
빌드는 작업공간 SDK로 한다. 스크립트가 이미 그렇게 되어 있다.

`assets/actor/parts/` 의 `dye-slots.txt`·`dye-colours.txt` 는 **그림이 아니라 글자 파일**이다. Godot
가 가져오지 않으므로 내보내기(export) 할 때 "리소스가 아닌 파일 필터"에 `*.txt` 를 넣어야 기기에서도
색이 나온다. 편집기에서 그냥 실행할 때는 신경 쓸 것 없다.

**부위 그림을 늘리려면** `build-client-assets.ps1` 위쪽 `$wardrobe` 목록에 번호를 더한다. 아카이브에
없는 번호는 "없음"만 찍고 넘어간다. 모든 부위를 **같은 칸(`80x88`)** 으로 뽑고 **바탕의 가운데를
맞추는** 것이 핵심이다 — 칸이 다르면 모자가 벗겨지고, 왼쪽 위를 맞추면 무기가 사람 옆에 뜬다. 새 그림을 넣은 뒤에는 Godot 가져오기를 한 번 돌린다:

```powershell
& '<godot>' --headless --path mobile/client --import
```

---

## 4. 지역 서버로 직접 확인하기

격리 시험은 자기 서버를 띄우지만, 화면을 눈으로 볼 때는 `tmp/hades-run`의 서버를 쓴다.

```powershell
./scripts/stop-hades.ps1                        # 먼저 항상 이것
Start-Process -FilePath 'D:\_personal\LOD\tmp\hades-run\Lorule.GameServer.exe' `
  -WorkingDirectory 'D:\_personal\LOD\tmp\hades-run' `
  -RedirectStandardOutput 'D:\_personal\LOD\tmp\hades-run\out.log' `
  -RedirectStandardError  'D:\_personal\LOD\tmp\hades-run\err.log'
```

로그에 `pet (Usage: pet)` 이 나오면 다 뜬 것이다(그게 마지막 줄이다). 끝나면 **반드시**
`./scripts/stop-hades.ps1`.

계정 두 개가 이미 있다 — `wren`(머리 3·색 20) / `friend`(머리 8·색 40), 둘 다 비밀번호 `test1234`
(머리 모양과 색을 일부러 다르게 해 두었다 — 사람마다 다르게 그려지는지 눈으로 볼 수 있게)
(`tmp/hades-run/database/server/aislings/`). **클라이언트에 계정 생성 기능이 없어서 파일로 만든 것이다.**
두 사람이 서로 보이는지 확인하려면 클라이언트를 두 번 띄우면 된다.

---

## 5. 걸려 넘어졌던 것들

앞으로도 같은 데서 넘어지기 쉬운 것만.

- **루트에서 그냥 `git status` 를 치지 않는다.** submodule 16개(그중 379MB 게임 폴더)를 훑느라 멈추고,
  멈추면 `index.lock` 때문에 git 전체가 마비된다. `git status --porcelain --ignore-submodules=all`.
- **`git add <폴더>` 로 디렉터리째 담지 않는다.** 이 저장소는 에이전트가 나눠 쓸 수 있고, 실제로 다른
  에이전트가 만든 `tests/docs-dashboard.test.js` 가 이쪽 커밋에 섞여 들어간 적이 있다. 경로를 명시한다.
- **서버를 띄웠으면 `scripts/stop-hades.ps1`.** 남은 프로세스가 다음 세션의 포트를 막는다.
- **격리 시험은 `Staging/net5.0` 을 실행한다.** 서버를 고치고 빌드하지 않으면 **옛 서버를 시험하고 통과한다.**
- **PowerShell → 네이티브 실행 파일에 한글 인자를 넘기면 깨진다.** `dat-extract`의 `투명` 인자가 그래서
  안 먹었고 말벌 시트에 배경이 칠해져 나왔다. ASCII 이름(`transparent`)도 받게 해 뒀다.
- **Godot 노드는 메인 스레드에서만 바꾼다.** 로그인 작업의 진행 상황은 큐에 넣고 `_Process`가 꺼낸다.
- **시험이 스스로 통과하지 않는지 본다.** 이번에만 두 번 있었다 — 걷기 거절 시험이 걷기 전부터 기대값과
  같아 아무것도 검사하지 않았고, 그 다음 판은 "지도 끝"이라 써 놓고 실은 속도 제한을 재고 있었다.
  **고친 뒤에는 일부러 되돌려서 빨개지는지 확인한다.**

---

## 6. 프로토콜 요약 (이미 읽는 것)

| 명령 | 방향 | 뜻 |
|---|---|---|
| `0x00` | 양쪽 | 클라이언트 버전 / 서버의 암호 매개변수 |
| `0x03` | C→S | 로그인 |
| `0x03` | S→C | 재접속 안내 — **평문** |
| `0x02` | S→C | 쪽지(거절 사유) — **암호문** |
| `0x10` | C→S | 입장권 제출 |
| `0x05` | S→C | **내 캐릭터 번호** (로그인 때 받은 번호와 다르다) |
| `0x15` | S→C | 지도 — 번호·크기·이름 |
| `0x04` | S→C | 내 위치 |
| `0x06` | C→S | 걷기 — 방향(0북 1동 2남 3서) + 걸음 수 |
| `0x38` | C→S | 새로고침 |
| `0x33` | S→C | 사람 표시 — 칸·방향·번호 (뒤쪽 겉모습은 아직 안 읽음) |
| `0x0C` | S→C | 누가 걸음 — **걸어온 칸**을 준다. 방향을 더해야 지금 칸 |
| `0x0E` | S→C | 사라짐 |

**서버는 허락한 걸음에 아무 말도 하지 않는다.** 주변 사람에게만 알린다. 그래서 클라이언트가 자기 그림을
먼저 움직이고, 서버가 말하면 그쪽으로 스냅한다. 거절하거나 너무 빠르면 그때 진짜 칸을 보내온다.
