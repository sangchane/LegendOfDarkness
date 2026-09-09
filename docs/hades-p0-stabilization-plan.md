# Hades P0 안정화 계획 및 회귀 테스트 경계

- 문서 버전: 1.1
- 상태: 확정 (1.1 — S0 착수에서 확인한 객체 서버 포트 제약과 S0 harness 위치를 반영)
- 기준일: 2026-09-09
- 적용 대상: Hades/Lorule 7.18 C# 서버
- 목적: 모바일 첫 테스트 버전을 실제 Hades 서버에 연결하기 전에 필요한 최소 안정화 범위와 통과 기준을 고정한다.

## 1. 결론

Hades는 현행 게임 규칙과 콘텐츠 동작을 확인하는 기준선으로 유지한다. 전면 리팩터링, DB 교체, 일괄 성능 최적화는 먼저 하지 않는다. 대신 서버 전체 중단, 인증 우회, 경로 이탈, 패킷 유실, 캐릭터 저장 손상, 기본 전투 오류로 이어질 수 있는 경계만 P0로 안정화한다.

진행 순서는 다음과 같이 확정한다.

1. **S0 — 현행 동작 고정:** 원본 7.18 동작을 변경하지 않고 특성화 테스트와 golden fixture를 만든다.
2. **S1 — 모바일 연동 안전선:** 네트워크, 입장 인증, 연결 정리, 저장, 기본 전투의 치명적 오류를 테스트 우선으로 수정한다.
3. **S2 — 5~10인 비공개 테스트 안전선:** 비밀번호 저장, 로그인 남용 방지, 정상 종료 저장, 지원 런타임, 10인 부하·복구를 검증한다.

Godot 화면은 fixture를 사용하는 격리 작업이라면 S0 이후 진행할 수 있다. 실제 Hades 연동은 S1 통과 후, 지인 5~10명이 접속하는 비공개 테스트는 S2 통과 후에만 진행한다.

## 2. 판단 근거

### 2.1 확인된 내용

| 영역 | 확인된 위험 | 코드 근거 |
|---|---|---|
| 예외 처리 | `ServerContext.Error`는 선언되어 있으나 할당 지점을 찾지 못했고 네트워크 예외 경로에서 null 확인 없이 호출된다. | `sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Infrastructure/ServerContext.cs`의 `ServerContext.Error`, `Network/NetworkServer.cs`의 수신·송신 예외 처리 |
| 패킷 입력 | 알 수 없는 명령과 짧거나 잘린 패킷을 안전하게 거부하는 경계 검사가 부족하다. | `Hades.Server.Base/Network/NetworkFormatManager.cs`의 `GetClientFormat`, `Network/NetworkPacket.cs` |
| 게임 입장 인증 | `Format10Handler`가 redirect 검증보다 `EnterGame()`을 먼저 호출한다. redirect 상태도 사용자명 목록에 의존한다. | `Hades.Server.Base/Network/Game/GameServerHandlers.cs`의 `Format10Handler`, `EnterGame`; `Network/Login/LoginServer.cs`의 `LoginAsAisling`; `Infrastructure/ServerContext.cs`의 `Redirects` |
| 파일 경로 | 캐릭터명이 `aislings` 아래 파일 경로에 직접 사용된다. 서버 경계의 허용 문자와 최종 경로 포함 검사가 부족하다. | `Hades.Server.Base/Storage/AislingStorage.cs`의 `Load`, `Save`; `Network/Login/LoginServer.cs`의 캐릭터 생성 처리 |
| 연결 수명 | 인증 전 연결 종료 시 기반 정리 경로를 건너뛸 수 있고, 접속자 컬렉션 잠금이 매번 새 목록을 대상으로 한다. | `Hades.Server.Base/Network/Game/GameServer.cs`의 `ClientDisconnected`; `Network/NetworkServer.cs`의 `Clients`, 연결 추가·삭제 |
| 송신 | `BeginSend`의 완료 바이트 수와 부분 송신을 보장하지 않으며 사용자별 순서 보장 큐가 없다. | `Hades.Server.Base/Network/NetworkClient.cs`의 `FlushAndSend`, `SendCompleted` |
| 저장 | 캐릭터 JSON을 대상 파일에 직접 쓰며 캐릭터별 동시 저장, 원자적 교체, 백업 복구가 없다. 저장 실패가 성공처럼 기록될 수 있다. | `Hades.Server.Base/Storage/AislingStorage.cs`의 `Save`; `Network/Game/GameClient.cs`의 `Save`; `Network/Game/Components/SaveComponent.cs` |
| 기본 전투 | 기본 공격 간격 계산의 뺄셈 방향이 반대다. 존재하지 않는 주문 슬롯은 cast 스택에서 제거되지 않아 갱신 루프가 멈출 수 있다. | `Hades.Server.Base/Network/Game/GameServerHandlers.cs`의 `Assail`; `Network/Game/GameClient.cs`의 `DispatchCasts` |
| 객체 서버 포트 | 게임 서버가 시작하면서 `http://localhost:2620/`을 하드코딩으로 연다. 설정 키가 없어 격리할 수 없고, 이미 사용 중이면 `HttpListener` 예외가 `SocketException`만 잡는 처리기를 그대로 통과해 **로그인 서버가 시작되지 않은 채 프로세스만 살아 있다.** (2026-09-09 harness에서 실제 재현) | `Hades.Server.Base/Network/Game/GameServer.cs`의 `Start`, `Network/WS/ObjectServer.cs`의 `Start`, `Infrastructure/ServerContext.cs`의 `StartServers` |
| 유지보수 기반 | 자동화된 서버 테스트와 CI가 없고 서버가 지원 종료된 `net5.0`을 사용한다. | `src/Hades.Server.Base/Hades.Server.Base.csproj`, `src/Lorule.GameServer/Lorule.GameServer.csproj` |

