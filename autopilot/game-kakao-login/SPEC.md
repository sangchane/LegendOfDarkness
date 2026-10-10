# SPEC: 게임 로그인을 카카오로 — 캐릭터 비밀번호 대신 (L 변경 · 2026-10-11)

## 배경
사용자: 「이거 실제 게임클라이언트에도 적용해」(홈페이지 카카오 로그인을 확인한 뒤) → 고른 방식 **「비밀번호 대신 카카오」** —
캐릭터 비밀번호를 없애고 캐릭터를 카카오 계정에 묶는다. 허가 목록·초대 번호는 홈페이지와 같은 것(`data/kakao-users.json`·`data/join-code`).
기존 캐릭터(사용자 같은 날): 10-08 까지는 사용자 시험용·봇, 그 뒤 실제 손님은 한 명 — **주몽애자**(갤럭시 S23, 안드로이드 앱).
앱 접속 기록으로 확인: 시험용 16(아이폰 15 Pro·맥만) · 봇 45(생태 40·동료사제 5) · 손님 1.

## 현재 상태
- 한 캐릭터 = 한 계정, 이름+비밀번호(`Aisling.Password`, PBKDF2 `Passwords.cs:20`). 저장 `database/server/aislings/<이름 소문자>.json`.
- 만들기 0x02(이름·비밀번호) → 0x04(생김새·직업) `LoginServer.cs:107-131,265-319` — 0x02 는 이름 중복만 본다.
- 로그인 0x03 `LoginServer.cs:182-263` — 생태 봇 이름은 같은 기계에서만(`EcoBots.MayEnter`), 대리 사냥은 비밀번호 칸에 일회용 열쇠
  (`ProxyHunt.Take`, 같은 기계만 `ProxyHunt.IsLoopback`), 그 밖은 `Passwords.Verify`. 비밀번호 바꾸기 0x26 `:389-427`.
- 서버는 HTTP 를 부르지 않는다. 설정은 `ServerConstants.cs`(인터페이스 `:46` + 값 `:250` 꼴) + `scripts/ops/server-config/LoruleConfig.template.json`, 환경변수 `ServerConfig__X` 로 덮을 수 있다.
- 봇 셋(동료·생태·대리)은 클라우드 안에서 127.0.0.1:2610 으로, 비밀번호(또는 대리 열쇠)로 붙는다. 동료·생태 봇은 "계정" 거절이면 스스로 만든다.
- 앱: 첫 화면 `LoginScreen`(이름·비밀번호·자동 로그인 `user://autologin.cfg` 평문 두 줄) → `CreateScreen`(이름·비밀번호·확인·생김새·직업).
  보내기 `HadesLoginClient.LoginAsync/CreateCharacterAsync(…, username, password, …)` — 비밀번호 칸은 StringA.
  HTTPS 는 업데이트 확인 하나(Godot `HttpRequest`, `LoginScreen.cs:110-134`), 바깥 브라우저 `OS.ShellOpen` 한 곳. 딥링크 없음.
- 홈페이지 서비스: 카카오 로그인·허가 목록·초대 번호·`data/session-secret`(서명 열쇠) 이미 있음(`autopilot/kakao-login/SPEC.md`).

## 제안 변경
1. **앱 → 카카오 (표 받기)** — `ability-ops-service.py`. 딥링크 없이 「브라우저에서 로그인, 앱은 기다렸다 받기」:
   - `POST /api/kakao/app-ticket` → `{"ticket"}`(서버가 만든 무작위 32바이트, 10분, 메모리 표 최대 1000개).
   - 앱이 브라우저로 `GET /api/kakao/start?app=<ticket>` → 홈페이지와 같은 카카오 로그인(대기면 초대 번호) → 끝은 `/app-login.html`
     「카카오 로그인이 끝났습니다. LOD 앱으로 돌아가세요」. callback 이 그 표에 카카오 id·닉네임을 묶는다(state 쿠키에 `state|next|ticket`).
   - 앱은 2초마다 `POST /api/kakao/app-token {"ticket"}` → 묶이기 전 202 `{"waiting"}` · 대기 202 `{"pending", "name"}` ·
     허가 200 `{"token", "name"}`(표는 지움) · 거부 403(지움) · 모르는 표·만료 404.
   - **게임 표** `g<id>.<만료>.<서명>` — 서명 = HMAC(session_key(`session-secret`), `game:<id>:<만료>`), 30일. 웹 쿠키(`kakao:`)와 서로 못 바꿔 쓴다.
   - `POST /api/kakao/game-check {"token"}` → 서명·만료가 맞고 허가면 200 `{"id","name"}`, 대기·거부 403 `{"status"}`, 그 밖 401.
     게임 서버가 로그인마다 부른다 — 관리자가 거부하면 다음 로그인부터 바로 막힌다. 표를 가진 사람만 답을 얻으니 밖에 열려 있어도 된다.
