# SPEC: 운영 홈페이지 카카오 로그인 (L 변경 · 2026-10-10)

## 배경
사용자: 「홈페이지 말이야 아무나 접근 할 수 없게 해줄래 카카오톡 로그인 같은 기능 말이야」.
정한 것(같은 날): 카카오로 처음 들어오면 **일단 모두 허가**, 관리자가 모르는 사람을 **거부**로 바꾼다 ·
손님 4자리 비밀번호는 없애고 관리자 비밀번호는 비상용으로 남긴다 · 카카오 손님이 보는 범위는 지금 손님과 같다
(자료 보기·앱 내려받기. 편집·접속 기록·사람 관리는 관리자만).

## 현재 상태
- 보기는 누구나: `scripts/ops/ability-ops-service.py:294` — `/index.html`·`/api/state/*`·`/api/ability-overrides` 가 로그인 없이 200.
- 로그인은 비밀번호 칸 하나 — 관리자(`data/credential`)·손님(`data/member-credential`) `:357-371`. 쿠키 `lod_ops=만료.서명`,
  서명 열쇠는 각자의 비밀번호에서(`session_key` `:206`). 역할 `_role` `:458`.
- 앱 파일은 nginx `auth_request /_signed_in` → `/api/signed-in`(`cloud-dashboard.sh:180-240`). `version-*.txt` 는 열려 있다(앱이 쿠키 없이 묻는다).
- 아이폰 설치는 관리자 비밀번호에서 나온 표(`ota_token`)를 주소에 단다.

## 제안 변경
1. **카카오 로그인(인가 코드 방식, 서버에서 교환)** — `ability-ops-service.py`
   - `GET /api/kakao/start?next=/…` → 무작위 state 를 쿠키 `lod_kakao`(HttpOnly·Secure·SameSite=Lax·Path=/api/kakao/·10분)에 `state|next` 로 두고
     `https://kauth.kakao.com/oauth/authorize?response_type=code&client_id=…&redirect_uri=…&state=…` 로 302.
     `next` 는 `/` 로 시작하고 `//`·`/\` 가 아닌 것만, 아니면 `/`.
   - `GET /api/kakao/callback?code&state` → 쿠키 state 와 같은지(compare_digest) → `POST https://kauth.kakao.com/oauth/token`
     (grant_type=authorization_code, client_id, redirect_uri, code, client_secret) → `GET https://kapi.kakao.com/v2/user/me`(Bearer)
     → `id`(정수)·닉네임. 교환 함수는 클래스 속성 `kakao_exchange` 로 두어 시험이 가짜로 바꾼다.
   - 실패(state 다름·취소 `error=`·교환 실패) → `/login.html?error=kakao`. 설정 파일 없음 → `?error=kakao-off`. 거부된 사람 → `?error=denied`.
2. **사람 목록** `data/kakao-users.json` — `{"<id>": {"name", "allowed", "first", "last"}}`(UTC ISO). 처음 오면 `allowed: true`, 올 때마다 `last`.
   잠금 하나로 읽고·바꾸고, `save_credential` 처럼 옆에 써서 이름 바꾸기.
3. **카카오 세션** — 쿠키 `lod_ops=k<id>.<만료>.<서명>`, 서명 = HMAC(session_key(관리자 비밀번호), `kakao:<id>:<만료>`), 30일,
   SameSite=Lax(카카오에서 돌아오는 302 사슬에서도 쿠키가 실리게). 요청마다 목록에서 `allowed` 를 본다 — 거부하면 바로 끊긴다.
   관리자 비밀번호를 바꾸면 카카오 로그인도 모두 풀린다(다시 카카오 한 번).
4. **모두 잠그기** — 역할이 없으면 `_static` 은 `login.html`·`login.js`·`favicon.svg` 만 주고 나머지는 `/login.html?next=<원래 주소>` 로 302.
   `GET /api/state/*`·`/api/ability-overrides` 는 401. `/api/session`·`/api/health`·`/api/signed-in`·`/api/ota-manifest`·카카오 두 길은 그대로 열림.
