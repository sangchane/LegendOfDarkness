# 원본 프로젝트 안전 실행 절차서

- 기준일: 2026-09-08
- 근거: `docs/current-system-analysis/` 01~09 + 이번 정적 조사(코드를 읽었을 뿐 **실행은 하지 않음**) + 이 PC 환경 점검
- 대상 후보
  - **A. Hades/Lorule 서버** (`sources/wren11/Dark-Ages-Private-Server`) + 원본 Windows 클라이언트
  - **B. Medenia** (`sources/FallenDev/dark-ages-ts`) 서버 + 브라우저 클라이언트
- 원칙
  1. `sources/` 아래 **소스 코드는 수정하지 않는다.** 바꾸는 것은 설정 파일과 `.env` 사본뿐이다.
  2. 접속은 `127.0.0.1`(내 PC) 안에서만 한다. 외부 서버로 나가는 연결은 만들지 않는다.
  3. 모든 단계에 "성공을 확인하는 방법"을 둔다. 확인이 안 되면 다음 단계로 가지 않는다.
- 표기: **확인됨**(코드·설정에서 직접 확인) / **추정**(구조로 판단, 실행 전) / **미확인**(실행해 봐야 안다)

---

## 0. 한눈에 보기

| | A. Hades 서버 | B. Medenia |
|---|---|---|
| 원본 파일 필요 | **필요**: Windows 클라이언트 7.18 (확인됨) | **불필요**: 변환된 자산 51,015개가 저장소에 있음 (확인됨) |
| 실행을 막는 것 | 설정 파일의 절대경로 3곳, 클라이언트 버전 불일치(7.18 ↔ 보유 7.41) | 코드에 박힌 포트 80·https 강제, 이 PC의 포트 80/8080/4000 점유, 외부 서버 접속 코드 |
| 소스 무수정으로 완주 가능? | 가능 (추정) | 우회로가 있으나 검증 안 됨 (추정) |
| 완성도 | 서버 로직·데이터 풍부 | 아이템·스킬·전투는 자리표시자 수준 (분석서 01) |

두 후보 모두 "실행해 보기 전" 단계다. 결정이 필요한 항목은 9절, 실제 순서는 10절 체크리스트에 있다.

---

## 1. 필요한 개발 도구와 버전

| 도구 | 필요한 것 | 근거 | 이 PC (2026-09-08 확인) | 판정 |
|---|---|---|---|---|
| .NET SDK | 5.0.x (`net5.0`) | `src/Lorule.GameServer/Lorule.GameServer.csproj:4`, `src/Hades.Server.Base/Hades.Server.Base.csproj:4`; README는 "VS2019 + .NET 5.0.1" | 5.0.214, 5.0.416 (+6.0, 8.0) | 확인됨 · 충족 |
| Visual Studio (선택) | 2019 이상 | `getting-started-developer-guide.md` | 2017 / 2019 / 2022 | 충족 |
| Bun | `^1.1.17` | `package.json:11` `packageManager: bun@1.1.17`, `README.md:9` | 1.4.2 | 추정 · 충족 (범위 안. 구형 `bun.lockb` 자동 변환 여부 미확인) |
| Node / npm | 클라이언트 dev 스크립트가 `npm run cdn` 사용 | `apps/client/package.json` | 24.12 / 11.6 | 충족 |
| Git | submodule 관리 | `.gitmodules` | 2.32 + `.tools/PortableGit` 2.55 | 충족 |
| Python | catch-up 훅 실행용 | `tools/hooks/print_next_action.py` | 3.14 (`python`; `python3`는 빈 스텁) | 충족 |
| 원본 클라이언트 | A: **7.18** | `src/Lorule.Config/LoruleConfig.json` `ClientVersion: 718`, `LoginServer.Format00Handler`가 비교 | `D:\_personal\LOD_\raw_data\DarkAges741single.exe` = **7.41** | **불일치** → 9절 질문 2 |
| 원본 .dat 아카이브 | B: 불필요 | `apps/client/public` 커밋 파일 51,015개(`git ls-files`) | `LOD_\raw_data\*.dat` 9개 보유 | B는 안 씀 |

용어: **SDK**는 빌드(소스→실행 파일) 도구 묶음, **런타임**은 실행 엔진이다. **Bun**은 Node.js처럼 TypeScript/JavaScript를 실행하는 프로그램이다.

