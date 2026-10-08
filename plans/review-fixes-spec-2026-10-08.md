# SPEC: 코드 리뷰(2026-10-08) 지적 15건 수정

등급 L 변경 · `dev:build` · 입력 `plans/code-review-2026-10-08.md`. 작업 목록 `plans/review-fixes-tasks-2026-10-08.md`.

## 배경
리뷰가 인증(입장 티켓·비밀번호), 금화 저장 잠금, 저장 실패 처리, 배포 중간 실패, 운영 도구의 동시 편집·비밀번호 처리, 취약 패키지를 지적했다. 운영 서버에는 사람과 봇 40여 명이 있다 — 금화·캐릭터를 잃거나 남이 들어오는 길은 막고, 배포가 반쯤 끝난 채 남지 않게 한다.

## 현재 상태 (리뷰 번호 → 코드)
1. 입장 티켓: `LoginServer.cs:72` 이름만 `ServerContext.Redirects`(List<string>)에 넣고, `GameServerHandlers.cs:784` 이름 제거만 검사, `:3039` 요청이 보낸 Parameters 채택. 유효기간 없음.
2. 비밀번호 변경: `LoginServer.cs:360-393` 디스크에서 읽은 별도 객체를 바꿔 통째 저장. 온라인 객체는 옛 비밀번호.
3. 은행: `Banker.cs:75` `lock (bank)`. 저장·경매는 `AislingStorage.LockFor(name)`.
4. 배포: `cloud-server.sh:389` upload(rsync --delete 운영 폴더) → 봇·프록시·생태계 빌드·전송 → restart. 중간 실패 시 혼합. 서버 빌드 안 함.
5. 저장 실패: `AislingStorage.Save` 결과 버림, `GameClient.Save:894` 실패에도 LastSave·성공 로그, 생성·비밀번호 변경 성공 응답.
6. 경매 commit: `AuctionHouse.cs:156-160` Pending 의 Write 결과 무시 후 Clear, `:225` SaveBook 성공 뒤 commit Write 결과 무시.
7. 모바일 입찰 기본값: `AuctionPanel.Forms.cs:128` `row.Price * 5` uint 넘침.
8. 로그인 대기: `HadesLoginClient.cs` AwaitRedirect 밖 단계는 호출자 토큰뿐.
9. 유찰품 기억: `EcoRunner.cs:41` 인스턴스 HashSet, 재접속마다 새 EcoRunner(`EcoHost.cs:280`).
10. 대시보드: `cloud-dashboard.sh` `rsync --delete docs/ → www/` 가 원격 `download/version-*.txt` 지움.
11. 운영 이름 편집: `ability-ops-service.py` StateStore 전체 덮어쓰기, `docs/items.js` 응답 상태 무시.
12. 운영 감사기록: 값 교체 뒤 감사로그, 로그 실패 시 값은 바뀌었는데 요청 실패.
13. 비밀번호를 shell 따옴표·printf JSON 에 직접 삽입: `cloud-dashboard.sh:274`, `cloud-server.sh:148`.
14. 취약 패키지(Newtonsoft 12.0.3 외 전이 5개), .NET 9 지원 종료 2026-11-10.
15. 경매 보고: Begin 의 goldBefore/After 는 예상값, 보고서는 그 산술만 비교. JSON 깨진 줄 조용히 건너뜀.

