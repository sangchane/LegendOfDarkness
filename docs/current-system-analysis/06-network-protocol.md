# 06. 클라이언트·서버 통신

## 레거시 프로토콜

**확인됨:** Hades와 Medenia의 레거시 호환 계층은 TCP 위의 자체 이진 패킷을 사용한다. 일반적인 웹 API(JSON/HTTP)가 아니라 바이트 배열을 정해진 순서로 읽고 쓰는 방식이다.

- 프레임 시작: `0xAA`
- 이어지는 2바이트: 패킷 길이(큰 수 자리부터 쓰는 big-endian)
- 그다음: 명령 번호(opcode)
- 암호화 패킷은 순번(ordinal), seed, salt/key를 사용한 XOR 계열 처리를 추가

Hades 근거:

- `src/Hades.Server.Base/Network/NetworkSocket.cs` — TCP `Socket`, 3바이트 헤더 수신
- `.../NetworkPacket.cs` — `0xAA`, 길이, 명령, ordinal 조립
- `.../NetworkPacketReader.cs`, `NetworkPacketWriter.cs` — 이진 직렬화
- `.../Security/SecurityProvider.cs` — seed/salt/ordinal 기반 암복호화
- `.../Network/ClientFormats`와 `ServerFormats` — 명령별 패킷 클래스

Medenia 근거:

- `packages/network/src/packets/packet-encoder.ts` — 프레임 인코딩
- `packages/encryption/src/{client-crypto,server-crypto}.ts` — NONE/NORMAL/MD5 계열 처리
- `packages/serialization` — 이진 reader/writer
- `apps/server/src/network/servers/game-server.ts` — TCP와 WebSocket 동시 수용

## 저장소별 통신 역할

- `Arbiter`: 로컬 프록시로 클라이언트↔서버를 양방향 중계하며 패킷 해석, 필터, 저장, 재전송을 제공한다. 프로토콜 명세 후보로 가장 읽기 쉽다.
- `Decipher`: 패킷의 문자열을 번역하고 다시 전달하는 TCP 프록시다.
- `Spark`: 클라이언트 실행 파일의 서버 주소/포트를 패치하고 접속 테스트를 하지만 게임 서버는 아니다.
- `SleepHunter4`: 서버 소켓이 아니라 `ReadProcessMemory`, `WriteProcessMemory`, `PostMessage`로 로컬 게임 프로세스와 통신한다.
- `da`, `ETDA`, `Dark-Ages-AI-Bot`: DLL 주입/함수 후킹으로 클라이언트 내부 송수신을 관찰하거나 호출한다.
- `DungMunkey/Dark-Ages`, `DAMapEditor`, `PalMake`, `bmp2epf`, `DADataViewer`, `Archivist`: 게임 서버 통신 없음.

**추정:** 모바일 클라이언트는 레거시 TCP를 직접 구현할 수도 있지만, 모바일 네트워크와 앱 심사·운영 편의상 TLS가 적용된 WebSocket 게이트웨이를 두고 내부에서 기존 패킷으로 변환하는 방식이 현실적이다. 이는 아직 구현 결정이 아니다.

