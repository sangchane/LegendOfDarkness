# 프로젝트 코드·개발 방식 리뷰 — 2026-10-08

## 판단과 범위

기능 개발과 자동 시험의 기반은 좋다. 엔진 독립 Core, 실제 TCP를 사용하는 격리 서버, 원자 파일 교체, 경매 금화·물건 보존 시험, 실패를 재현한 뒤 고치는 기록이 있다. 그러나 **인증 경계, 기능 사이의 저장 동시성, 부분 실패, 시험·배포 산출물 일치**가 충분히 보장되지 않는다. 현재 상태를 안정적인 운영 개발 절차가 완성됐다고 평가하기는 어렵다.

루트 지침·현행 구조·NEXT/WORKLOG를 기준으로 직접 유지하는 모바일 Core/Godot/봇, Hades 서버의 인증·저장·은행·경매, 운영·생성 스크립트, 대시보드와 시험을 집중 검토했다. 참고용 외부 submodule 16개 전체의 모든 줄을 읽은 감사는 아니다. 클라우드 공격 재현, 원격 배포, 실제 아이폰·Windows 플레이는 하지 않았다. 아래에서 코드 경로로 확인한 결함과 실제 발생 빈도가 미확인인 위험을 구분한다.

검토 기준: 루트 HEAD `3ed71338`, Hades submodule HEAD `55dcf8f4e` 및 당시 작업 폴더. 서버 submodule은 추적 변경이 없었다. 사용자 기존 untracked 파일·출력은 보존했다.

게임 코드 수정·커밋·배포 없이 리뷰했다. 보고서는 자동 공개되는 `docs/` 대신 `plans/`에 둔다. P1은 우선 수정할 인증·데이터·배포 문제, P2는 사용자 기능 또는 운영 신뢰도를 훼손하는 문제, P3은 낮은 우선순위의 시험 개선이다.

## 먼저 고칠 문제

### 1. P1 — 게임 입장 티켓이 인증한 요청과 연결되지 않는다

- 위치: `sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Network/Login/LoginServer.cs:64`, `Network/Game/GameServerHandlers.cs:782`, `Network/ClientFormats/ClientFormat10.cs:11`.
- 로그인에서 Serial/Salt/Seed를 만들지만 서버가 보관하는 입장 허가는 이름뿐이다. 게임 입장에서는 이름의 일회성 제거만 검사하고 일반 로그인 티켓의 Id·암호화 매개변수·유효기간을 검증하지 않는다. 요청이 가져온 Parameters를 `GameServerHandlers.cs:3039`에서 채택한다.
- 정상 로그인으로 아직 소모되지 않은 입장 허가가 있으면 다른 연결이 같은 이름으로 입장 허가를 선점할 수 있는 코드 경로다. 입장을 포기한 허가는 만료되지 않는다. 실제 운영 공격은 실행하지 않았다.
- 기존 `tests/hades-characterization/EntryTicketTests.cs:15`는 같은 **정상 티켓**의 중복 소비만 막는지 검사한다. 일회성이 곧 인증이라는 가정을 놓쳤다.
- 수정 방향: 서버가 보관한 예측 불가 티켓과 Id/Parameters/만료를 검증하고 원자적으로 소비한다. 잘못된 티켓·다른 연결·만료·정상 입장을 함께 시험한다.

### 2. P1 — 온라인 비밀번호 변경과 캐릭터 저장이 충돌한다

- 위치: 같은 서버의 `Network/Login/LoginServer.cs:369`, `Network/Game/GameClient.cs:894`.
- 비밀번호 변경은 디스크의 별도 캐릭터 객체를 읽어 비밀번호를 바꾼 뒤 **캐릭터 전체**를 저장한다. 온라인 객체의 비밀번호는 바꾸지 않는다.
- 변경 성공 뒤 온라인 객체가 저장되면 옛 비밀번호가 다시 기록된다. 변경 저장 자체도 마지막 파일 저장 이후의 경험치·물건·금화를 과거 상태로 덮을 수 있고, 그 사이 종료되면 진행이 손실될 수 있다.
- 수정 방향: 계정 정보 분리 또는 같은 캐릭터 잠금 안에서 최신 온라인 객체를 선택해 변경한다. 온라인 변경→주기 저장→로그아웃→새 비밀번호 로그인, 변경 중 저장 실패를 시험한다.

### 3. P1 위험 — 은행과 저장·경매가 다른 잠금을 사용한다

