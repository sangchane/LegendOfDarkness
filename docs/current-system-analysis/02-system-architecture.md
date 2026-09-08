# 02. 시스템 구성과 역할

## 2.1 실제 게임 계열

### Hades/Lorule 서버

**확인됨:** `sources/wren11/Dark-Ages-Private-Server/src/Lorule.GameServer/Program.cs`의 `Program.Main`이 설정과 의존성 주입을 구성해 `Server`를 생성하고, `Server` 생성자가 `ServerContext.Start`를 호출한다. 핵심 서버는 다음처럼 나뉜다.

- `src/Hades.Server.Base/Network/Login/`: 로그인 서버 (`LoginServer`, `LoginClient`)
- `src/Hades.Server.Base/Network/Game/`: 게임 서버 (`GameServer`, `GameClient`, `GameServerHandlers`)
- `src/Hades.Server.Base/Types/`: 캐릭터 `Aisling`, 맵 `Map`, NPC 성격의 `Mundane`, `Monster`, `Item`, `Skill`, `Spell`
- `src/Hades.Server.Base/Storage/`: 파일 저장소 (`AislingStorage`, `AreaStorage`, `TemplateStorage<T>`)
- `src/Hades.Server.Base/Scripting/` 및 `database/server/scripts/`: Roslyn으로 컴파일되는 게임 규칙
- `database/server/`: 실제 콘텐츠 및 런타임 파일 저장 루트

`src/Hades.Client/Program.cs`는 `Thread.CurrentThread.Join()`만 하고 “Implement client” TODO가 있어 완성된 플레이 클라이언트가 아니다. 실제 접속은 별도로 배포된 레거시 Dark Ages 클라이언트를 전제로 한다.

### Medenia (`dark-ages-ts`)

**확인됨:** 브라우저 클라이언트와 서버를 함께 둔 monorepo다.

- `apps/client`: Phaser 렌더링 + Svelte UI
- `apps/server`: Bun 기반 TCP/WebSocket 서버와 SQLite
- `packages/network`: 패킷 정의와 인코더
- `packages/encryption`: 레거시 암호화
- `packages/serialization`: 이진 읽기/쓰기
- `packages/fsm`: 상태 머신

## 2.2 프로토콜·역공학 계열

- `Arbiter`: `Arbiter.Net.Proxy.ProxyServer/ProxyConnection`이 양방향 트래픽을 중계하고, `ClientMessageFactory`와 `ServerMessageFactory`가 패킷을 구조화한다.
- `DAGL`: `src/741` 아래에 클라이언트 7.41의 UI·게임 로직·네트워크 구조가 광범위하게 있으나 실행 파일 시작점이 없는 `.NET` 라이브러리다. README도 사용 금지를 명시하므로 참조 사전으로 취급한다.
- `Decipher`: `Akorade.Client/Server`와 `Decipher_Server.Client/Server`가 패킷을 중계하면서 Google Translation을 적용하는 프록시다.
- `da-lib`: 레거시 리소스와 패킷 암호화를 읽고 쓰는 순수 라이브러리다.

## 2.3 운영·콘텐츠·자동화 도구

- 런처: `Spark`
- 패킷 분석: `Arbiter`, 번역 프록시 `Decipher`
- 맵/에셋: `DAMapEditor`, `PalMake`, `bmp2epf`, `DADataViewer`
- 자동화/후킹: `SleepHunter4`, `da`, `ETDA`, `Dark-Ages-AI-Bot`
- 미완성 골격: `Archivist` (`MainViewModel`에 “Actually do things” TODO만 존재)

**결정됨(2026-09-08):** 향후 제품 기준선은 Hades 서버의 도메인·콘텐츠다. Medenia의 웹 클라이언트는 렌더링·자산 로딩 접근법만 참고하며 실행 대상이나 결합 대상으로 삼지 않는다. 두 저장소는 프로토콜 버전과 데이터 모델이 달라 그대로 결합할 수 없다.