.NET 5는 2022-05에 지원이 끝났다(확인됨, 분석서 03). 빌드·실행은 되지만 보안 패치가 없으므로 로컬 실행에만 쓴다.

---

## 2. 클라이언트 · 서버 · DB 실행 순서

### A. Hades
프로세스는 **하나**다. 로그인 서버와 게임 서버가 같은 프로그램 안에서 순서대로 뜬다(확인됨).

```
Lorule.GameServer (Program.Main)
 └ ServerContext.InitFromConfig()   설정 읽기
 └ ServerContext.Startup()          LoadAndCacheStorage(): 맵·템플릿·스크립트 로드
 └ ServerContext.StartServers()     Game.Start(2615) → Lobby.Start(2610)   (ServerContext.cs:193-217)
```

순서: ① 서버 실행 → ② 클라이언트 실행 → ③ 클라이언트가 2610(로그인)으로 접속 → 로그인 뒤 2615(게임)로 **리다이렉트**(서버가 "이제 저쪽 포트로 가라"고 넘겨줌). DB 단계는 없다(4절).

**포트 3개를 쓴다. 그리고 반드시 종료해야 한다(확인됨).** 2610(로그인)·2615(게임) 외에 게임 서버가 시작하면서 `http://localhost:2620/`을 **하드코딩**으로 연다(`Network/Game/GameServer.cs`의 `Start` → `Network/WS/ObjectServer.cs`). 설정 키가 없어 옮길 수 없으므로 이 PC에서 Hades는 **한 번에 한 대만** 뜬다. 남아 있는 서버가 2620을 쥐고 있으면 새로 띄운 서버는 `HttpListener` 예외를 `StartServers`의 `catch (SocketException)`이 못 잡아 **로그인 서버 없이 프로세스만 살아 있는** 상태가 된다(2026-09-09 재현).

- 서버를 띄우기 전과 세션을 끝낼 때 `powershell -File scripts\stop-hades.ps1`을 실행한다. 남은 프로세스를 종료하고 세 포트가 비었는지 확인해 준다.
- 격리 harness(`tests/hades-characterization/`)는 2620을 사전 검사해서, 다른 Hades가 떠 있으면 기다리지 않고 즉시 실패한다.

### B. Medenia
`bun run dev`(루트) 하나가 Turborepo로 아래를 **동시에** 띄운다(확인됨, `package.json`, `turbo.json`, `README.md:29`).

| 프로세스 | 명령 | 포트 | 근거 |
|---|---|---|---|
| 서버 | `apps/server`: `bun --inspect=4000 --watch ./src/index.ts` | TCP 2610(레거시, 웹 클라는 안 씀) + WebSocket **80**(dev) / 443(prod), 디버거 4000 | `game-server.ts:27-28`, `web-server.ts:9-19` |
| 자산 서버 | `apps/client`: `http-server ./public --cors -s` | 8080 | `apps/client/package.json` `cdn` |
| 클라이언트 | `apps/client`: `vite` | 5173 | Vite 기본값 |

순서(따로 띄울 때): ① 서버(DB 자동 생성) → ② 자산 서버 → ③ vite → ④ 브라우저에서 `http://localhost:5173`.

주의(확인됨): `WebServer`는 인자로 받은 포트를 무시하고 `ENV`가 `prod`가 아니면 80번에 묶는다(`web-server.ts:16-19`). 클라이언트도 `connect()`의 주소·포트 인자를 무시하고 항상 `${VITE_PROTOCOL}://${VITE_GAME_SERVER}`로 붙는다(`apps/client/src/network/client.ts:46`). 즉 **포트는 설정으로 못 바꾼다.**

---

## 3. 필요한 설정 파일과 변경해야 할 값

### A. Hades — `src/Lorule.Config/LoruleConfig.json` (유일한 런타임 설정, 확인됨)