경로는 현재 submodule 배치를 기준으로 적었다. 구현 브랜치에서는 실제 fork 경로와 줄 번호를 다시 고정한다.

### 2.2 판단 또는 아직 확인이 필요한 내용

- 위 결함이 실제 운영 중 모두 재현됐다는 뜻은 아니다. 코드상 실패 가능성이 확인되어 P0 방어 대상으로 분류했다.
- 다만 예외 격리 결함은 2026-09-09 S0 harness 실행에서 실제로 재현됐다. 2620이 선점된 상태에서 서버는 예외를 삼키고 로그인 서버 없이 계속 떠 있었다.
- 원본 클라이언트가 기대하는 정상 패킷 바이트와 서버 상태 변화는 S0의 실제 캡처로 확정해야 한다.
- Hades 서버의 현재 처리량 병목은 아직 측정하지 않았다. 따라서 CPU 최적화와 게임 루프 재설계는 P0에 포함하지 않는다.
- 5~10명 규모에서는 JSON 저장을 유지할 수 있다고 판단한다. 단, 원자적 저장과 복구 시험을 통과해야 한다.

### 2.3 하드코딩된 상수 목록 (S1 정리 대상)

배포 환경에 따라 달라지는 값이 코드나 데이터에 박혀 있으면 격리 실행과 병렬 테스트가 막히고, 나중에 서버를 옮길 때도 그대로 걸린다. S1 fork 작업에서 아래를 설정 키로 빼고 환경변수로 덮어쓸 수 있게 한다. 검토 중 새로 찾은 값은 이 표에 추가한다.

| 값 | 위치 | 지금 영향 | S1 목표 |
|---|---|---|---|
| `http://localhost:2620/` | `Hades.Server.Base/Network/Game/GameServer.cs`의 `Start` | 설정 키가 없어 이 PC에서 서버가 한 대만 뜬다. 격리 harness의 병렬 실행이 불가능하고, 선점 시 로그인 서버 없이 조용히 뜬다 | `ServerConfig`에 객체 서버 포트 키를 두고 환경변수 override 허용 |
| `MServerTable.xml`의 `Port` = 2610 | 빌드 산출물 데이터 | 로비 리다이렉트가 격리 밖 포트로 클라이언트를 보낸다. 현재는 harness가 실행마다 이 파일을 다시 써서 우회 중 | 실행 시 `LOGIN_PORT`에서 파생하거나 설정으로 주입 |