- 위치: `database/server/scripts/Mundanes/Banker.cs:75`, `:157`; `src/Hades.Server.Base/Storage/AislingStorage.cs:167`; `Types/AuctionHouse.cs:405`.
- 은행 요청은 `lock(bank)`에서 손 금화를 줄이고 은행 금화를 더한다. 캐릭터 직렬화와 경매는 `AislingStorage.LockFor(username)`를 사용한다. 관측용 `ActivitySession.BeginMutation`은 상호 배제가 아니다.
- 은행 변경 사이에 주기 저장이 실행되면 서로 다른 시점의 손·은행 금화를 저장할 수 있다. 은행 Dictionary 변경 중 직렬화도 실패할 수 있다. 잠금 불일치는 확인했으며 실제 발생 빈도·손실은 런타임 재현이 필요하다.
- 수정 방향: 캐릭터 경제 변경과 스냅샷의 잠금을 통일하고 필요한 잠금 순서를 정한다. 은행↔주기 저장↔경매를 의도적으로 교차 실행해 총금화·물건 보존을 검사한다.

### 4. P1 — 배포 중간 실패가 운영 디렉터리를 혼합 버전으로 남긴다

- 위치: `scripts/ops/cloud-server.sh:57`, `:389`.
- 실행 중인 서버 설치 폴더에 `rsync --delete`로 새 DLL·자료를 먼저 쓴다. 이후 봇·프록시·생태계 봇의 빌드·전송을 하고 마지막에 재시작한다.
- 중간 실패 시 `set -e`로 끝나서 프로세스는 옛 버전, 디스크는 새 버전 또는 부분 갱신 상태가 된다. 다음 재시작에서 검증하지 않은 조합이 실행될 수 있다. 서버는 현재 소스를 빌드·시험하지 않고 기존 Staging을 올린다.
- 수정 방향: 전체 구성요소를 먼저 빌드·검증하고 별도 버전 디렉터리에 업로드한 뒤 전환한다. 이전 릴리스 보존, 실패 롤백, 실제 로그인 smoke test를 추가한다. 각 전송·빌드 단계 실패를 주입해 옛 서비스가 유지되는지 검사한다.

### 5. P2 — 저장 실패를 성공으로 취급하는 호출자가 있다

- 위치: 서버 `Storage/AislingStorage.cs:143`, `:178`; `Network/Game/GameClient.cs:894`; `Network/Login/LoginServer.cs:390`.
- `TrySave`는 오류를 기록하고 false를 반환하지만 `Save`가 결과를 버린다. 게임 저장은 실패해도 LastSave를 갱신하고 성공 로그를 남긴다. 비밀번호 변경도 성공 응답을 보낸다.
- 예외를 잡는 것 자체보다 **실패 후 어떤 상태·응답을 보장하는가**가 빠져 있다. 디스크 오류 때 운영 로그와 사용자 응답이 잘못되며 재시도가 지연될 수 있다.
- 수정 방향: 성공할 때만 LastSave·성공 로그를 갱신하고 실패를 호출자에게 전달한다. 쓰기 실패 뒤 성공 응답/로그가 없는지, 재시도가 되는지 검사한다.

### 6. P2 — 경매 완료 로그 실패가 재시도에서 빠진다

- 위치: 서버 `Types/AuctionHouse.cs:159`, `:223`.
- book 저장이 성공한 뒤 commit 로그의 `Write(false)`를 무시한다. Pending 재시도 역시 각 Write 결과를 무시하고 목록 전체를 지운다.
- 실제 완료된 거래가 보고서에는 영구히 끊긴 조작으로 남을 수 있다. 자동 중복 지급을 확인한 것은 아니지만 수동 복구 판단의 근거가 잘못된다.
- 수정 방향: 실패한 commit만 Pending에 남겨 재시도한다. book 저장 성공→로그 append/flush 실패→다음 저장 시 복구 시나리오를 시험한다. 보고서의 끊긴 조작 0은 금화 보존 전체의 증명으로 사용하지 않는다.

### 7. P2 — 고가 경매의 기본 입찰 금액이 넘친다

- 위치: `mobile/client/src/Windows/AuctionPanel.Forms.cs:128`; 서버 `Types/AuctionHouse.cs:536`.
- `uint`인 `row.Price * 5`가 현재가 858,993,460전부터 넘친다. 10억전 경매는 UI가 1,007,050,327전을 채우지만 서버 최소 입찰은 1,050,000,000전이다. 기본 입찰이 거절된다.
- 수정 방향: 계산 전에 넓은 정수로 승격한다. 858,993,459/460·10억·20억 경계와 실제 UI가 보내는 금액을 검사한다. Core 경매 parser 시험은 Godot의 이 계산을 실행하지 않는다.

### 8. P2 — 로그인 단계 일부에 제한시간이 없다