| 키 | 현재 값 | 바꿀 값 | 비고 |
|---|---|---|---|
| `Content.Location` | `C:\\Users\\Dean\\Documents\\GitHub\\DarkAges-Lorule-Server\\database\\server` | `D:\\_personal\\LOD\\sources\\wren11\\Dark-Ages-Private-Server\\database\\server` | **필수**. 역슬래시는 두 번(`\\`) |
| `Editor.Location` | `...\\database` | `D:\\_personal\\LOD\\sources\\wren11\\Dark-Ages-Private-Server\\database` | 필수 |
| `Editor.GameLocation` | `...\\game` | `D:\\_personal\\LOD\\sources\\wren11\\Dark-Ages-Private-Server\\game` | 필수 |
| `Content.ServerIP` | `127.0.0.1` | 유지 | |
| `LOGIN_PORT` / `SERVER_PORT` | 2610 / 2615 | 유지 | 이 PC에서 비어 있음(확인됨) |
| `ClientVersion` | 718 | 결정 필요 (9절 질문 2) | `Format00Handler`가 이 값과 클라이언트 버전을 비교 |
| `GameMasters` | `["wren","lol"]` | 결정 필요 (질문 5) | 관리자 권한 계정명 |
| `DevMode` | false | 유지 | true면 기존 계정 자동 로그인(테스트 계정 없으면 무의미) |
| `UseLobby` / `DontSavePlayers` | true / false | 유지 | |

위험(확인됨): `Content.Location`이 없거나 오타면 `Program.cs`의 `Server` 생성자가 **아무 메시지 없이 그냥 종료**하고, 경로가 존재하지 않으면 `ServerContext.InitFromConfig()`(`ServerContext.cs:424-431`)가 **빈 폴더를 새로 만들고 조용히 진행**한다. "서버가 떴는데 아무것도 없다"면 이 키부터 본다.

**소스를 더럽히지 않는 방법(추정):** 이 파일은 빌드마다 출력 폴더로 복사되고(`Lorule.Config.projitems`, `CopyToOutputDirectory=Always`), 서버는 **현재 작업 폴더**에서 `LoruleConfig.json`을 읽는다(`Program.cs`, `SetBasePath(Directory.GetCurrentDirectory())`). 따라서 빌드 결과 폴더 `Staging\`를 루트의 `tmp\hades-run\`(`.gitignore`에 `tmp/` 있음)로 복사하고 **그 사본의** 설정만 고쳐 실행하면 submodule은 깨끗하게 남는다.

### B. Medenia — `.env` 두 개 (`.env.example`에서 복사, `.gitignore` 대상이라 submodule을 더럽히지 않음, 확인됨)

`apps/server/.env`

| 키 | 예시값 | 바꿀 값 |
|---|---|---|
| `SERVER_ENDPOINT` | `127.0.0.1` | 유지 |
| `ENV` | `dev` | 유지 (질문 3에서 `prod` 우회를 택하면 변경) |
| `CERT` / `KEY` | 빈 문자열 | 유지 (prod 우회 시 인증서 경로) |

`apps/client/.env`

| 키 | 예시값 | 바꿀 값 |
|---|---|---|
| `VITE_ASSET_PATH` | `http://localhost:8080/` | `http://localhost:8081/` (8080은 이 PC에서 사용 중) |
| `VITE_GAME_SERVER` | `127.0.0.1` | 유지 |
| `VITE_PROTOCOL` | `ws` | 유지 (prod 우회 시 `wss`) |

설정으로 못 바꾸는, 코드에 박힌 값(확인됨): 포트 80/443과 `https` 강제(`web-server.ts:16-19`), 클라이언트 접속 대상(`client.ts:46`), 모듈 로드 시 `da0.kru.com:2610`으로 접속하는 개발 잔재 코드(`apps/server/src/index.ts:1-45`). `AUTH_PORT`/`WORLD_PORT`는 `.env.example`에 없어 `NaN`이 되지만 리다이렉트 포트가 2610으로 하드코딩돼 실제로는 막지 않는다(추정).

---

## 4. DB 생성 및 연결 방법