**규칙:** 주소·포트·경로처럼 환경마다 달라지는 값은 설정 파일 키를 정본으로 두고, 환경변수로 덮어쓸 수 있게 한다. 코드 안의 리터럴은 기본값 자리로만 남긴다.

## 3. 범위

### 3.1 P0에 포함

- 패킷 프레임, 길이, 명령, 문자열 경계 검증
- 오류 처리기가 다시 예외를 만들지 않는 서버 수준 예외 격리
- 캐릭터명 정규화와 `aislings` 저장 경로 이탈 방지
- 만료되는 일회용 게임 입장 티켓과 검증 후 입장 순서
- 연결 컬렉션 동기화, 인증 전·후 연결 정리, handshake 시간 제한과 연결 수 상한
- 사용자별 순서 보장·용량 제한 송신 큐와 완전 전송 처리
- 캐릭터별 직렬화, 임시 파일 쓰기, 원자적 교체, 백업 및 복구
- 기본 공격 간격 계산과 잘못된 cast 항목의 무한 반복 수정
- 하드코딩된 주소·포트 상수의 설정 키 분리와 환경변수 override (2.3의 목록)
- 실패 원인을 비밀정보 없이 식별할 최소 구조화 로그와 계수기
- 지원 중인 .NET LTS로의 단계적 이전과 의존성·빌드 검증(S2)

### 3.2 P0에서 제외

- 전체 구조 재작성, 큰 파일의 일괄 분할, 전역 상태의 전면 제거
- JSON을 SQL DB로 교체하거나 다중 서버 구조를 도입하는 작업
- 측정 결과가 없는 게임 루프, 암호화, 가시성 계산 최적화
- Godot 화면의 색상·아트·최종 UX와 WebView 전환
- 신규 NPC·맵·퀘스트·직업·스킬 콘텐츠와 게임 밸런스
- App Store 또는 공개 인터넷 서비스를 위한 TLS 게이트웨이와 배포 자동화

## 4. 단계별 실행 계획

### S0 — 특성화 기준선

목표는 취약한 동작을 보존하는 것이 아니라, 정상 7.18 흐름과 저장 결과를 변경 전에 기록하는 것이다.

| ID | 작업 | 산출물 | 완료 조건 |
|---|---|---|---|
| P0-00 | 원본 서버를 수정하지 않는 격리 harness 구성 | 테스트 전용 프로세스·포트·임시 데이터 경로 | 실행 중인 수동 서버 및 실제 데이터와 분리됨 |
| P0-01 | 7.18 로그인→redirect→게임 입장 정상 흐름 캡처 | 비밀정보가 제거된 golden packet/state fixture | 합성 계정으로 같은 입력이 같은 명령 순서와 핵심 상태를 재현함 |
| P0-02 | 저장 전후 정상 상태 고정 | 최소 캐릭터 JSON fixture와 의미 비교기 | 시간·임의값을 제외한 필드 변화가 설명되고 반복 가능함 |

**진행 상황 (2026-09-09):** P0-00 완료 — `tests/hades-characterization/`(net8.0, xunit)의 `IsolatedHadesServer`가 빌드 산출물과 `database/server`를 임시 경로로 복사하고, 비어 있는 `aislings`와 사용 중이 아닌 포트를 배정한 뒤 서버를 띄우고 종료 시 지운다. 격리를 일부러 깨면 원본 무변경 테스트가 실제로 실패하는 것까지 확인했다. 하드코딩된 2620 때문에 harness는 한 번에 한 대만 띄울 수 있어, 다른 Hades가 떠 있으면 `Start`가 즉시 실패하고 스위트도 직렬로 실행한다(전체 11개, 약 1분).

P0-01 완료 — 합성 계정으로 로그인부터 월드 입장까지 완주하고, 16단계 명령 순서를 `Fixtures/hades-718-login-flow.json`에 고정했다. 성공 판정은 서버 로그의 `<이름> : Welcome to Lorule`이다. seed·salt·해시·serial처럼 실행마다 달라지는 값은 고정하지 않았고 실제 계정·비밀번호는 쓰지 않는다. 암호화는 재작성 대신 서버의 `SecurityProvider`를 참조해 정본으로 쓴다. 기록 과정에서 예상과 달랐던 동작 하나를 확인했다 — 리다이렉트 후 새 연결마다 handshake 배너(0x7E)가 다시 온다.

