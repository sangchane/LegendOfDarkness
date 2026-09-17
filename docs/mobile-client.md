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
| 다른 사람 표시·이동·사라짐 | 됨 — 사람·괴물 모두 **칸 사이를 걸어서** 옮긴다(`Actor.GoTo`). 서버는 지금 칸만 주므로 사이는 클라이언트가 걷는 동작으로 채운다. 한 칸보다 멀면(방금 나타남·끌려감) 그냥 그 자리에 선다 |
| 사람마다 다른 옷 | **됨** — 서버가 말한 몸·머리·바지·신발·방패·갑옷·도포·무기·장신구를 겹쳐 그리고, 머리·신발·바지는 말한 색으로 염색한다 |
| 괴물·상인·바닥 아이템 | **안 됨** — 이 서버의 안전 가옥에 하나도 없어 확인 불가 |
| 대상 고르기 | **됨** — 사람을 탭하면 발밑 고리·머리 위 화살표·위 가운데 이름 |
| 인벤토리 | **됨** — `장비`·`소지품` 두 탭. 소지품은 원작식 아이콘 격자(입기·버리기·정렬), 장비는 원작 신형 장비창의 고리 배치 18자리에 종이인형(`docs/original-equipment-window.md`). 열려 있는 동안 걷기·탭이 막힌다. 벗기까지 된다(`0x44`) |
| 괴물·상인 | **됨** — `0x07` 을 읽어 그리고, 탭해서 고를 수 있다. 그림은 `assets/actor/creature/mns###.png` 이고 **그 옆 `.txt` 가 걷기·공격 프레임 구간을 말한다** — 괴물마다 다르다. 서버의 몬스터 템플릿이 부르는 번호만 뽑는다(지금 1·53·197), 없는 번호는 안 그린다 |
| 바닥 아이템 | **됨** — 그림도 나오고, 눌러서 줍는 것까지 이었다(`0x07`). 원작에 자동 루팅은 없으므로 밟아도 줍지 않는다 |
| 전투 | **됨** — 처치까지 확인했다. 휘두르는 동작이 그려지고(평타 `02` 파일), `공격`이 서버에 닿고(`0x13`), 서버가 하는 말이 아래 줄에 뜨고(`0x0A`), 맞은 대상의 체력이 위 가운데에 뜬다(`0x13`, 백분율). 남이 휘두르는 것도 그린다(`0x1A`). 죽은 뒤 떨어지는 물건은 못 읽는다 |
| 기술·마법 | **첫 세로 조각 됨** — 배운 목록과 원본 아이콘을 받고, 첫 기술·첫 마법 단축키가 실제 Hades 스크립트까지 실행된다. 대상형은 월드에서 고른 대상을 쓰며, 입력형 마법과 전체 기술·마법 창은 아직 없음 |
| 기술·마법 몸 동작 | **됨** — 서버가 보낸 동작 번호(`0x1A`)대로 직업 동작 파일(`b` 성직자 · `c` 전사 · `d` 무도가 · `e` 도적 · `f` 마법사)의 구간을 보는 방향의 앞/뒷모습으로 재생한다(`BodyMotion`). 나중에 온 동작이 앞 것을 대신한다. **마법은 대상 쪽으로 돌지 않는다**(사용자 결정 — 정면이어야 하는 것은 기술뿐). 그 동작 파일이 없는 부위(방패, 바지의 도적 동작)는 동작하는 동안 그리지 않는다. **직업 동작은 그 직업 의상을 입어야 온전하다** — 기본 옷으로는 원작도 몸이 비친다(사용자). 동작을 녹화해 보일 때는 직업 의상을 입힌다 |
| 기술 이펙트·소리 | **됨** — 이펙트(`0x29`)는 맞는 쪽·쓴 쪽 몸 위에, 땅 이펙트는 그 칸에 한 번 그리고 사라진다(`Flash`). 소리는 `0x19` 와 체력바(`0x13`) 끝 바이트 둘 다에서 온다 — 하데스의 맞는 소리는 뒤쪽이다. 그림은 `assets/effect/efct###.png`(102개) · 소리는 `assets/sound/N.mp3`(165개) |
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
**실기기에서도 떴다** — iPad 9세대(iPad12,2, iPadOS 26.5.2)에서 실행 확인했다(2026-09-11).