### A. Hades — DB 없음, JSON 파일 저장소 (확인됨)
- 위치: `database/server/` 아래 `maps/*.map`(lod0·1·2·3·99999), `areas/*.json`(4개), `templates/{items,monsters,nations,popups,servervars,skills,warps,worldmaps}`, `scripts/{Areas,Creations,Formulas,Items,Monsters,Mundanes,Skills,Spells,Weapons}`(C# 스크립트, 시작 시 Roslyn으로 컴파일).
- 계정 저장소 `database/server/aislings/`는 저장소에 없고 첫 실행 때 자동 생성된다(`AislingStorage.cs:13-19`).
- "연결 확인" = 서버 시작 로그에서 맵·템플릿·스크립트 로드 메시지와 `aislings` 폴더 생성을 본다. 로드 실패는 `try/catch`로 삼켜지므로(`ServerContext.cs:193-238`) **로그를 반드시 읽는다**. 로그 형식은 미확인.

### B. Medenia — SQLite 파일 (확인됨)
- `apps/server/src/database/index.ts:4-10`: TypeORM `sqlite`, 파일 `db.sqlite`(상대경로 → `apps/server/db.sqlite`), `synchronize: true`.
- 첫 실행 때 파일과 `aisling` 테이블이 **자동 생성**된다. 마이그레이션·시드 스크립트는 없다.
- 드라이버는 `sqlite3`(네이티브 모듈). Windows용 미리 빌드된 바이너리가 `bun install`에서 잡히는지는 미확인.
- "연결 확인" = `apps/server/db.sqlite` 파일이 생기고 크기가 0보다 크다.

용어: **SQLite**는 파일 하나짜리 DB, **TypeORM**은 코드의 클래스를 DB 테이블로 맞춰 주는 라이브러리, `synchronize`는 시작할 때 테이블을 자동으로 맞추는 옵션이다.

---

## 5. 빌드 방법

### A. Hades (PowerShell, 루트 기준)
```powershell
cd sources\wren11\Dark-Ages-Private-Server
dotnet restore src\Hades.sln          # NuGet 패키지 내려받기 — 인터넷 필요 (원칙 2의 유일한 예외)
dotnet build src\Hades.sln -c Debug   # 결과: <저장소>\Staging\  (csproj OutputPath ..\..\Staging, 확인됨)
```
실행(3절의 사본 방식):
```powershell
cd D:\_personal\LOD
New-Item -ItemType Directory -Force tmp\hades-run
Copy-Item sources\wren11\Dark-Ages-Private-Server\Staging\* tmp\hades-run -Recurse -Force
# tmp\hades-run\LoruleConfig.json 의 경로 3곳 수정 (3절)
cd tmp\hades-run
dotnet Lorule.GameServer.dll
```
`dotnet run --project ...`는 작업 폴더가 프로젝트 폴더가 되어 `LoruleConfig.json`을 못 찾을 수 있다(추정). 출력 폴더에서 직접 실행한다. 사전 빌드 스크립트는 없고 `MServerTable.xml`, `netstandard.dll`, `Notification.txt`가 함께 복사된다(확인됨).

### B. Medenia
```powershell
cd sources\FallenDev\dark-ages-ts
bun install                                            # 패키지 내려받기 — 인터넷 필요
Copy-Item apps\server\.env.example apps\server\.env
Copy-Item apps\client\.env.example apps\client\.env    # 3절대로 VITE_ASSET_PATH 수정
```
워크스페이스 패키지는 `exports: ./src/index.ts`라 별도 빌드가 없다(확인됨). 실행은 `bun run dev`(전부 동시) 또는 10절처럼 프로세스별로 따로 띄운다.

---

## 6. 테스트 계정 생성 또는 확인 방법

### A. Hades
- 서버 명령·시드 파일로 계정을 만드는 경로는 **없다**(확인됨, `Commander` 명령 목록·grep 0건).
- 유일한 경로는 클라이언트 캐릭터 생성 화면: `LoginServer.Format02Handler`(이름 중복 확인, `LoginServer.cs:90-101`) → `Format04Handler`(생성, `:157-176`) → `Aisling.Create()` → `database/server/aislings/<이름>.json` 저장.
- 확인: 위 JSON 파일이 생겼는지 본다.
- 손으로 JSON을 쓰는 방법은 `Aisling` 스키마가 크고 미확인이라 권장하지 않는다.

### B. Medenia
- 브라우저 UI `CreateForm.svelte` → `CharacterCreationRequestPacket` → `AuthService.create()`(`apps/server/src/services/auth-service.ts:17-46`, argon2로 비밀번호 해시) → `db.sqlite`.
- 저장소에 기본 계정은 없다. `apps/server/src/index.ts:53`의 계정명은 외부 서버 접속용 잔재 코드이며 로컬 DB와 무관하다(확인됨).
- 확인: 생성 직후 같은 계정으로 로그인이 되는지로 본다(간단), 또는 SQLite 뷰어로 `aisling` 테이블 조회.

---

## 7. 로그인부터 맵 입장까지 검증하는 방법

### A. Hades — 서버 콘솔 로그 체크포인트 (확인됨)
| # | 신호 | 위치 | 뜻 |
|---|---|---|---|
| 1 | `Login server is online.` / `Game server is online.` | `ServerContext.cs:200-206` | 두 서버 기동 |
| 2 | (없음) | `LoginServer.cs:107-135` | 로그인 실패는 **클라이언트 메시지박스만** 뜨고 서버 로그 없음 |
| 3 | (없음) | `GameServerHandlers.cs:721-747` `Format10Handler` | 리다이렉트 명단에 없으면 **조용히 연결 끊김** |
| 4 | `<계정명> : Welcome to Lorule` | `GameServerHandlers.cs:2615-2669` `LoadPlayer()` | **로그인 성공 + 맵 입장 시작** — 사실상 유일한 성공 로그 |
| 5 | 클라이언트 화면에 맵 표시 | `StartingMap: 1` → `lod1.map` (추정) | 완주 |

성공 기준: 1번 두 줄 + 4번 한 줄 + 클라이언트에 맵이 보임.

### B. Medenia — 브라우저 + 서버 로그 (확인됨, 분석서 04와 일치)
| # | 신호 | 위치 |
|---|---|---|
| 1 | 서버 콘솔에 리슨 메시지, `db.sqlite` 생성 | `web-server.ts`, `database/index.ts` |
| 2 | 브라우저 DevTools → Network → WS 연결이 `101 Switching Protocols` | `client.ts:46` |
| 3 | `PreloadScene`이 버전 913 전송 | `preload-scene.ts:35` |
| 4 | 계정 생성 → 로그인 → `AuthListener.onLogin` | `auth-listener.ts:27-35` |
| 5 | 리다이렉트(subject `game`) → `WordListener.clientRedirected` | `listener.ts:13` |
| 6 | `PlayerCache.connect` → `mapManager.transfer('mileth-inn', …, 6, 6)` | `player-cache.ts:65` |
| 7 | 브라우저에 Mileth Inn 맵 렌더 | 완주 |

성공 기준: 2번 WS 연결 + 7번 맵 표시.

---

## 8. 현재 환경에서 실행을 막을 가능성이 있는 문제

| # | 문제 | 대상 | 상태 | 영향 | 우회 (소스 무수정 여부) |
|---|---|---|---|---|---|
| 1 | **포트 80 점유** — PID 4(Windows 시스템, HTTP.sys 계열 추정) | B | 확인됨(이 PC) | `WebServer` 바인드 실패 → 서버 못 뜸 | 80을 쓰는 Windows 서비스 중지(무수정) 또는 `web-server.ts` 수정(수정) |
| 2 | **https 강제 ↔ 클라 `ws`** — dev에서도 `https.createServer({})` | B | 확인됨(코드) / 실패는 추정 | WebSocket 핸드셰이크 실패 → 로그인 전에 막힘 | (a) `ENV=prod` + 자체서명 인증서(`CERT`/`KEY`) + 443 + `VITE_PROTOCOL=wss`, 브라우저에서 인증서 예외 허용 (무수정, 추정) (b) `web-server.ts` `http`로 수정(수정) |
| 3 | **포트 8080 점유** — PID 7212(LOD_ 개발 관리자 서버 추정) | B | 확인됨 | 자산 서버 실패 → 화면 안 뜸 | 자산 서버를 8081로 수동 실행 + `VITE_ASSET_PATH` 변경 (무수정) |
| 4 | 포트 4000 점유 — PID 42916 | B | 확인됨 | bun 디버거 충돌(동작 미확인) | `bun --watch ./src/index.ts`로 `--inspect` 없이 실행 (무수정) |
| 5 | **외부 서버 접속 잔재 코드** — `da0.kru.com:2610`으로 로그인 패킷 전송 | B | 확인됨 | 원칙 2 위반, 원저작사 서버 접속, 처리 안 된 Promise 오류 가능 | 주석 처리(수정) 또는 Windows 방화벽 아웃바운드 규칙으로 차단(무수정) |
| 6 | `sqlite3` 네이티브 모듈 | B | 확인됨(실행) | 루트 `node_modules/sqlite3`에 미리 빌드된 바이너리가 정상 설치됨. 단 Python 3.14에는 distutils가 없어 node-gyp 재빌드는 실패함 | 재빌드하지 말 것. 중복 사본이 생기면 `apps/server/node_modules/sqlite3`를 지운다 (무수정) |
| 7 | **클라이언트 7.18 ↔ 보유 7.41** | A | 확인됨(불일치) | `Format00Handler` 버전 검사에서 거부 | 9절 질문 2 |
| 8 | `LoruleConfig.json` 절대경로 3곳 | A | 확인됨 | 조용히 빈 서버 | 3절 사본 방식 (무수정) |
| 9 | 조용한 실패 — 전역 `try/catch`, `Location == null`이면 무언 종료 | A | 확인됨 | 원인 파악 어려움 | 7절 로그 체크포인트로 판정 |
| 10 | Windows 방화벽 인바운드 프롬프트 | A·B | 확인됨(README) | 최초 실행 시 팝업 | "개인 네트워크"만 허용 |
| 11 | .NET 5 EOL | A | 확인됨 | 보안 패치 없음 | 로컬 한정 |
| 12 | 패키지 다운로드(NuGet, bun) | A·B | 확인됨 | 인터넷 필요 | 원칙 2의 1회 예외로 인정 |
| 13 | Hades 콘텐츠로 완주 가능한지(맵 5개, 스크립트 컴파일, 시작 맵 매핑) | A | 미확인 | | 실행 |
| 14 | 7.41 클라이언트를 127.0.0.1로 돌리려면 실행 파일 패치 필요(Spark: `sources/FallenDev/Spark`, 호스트명 `0x4333C2`·포트 `0x4333E4`) | A | 확인됨(분석서 06) | 클라이언트가 원래 서버로 접속 시도 | 질문 2에 포함 |
| 15 | **로그인 직전 프로토콜 오류** — 최초 실행에서 두 번째 `ServerTableRequestPacket`의 id가 0으로 처리돼 `entry.ip` TypeError 발생. 후속 미커밋 `ClientCrypto` 오프셋 수정 뒤 id=1과 redirect 송신까지 확인했으나 `ServerTableEntry.port`가 `NaN` | B | 확인됨(실행, 2026-09-08) | 로그인 완주 불가 | Medenia는 참고 자료로만 유지. 임시 변경은 커밋하지 않으며 추가 수정은 중단 |

---

## 9. 결정이 필요한 질문 (저장소만으로는 답이 안 나오는 것)

1. **어느 후보부터 실행할까?** 권장은 B(원본 파일 불필요, 브라우저로 끝까지 확인 가능) → A 순서. 다른 순서를 원하면 알려 달라.
2. **A의 클라이언트 버전** 셋 중 하나:
   (a) 로컬 `sources\DarkAges718single.exe`의 복사본을 사용한다(2026-09-09 존재·SHA-256 확인, 실행 미검증) — 파일 출처·사용 권리 확인 필요.
   (b) 보유한 7.41을 Spark로 127.0.0.1:2610에 패치하고 `ClientVersion`을 741로 바꾼다 — 버전 검사는 통과하지만 7.18↔7.41 패킷 호환은 **미확인**.
   (c) A는 보류하고 B만 진행.
3. **B의 코드 수정 허용 범위.** 소스 무수정 우회(80번 서비스 중지 + `ENV=prod` 자체서명 인증서 + 방화벽 차단)는 검증되지 않았고 손이 많이 간다. 대안은 `web-server.ts`(http/포트)와 `index.ts`(외부 접속 코드) 두 곳을 **커밋하지 않는 로컬 임시 수정**으로 두는 것, 또는 WORKFLOW.md대로 fork 브랜치를 만드는 것. 어느 쪽?
4. **포트 정리 허용.** 8080(LOD_ 개발 관리자 추정)·4000·80을 쓰는 프로세스를 멈춰도 되나? 80은 Windows 서비스라 무엇인지 먼저 확인이 필요하다.
5. **A의 관리자·테스트 계정명.** 기본 `wren`을 그대로 쓸지, 새 이름을 쓸지. (기본값 제안: `wren` 유지)
6. **실행 주체.** 10절 체크리스트를 내가 실행할지(패키지 다운로드로 인터넷 접속 발생), 사용자가 직접 따라 할지.

---

## 10. 단계별 실행 체크리스트

### 0단계 — 공통 준비
- [ ] 9절 질문 1~6 답 확정
- [ ] `git status`가 루트와 대상 submodule 모두 clean인지 확인 (`git submodule status`)
- [ ] 포트 점검: `netstat -ano -p tcp | findstr LISTENING | findstr ":80 :443 :2610 :2615 :4000 :5173 :8080"`
- [ ] 방화벽 팝업이 뜨면 "개인 네트워크"만 허용

### B단계 — Medenia (권장 선행)
실행 결과 2026-09-08: 1~7 통과(포트 8082/8081/5173, WS 원시 프로브 open), 8은 Playwright가 WS를 노출하지 않아 7의 원시 프로브로 대체, 9는 미완료(8절 15번). 최초 패치는 `web-server.ts`(http + `WEB_PORT`)·`index.ts`(`LEGACY_PROBE` 가드)·`bun.lockb`이며 `tmp/medenia-local-run.patch`에 보존했다. 후속 조사로 `gateway-listener.ts` 디버그 로그와 `client-crypto.ts` 암호화 오프셋 변경이 추가됐지만 미커밋 상태로만 보존한다. Medenia는 실행 대상에서 제외했으므로 추가 수정·로그인 검증은 진행하지 않는다. 롤백 명령은 `git -C sources/FallenDev/dark-ages-ts checkout -- . && git -C sources/FallenDev/dark-ages-ts clean -fd` + `.env` 2개·`db.sqlite` 삭제다.

1. [ ] `cd sources\FallenDev\dark-ages-ts` → `bun install` — 성공 기준: 오류 0, `node_modules` 생성. `sqlite3` 빌드 오류가 나면 **중단하고 보고**
2. [ ] `.env` 2개 복사, `apps/client/.env`의 `VITE_ASSET_PATH`를 `http://localhost:8081/`로
3. [ ] 질문 3의 결정대로 80번/https 문제 처리 (무수정 우회 또는 임시 수정)
4. [ ] 질문 3·4의 결정대로 `da0.kru.com` 접속 코드 처리 (방화벽 차단 또는 임시 주석)
5. [ ] 자산 서버: `cd apps\client` → `bunx http-server ./public --cors -s -p 8081` — 확인: 브라우저 `http://localhost:8081/` 응답 200
6. [ ] 서버: `cd apps\server` → `bun --watch ./src/index.ts` — 확인: 리슨 로그, `apps/server/db.sqlite` 생성(크기 > 0), 외부 접속 오류 로그 없음
7. [ ] 클라이언트: `cd apps\client` → `bunx vite` — 확인: `http://localhost:5173` 열림
8. [ ] 브라우저 DevTools → Network → WS가 `101`인지 확인 (실패 시 3단계로 복귀)
9. [ ] 계정 생성 → 같은 계정으로 로그인 → **Mileth Inn 맵 표시** — 서버 로그에서 4·5·6번 신호(7절) 확인
10. [ ] 결과(성공/실패 지점, 오류 문구)를 `WORKLOG.md`에 한 줄로 기록

### A단계 — Hades (질문 2 결정 후)

실행 결과 2026-09-09: **1~9 전부 통과 (로그인→맵 입장 완주)**. 확인된 신호는 서버 로그 `wren : Welcome to Lorule`, 게임서버 2615 ESTABLISHED, `aislings/wren.json`(`GameMaster=True`, `CurrentMapId=1`, `4,4`), stderr 0바이트다. 아래 단계 설명은 실행하며 확인된 값으로 고쳤다.

1. [x] `cd sources\wren11\Dark-Ages-Private-Server` → `dotnet restore src\Hades.sln` → `dotnet build src\Hades.sln -c Debug` — 확인: 오류 0(실제 경고 8개), 출력은 **`Staging\net5.0\Lorule.GameServer.dll`**. .NET SDK 8로 빌드하면 대상 프레임워크 폴더가 하나 더 생긴다(확인됨). 경고는 기록만
2. [x] **`Staging\net5.0\*`**를 루트 `tmp\hades-run\`로 복사
3. [x] **`database\server`를 `tmp\hades-run\database\server`로 먼저 복사한 뒤**, `tmp\hades-run\LoruleConfig.json`의 **`Content.Location`은 그 사본 경로로**, `Editor.Location`·`Editor.GameLocation`은 3절 값으로 수정(역슬래시 두 번)
   - **이유(확인됨)**: 서버는 시작할 때 `Content.Location` 아래 `areas/*.json`의 `FilePath`를 자기 절대경로로 다시 써서 저장한다. submodule을 직접 가리키면 매번 그 4개 파일이 수정된 상태가 된다. 사본은 2.3MB뿐이다
   - `ClientVersion: 718`·`GameMasters: ["wren", "lol"]`은 기본값 그대로 쓰면 된다(질문 2·5 결론)
4. [x] `cd tmp\hades-run` → `dotnet Lorule.GameServer.dll` — 확인: `Login server is online.` / `Game server is online.` 두 줄, 오류 로그 없음. 무언 종료면 3단계 경로 재확인
5. [x] `database\server\aislings\` 폴더가 생겼는지 확인(3단계에서 만든 사본 쪽)
6. [x] 클라이언트 준비: **받아 둔 `sources/DarkAges718single.exe`도 `LOD_\raw_data`의 7.41 자료도 필요 없다.** 저장소 `game/`에 7.18 클라이언트(`Hades.exe`, 127.0.0.1:2610으로 하드코딩된 `mServer.tbl`)와 `.dat` 8개가 통째로 커밋돼 있다(추적 파일 93개, 확인됨)
   - `game/`을 `tmp\hades-run\game\`으로 복사한다(약 379MB). `game/`은 추적 대상이라 제자리에서 실행하면 submodule이 더러워진다
   - **`game/`에는 `Legend.dat`과 `cious.dat`이 빠져 있다.** 그대로 실행하면 `LOD Error: main data file not found` 창을 띄우고 종료한다(확인됨). 둘 다 같은 저장소 `database\archives\legend\`·`database\archives\cious\`에 있으니 사본의 `game\`으로 복사한다
7. [x] 클라이언트 실행 → 서버 공지(`Notification`) 창의 Ok → Create → 캐릭터 생성 — 확인: `aislings\<이름>.json` 생성
   - **이 단계는 사람이 직접 타이핑해야 한다(확인됨).** 이름 칸은 합성 키 입력을 받지만 Password·Confirm 칸은 SendInput·WM_CHAR를 모두 무시한다. 자동화로는 계정을 만들 수 없다
   - 관리자 권한을 쓰려면 이름을 `GameMasters` 목록의 값(`wren`)으로 만든다. 생성된 json에 `GameMaster: true`가 찍히면 성공
8. [x] 메인 메뉴 Continue로 로그인 — 확인: 서버 로그 `<이름> : Welcome to Lorule`, 게임 포트 2615 연결이 ESTABLISHED, 클라이언트에 시작 맵 표시(설정값대로 zone `Safe House`, 좌표 4,4)
9. [x] 결과를 `WORKLOG.md`에 기록. `tmp\hades-run`은 `.gitignore` 대상이라 커밋되지 않음
10. [ ] **서버를 끈다.** `powershell -File scripts\stop-hades.ps1` — 안 끄면 2610·2615·2620이 잡힌 채 남아 다음 세션의 실행과 harness를 전부 막는다(2026-09-09에 실제로 발생)

### 완료 판정
- B: 체크리스트 8·9 통과 → "브라우저 클라이언트 로그인→맵 입장 검증 완료"
- A: 체크리스트 4·8 통과 → "Hades 서버 로그인→맵 입장 검증 완료"
- 어느 쪽이든 실패하면 실패한 단계 번호와 오류 문구만 남기고 멈춘다. 원인 수정은 별도 결정 사항이다.

---

## 부록 — 용어 풀이
- **submodule**: 다른 Git 저장소를 폴더처럼 끼워 넣은 것. `sources/` 아래가 전부 이것이다.
- **포트**: 한 컴퓨터 안에서 프로그램별로 나뉜 통신 창구 번호. 하나의 포트는 한 프로그램만 쓸 수 있다.
- **TCP / WebSocket**: TCP는 원본 클라이언트가 쓰는 기본 통신 방식, WebSocket은 브라우저가 쓰는 방식이다.
- **https / wss / TLS**: 통신을 암호화하는 방식. 인증서 파일이 있어야 하며, 없으면 연결이 실패한다.
- **`.env`**: 환경별 설정값을 적는 텍스트 파일. Git에 올리지 않는다.
- **리다이렉트**: 로그인 서버가 "인증 끝났으니 게임 서버 포트로 다시 접속해라"라고 넘겨주는 절차.
- **Format 번호(Format02 등)**: 클라이언트와 서버가 주고받는 메시지 종류 번호. 분석서 06 참조.
- **Roslyn 스크립트**: C# 코드 파일을 서버가 시작할 때 즉석에서 컴파일해 쓰는 방식. Hades의 `database/server/scripts`가 이것이다.