2. **게임 서버** (우리 갈래 `sangchane/Dark-Ages-Private-Server`)
   - 설정 `KakaoCheckUrl`(기본 `""` = 끔 → 지금과 똑같이 동작). 클라우드 틀은 `http://127.0.0.1:8787/api/kakao/game-check`.
   - `Aisling.KakaoId`(문자열, 없으면 빈칸) — 이 캐릭터의 주인.
   - 판단은 새 `Types/KakaoLogin.cs` 의 순수 함수(시험이 프로세스 안에서 부른다) + 확인 호출(HttpClient, 3초):
     - 비밀번호 칸이 게임 표 → 확인 → 캐릭터 주인과 같으면 들어감 / 주인이 다르면 「다른 카카오 계정의 캐릭터입니다.」 /
       주인이 없으면 「아직 카카오에 이어지지 않은 캐릭터입니다 — 「옛 캐릭터 잇기」로 옛 비밀번호를 한 번 넣어 주십시오.」
     - `표|옛비밀번호` → 주인이 없고 옛 비밀번호가 맞으면 주인을 묶고 저장한 뒤 들어감(틀리면 「비밀번호가 틀렸습니다.」).
     - 같은 기계(127.0.0.1)에서 온 평문 비밀번호·대리 열쇠 → 지금과 같음(봇 셋, 시험).
     - 밖에서 온 평문 비밀번호 → 「새 앱으로 카카오 로그인을 해 주십시오.」(옛 앱).
     - 표 만료·서명 틀림 → 「카카오 로그인이 끝났습니다. 앱에서 다시 로그인해 주십시오.」 · 대기 → 「관리자 승인을 기다리는 중입니다.」 ·
       거부 → 「들어올 수 없는 카카오 계정입니다.」 · 확인 서버에 못 닿음 → 「로그인 확인 서버에 닿지 못했습니다. 잠시 뒤 다시 해 주십시오.」
   - 만들기 0x02: 밖에서는 비밀번호 칸이 게임 표여야 하고(확인 통과) 0x04 가 `KakaoId` 를 넣고 비밀번호는 무작위 해시. 같은 기계는 지금과 같음.
   - 비밀번호 바꾸기 0x26: 켜져 있으면 밖에서는 「카카오 계정은 비밀번호가 없습니다.」
3. **앱** (`mobile/src/Lod.Mobile.Core` + `mobile/client`)
   - Core `Net/KakaoSignIn.cs`: 표·응답 읽기·게임 표 저장 줄·잇기 칸(`표|옛비밀번호`) — 순수 함수, 시험.
   - `LoginScreen`: 표가 없으면 노란 「카카오로 로그인」 → app-ticket → `OS.ShellOpen(start?app=…)` → 2초마다 app-token
     (「브라우저에서 카카오 로그인을 마치고 돌아오세요」 / 대기 「초대 번호를 넣거나 관리자 승인을 기다려 주세요」). 받으면 `user://kakao.cfg`.
     표가 있으면 「카카오: 닉네임」 · 캐릭터 이름 칸(마지막 이름 기억) · [들어가기] · [새 캐릭터] · [옛 캐릭터 잇기](이름+옛 비밀번호, 한 번) · [카카오 로그아웃].
     서버가 「카카오 로그인이 끝났습니다」면 표를 지우고 처음으로. 자동 로그인은 이름만 기억(표는 kakao.cfg).
   - `CreateScreen`: 비밀번호·확인 칸을 없애고 게임 표를 보낸다.
   - 데스크톱 `login.cfg`(맥 시험용 이름·비밀번호)는 그대로 — 맥 서버(카카오 끔)와 리허설용.
4. **운영** — 서버 틀에 `KakaoCheckUrl` · `cloud-server.sh deploy`(접속자 끊김 — 직전에 알림) · `cloud-dashboard.sh deploy` ·
   앱 `ios-build.sh install` + 안드로이드·윈도우 release(S23 손님이 안드로이드). 옛 앱은 업데이트 알림(`version-*.txt`)으로 넘어온다.

