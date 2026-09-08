# 04. 로그인부터 게임 입장까지

## 4.1 Hades/Lorule 서버

**확인됨:** 다음 흐름은 `LoginServer`, `GameServerHandlers`, `AislingStorage` 코드에서 직접 확인된다.

1. `ServerContext.StartServers()`가 로그인 TCP 포트와 게임 TCP 포트를 따로 연다. 기본 설정은 `LOGIN_PORT=2610`, `SERVER_PORT=2615`다.
2. 로그인 연결 시 `LoginServer.ClientConnected()`가 초기 서버 패킷을 보낸다.
3. 클라이언트 버전 패킷은 `LoginServer.Format00Handler()`가 처리한다. 설정된 `ClientVersion`과 맞으면 암호화 파라미터와 서버 테이블을 제공한다.
4. 로그인 요청은 `LoginServer.Format03Handler()`가 `StorageManager.AislingBucket.Load(username)`로 `database/server/aislings/<name>.json`을 읽고 비밀번호를 비교한다.
5. 성공하면 `LoginAsAisling()`이 이름·serial·seed·salt가 든 `Redirect`를 만들고, 사용자명을 `ServerContext.Redirects`에 임시 등록한 뒤 게임 포트로 리다이렉트한다.
6. 게임 포트에서 `GameServerHandlers.Format10Handler()`가 `EnterGame()`을 호출한다. `LoadPlayer()`가 캐릭터 JSON을 다시 읽고 `GameClient.Load()`로 장비·인벤토리·스킬·마법·버프를 구성한다.
7. `GameClient.Refresh()`가 맵과 주변 객체를 클라이언트에 전송하고, `LoggedIn(true)`가 입장 상태를 확정한다.

근거 파일/클래스:

- `sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Infrastructure/ServerContext.cs` — `ServerContext`
- `.../Network/Login/LoginServer.cs` — `LoginServer`
- `.../Network/Game/GameServerHandlers.cs` — `Format10Handler`, `EnterGame`, `LoadPlayer`
- `.../Network/Game/GameClient.cs` — `GameClient.Load`, `Refresh`, `LoggedIn`
- `.../Storage/AislingStorage.cs` — `AislingStorage`

**확인된 위험:** 비밀번호는 해시가 아니라 `Aisling.Password != format.Password`로 직접 비교되고 캐릭터 JSON에 직렬화된다. 또한 `Format10Handler`는 `EnterGame()` 후에 redirect 목록을 검증한다. 운영 전 인증 흐름 재설계가 필요하다.

## 4.2 Medenia (`dark-ages-ts`)

1. `PreloadScene.create()`가 브라우저 WebSocket으로 접속한다.
2. `GatewayListener`가 접속 수락 패킷을 보내고, 클라이언트가 버전 `913`을 보낸다.
3. 서버 테이블 요청 후 `RedirectPacket(subject='auth')`로 인증 단계에 이동한다. 실제로는 같은 WebSocket 주소에 재연결하고 redirect token을 검증한다.
4. `LoginForm.svelte`가 `LoginPacket`을 보내면 `AuthService.login()`이 SQLite의 `AislingEntity`를 조회하고 Argon2 해시를 검증한다.
5. 성공하면 `Redirect(subject='game')`; `WordListener`(파일은 `world-listener.ts`, 클래스명 오타)가 `PlayerCache.connect()`를 호출한다.
6. 플레이어를 `mileth-inn`의 `(6,6)`으로 옮기고 `MapRoom`이 맵 정보와 행 단위 맵 데이터를 보낸다.
7. `MapScene`과 `IsoMap.setMapData()`가 화면을 구성한다.

근거 파일/클래스:

- `sources/FallenDev/dark-ages-ts/apps/client/src/scenes/preload-scene.ts` — `PreloadScene`
- `.../auth-scene.ts` — `AuthScene`
- `.../ui/routes/auth/LoginForm.svelte`
- `apps/server/src/network/listeners/gateway-listener.ts` — `GatewayListener`
- `.../auth-listener.ts` — `AuthListener`
- `.../world-listener.ts` — `WordListener`
- `apps/server/src/services/auth-service.ts` — `AuthService`
- `apps/server/src/maps/map-room.ts` — `MapRoom`

## 4.3 나머지 저장소

**확인됨:** SleepHunter4는 로그인을 구현하지 않고 외부 클라이언트 메모리에서 캐릭터명과 레벨을 읽어 로그인 완료를 사후 감지한다. Arbiter/DAGL/Decipher/da는 로그인 패킷 정의 또는 관찰·중계 코드를 포함하지만 권위 있는 계정 인증 서버가 아니다.