### 실기기에서 서버에 붙기 (2026-09-11 확인)

아이패드에서 Mac 의 Hades 에 붙어 **월드 입장까지 확인했다**(서버 로그에 `lodtest : Welcome to Lorule`).
막은 것이 넷이었고 전부 다른 층이었다.

**① 서버 주소는 빌드 설정이지 화면이 아니다 — 정해 둔 것.** 로그인 화면에 주소 칸을 두는 쪽도 있었지만
쓰지 않는다. **이 프로토콜의 구조가 그렇게 돼 있지 않다**: 클라이언트는 로그인 서버 하나만 알면 되고,
그 다음 접속처는 서버가 `MServerTable.xml` 로 내려준다(0.6절). "클라이언트가 서버 목록을 고른다"는
개념이 원작에 없고, 시안 3절이 `서버 목록 선택`을 범위 제외로 둔 것도 같은 이유로 읽는다. 다른 서버를
가리키려면 다시 빌드한다. 실기기는 아이콘을 탭해 여는 것이 전부라 `--server` 도 환경변수도
받을 수 없다. 그렇다고 주소를 박으면 붙을 기계가 바뀔 때마다 소스를 고쳐야 한다. 읽는 순서는 이렇다:

```
--server 인자  →  LOD_SERVER 환경변수  →  res://server.cfg 첫 줄  →  127.0.0.1:2610
```

`mobile/client/server.cfg` 는 **커밋되지 않는다**(`.gitignore`). 한 줄에 `host:port` 만 적는다. 프리셋의
`include_filter="*.cfg"` 가 그 파일을 빌드에 싣는다.

**② `OS.HasFeature("mobile")` 로 기기를 가려내면 안 된다.** Godot 4 의 iOS 에서 거짓이다. 그것 때문에
실기기가 `127.0.0.1`(자기 자신)에 붙으려다 **"연결이 거부되었습니다"** 가 났다. 기기를 가려야 하면
`OS.GetName()` 을 쓴다.

**③ iOS 로컬 네트워크 권한.** iOS 14 부터 같은 망의 기기에 붙으려면 권한이 필요하고, `Info.plist` 에
`NSLocalNetworkUsageDescription` 이 없으면 **묻지도 않고 조용히 막는다.** 서버에는 연결 시도조차 닿지
않는다. 프리셋의 `application/additional_plist_content` 에 넣어 뒀다.

**④ 서버가 알려 주는 주소가 두 군데에서 온다.** 이것이 가장 오래 걸렸다 — `run-procedure.md` 0.6절.

**확인하는 법.** 화면만 보면 전부 똑같이 "접속중" 이거나 "거부" 다. 서버 쪽에서 본다:

```bash
lsof -nP -iTCP:2610 -iTCP:2615 | grep <기기 IP>   # 어디까지 닿았나
grep "Welcome to Lorule" <서버 로그>              # 월드에 들어왔나
```

### iPad·iPhone 에 클라이언트 올리기 (2026-09-11 확인)

로그인 화면까지 iPad 9세대에서 확인했다. 준비물은 위 macOS 절과 같고, 여기에 `DEVELOPER_DIR` 이 필요하다.

```bash
export DOTNET_ROOT="$PWD/.tools/dotnet-9.0.317"; export PATH="$DOTNET_ROOT:$PATH"
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
# export_presets.cfg 의 application/app_store_team_id 에 Team ID 를 넣고 — 끝나면 반드시 되돌린다
"$GODOT" --headless --path mobile/client --export-debug "iOS"
xcrun devicectl device install app --device <기기> mobile/client/build/ios/LodClient.ipa
```