5. **역할** — 관리자 쿠키·Basic → `admin`, 허가된 카카오 쿠키 → `member`, 그 밖 → 없음. 손님 비밀번호(`member`)와 `member-credential` 읽기를 없앤다.
6. **관리 API(관리자만)** — `GET /api/kakao/users` → `[{id, name, allowed, first, last}]`(마지막 접속 순) · `PUT /api/kakao/users/<id>` `{"allowed": bool}`.
7. **화면**
   - `docs/login.html`·`login.js`: 노란 「카카오 로그인」 단추(카카오 디자인 — 배경 #FEE500, 글자 #000 85%, 말풍선 심볼) → `/api/kakao/start?next=…`.
     관리자 비밀번호 칸은 그 아래 「관리자 비밀번호로 들어가기」 접힘. `?error=` 문구 셋.
   - `docs/index.html`·`session.js`: 관리자 「계정 관리」 창에 「카카오로 들어온 사람」 표(이름·처음·마지막·[허가/거부]).
8. **운영** — `cloud-dashboard.sh`: `member-password` 명령·함수를 지우고 배포 때 `data/member-credential` 을 지운다 ·
   `kakao-keys` 명령(stdin 두 줄: REST API 키, Client Secret → `data/kakao.json` 600, redirect `https://lodgame.duckdns.org/api/kakao/callback`) ·
   backup 은 `kakao.json` 을 받지 않는다. 설정 파일 `kakao.json` = `{"client_id", "client_secret", "redirect_uri"}`.

## 완료 기준
- [ ] SC-1 로그인 없이 `/index.html`·`/?view=abilities` → 302 `/login.html?next=…`, `/api/state/x`·`/api/ability-overrides` → 401, `/login.html` 200 — 시험
- [ ] SC-2 `/api/kakao/start` → 302 kauth 주소(client_id·redirect_uri·state), state 쿠키 Lax·HttpOnly — 시험
- [ ] SC-3 callback: 맞는 state + 가짜 교환 → 세션 쿠키·302 next, 목록에 allowed true · 다른 state/`error=` → `?error=kakao`, 쿠키 없음 — 시험
- [ ] SC-4 카카오 손님: `/index.html` 200 · `/api/signed-in` 204 · PUT·activity·password·kakao/users 401 · session `{"signedIn": false, "role": "member"}` — 시험
- [ ] SC-5 거부로 바꾸면 그 쿠키로 바로 302/401, 다시 카카오로 와도 `?error=denied` — 시험
- [ ] SC-6 손님 비밀번호로는 못 들어온다(401), 관리자 비밀번호·Basic·OTA 표는 그대로 — 시험(기존 시험 고침)
- [ ] SC-7 `next` 가 바깥 주소(`//evil`·`/\evil`·`https://…`)면 `/` 로 — 시험
- [ ] SC-8 화면: 로그인(카카오 단추·접힌 관리자 칸, 390px·1440px) · 계정 관리 표 — 헤드리스 사진
- [ ] SC-9 클라우드: 키를 넣고 배포 → 로그인 없이 302 · version-ios.txt 200 · 사용자가 폰에서 카카오로 들어옴 · 관리자 화면에 그 이름

## 테스트 계획
`tests/test_ability_ops_service.py` 에 카카오 시험 묶음(가짜 `kakao_exchange`, 302 를 따라가지 않는 요청). 손님 비밀번호 시험 2개는 카카오 손님으로 바꾼다.
전체: `python3 -m unittest tests.test_ability_ops_service`.

## 검증 방법
위 시험 통과 → 맥에서 서비스를 띄워 헤드리스로 로그인 화면·계정 관리 사진 → (키 받은 뒤) 배포 → curl 로 SC-9 앞 둘 → 사용자 실제 로그인.

## 롤백
`git revert` 뒤 `cloud-dashboard.sh deploy`. 손님 비밀번호를 되살리려면 `member-password` 가 있던 커밋으로. `kakao-users.json` 은 남아도 해 없음.

## 안 할 것
게임 안 로그인 연동 · 카카오 이메일·전화(비즈 앱 심사 필요) · 사람 목록에서 지우기 · 카카오 사람을 관리자로 올리기 · 초대 링크.

## 참조 파일
`scripts/ops/ability-ops-service.py` · `scripts/ops/cloud-dashboard.sh` · `docs/login.html` · `docs/login.js` · `docs/session.js` · `docs/index.html`
· `tests/test_ability_ops_service.py` · `plans/ops-login-and-backup.md`(손님 비밀번호 결정 기록)

## 작업 순서
T1 서비스: 잠그기·역할·손님 비번 없애기 + 시험(SC-1·4·6) → T2 카카오 start/callback·목록·세션 + 시험(SC-2·3·5·7) → T3 관리 API + 화면(SC-8)
→ T4 운영 스크립트(kakao-keys·member 지우기) → 리뷰(정확성 + 보안) → T5 키 받아 배포(SC-9).