## 제안 변경 (결정)
1. **입장 티켓** — 새 `Types/EntryTickets.cs`(정적, 자체 lock). `Issue(name, id, seed, salt, now)` 가 이름별 한 장(덮어씀), 유효 60초. `TryConsume(name, id, parameters, now)`: 이름·Id·Seed·Salt 가 모두 맞고 유효기간 안이면 지우고 true. 틀리면 **지우지 않고** false(남의 틀린 시도가 정상 입장을 막지 못하게). 만료면 지우고 false. `Revoke(name)`. `ServerContext.Redirects` 는 없앤다(모든 사용처 교체). `EnterGame` 은 검증한 티켓의 Parameters 만 쓴다.
2. **비밀번호 변경** — `LoginServer` 에 한 곳: `lock (LockFor(name))` 안에서 접속 중인 객체가 있으면 그것, 없으면 디스크 객체를 대상으로 옛 비밀번호 확인 → 새 해시 → `TrySave`. 실패하면 메모리 값을 되돌리고 실패 응답. 로그인 시 옛 평문 비밀번호 재해시(`:199`)도 같은 길.
3. **은행 잠금** — `Banker.OnResponse` 의 `lock (bank)` → `lock (AislingStorage.LockFor(aisling.Username))`. 같은 스레드 재진입 가능(Monitor). 은행 안에서 경매 문(Gate)·SyncLock 을 잡지 않음을 확인한다(잠금 순서: Gate → LockFor, SyncLock → LockFor).
4. **배포** — 모든 것을 먼저 만든다(서버 `dotnet build`, 봇·프록시·생태계 publish). 원격에는 운영 폴더 옆 `.next` 에 올린다. 전부 올라간 뒤에만 한 번의 원격 전환: 서비스 정지 → 현재 코드 폴더를 `.prev` 로 보존 → `.next` 를 운영 자리에 반영(live 자료 `aislings/` `auction/` `activity/` 설정 파일은 건드리지 않음) → 시작 → 확인(포트 + 게임 로그인 1회). 확인 실패면 `.prev` 로 되돌리고 다시 시작, 0 아닌 종료. 서버 산출물에 manifest(루트·서버 커밋, dirty 여부, SDK) 를 넣는다.
5. **저장 실패** — `IStorage<T>.Save` 는 그대로, 호출자는 `TrySave` 결과를 본다. `GameClient.Save()` → bool: 성공일 때만 LastSave·성공 로그. 실패면 오류 로그 1줄, LastSave 는 그대로(다음 주기에 다시 시도). `DontSavePlayers` 설정은 지금처럼 저장 없이 성공 취급. 캐릭터 생성·비밀번호 변경은 저장 실패 시 실패 응답.
6. **경매 commit 재시도** — 실패한 commit 만 Pending 에 남긴다: `Commit` 은 SaveBook 성공 뒤 Write 실패면 Pending 에 넣고, SaveBook 은 `Pending.RemoveAll(seq => Write(commit))`.
7. **입찰 기본값** — Core 에 서버와 같은 식 `AuctionMath.NextBid(price, hasBid)`(ulong 계산, uint 포화)를 두고 화면이 쓴다.
8. **로그인 제한시간** — `LoginAsync`·`CreateCharacterAsync` 전체(세계 접속 첫 응답까지)에 기본 30초 deadline(호출자 토큰과 연결). 넘으면 `TimeoutException`("서버가 응답하지 않습니다"). 화면은 실패 때 버튼을 되살린다.
9. **유찰품 기억** — 봇 이름별 유찰 목록을 EcoHost 가 들고 재접속에 넘기며, 생태계 봇 설정 옆 파일(`eco-unlisted.json`, 원자 쓰기)에 남겨 프로그램 재시작에도 유지.
10. **대시보드** — `--delete` 동기화에서 `download/version-*.txt` 를 제외(보존).
11. **운영 이름 편집** — 항목 단위 갱신(서버가 lock 안에서 최신 파일에 한 항목만 병합), 클라이언트는 `res.ok` 를 확인하고 실패를 화면에 보인다.
12. **감사기록 순서** — 감사기록을 먼저 쓴다. 실패하면 값을 바꾸지 않고 500. 값 교체가 실패하면 실패 줄을 감사기록에 남기고(최선) 500.
13. **비밀번호 전달** — 값은 stdin 으로, JSON 은 serializer(`python3 json.dumps`)로 만든다. shell 문자열에 끼워 넣지 않는다.
14. **패키지** — Newtonsoft.Json 13.0.3, 취약 전이 패키지는 고친 버전을 직접 참조해 올린다. `dotnet list package --vulnerable --include-transitive` 0건. .NET 10 전환은 계획 문서만(`plans/dotnet10-migration-2026-10-08.md`) — 클라우드 런타임 설치·Godot/iOS 가 걸려 이번에 바꾸지 않는다.
15. **경매 실제 저장값** — 캐릭터 저장이 성공한 직후(같은 캐릭터 잠금 안) `{"seq","ev":"saved","who","goldSaved": 손+은행}` 줄을 쓴다. 보고서는 commit 된 조작마다 예상(goldAfter) 과 저장(goldSaved) 을 비교하고, saved 줄이 없는 옛 기록은 따로 센다. 깨진 JSON 줄은 파일·줄 번호와 개수를 보고한다. 「금화 어긋남 0」 문구는 「예상과 저장값 일치」로 바꾼다.