**`EXPORT SUCCEEDED` 를 믿지 마라.** 이것이 오늘 제일 비싸게 배운 것이다. iOS 는 AOT 로 게시되는데,
그때 ILC 가 내는 트림·AOT 분석 경고가 수십 개 나온다 — **전부 GodotSharp 안에서** 나오는 것이라 우리
코드로는 없앨 수 없다. 작업공간의 `TreatWarningsAsErrors` 가 그것을 오류로 올려 .NET 게시를 실패시키고,
**Godot 은 그 실패를 삼킨 채 `.ipa` 를 만든다** — C# 프레임워크가 통째로 빠진 채로. 서명도 정상이고
설치도 되고, 기기에서 엔진이 올라온 **직후에** 죽는다. 로그에 남는 것은
`Failed to build project. Check MSBuild panel for details.` 한 줄뿐이고 진짜 이유는 보여 주지 않는다.

그래서 `LodClient.csproj` 는 iOS RID 일 때만 `IlcTreatWarningsAsErrors` 를 끈다. 데스크톱 빌드의
엄격함은 그대로다. 그리고 **.ipa 가 제대로 됐는지는 크기가 아니라 프레임워크로 확인한다** — 빠진
빌드와 제대로 된 빌드가 둘 다 23MB 였다:

```bash
unzip -l mobile/client/build/ios/LodClient.ipa | grep LodClient.framework   # 없으면 C# 이 빠진 것
```

`mobile/client/LodClient.sln` 이 필요한 이유도 같은 종류다 — 없으면 Godot 이 "no solution file exists"
라고 적고 **C# 을 아예 게시하지 않은 채** 역시 성공으로 끝낸다. 윈도우에서는 `dotnet build` 를 csproj 로
직접 부르므로 이 구멍이 드러나지 않는다.

**smoke 게이트 통과가 클라이언트를 보장하지 않는다.** `experiments/godot-csharp-mobile-smoke` 는
`mobile/` 밖이라 `mobile/Directory.Build.props` 를 물려받지 않는다. smoke 는 처음부터 잘 나갔고
클라이언트만 죽었다.

기기 등록·개발자 모드 같은 iOS 쪽 함정은 `experiments/godot-csharp-mobile-smoke/README.md` 에 있다.

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
& $dotnet test tests/hades-characterization/Hades.Characterization.Tests.csproj  # 52개, 약 1분
& $dotnet test tools/tests/DatExtract.Tests/DatExtract.Tests.csproj              # 12개, 즉시
```

**격리 시험을 돌리기 전에 `sources/wren11/Dark-Ages-Private-Server/database/server/aislings/` 를 비운다.**
그 폴더가 비어 있어야 한다고 시험이 단언하는데, 손으로 만든 계정과 서버가 만드는 `.backup` 이 거기 쌓인다.

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

**움직임은 사진 한 장으로 못 잰다.** 걷기 사이·이펙트 한 번은 0.1~0.5초라 `--shot` 에 잘 안 걸린다.
Godot 의 녹화 모드로 프레임을 전부 떨군 뒤 이어서 본다 — 게임 시간이 프레임마다 1/fps 씩 가므로 느린 기계에서도 빠짐없다:

```bash
"$GODOT" --path mobile/client --write-movie out/f.png --fixed-fps 10 -- --login watch:1234 --hunt --shot out.png --shot-after 40
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

**기술 이펙트·소리**는 따로 뽑는다(`python3 scripts/build-client-effects.py`). 서버가 보낼 수 있는 번호
(5.99 스크립트의 `effect`, 하데스 템플릿의 `TargetAnimation`·`Animation`, 코드의 `SendAnimation`)만 모은다.
231 번까지는 `roh.dat` 의 EPF, **232 번부터는 5.99 한국 클라이언트(`~/Downloads/5.99 클라이언트/roh.dat`)에만 있고
형식도 EFA** 다(`dat-extract efa`). 프레임 수는 옆 `effects.txt` 에 적힌다 — 내보낼 때 `*.txt` 필터가 필요하다.
맥에서 그림을 들여올 때(`--import`) **`dotnet` 이 `PATH` 에 있어야** 한다 — 없으면 C# 쪽을 못 읽어 실패한다.

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
평타 파일은 네 칸뿐이고 걷기와 이어지지 않는다(문서 3.3절). 자기 동작 그림이 없는 부위는 그 동작 동안
그리지 않는다(`Actor.Play`).