P0-02 완료 — 갓 생성된 캐릭터의 최소 상태를 `Fixtures/hades-718-character.json`에 고정하고, 두 번의 독립 실행을 비교하는 의미 비교기를 붙였다. 실행 간 달라지는 필드는 `Created`, `LastLogged`, `SkillBook` 셋뿐이며 각각 이유를 fixture에 적었다(`SkillBook`은 `GiveAssailOnCreate`로 부여되는 스킬의 `ID`가 난수 시리얼이고 나머지 필드는 동일하다). 비교기가 실제로 드리프트를 잡는지도 값을 일부러 바꿔 확인했다.

**S0 gate: 통과 (2026-09-09).** 테스트 14개가 원본 기준선에서 재현되며, fixture·로그·테스트 결과에 실제 계정과 비밀번호가 없다(합성 계정 `lodharness`만 사용). 전제: 서버가 미리 빌드돼 있어야 하고(`Staging/net5.0`), 2620 제약 때문에 스위트는 직렬로 약 30초 걸린다.

**S0 gate 원문:** 격리 테스트가 원본 기준선에서 재현되며 fixture, 로그, 테스트 결과에 실제 계정이나 비밀번호가 없다.

### S1 — 실제 모바일 연동 안전선

| ID | 작업 | 선행 | 핵심 완료 조건 |
|---|---|---|---|
| P0-10 | 패킷 검증과 예외 격리 | S0 | 악성·잘린 패킷은 해당 연결만 종료하고 서버와 정상 연결은 계속 동작 |
| P0-11 | 사용자명·저장 경로 검증 | P0-10 | 허용되지 않은 이름과 루트 밖 정규화 경로가 모두 거부됨 |
| P0-12 | 입장 티켓과 인증 순서 수정 | P0-10 | 유효한 티켓을 검증·원자적으로 소비한 연결만 `EnterGame` 수행 |
| P0-13 | 연결 수명과 동시성 수정 | P0-10 | 인증 전 종료와 반복 재접속 후에도 socket과 접속자 항목이 남지 않음 |
| P0-14 | 송신 큐와 완전 전송 | P0-13 | 부분 송신·느린 수신에서도 패킷 순서와 전체 바이트가 유지되고 상한 초과 정책이 동작 |
| P0-15 | 원자적 저장과 복구 | P0-11 | 동시 저장·강제 중단 후 현재 파일 또는 백업 중 하나가 유효함 |
| P0-16 | 기본 전투 정확성 | P0-10 | 공격 간격 경계가 결정적으로 동작하고 잘못된 cast가 갱신 루프를 막지 않음 |
| P0-17 | 하드코딩 상수 설정화 | P0-10 | 2.3 표의 값이 설정 키로 분리되고 환경변수로 덮어써지며, harness가 객체 서버 포트까지 격리해 병렬 실행됨 |

**S1 gate:** 아래 5장의 필수 suite가 모두 통과하고, 변경 코드의 line/branch/method coverage가 각각 80% 이상이며 인증·경로·저장 손상 경계의 branch coverage는 100%이고 skip된 필수 테스트가 없다. 원본 7.18 클라이언트의 로그인→redirect→맵 입장이 10회 연속 성공해야 한다. 이 gate 뒤에만 Godot 클라이언트를 실제 Hades에 연결한다.

### S2 — 5~10인 비공개 테스트 안전선

| ID | 작업 | 완료 조건 |
|---|---|---|
| P0-20 | 비밀번호 저장 이전 | 신규·기존 테스트 계정이 검증된 password hash로 전환되고 평문을 다시 저장하지 않음 |
| P0-21 | 로그인 남용 방지 | 계정 존재를 노출하지 않는 오류, 시도 제한, 연결·handshake 제한이 자동 시험을 통과 |
| P0-22 | 정상 종료 저장 | 종료 신호 후 신규 요청을 중지하고 접속 캐릭터 저장과 socket 정리를 제한 시간 안에 완료 |
| P0-23 | 런타임·의존성 이전 | 특성화 계약을 유지한 채 지원 중인 .NET LTS에서 Release 빌드·전체 테스트 통과 |
| P0-24 | 10인 soak 및 복구 | 10개 연결이 30분간 핵심 흐름과 재접속을 반복하고 아래 정량 기준을 통과 |

