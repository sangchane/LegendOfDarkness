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
| 대상 고르기 | **됨** — 사람을 탭하면 발밑 고리·머리 위 화살표·위 가운데 이름 |
| 인벤토리 | **됨** — `장비`·`소지품` 두 탭. 소지품은 원작식 아이콘 격자(입기·버리기·정렬), 장비는 원작 신형 장비창의 고리 배치 18자리에 종이인형(`docs/original-equipment-window.md`). 열려 있는 동안 걷기·탭이 막힌다. 벗기까지 된다(`0x44`) |
| 괴물·상인 | **됨** — `0x07` 을 읽어 그리고, 탭해서 고를 수 있다. 그림은 `assets/actor/creature/mns###.png` 이고 **그 옆 `.txt` 가 걷기·공격 프레임 구간을 말한다** — 괴물마다 다르다. 서버의 몬스터 템플릿이 부르는 번호만 뽑는다(지금 1·53·197), 없는 번호는 안 그린다 |
| 바닥 아이템 | **됨** — 그림도 나오고, 눌러서 줍는 것까지 이었다(`0x07`). 원작에 자동 루팅은 없으므로 밟아도 줍지 않는다 |
| 전투 | **됨** — 처치까지 확인했다. 휘두르는 동작이 그려지고(평타 `02` 파일), `공격`이 서버에 닿고(`0x13`), 서버가 하는 말이 아래 줄에 뜨고(`0x0A`), 맞은 대상의 체력이 위 가운데에 뜬다(`0x13`, 백분율). 남이 휘두르는 것도 그린다(`0x1A`). 죽은 뒤 떨어지는 물건은 못 읽는다 |
| 대화 | **안 됨** — 시안만 있음 |

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

### macOS (2026-09-11 확인)

같은 커밋을 Mac(Apple M2, macOS 26.5)에서 열어 **빌드·실행·촬영까지 확인했다.** 도구는 윈도우와 같은
버전을 작업공간 안에 둔다(`.tools/`, 커밋되지 않는다).

```bash
export DOTNET_ROOT="$PWD/.tools/dotnet-9.0.317"     # 윈도우와 같은 SDK 버전
export PATH="$DOTNET_ROOT:$PATH"
GODOT="$PWD/.tools/godot-4.6-mono/Godot_mono.app/Contents/MacOS/Godot"   # 4.6-stable mono, 윈도우와 같음
```

처음 한 번은 그림을 들여와야 한다 — `"$GODOT" --headless --path mobile/client --import`.

| | 결과 |
|---|---|
| `Lod.Mobile.Core` 빌드·시험 | **통과** — 경고 0, 시험 123개 |
| `LodClient` 빌드 | **통과** — 경고 0 |
| 게임 화면 실행·촬영 | **됨** — OpenGL 4.1 Metal(GL Compatibility), Apple M2 |
| 레이아웃 12화면 | **통과** — 어긋난 줄 없음 |

**Mac 에서 다르게 나오는 것 셋:**

1. **한글은 멀쩡한데 화면은 깨진다고 말한다.** `Main.BuildTheme` 이 `C:/Windows/Fonts/malgun.ttf` 가
   있는지만 보고 없으면 "글꼴 없음 — 한글이 깨집니다"라고 적는다. 그런데 macOS 에서는 Godot 이 시스템
   글꼴로 대신 그려 한글이 전부 제대로 나온다 — 로그인 화면이 그 문장을 한글로 또렷이 띄운다. 검사가
   **글꼴이 그려지는지**가 아니라 **윈도우 글꼴 파일이 있는지**를 보고 있어 생기는 거짓 경고다.
   글꼴을 프로젝트에 넣어야 한다는 결론 자체는 그대로다 — iOS·Android 에는 기댈 시스템 글꼴이 없다.