**옷장 전체(갑옷·무기·투구·방패·신발)와 직업 기술 동작**은 `python3 scripts/build-client-wardrobe.py` 가 뽑는다.
서버 아이템 템플릿 중 `ScriptName` 이 `Armor`·`Weapon`·`Helmet`·`Shield`·`Boot` 인 것의 `Image` 가 입은 번호이고
(성별은 `Gender`), 그 부위마다 `01`·`02` 와 `mb001d.png`(몸의 무도가 동작)처럼 부위 이름 끝에 파일 글자.
**직업 갑옷·무기는 자기 직업 동작 파일만 갖는다**(전사 칼 `mw002` 는 `c`, 도적 단검 `mw028` 은 `e`).
칸보다 큰 그림은 자르지 않고 그 파일만 뺀다. **칸은 120x96** 이다 — 무기가 몸 밖으로 가로 114 · 세로 89 까지 뻗어 예전 80x88 에서는 무기 129 파일이 들어가지 않았다. 도구가 칸의 왼쪽 위에 맞춰 그리므로 칸을 키워도 발 자리(31.5, 83)는 그대로다. 번호가 255 를 넘는 무기 넷(280·612·647·665)은 하데스가
무기를 1바이트로 보내(`ServerFormat33`) 전달되지 않고, 원작 아카이브에도 그림이 없다. 칸 수는 b 14 · c 30 · d 18 · e 36 · f 12(`skill.tbl` 이 빈틈없이 채우는 만큼).
**부위마다 한 아카이브에서 전부** 뽑는데, 5.99 한국 클라이언트에 그 부위가 있으면 5.99 다 — 몸·신발·머리는 두
아카이브가 같지만 **바지·갑옷은 그림이 다르고** 하데스 바지는 동작 칸도 모자라다(`mn001c` 14칸 · 5.99 30칸).
그래서 그런 부위는 `01`·`02` 도 5.99 에서 다시 뽑는다. 여자 부위는 5.99 에 없어 하데스 것이다.
직업 의상(전사 옷 2 · 도적 옷 4)도 함께 뽑는다 — 직업 동작은 그 옷에서만 온전하다(`docs/original-sprite-animation.md` 3.4절).

**방패는 방향에 따라 겹치는 순서가 바뀐다** — 등을 보이면 몸 뒤, 앞을 보면 맨 위(`Actor.Face`,
`docs/original-sprite-animation.md` 2.1절).

**부위 그림을 늘리려면** 서버에 그 아이템 템플릿을 넣고 `build-client-wardrobe.py` 를 다시 돌린다(몸·바지·머리 모양은
`build-client-assets.ps1` 위쪽 `$wardrobe` 목록). 아카이브에
없는 번호는 "없음"만 찍고 넘어간다. 모든 부위를 **같은 칸(`120x96`)** 으로 뽑고 **바탕의 가운데를
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
| `0x1A` | S→C | 몸 동작 — 번호·동작(1 평타, 128+ `skill.tbl`)·속도·소리(255 없음) |
| `0x11` | S→C | 누가 제자리에서 돎 — 번호·방향. **괴물은 때리기 직전에 이것으로 상대를 본다** — 안 읽으면 마지막으로 걸은 쪽으로 휘두른다 |
| `0x0E` | S→C | 사라짐 |
| `0x29` | S→C | 이펙트 — 맞는 쪽 번호·쓴 쪽 번호·**맞는 쪽 그림**·쓴 쪽 그림·속도. 첫 번호가 0 이면 그림 하나와 칸(땅 이펙트). 하데스 `ServerFormat29` 의 인자 이름은 거꾸로다(`CasterEffect` 가 맞는 쪽에 그려진다) |
| `0x19` | S→C | 소리 — 빈 바이트 하나 뒤 번호 |
| `0x13` | S→C | 체력바 — 번호·체력(백분율, 255 는 표시 없음)·**소리**. 소리 0·255 는 안 낸다 |

**서버는 허락한 걸음에 아무 말도 하지 않는다.** 주변 사람에게만 알린다. 그래서 클라이언트가 자기 그림을
먼저 움직이고, 서버가 말하면 그쪽으로 스냅한다. 거절하거나 너무 빠르면 그때 진짜 칸을 보내온다.