**S2 gate:** 보안 음성 테스트와 10인 soak를 포함한 전체 suite가 통과한 빌드만 지인에게 배포한다.

## 5. 회귀 테스트 경계

### 5.1 필수 suite

| suite | 포함하는 경계 | 대표 판정 기준 |
|---|---|---|
| 단위 테스트 | frame magic, 길이 0~2·truncated·최대값, 미등록 명령, CP949 255-byte 경계, 사용자명과 경로, 티켓 만료·재사용·불일치, 저장 교체, 공격 clock, 잘못된 cast | 외부 프로세스 없이 결정적으로 통과; fake clock과 임시 경로 사용 |
| 통합 테스트 | 격리 TCP listener, 조각 수신, 부분 송신, 동시 입장, 동시 저장, 인증 전 disconnect, 반복 reconnect | 한 연결의 실패가 다른 연결과 서버 생존에 영향을 주지 않음 |
| 특성화 테스트 | 원본 7.18의 정상 로그인→맵 입장 패킷 순서와 핵심 캐릭터 상태 | 알려진 취약 동작은 기대값으로 고정하지 않으며 정상 흐름만 비교 |
| 보안 음성 테스트 | `..`·구분자·절대경로 이름, 틀린·만료·재사용 ticket, seed/salt 이상값, 로그인 burst, 비밀정보 로그 유출 | 모두 통제된 거부 또는 제한; 서버 생존; 실제 비밀번호 기록 0건 |
| 시스템·부하 테스트 | 10 clients, 30분, 로그인·입장·이동·전투·획득·저장·재접속 | unhandled exception 0, save failure 0, ghost client 0 |

### 5.2 정량 합격 기준

- malformed, fragmented, 최대 크기 경계 입력 1,000건 후 서버가 살아 있고 정상 클라이언트의 핵심 흐름이 완료된다.
- 동일 입장 티켓을 동시에 소비하는 2개 연결 중 정확히 1개만 성공한다.
- 연결·로그인·종료 100회 후 접속자 컬렉션과 socket에 잔여 항목이 0개다.
- 동일 계정 저장·재시작 100회 후 현재 JSON 또는 백업으로 100% 복구된다.
- 잘못된 cast 입력 처리는 50ms 안에 반환되고 다음 게임 갱신이 계속된다.
- 10인 30분 soak에서 tick 지연은 p95 16ms 미만, p99 33ms 미만이다.
- 디스크 저장 시간을 제외한 패킷 handler 지연은 p95 10ms 미만이다.
- 사용자별 송신 queue 깊이는 p99 50 미만이며 시간에 따라 계속 증가하지 않는다.
- warm-up 이후 30분간 관리 메모리 증가는 20% 이하다.
- 변경·추가한 안정화 코드의 line, branch, method coverage가 각각 80% 이상이다.
- 인증 선검증, 저장 경로 containment, 입장 티켓 단일 소비, 원자적 저장 실패·복구 경로의 branch coverage는 100%다.

성능 수치는 첫 기준값이다. 기능 정확성 시험은 수치 조정으로 우회할 수 없다. 환경 차이로 성능 기준 조정이 필요하면 측정 장비, 빌드, 원자료를 기록한 별도 결정으로 변경한다.

### 5.3 테스트가 소유하지 않는 영역

P0 서버 suite는 Godot 표시 품질, 모바일 조작성, safe area, NPC 대사의 내용 품질, 전체 전투 밸런스를 판정하지 않는다. 이 항목은 모바일 PRD와 와이어프레임의 별도 acceptance criteria가 담당한다. 반대로 모바일 E2E 통과만으로 서버의 동시성, 저장 복구, 인증 안전성을 통과 처리하지 않는다. 현재 `mobile/tests`의 synthetic login fixture도 클라이언트 codec 계약만 담당하며 Hades 서버 P0 통과 증거로 사용하지 않는다.