- 위치: `mobile/src/Lod.Mobile.Core/Net/HadesLoginClient.cs:145`; `mobile/client/src/Screens/LoginScreen.cs:210`.
- 초기 인사·암호 매개변수·redirect·로비 인사를 기다릴 때 호출자 토큰만 쓴다. 제한시간은 자격 증명 제출 후 AwaitRedirect에만 있다. 화면은 종료 토큰을 넘기고 접속 버튼을 잠근다.
- TCP만 연결하고 침묵하는 서버에서는 실패 응답도 버튼 복구도 없이 기다릴 수 있다. 봇 역시 접속 후 90초 감시 단계까지 못 갈 수 있다.
- 수정 방향: 전체 로그인 또는 단계별 deadline과 취소를 정의한다. 가짜 TCP peer로 각 단계에서 침묵·절단·취소시킨 뒤 재시도 가능성을 검사한다.

### 9. P2 — 유찰품 재등록 금지가 재접속 때 사라진다

- 위치: `mobile/bots/Lod.EcoBots/EcoRunner.cs:41`, `:658`; `EcoHost.cs:280`.
- 회수한 유찰품 이름은 `_unlisted` 인스턴스 HashSet에만 있다. 재접속마다 새 EcoRunner를 만들므로 서버 배포·연결 끊김·무응답 재접속에서 정책을 잊는다.
- 다시 올릴 때 2% 보증금을 반복 손실할 수 있다. 주석의 “프로그램 재시작”보다 영향 범위가 넓다.
- 수정 방향: 재접속 밖의 계정 상태에 정책을 보관한다. 유찰→회수→재접속→장보기 시나리오로 다시 올리지 않는지 시험한다.

### 10. P2 — 대시보드 배포가 앱 최신 버전 파일을 삭제한다

- 위치: `scripts/ops/cloud-dashboard.sh:23`, `:239`.
- 앱 release는 원격 `www/download/version-ios.txt`, `version-windows.txt`를 만들지만 로컬 docs/download에는 없다. 대시보드의 `rsync --delete docs/ -> www/`가 이를 지운다.
- 대시보드 재배포 후 앱 업데이트 조회가 404가 돼 안내가 깨진다.
- 수정 방향: 운영 생성 파일의 경로 분리 또는 명시적 보존 규칙. 앱 release→대시보드 deploy→버전 조회를 시험한다.

### 11. P2 — 운영 이름 편집이 다른 기기의 변경을 조용히 덮는다

- 위치: `scripts/ops/ability-ops-service.py:78`; `docs/items.js:48`.
- StateStore는 전체 사전을 덮어쓰며 revision 검사가 없다. Lock은 개별 쓰기를 직렬화할 뿐 오래된 사본의 저장을 막지 않는다. 서로 다른 항목을 수정해도 나중 저장이 앞 변경을 지운다. 임시폴더에서 두 저장 후 마지막 사전만 남는 것을 재현했다.
- 클라이언트 `.catch`는 HTTP 401/400을 실패로 취급하지 않으므로 저장 거절도 보이지 않는다. 기술 override에는 revision 검사가 있어 일관성도 다르다.
- 수정 방향: revision/ETag 충돌 또는 항목별 갱신, 응답 상태 확인과 오류 표시. 두 기기 동시 편집·권한 만료·실패 후 재시도를 시험한다.

### 12. P2 — 운영값 저장과 감사기록 실패의 결과가 모순된다

- 위치: `scripts/ops/ability-ops-service.py:87`, `:158`, `:338`.
- 값을 atomic replace한 뒤 감사로그를 쓴다. 로그 I/O 오류는 이미 값이 바뀐 뒤 요청을 실패시킬 수 있고 기록은 남지 않는다. 임시폴더에서 로그 경로를 디렉터리로 만들어 저장된 값과 IsADirectoryError를 함께 확인했다.
- 수정 방향: 상태와 감사기록을 하나의 트랜잭션으로 저장하거나, 부분 성공의 복구·응답 의미를 명확히 정한다. 디스크 부족·권한 오류·재시도와 revision 처리를 검사한다.

### 13. P2 — 운영 비밀번호 문자열을 shell/JSON에 직접 삽입한다

- 위치: `scripts/ops/cloud-dashboard.sh:274`; `scripts/ops/cloud-server.sh:148`.
- 관리자 비밀번호는 원격 shell 작은따옴표 안에, 동료 봇 비밀번호는 printf JSON 안에 들어간다. 작은따옴표는 shell 문법을 깨고, 큰따옴표·역슬래시는 잘못된 JSON을 만든다. 인터넷 공격 가능성을 입증한 항목은 아니며 운영 입력 처리 결함이다.
- 수정 방향: shell 코드와 값 전달을 분리해 stdin으로 넘기고 JSON serializer를 사용한다. 특수문자·공백을 포함한 입력을 시험한다. 생태계 봇 설정의 json.dumps 방식을 참고할 수 있다.