## 완료 기준
- [ ] SC-1 틀린 Id·틀린 Salt 입장은 거절되고 그 뒤 정상 입장은 된다; 만료 티켓 거절; 같은 티켓 두 번 금지 유지 — `EntryTicketTests`(격리 서버) + `EntryTickets` 단위 시험
- [ ] SC-2 접속 중 비밀번호 변경 → 주기 저장·로그아웃 → 새 비밀번호 로그인 성공·옛 것 실패 — 격리 서버 시험
- [ ] SC-3 은행 금화 맡기기/찾기를 반복하며 저장 파일을 계속 읽어도 손+은행 합이 늘 같다 — 격리 서버 시험(수정 전 RED)
- [ ] SC-4 저장 폴더 쓰기 금지 동안 「saved」 로그 없음·실패 로그 있음, 풀면 다시 저장됨; 그동안 비밀번호 변경은 실패 응답 — 격리 서버 시험
- [ ] SC-5 사건 기록 쓰기 실패 뒤 다음 조작에서 빠진 commit 이 채워진다 — 격리 서버 시험
- [ ] SC-6 858,993,459/460·10억·20억 경계에서 화면 입찰 기본값 = 서버 최소 입찰 — Core 시험
- [ ] SC-7 침묵하는 TCP 상대에 각 단계에서 deadline 안에 실패하고 다시 시도할 수 있다 — Core 시험
- [ ] SC-8 유찰→회수→새 runner(재접속)·새 host(재시작)에서도 다시 올리지 않는다 — 시험
- [ ] SC-9 배포 각 단계(빌드·전송·전환·시작·확인) 실패 주입 시 옛 운영 폴더·서비스 유지, 확인 실패 시 되돌림 — 가짜 ssh/rsync/systemctl 로 로컬 시험
- [ ] SC-10 앱 버전 파일이 대시보드 동기화 뒤에도 남는다 — 로컬 rsync 시험
- [ ] SC-11 두 기기가 서로 다른 항목을 저장해도 둘 다 남고, 401/400 은 화면에 실패로 보인다 — Python·Node 시험
- [ ] SC-12 감사기록 쓰기 실패면 값이 안 바뀐다 — Python 시험
- [ ] SC-13 작은따옴표·큰따옴표·역슬래시·공백 비밀번호가 그대로 전달된다 — 시험
- [ ] SC-14 취약 패키지 0건, 서버 빌드·전체 격리 시험이 이전과 같은 실패 6건 밖으로 늘지 않는다
- [ ] SC-15 의도는 맞고 저장값이 다른 fixture, 중간·마지막 줄이 깨진 fixture 를 보고서가 잡아낸다 — Python 시험

## 테스트 계획
위험 순: 인증(1·2) → 금화(3·5·6·15) → 배포(4) → 나머지. 서버는 격리 서버 시험(실제 TCP), Core·스크립트는 단위 시험. 수정 전에 실패하는 시험을 먼저 만든다(3은 확률 경쟁이라 반복 횟수로 RED 확인). 끝에 전체: Core·Python·Node·서버 빌드·Godot 빌드·서버 격리 시험 전체.

## 검증 방법
리뷰 보고서의 실행 명령 7개를 그대로 다시 돌려 비교한다. 서버 격리 시험은 리뷰 때 실패 6건(별개 원인) 외 새 실패가 없어야 한다.

## 롤백 계획
루트·서버 submodule 모두 `feature/loot-roll-auction` 위 작은 커밋들 — 항목별 revert. 운영 배포는 이번 작업에서 하지 않는다(사용자가 저녁에). 새 배포 스크립트가 실패하면 `.prev` 되돌림이 자동, 스크립트 자체는 git revert.

## 안 할 것
- 실제 클라우드 배포·ssh 접속(시험은 로컬 가짜 원격으로).
- .NET 10 전환 실행, 게임 채널 TLS, 함수 분할 리팩터, 상점 등 은행 밖 경제 변경의 잠금(별도 기록).
- 리뷰 「블라인드 스팟」 표와 기존 실패 시험 6건 — `plans/backlog.md` 로 옮긴다.

## 참조 파일
`plans/code-review-2026-10-08.md`, `tests/hades-characterization/{EntryTicketTests,BankTests,AuctionTests,CharacterSaveTests,LoginFlow}.cs`, `scripts/ops/{cloud-server.sh,cloud-dashboard.sh,ability-ops-service.py,auction-report.py}`, `docs/items.js`.