2. **세로에서만 GL 텍스처가 샜다 — 글꼴이 원인이었고 1번을 고치니 함께 사라졌다.** 끝날 때
   `Texture with GL ID of 31: leaked 131072 bytes` 가 떴다. 131072 는 256×256 LA8 한 장, 곧 **글꼴
   아틀라스 한 쪽**의 크기다. 윈도우 글꼴이 없을 때 이 화면은 아무 글꼴도 테마에 넣지 않았고, 한글은
   엔진이 뒤에서 시스템 글꼴로 대신 그려 주고 있었다 — 그렇게 생긴 아틀라스는 테마가 쥐고 있지 않아
   끝날 때 놓아주지 못했다. 세로에서만 난 것은 세로에만 있는 아래 기록 줄이 글자를 더 그려 아틀라스가
   한 쪽 더 필요했기 때문이다. 시스템 글꼴을 테마에 **명시적으로** 넣자 누수가 없어졌다. 지금 남는 것은
   `FontAdvanced ... were leaked at exit` 하나뿐이고, 그것은 검사기가 이미 허용하는 종류다.
3. **`--screen login` 이 매 프레임 경고를 쏟는다.** `LoginScreen._Process` 가
   `DisplayServer.VirtualKeyboardGetHeight()` 를 무조건 부르는데 데스크톱 display server 에는 화상
   자판이 없다 — 2초에 191줄. 윈도우 데스크톱도 같을 것으로 **추정**하나 거기서는 확인하지 않았다.

**`scripts/check-layout.ps1` 은 Mac 에서 못 돈다** — PowerShell 이 없고 기본 경로가 윈도우 exe 다.
위 12번은 같은 내용을 bash 로 옮겨 돌린 것이며 저장소에는 넣지 않았다.

그 검사기를 시험하는 `tests/check-layout-script.test.js` 도 `powershell.exe` 를 부른다. Mac 에서 이 파일은
**둘이 실패하고 셋은 거짓으로 통과한다** — 셋은 `status !== 0` 이면 되는데, 실행 자체가 안 되면 `status`
가 `null` 이라 그 조건이 그냥 맞아 버린다. 못 돈 시험이 초록으로 보이는 쪽이 못 도는 것보다 나쁘다.
나머지는 성하다: `node --test tests/*.test.js` 가 37개 중 그 둘만 빼고 통과한다.

**iOS 는 .ipa 까지 됐다**(2026-09-11). Xcode 26.5 · iOS 26.5 SDK 가 이 Mac 에 이미 있었다 — `xcode-select` 가
Command Line Tools 를 가리켜 `xcodebuild` 가 거부했을 뿐이라, `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`
를 주면 sudo 없이 쓴다. 게이트 프로젝트와 결과는 `experiments/godot-csharp-mobile-smoke/README.md`.
**남은 것은 실기기 설치뿐이고 iPhone 을 꽂아야 한다.**

### 화면이 들어맞는지

```powershell
./scripts/check-layout.ps1     # 여섯 크기 x (인벤토리 닫음/열음) = 12번
```

**빈 화면을 재면 아무것도 못 잡는다.** 전에는 `--pack` 이 90프레임 뒤에 여는데 검사는 3프레임 만에
끝나서, 열두 번 다 *닫힌* 화면을 쟀다 — 그래서 장비 칸을 붙여 패널이 넘쳤는데도 0 오류였다. 지금은
검사 중에는 바로 열고, 소지품 60칸과 장비 18자리를 채운 뒤, **두 탭을 각각** 잰다.

손으로 한 장 찍어 보려면 같은 내용물을 `--stuff` 로 부르고 `--gear` 로 장비 탭에서 연다:

```powershell
$godot --path mobile/client -- --screen game --size 360x780 --orient portrait `
  --pack --stuff --gear --shot out.png --shot-after 3
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
| `--pick` | (값 없음) | 서버가 보여 준 첫 사람을 실제 탭으로 골라 본다. 손 없이 확인할 때 |
| `--pack` | (값 없음) | 월드가 자리를 잡으면 인벤토리를 스스로 연다 |
| `--size` | `360x780` | 논리 화면 크기. 화면비를 바꿔 볼 때 |
| `--layout` | (값 없음) | 각 줄의 자리를 찍고, 넘치거나 겹치면 0이 아닌 값으로 끝낸다 |
| `--say` | 한 줄 | 월드에 들어간 뒤 그 말을 한다(`0x0E`) |
| `--lift` | (값 없음) | 바닥에 놓인 것 중 가장 가까운 것을 **그림 한가운데를 실제로 탭해서** 주워 본다. 보낼 때만 `GREYBOX_LIFTED` 가 찍힌다 |
| `--throw` | (값 없음) | 소지품 첫 칸을 버린다. `--lift` 와 같이 주면 버린 뒤 소지품을 닫고 그것을 다시 줍는다 |
| `--stuff` | (값 없음) | 서버 없이도 소지품 60칸·장비 18자리를 가짜로 채운다. 빈 화면을 재면 아무것도 못 잡는다 |
| `--gear` | (값 없음) | 소지품을 장비 탭에서 연다 |
| `--strike` | (값 없음) | 자리를 잡으면 **1초마다** 휘두른다(0.28초짜리 동작을 사진으로 잡으려고 — 화면의 버튼은 한 번 누르면 한 번이다). 잠시 뒤 괴물 체력과 서버가 한 말을 찍는다 |
| `--shot` | 파일 경로 | 한 장 찍고 종료 |
| `--shot-after` | 초 | 찍기 전에 기다릴 시간(접속처럼 프레임보다 느린 것) |

예 — 지역 서버에 붙어 북쪽으로 넷 걷고 찍기:

```powershell
& '<godot>' --path mobile/client -- --login wren:test1234 --walk NNNN --shot out.png --shot-after 10
```

### 화면이 다른 크기에서도 들어맞는지

```powershell
./scripts/check-layout.ps1
```

**레이아웃은 조용히 깨진다** — 줄 하나가 화면 밖으로 밀려도 아무 소리가 나지 않는다. 실제로 두 번
겪었다: 인벤토리 패널에 최소 높이를 박았더니 상태 막대와 방향판이 창 밖으로 나갔고, 조작 줄의
`ThumbSpanMaximum`(680)이 **최소** 너비로 걸려 있어 640 폭 화면에서 좌우로 20씩 삐져나갔다(그리고
세로 칸이 가장 넓은 자식을 따라가므로 상태 막대까지 같이 끌려 나갔다).

그래서 시안 2.1·2.3절이 정한 기준 크기와 그 양옆을 한 번씩 띄워 보고, 각 줄의 자리를 찍은 뒤
화면을 벗어나거나 서로 겹치면 실패로 끝낸다. 인벤토리를 연 상태로도 한 번씩 본다.

| 화면 | 왜 |
|---|---|
| 세로 360×780 | 시안 기준 |
| 세로 360×640 | 가장 낮은 세로 — 여기서 넘치면 다 넘친다 |
| 세로 360×800 | 20:9 |
| 가로 800×360 | 시안 기준 |
| 가로 640×360 | 16:9 — 조작 줄 상한이 걸리는 자리 |
| 가로 840×360 | 21:9 |

한 화면만 볼 때는 클라이언트에 직접 준다:

```powershell
& '<godot>' --path mobile/client -- --screen game --size 360x780 --orient portrait --layout
```

**웹에서처럼 폭이 아니라 높이가 먼저 터진다.** 세로 780은 시안이 52(상태) + 400(월드) + 76(기록) +
252(조작)으로 이미 다 쓰고 있어서, 새 줄에 최소 높이를 주면 반드시 무언가가 밀려난다. 새로 넣는
칸은 **남는 높이를 나눠 갖게**(`ExpandFill`) 하고, 닫혀 있을 때는 줄째로 숨겨 자리를 돌려준다.

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

부위마다 그림이 **두 벌**이다 — `mb001.png`(서기·걷기, `01` 파일)와 `mb00102.png`(평타, `02` 파일).
평타 파일은 네 칸뿐이고 걷기와 이어지지 않는다(문서 3.3절). 자기 평타 그림이 없는 부위(모자 같은)는
서 있는 그림 그대로 둔다 — 원작도 그렇다.

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

**바닥에 쌓인 물건은 서버를 다시 띄우면 사라진다** — 원작이 그렇고 Hades 도 그렇다. 저장되는 것이
아니라 서버 메모리에만 있다. 사람이 접속을 끊었다 들어오는 것은 상관없다.