### 14. P2 — 알려진 취약 의존성과 지원 종료 계획의 공백

`Hades.Server.Base.csproj:23`에 오래된 직접 의존성이 있고, NuGet 직접·전이 취약성 검사를 실행해 다음을 확인했다.

| 패키지 | 해석된 버전 | 검사 심각도 | 공지 |
|---|---:|---|---|
| Newtonsoft.Json | 12.0.3 | High | [GHSA-5crp-9r3c-p9vr](https://github.com/advisories/GHSA-5crp-9r3c-p9vr) |
| System.Drawing.Common | 4.7.0 | Critical | [GHSA-rxg9-xrhp-64gj](https://github.com/advisories/GHSA-rxg9-xrhp-64gj) |
| System.Net.Http | 4.3.0 | High | [GHSA-7jgj-8wvc-jh57](https://github.com/advisories/GHSA-7jgj-8wvc-jh57) |
| System.Security.Cryptography.Xml | 4.5.0 | Moderate | [GHSA-vh55-786g-wjwj](https://github.com/advisories/GHSA-vh55-786g-wjwj) |
| System.Text.Encodings.Web | 4.5.0 | Critical | [GHSA-ghhp-997w-qr28](https://github.com/advisories/GHSA-ghhp-997w-qr28) |
| System.Text.RegularExpressions | 4.3.0 | High | [GHSA-cmhx-cq75-c4mj](https://github.com/advisories/GHSA-cmhx-cq75-c4mj) |

패키지 그래프의 검출 결과다. 새로 빌드한 `Staging/net9.0/Lorule.GameServer.deps.json`에는 Newtonsoft.Json·System.Drawing.Common·System.Security.Cryptography.Xml의 runtime 자산이 있으며, 나머지 세 패키지에는 별도 runtime 자산이 없다. .NET 9 실제 로딩 assembly·외부 입력 도달성을 확인해 즉시 적용 가능한 취약점을 선별해야 한다. Critical이라는 검사 표시만으로 원격 코드 실행이 가능하다고 단정하지 않는다. Newtonsoft 공지의 수정 버전은 13.0.1이며 저장 직렬화·스크립트와 호환성 회귀 시험이 필요하다.

실제 서버·모바일·봇은 net9.0이다. .NET 9 지원 종료는 2026-11-10으로, 리뷰일 기준 약 한 달 남았다. Godot·iOS AOT와 함께 지원 런타임 전환 계획이 필요하다. [Microsoft 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

### 15. P2 — 경매 보고의 금화 일치 검사가 실제 저장 결과를 검증하지 않는다

- 위치: 서버 `Types/AuctionHouse.cs:568`, `:428`; `scripts/ops/auction-report.py:84`.
- 거래 전에 Begin에 `goldBefore = Funds(me)`, `goldAfter = Funds(me) - price`라는 **예상값**을 기록한다. 보고서는 그 두 값의 차이가 price와 같은지 계산한다. 실제 변경 후의 캐릭터 파일·은행·경매 장부를 비교하는 검사가 아니다.
- 따라서 “금화가 기록과 다른 줄 0”은 예상 산술의 일치이며, 거래 중 경쟁이나 저장 실패 이후 실제 총재화 보존을 입증하지 않는다. 금화를 잘못 저장해도 이 검사는 0일 수 있다. `lines():46`의 JSON 파싱 실패는 위치·개수 표시 없이 건너뛰어 로그 손상도 보고서에서 빠진다.
- 수정 방향: 의도와 실제 저장된 결과를 구분해 기록하고 완료 거래·캐릭터·장부를 교차검증한다. 의도 기록은 정상이나 실제 파일이 불일치하는 fixture, 중간/마지막 손상 로그를 시험한다. 현재 24시간 운영 판정에 이 지표만으로 무결성을 선언하지 않는다.

## 유지보수·하드코딩·주석에 대한 평가

- **책임 분리:** EcoRunner 약 901줄에 접속·가입·파티·사냥·장보기·경매·장비·부활·경험치 구매·이동·기록이 모여 있다. WorldClient partial은 합계 약 2,300줄에 protocol parsing·상태·송신·수명·표시 이벤트가 모인다. GameServerHandlers도 3,000줄 이상이다. 파일 분할만으로 책임이 분리되지는 않는다. 다만 EcoRunner.Once처럼 보조 함수를 순서대로 호출하는 조율 함수는 여러 호출이 있다는 이유만으로 결함은 아니다. 실패/재시도 정책이 다른 접속·경매·캐릭터 경제 변경부터, 다음 수정 때 좁게 분리하는 편이 낫다.
- **의존 방향:** `Lod.EcoBots.csproj:11`은 공통 기능 재사용 때문에 CompanionBot·HuntProxy 실행 프로그램에 의존한다. 안정적인 공통 로직을 라이브러리로 옮기는 편이 테스트와 배포 영향 범위를 줄인다. 즉시 전체 재작성할 이유는 없다.
- **하드코딩:** 원작 맵·프로토콜 번호·사용자가 정한 가격 상한은 그 자체로 버그가 아니다. 문제는 클라이언트/서버의 같은 규칙이 다른 자료형·계산·수명으로 복제되고, 생성 순서와 배포 전제조건이 사람 기억에 의존하는 것이다. 확인된 사례는 경매 최소금액 계산과 재접속 정책이다. 일회성 수치 전부를 설정화하는 작업은 권하지 않는다.
- **주석:** 티켓 주석은 일회성 소비를 설명하지만 인증 검증을 보장하지 않는다. WorldState의 전체 상태 발표 설명도 맵 전환 중 옛 좌표를 보장하지 못한다. CharacterSaveTests의 옛 2초 저장 제한 주석과 실제 항상 종료 저장은 다르다. 주석을 늘리기보다 실제 계약을 검사하고 그 계약을 적어야 한다.
- **예외 처리:** 원자 저장·오류 로그·연결 Dispose·송신 직렬화는 좋은 기반이다. 반면 저장 false 무시, HTTP 상태 무시, commit 로그 false 무시, 로그인 무기한 대기는 실패를 성공·침묵으로 바꾼다. catch 개수보다 오류 전달·취소·재시도·부분 성공 정책을 점검해야 한다.

## 시험·운영의 블라인드 스팟

| 빈틈 | 현재 근거 | 필요한 검사 |
|---|---|---|
| 시험한 서버와 현재 소스가 다를 수 있음 | characterization csproj:14는 Staging DLL HintPath, :17은 존재 여부만 검사 | 서버 빌드→시험→같은 artifact 배포를 단일 절차로, commit·SDK·자료 manifest 기록 |
| 통과 개수에 실행하지 않은 시나리오 포함 | BotLoadTests:31은 LOD_BOT_LOAD 없으면 Fact에서 return; OtherGearWindowTests:82도 LOD_GEAR_SHOT 없으면 return | 명시적 skip 또는 별도 성능 실행 결과; 수행 여부 표시 |
| 성능 측정과 게임 생존 검증 혼동 | BotLoadTests:68은 HP 100,000; 측정값에 회귀 실패 임계값 없음 | 일반 HP·물약·쿨타임·재접속으로 장시간 생존/보급을 검증, 별도 성능 기준 |
| Core 통과가 Godot 행동까지 보장하지 않음 | Core.Tests는 Core만 참조; 경매 UI 산술은 밖에 있음 | UI가 보낼 금액·버튼 재시도·실기 smoke check |
| 맵 전환 중간 상태 | WorldState.cs:44는 새 맵에 옛 _where로 Place | 맵/위치 패킷 사이 지연 시 준비 상태·좌표·봇 행동; 실제 소비자 피해는 추가 확인 |
| 느린 환경의 네트워크 시험 | HadesConnectionTests.cs:13 static 10초 CTS 공유 | 테스트마다 독립 deadline(P3) |
| 중단된 격리 시험의 프로세스 잔존 | 리뷰 시작 전에 생성된 harness 서버 7개가 PPID=1·실행시간 2일 20시간으로 남아 있음; lsof cwd로 시험 디렉터리 확인 | testhost 비정상 종료 후 자식 회수, 실행 manifest·TTL 기반 잔존 감지; 기존 프로세스는 이번 리뷰에서 종료하지 않음 |
| 종료 저장 시험의 숨은 대기 | CharacterSaveTests.cs:40은 3초 기다림 | 학습/획득 직후 즉시 종료→재접속 시험 |
| 운영 성공 판정이 얕음 | cloud-server.sh:353은 포트 열림 위주, 일부 봇 재시작 오류 무시 | 로그인·저장소·봇 동작 확인 및 실패 롤백 |
| 게임 채널의 서버 인증·전송 보호 | HadesConnection.cs:16은 raw NetworkStream; 원작 패킷 암호화와 dashboard HTTPS는 게임 채널 TLS 보장이 아님 | 클라이언트·서버 채널 보호 설계와 호환성 검증; [OWASP TLS 지침](https://cheatsheetseries.owasp.org/cheatsheets/Transport_Layer_Security_Cheat_Sheet.html) |
| 백업 파일 생성과 일관성 혼동 | cloud-server.sh:383은 live tar, ignore-failed-read | 캐릭터·경매의 일관된 snapshot과 격리 복원 후 총재화 검사 |
| 생성 성공과 의미 보존 혼동 | build-pack-npcs.py:377은 미지원 명령 건너뛰기·없는 label 종료로 변환 | 미지원 목록 baseline, NPC 거래/퀘스트/분기 의미 시험 |
| 앱 리소스 준비 실패 은폐 | ios-build.sh:229의 import 실패 무시 | import 실패 시 중단·신규 리소스 artifact 검사 |
| 오래된 운영 문서가 시험으로 고정됨 | service-readiness.md는 공개 테스트 전·해시/원자 저장/E2E 미구현; security-maintenance.md는 .NET5 | 현재 구현·미완료를 재평가하고 문구 존재 시험을 실제 확인 절차에 연결 |

루트 추적 파일에서 CI 정의·global.json·NuGet lockfile은 발견하지 못했다. 원격 CI/브랜치 보호 설정은 조사하지 않았으므로 없다고 단정하지 않는다. 현재 로컬 시험 실행의 좋은 관행을 릴리스 필수 조건으로 강제하는 저장소 내 절차는 약하다. 알려진 실패를 “원래 깨짐”으로 계속 넘길 때에는 원인·담당·기한·배포 영향이 있는지 기록해야 한다.

## 실행 검증

저장소 SDK `.tools/dotnet-9.0.317`을 PATH에 넣고 실행했다. 서버 시험 전에 현행 서버를 새로 빌드했다. Python·Node는 현행 로컬 runtime을 사용했다.

| 검사 | 결과 |
|---|---|
| 모바일 Core 전체 | 766 통과, 실패 0, 건너뜀 0 |
| Python unittest 전체 | 98 통과 |
| Node 전체 | 51개: 45 통과, 1 실패, 5 건너뜀 |
| Hades 서버 빌드 | 오류 0, Newtonsoft 취약성 NU1903 경고 2 |
| Godot C# 프로젝트 빌드 | 오류·경고 0; export/실기 검증과는 다름 |
| Hades characterization 전체 | 396개: 390 통과, 6 실패, xUnit 건너뜀 0; 59분 22초 |
| NuGet 직접·전이 취약성 조회 | 위 패키지 6개 검출 |

Node 실패: `tests/ability-operations-ui.test.js:16`의 서버 템플릿 전체 포함 검사. 현황판 731개, 서버 기대 734개로 `skill:더블어택`, `skill:드래곤모드`, `skill:트리플어택`이 빠져 있다(템플릿 Name과 운영키 비교). 생성 산출물과 소스 사이의 드리프트를 실제로 잡은 유용한 검사다. 5개 skip은 Windows 전용 PowerShell 검사이며 별도로 통과에 포함하지 않았다. 서버의 조건부 부하·화면 촬영 시험은 환경변수를 설정하지 않아 실제 시나리오를 실행하지 않았지만 xUnit 집계에는 통과로 포함된다. 이번 빌드·시험은 실제 기기 검증을 대신하지 않는다.

서버 실패 6건의 실제 TRX 메시지와 판정:

| 시험·실패 위치 | 이번 실행에서 확인한 결과 | 해석·남은 확인 |
|---|---|---|
| CompanionComaTests.cs:89 | 혼수에서 깨우기는 지나갔으나 60초 안 뮤레칸의방으로 이동하지 않음(맵 20263) | 기존 기록에도 실패. 봇 재사망·유령 복귀 시나리오가 검증되지 않음; 원인은 미확정 |
| OriginalItemValueTests.cs:84 | 장비 15개의 Value: 원작 3천만 ↔ 서버 7백만 | 승인된 서클 가격 상한 변경과 옛 원작 동일성 기대값의 충돌. 시험 안내대로 원작 생성기를 다시 쓰면 승인된 가격 인하를 되돌릴 수 있으므로 그대로 실행하면 안 됨 |
| MissEffectTests.cs:70 | 양의신권 빈 앞칸 공격에서 Miss(115) 효과를 제한시간 안 받지 못함 | 뒤의 단각·자기 회복 검사는 도달하지 못함. 효과 구현·skill 조건·시험 환경 중 원인 분리 필요; 영문 회복 삭제 때문이라고 단정하지 않음 |
| EvidenceBackedDropDistributionTests.cs:76 | 아이템 DropRate 0인 5종을 실패로 판정 | 10개 연결 모두 호러캐슬 괴물 자체 DropRate 0.003/0.0032가 있음. 실제 Formulas/monsterexp.cs:115는 괴물 확률을 우선하므로 아이템 확률만 보는 시험의 잘못된 전제. 드랍 불가로 단정하면 안 됨 |
| EvidenceBackedDropDistributionTests.cs:91 | 템플릿 기대 1,364개, 실제 1,600개 | 고정 개수 기대값이 현행 자료와 불일치. 추가 236개가 모두 승인된 것인지는 별도 자료 대조 필요; 이후 연결 개수 검사는 도달하지 못함 |
| RespawnTests.cs:148 | 앞 1분 17마리, 뒤 1분 7마리로 속도 유지 조건 실패 | 장시간 사냥의 재생성·이동·부하를 분리해 확인할 필요. 이것만으로 재생성 버그라고 확정하지 않음 |

TRX는 `/tmp/lod-review-server-results/review.trx`, 전체 서버 출력은 `/tmp/lod-review-server-test.log`에 남겼다. 리뷰 전에 남아 있던 고아 시험 서버들이 CPU를 사용해 시간 기반 실패에 영향을 주었을 가능성은 있으나 입증하지 않았다. 기존 프로세스를 임의 종료하거나 전체 시험을 반복하지 않았다. 이번 결과는 **시험이 모두 통과한 상태가 아니며**, 최소 3건은 기대값·규칙의 불일치, 나머지 3건은 행동/시간 실패로 원인 확인이 필요하다.

빠른 Core·자료·프로토콜 회귀, 약 한 시간의 전체 격리 서버 회귀, 성능·실기·복원 검사를 구분하고 각 실행 여부를 릴리스 기록에 남기는 편이 좋다. 전체 서버 시험 통과만으로 부하·화면·복원 검증까지 수행했다고 표시하지 않는다.

실행 명령:

```sh
dotnet test mobile/tests/Lod.Mobile.Core.Tests/Lod.Mobile.Core.Tests.csproj --nologo -v q
python3 -m unittest discover -s tests -p 'test_*.py' -q
node --test tests/*.test.js
dotnet build sources/wren11/Dark-Ages-Private-Server/src/Lorule.GameServer/Lorule.GameServer.csproj --nologo -v q
dotnet build mobile/client/LodClient.csproj --nologo -v q
dotnet test tests/hades-characterization/Hades.Characterization.Tests.csproj --nologo -v q --logger 'trx;LogFileName=review.trx' --results-directory /tmp/lod-review-server-results
dotnet list sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Hades.Server.Base.csproj package --vulnerable --include-transitive
```

## 권장 수정 순서와 완료 기준

1. **인증·저장 경계:** 입장 티켓, 온라인 비밀번호 변경, 경제 잠금 통일, 저장 실패 전달. 인증 부정 입력과 기능 간 동시성·디스크 실패를 먼저 실패하는 시험으로 만든다. 인증/데이터 수정 착수는 dev:build L 변경 경로로 진행한다.
2. **릴리스·의존성:** 새 빌드·시험·동일 artifact 승격, 중간 실패 롤백·복원 시험, 취약 패키지 실제 도달성 확인과 지원 런타임 계획. 기존 서버·봇·클라이언트가 같은 manifest로 묶여야 한다.
3. **사용자·운영 회귀:** 경매 금액, 로그인 deadline, 유찰 재접속, 대시보드 버전 파일, 운영 편집 충돌/감사실패. 문서·생성 결과를 현행으로 정리하고 미실행 시험·알려진 실패를 명시한다.

이 과정에서 긴 함수를 전부 쪼개거나 상수를 전부 설정으로 옮기기보다, 확인한 결함을 막는 공통 경계와 시험을 먼저 만든다. 새 기능 수·통과 시험 수만으로 진척을 판단하지 않고 **동일한 빌드의 검증·실패 복구·변경 후 운영 관측**까지 완료 기준에 넣는 것이 필요하다.

## 수정 결과 (2026-10-08, L 변경 · `dev:build`)

명세 `plans/review-fixes-spec-2026-10-08.md`. 배포는 하지 않았다(사용자가 저녁에).

| # | 고친 것 | 시험 | 남은 한계 |
|---|---|---|---|
| 1 | 입장권 = 이름 + 로그인한 그 접속의 Id·Seed·Salt, 60초, 한 번(`Types/EntryTickets.cs`). 틀린 시도는 입장권을 지우지 않는다 | `EntryTicketTests` 격리 서버(틀린 Id·Salt 거절 뒤 주인 입장) + 단위(만료·한 번) | Id·Salt 는 `System.Random` — 엿보는 길이 있을 때만 의미(보안 리뷰 Low) |
| 2 | 비밀번호 변경은 접속 중이면 그 사람(메모리)을 바꿔 캐릭터 자물쇠 안에서 저장, 실패면 되돌림. 확인·해시는 자물쇠 밖 | `AccountSaveTests` — 접속 중 변경 → 주기 저장 → 나감 → 새 비밀번호만 됨 | 변경 횟수 제한 없음(전부터) |
| 3 | 은행이 저장·경매와 같은 캐릭터 자물쇠 | `BankTests` 계약(스크립트가 그 자물쇠를 잡는다) + 기존 은행 시험 | 밖에서 경쟁을 매번 재현할 수 없어 계약 시험. 상점 등 은행 밖 경제 변경은 그대로(backlog) |
| 4 | 배포: 맥에서 모두 빌드 → `.next` 로만 올림 → 원격 한 번 전환(멈춤·`.prev` 보존·반영·켬·확인) → 실패면 `.prev`. 서버 폴더에 `deploy-manifest.json`. 되돌림은 게시판 글을 지우지 않음. 원격 `flock` | `tests/test_cloud_server_deploy.py` 가짜 원격 9건(각 단계 실패 주입) | 확인은 포트 + 「Game server is online.」까지(실제 로그인 아님). 클라우드 rsync 의 hardlink 는 첫 배포에서 확인 |
| 5 | `GameClient.Save()` → bool, 성공일 때만 LastSave·성공 줄. 생성·비밀번호 변경은 저장 실패면 실패 응답(앱도 그 까닭을 보인다) | `AccountSaveTests` 쓰기 금지 폴더 · `MobileClientProtocolTests` | 실패 중 재시도는 핑마다 |
| 6 | 못 적은 commit 만 Pending 에 남겨 다시 | `AuctionTests` Pending 길 | 「경매장 파일은 썼는데 commit 줄만 못 씀」은 밖에서 만들 수 없어 코드 검토로만 |
| 7 | 입찰 기본값 = 서버와 같은 식 `Auction.NextBid`(ulong, uint 포화) | Core 경계 5건 | — |
| 8 | 로그인·만들기 전체 30초(호출자 취소와 구분) | Core 가짜 TCP 단계별 9건 | 입장 요청 뒤(세계 첫 응답) 침묵은 범위 밖 — 봇은 90초 감시 |
| 9 | 봇 이름별 유찰 목록을 `eco-unlisted.json` 에 남김(재접속·재시작) | Core 5건 | 목록은 줄지 않는다(파일을 지워 초기화) |
| 10 | 대시보드 동기화가 `download/version-*.txt` 를 지우지 않음 | 가짜 원격 | — |
| 11 | 이름 편집은 바꾼 칸만 병합, 화면은 실패를 알림 띠로 | Python·Node | — |
| 12 | 감사기록 먼저, 실패면 값 그대로 · 값 쓰기 실패는 실패 줄 | Python | — |
| 13 | 비밀번호는 stdin·`json.dumps` 로(따옴표·역슬래시·공백·한글 그대로) | 가짜 원격 2건 | — |
| 14 | Newtonsoft 13.0.3 · 전이 취약 5개 고친 판 직접 참조 · 게임 서버의 안 쓰는 ASP.NET Core 묶음(Kestrel Critical) 뺌 | `dotnet list … --vulnerable` 0건, 빌드 경고 0 | .NET 10 전환은 계획만 `plans/dotnet10-migration-2026-10-08.md` |
| 15 | 캐릭터 저장 직후 `saved`(goldSaved) 줄, 보고서가 예상과 저장값을 비교 · 깨진 줄을 파일:줄로 | `tests/test_auction_report.py` · `AuctionTests` | 그사이 경매 밖에서 금화가 바뀌면 다르게 나올 수 있다(보고서가 안내) |

그 밖: `out/` 을 git 무시(로그인 정보 `credential` 이 섞여 있었다) · 리뷰 전부터 남은 격리 시험 서버 7개를 껐다.

검증:
- Core 785 통과 · Python 114 통과 · Node 54 중 48 통과/1 실패(위 `ability-operations-ui` 드리프트, 그대로)/5 건너뜀
- 서버 빌드 경고 0 · 취약 0 · 앱·봇 셋 빌드 0/0
- 서버 전체 403: 396 통과/7 실패 — 리뷰 때와 같은 5(드랍 둘·CompanionComa·OriginalItemValue·MissEffect) + HorrorCastle·PoteDungeon(따로 돌리면 4/4 통과 — 전체 실행 부하 아래 흔들림). Respawn 은 이번에 통과
- 보안 리뷰 반영 뒤: 바뀐 길 격리 시험 66/66 · 배포·운영 28/28
- 리뷰: 정확성(다른 모델) 1회 · 보안 1회 — 지적 중 정확성·보안에 닿는 것은 반영, 나머지는 위 「남은 한계」와 `plans/backlog.md`