## 완료 기준
- [ ] SC-1 서비스: app-ticket → start?app= → callback 이 표에 묶음 → app-token 이 waiting/pending/token/403/404 를 차례대로 — 시험
- [ ] SC-2 서비스: game-check 허가 200(id·name) · 대기·거부 403 · 서명 틀림·만료·웹 쿠키 값 401 — 시험
- [ ] SC-3 서버 판단(프로세스 안): 내 표로 내 캐릭터 들어감 · 남의 캐릭터 거절 · 주인 없는 캐릭터는 잇기 요구 · 잇기(옛 비밀번호 맞음/틀림) · 밖 평문 거절 · 같은 기계 평문·대리 열쇠 그대로 · 대기·거부·확인 실패 문구 — 시험
- [ ] SC-4 서버 실제(격리 서버 + 가짜 확인 HTTP): 표로 만들기 → 들어가기 → 저장된 JSON 에 KakaoId · `KakaoCheckUrl` 빈칸이면 기존 시험 모두 그대로 — 시험
- [ ] SC-5 앱 Core: 표 응답 읽기·kakao.cfg 줄·잇기 칸 — 시험(`dotnet test mobile/tests/…`)
- [ ] SC-6 앱 화면: 표 없음 / 기다림 / 표 있음 / 잇기 / 새 캐릭터(비밀번호 칸 없음) — 헤드리스 배치 검사·사진
- [ ] SC-7 클라우드: 배포 뒤 사용자가 아이폰에서 카카오로 새 캐릭터 또는 잇기 → 들어감, 봇 셋이 그대로 접속, 홈페이지 사람 목록 그대로 · 주몽애자 손님이 S23 에서 옛 비밀번호로 잇기

## 테스트 계획
서비스 `python3 -m unittest tests.test_ability_ops_service` · 서버 `dotnet test tests/hades-characterization/… --filter Kakao` + 로그인 관련 기존 묶음
(`LoginFlow`·`Passwords`·`ProxyHunt`·`EcoBotServer`·`AccountSave`) · 앱 `dotnet test mobile/tests/Lod.Mobile.Core.Tests/…`.

## 롤백
서버 설정 `KakaoCheckUrl` 을 빈칸으로 → 다시 켜기만 하면 옛 방식(묶인 `KakaoId` 는 남아도 무해). 코드는 `git revert` 뒤 각 deploy.

## 알려진 한계
- 남이 만든 표 링크를 눌러 로그인하면 그 사람 앱이 내 표를 받는다(기기 짝짓기 방식의 피싱). 초대받은 사람끼리라 받아들임 —
  공개로 넓히면 앱과 브라우저에 같은 확인 번호를 띄워 맞추는 단계를 더한다.
- 표는 30일 — 그 안에 거부하면 다음 로그인에서 막히지만 이미 접속해 있는 사람은 나갈 때까지 그대로.
- 캐릭터 목록 API 없음 — 앱은 마지막 이름만 기억한다.

## 안 할 것
카카오 SDK·딥링크 · 캐릭터 지우기·탈퇴 · 서버의 「내 캐릭터 목록」 · 홈페이지에서 캐릭터 관리 · 봇을 카카오로.

## 참조 파일
`scripts/ops/ability-ops-service.py` · `tests/test_ability_ops_service.py` · `docs/app-login.html`(새) ·
서버 `Network/Login/LoginServer.cs`·`Types/ProxyHunt.cs`·`Types/EcoBots.cs`·`Types/Aisling.cs`·`Infrastructure/ServerConstants.cs`·`Types/KakaoLogin.cs`(새) ·
`scripts/ops/server-config/LoruleConfig.template.json` · `tests/hades-characterization/`(로그인 묶음) ·
앱 `mobile/client/src/Screens/LoginScreen.cs`·`CreateScreen.cs`·`Main.Settings.cs` · `mobile/src/Lod.Mobile.Core/Net/HadesLoginClient.cs`·`Net/KakaoSignIn.cs`(새).

## 작업 순서
T1 서비스: app-ticket·app-token·game-check·게임 표 + 시험(SC-1·2) → T2 서버: 설정·KakaoId·KakaoLogin 판단 + 프로세스 안 시험(SC-3)
→ T3 서버 실제 시험(SC-4, 가짜 확인 HTTP) → T4 앱 Core(SC-5) → T5 앱 화면(SC-6) → 리뷰(정확성 + 보안)
→ T6 배포: 서비스 → 앱(아이폰 install·안드로이드·윈도우 release) → 서버(알림 뒤) → SC-7.