## 6. 구현 및 Git 경계

### 6.1 원본과 fork

- `sources/`의 원본 submodule은 읽기 전용 기준선으로 유지한다.
- S0 특성화 harness는 원본을 수정하지 않으므로 root 저장소 `tests/hades-characterization/`에 두고, fork는 서버 코드를 실제로 고치는 S1 시점에 만든다.
- 서버 변경은 `kimsangchan/Dark-Ages-Private-Server` fork를 만들거나 확인한 뒤 그 저장소에서 수행한다.
- 원본 저장소는 `upstream`, 개인 fork는 `origin`으로 구분한다.
- root 저장소에는 검증된 Hades fork의 submodule commit 포인터만 반영한다.

### 6.2 Graphite stack

서버 fork의 권장 스택은 다음과 같다.

1. `test/hades-characterization` (S0 산출물은 root 저장소에 있으므로, 이 브랜치는 S1에서 fork로 옮겨 갈 때만 만든다)
2. `fix/hades-network-boundary`
3. `fix/hades-auth-boundary`
4. `fix/hades-persistence`
5. `fix/hades-gameplay-correctness`
6. `chore/hades-runtime-lts`

각 브랜치는 한 실패 경계와 그 테스트만 소유한다. 기존 패킷 계약과 무관한 이름 변경, 포맷팅, 대규모 파일 이동은 섞지 않는다. root 저장소에서는 이 계획 문서, 모바일 프로토콜 코어, 검증된 submodule 포인터를 서로 다른 Graphite 변경으로 관리한다.

## 7. 중단 및 되돌림 기준

- S0 기준선을 재현하지 못하면 서버 코드를 수정하지 않고 fixture 또는 실행 환경 차이를 먼저 해결한다.
- 정상 7.18 흐름이 깨지는 변경은 해당 stack에서 중단하고 마지막 green parent로 되돌린 뒤 원인을 분리한다.
- 테스트가 실제 계정, 평문 비밀번호, 전체 패킷 평문을 출력하면 결과물을 배포·커밋하지 않고 fixture를 폐기해 다시 만든다.
- 원자적 저장 시험에서 현재 파일과 백업이 모두 손상되면 실제 사용자 데이터로 시험하지 않는다.
- 성능 기준을 통과하지 못하면 측정 자료로 병목을 특정한 뒤 별도 `perf` 변경을 만든다. P0 기능 변경에 추측성 최적화를 섞지 않는다.

## 8. 결정 기록

| 결정 | 선택 | 이유 | 재검토 시점 |
|---|---|---|---|
| 안정화 순서 | 특성화 → 최소 안전화 → 모바일 실제 연동 | 현행 동작을 잃지 않으면서 치명적 실패 경계만 먼저 제거 | S1 gate 실패가 구조적 재설계를 요구할 때 |
| 서버 기준선 | Hades C# 유지 | 조사한 후보 중 게임 규칙과 콘텐츠가 가장 완성됨 | 수직 흐름을 재현할 수 없을 때 |
| 저장소 | P0에서 JSON 유지 | 5~10인에는 DB 전환보다 원자성·복구가 우선 | soak에서 저장 지연 또는 일관성 기준 실패 시 |
| 런타임 | 원본 `net5.0`은 oracle로만 유지하고 S2에서 지원 LTS 이전 | .NET 5는 지원 종료 상태이며, 업그레이드와 동작 변경을 한 번에 섞지 않기 위해서. 현재 지원 주기는 [.NET 공식 정책](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)을 기준으로 구현 시 다시 확인 | S0/S1 계약이 green인 뒤 |
| 모바일 연동 | fixture UI는 S0 후, 실제 Hades는 S1 후 | UI 진행을 막지 않으면서 취약 서버 직접 연결을 방지 | 각 gate 통과 시 |

## 9. 다음 작업

이 문서 확정 직후의 유일한 구현 작업은 **P0-00 격리 harness와 P0-01 정상 7.18 golden fixture**다. Hades 원본 코드는 변경하지 않는다. UI greybox는 별도 와이어프레임의 남은 결정 사항 승인 전에는 시작하지 않는다.