**이것이 줍기를 확인하는 전제다.** 서버를 오래 켜 두면 같은 칸에 옛 물건이 겹겹이 쌓이는데, 그중에는
`Format07Handler` 가 조용히 건너뛰는 것이 섞여 있어 **눌러도 아무 일이 안 일어난다**. 되는지 안 되는지
보려면 **먼저 서버를 다시 띄워 바닥을 비우고** 새로 버린 것으로 확인한다 — 2026-09-11 에 그걸 모르고
한참 헤맸다. 한 판에서 버리고 바로 줍는 것은 이렇게 한다:

```powershell
& $godot --path mobile/client -- --login wren:test1234 --throw --lift --shot out.png --shot-after 22
```

**두 사람을 붙여 볼 때는 두 번째 클라이언트를 그때그때 새로 띄운다.** 오래 놔둔 클라이언트는 조용히
월드에서 빠져 있어 상대가 안 보인다 — 화면이 비어 보이면 그것부터 의심한다.

```powershell
Start-Process $godot -ArgumentList '--path','mobile/client','--','--login','friend:test1234'
& $godot --path mobile/client -- --login wren:test1234 --pick --shot out.png --shot-after 22
& $godot --path mobile/client -- --login wren:test1234 --wear --shot out.png --shot-after 30   # 첫 줄을 입어 본다
```

`--pick` 이 성공하면 로그에 `GREYBOX_PICKED <이름>` 이 찍힌다.

계정 두 개가 이미 있다 — `wren`(머리 3·색 20) / `friend`(머리 8·색 40), 둘 다 비밀번호 `test1234`
(머리 모양과 색을 일부러 다르게 해 두었다 — 사람마다 다르게 그려지는지 눈으로 볼 수 있게).
`wren` 의 소지품 1번 칸에는 **Shagreen Boots 한 켤레**를 넣어 두었다 — 인벤토리 화면을 확인하려고.

**아이템을 더 넣으려면 캐릭터 파일을 고친다.** `give <아이템>` 은 채팅 명령이 아니라 **주문**이라
캐스팅 경로가 필요하다(서버가 시작할 때 찍는 `(Usage: ...)` 줄은 전부 주문 목록이다). 대신
`tmp/hades-run/database/server/aislings/<이름>.json` 의 `Inventory.Items` 에 칸 번호를 키로 넣으면
서버가 로그인할 때 `Template.Name` 으로 진짜 템플릿을 찾아 채운다:

```json
"1": { "Template": { "Name": "Shagreen Boots" }, "Slot": 1, "Image": 1,
       "DisplayImage": 32882, "Color": 1, "Stacks": 1, "Durability": 100 }
```

넣은 뒤에는 **서버를 다시 띄운다**(이미 읽어 둔 것을 덮어쓴다). 쓸 수 있는 이름은 세 개뿐이다 —
`sources/wren11/Dark-Ages-Private-Server/database/server/templates/items/`.
(`tmp/hades-run/database/server/aislings/`). **클라이언트에 계정 생성 기능이 없어서 파일로 만든 것이다.**
두 사람이 서로 보이는지 확인하려면 클라이언트를 두 번 띄우면 된다.

**안전 가옥의 말벌도 손으로 만든 것이다.** `tmp/hades-run/database/server/templates/monsters/
insight_1/safehouse_wasp.json` — 저장소의 `sources/` 에는 없다. `AreaID 1`·(23,27)·HP 30·
`Drops: ["Shagreen Boots"]` · `LootType 36`(= `Gold` 32 + `Table` 4) 이라 잡으면 **장화와 돈을** 떨군다.
돈이 바닥에 남는 것을 보려면 `wren.json` 의 `GameSettings` 에서 `AUTO LOOT GOLD` 를 `false` 로 둬야
한다 — 켜져 있으면 서버가 곧바로 주워서 "You've Received N coins." 만 뜬다(그렇게 해 뒀다).
`tmp/` 를 지우면 이 말벌도 설정도 사라지므로, 다시 만들려면 이 문단이 근거다.

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
