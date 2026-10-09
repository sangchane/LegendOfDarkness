# 관리 페이지 — 팝업 없는 로그인 · 바꾼 값 서버 보관 (2026-09-30, 등급 L 변경)

사용자: "로그인 팝업 뜨지 않도록" → "로그인 하고 로그인 유지 기능 만들어 팝업으로 처리하지 말고",
"어떤식으로든 변경사항 생길때 값들 서버에 남겨둬서 백업할 수 있게".

## 무엇
- 브라우저 Basic 팝업을 없앤다. nginx 는 인증 없이 모든 요청을 운영 서비스(127.0.0.1:8787)로 넘기고,
  서비스가 로그인을 본다. 로그인 안 한 사람이 페이지를 열면 페이지 안 로그인 화면(`login.html`)이 나온다.
- 로그인: `POST /api/login {password, remember}` → 서명 쿠키 `lod_ops`(HttpOnly · Secure · SameSite=Strict).
  「로그인 유지」면 30일, 아니면 브라우저를 닫을 때까지(토큰 자체는 12시간). 서버에 세션을 두지 않는다 —
  비밀번호를 바꾸면(`cloud-dashboard.sh password`) 모든 쿠키가 무효가 된다. 스크립트용 Basic 헤더도 계속 받는다.
- 틀린 비밀번호: 같은 곳에서 10분에 5번 넘게 틀리면 10분 동안 429.
- 바꾼 값 보관: 페이지에서 바꾸는 값은 모두 서버에 저장한다.
  - 기술·마법 연출(이미 서버) · 아이템 한글 이름 입력(`item-names`). 아이콘 고르기(`ability-icon-picker.js`)는
    어느 페이지에도 붙어 있지 않아 뺐다 — 붙일 때 `StateStore.NAMES` 에 더한다.
  - `GET/PUT /api/state/<이름>` → `data/state/<이름>.json`. 브라우저에 먼저 있던 값은 처음 열 때 서버로 합쳐 올린다.
  - 모든 저장은 `data/changes.jsonl` 에 한 줄씩 쌓인다 — 지우지 않는다.
    꼴: `{"at": "2026-09-30T12:00:00+00:00", "kind": "state", "key": "item-names", "value": {…}}`
  - `cloud-dashboard.sh backup` → `~/LOD-backups/cloud/ops-data-<시각>/`(비밀번호 파일 제외).

## 손님 비밀번호 · 내려받기 탭 통합 (2026-10-09, 등급 L 변경)
사용자: "download 페이지도 비번 있어야 접속되게 하고 비밀번호는 **** 로" → "다운로드 탭이랑 통합하고 다운로드 탭을 로그인 옆에
탭으로 메뉴 빼둬. ****로 들어오는 사람은 앱 접속·활동 같은 관리자 메뉴 보이지 않도록, lod-admin 만 모든 메뉴".

- 비밀번호 둘: 관리자 `data/credential`(lod-admin, 그대로) · 손님 `data/member-credential`(`member:<손님 비밀번호>` — 값은 공개 저장소에 적지 않는다, 서비스가 credential 옆에서 읽는다).
  로그인 칸은 하나 — 관리자 것과 맞으면 관리자, 손님 것과 맞으면 손님. 쿠키 서명 열쇠가 각자의 비밀번호에서 나와 섞이지 않는다.
  `cloud-dashboard.sh member-password <새것>` 으로 바꾼다(관리자 `password` 와 따로).
- `GET /api/session` → `{"signedIn": 관리자인가, "role": "admin"|"member"|null, "ota": 관리자에게만}`. 고치기·`/api/activity`·`/api/password` 는 관리자만(그대로).
- 화면: 「앱 내려받기」는 왼쪽 메뉴에서 빼서 위 띠의 로그인 단추 옆. 「접속·활동」·계정 관리·편집은 관리자에게만 보인다.
  공개 내려받기 페이지(`docs/download/index.html`)의 안내·그림을 이 탭으로 옮기고 그 파일은 지운다. 로그인 전에는 탭이 로그인을 권한다.
- nginx: `/download/` → `/?view=download`(앱의 「새 판」 주소가 그대로 산다). `.ipa`·`.apk`·`.zip` 은 auth_request(`/api/signed-in` 204/401)
  → 401 이면 `/login.html?next=…`. `version-*.txt` 는 열어 둔다(앱이 쿠키 없이 묻는다).
- 「내 아이폰에 설치」: 아이폰 시스템은 쿠키 없이 manifest·.ipa 를 받는다 → 관리자 비밀번호에서 나온 표(`ota`)를 주소에 단다.
  `/download/manifest.plist?ota=…` 는 서비스(`/api/ota-manifest`)가 .ipa 주소에 같은 표를 붙여 돌려주고, `/api/signed-in` 은 원래 주소(X-Original-URI)의 표도 받는다.
- 완료 기준: 로그인 없이 .ipa·.apk·.zip → 302 login · 손님·관리자 쿠키로 200 · 표 붙은 manifest·.ipa 200, 틀린 표 401 · `version-ios.txt` 200 ·
  손님 비밀번호로 들어가면 「접속·활동」 없음·편집 잠김·내려받기 보임 · 관리자 비밀번호로 전부 보임 · 손님 쿠키로 PUT·activity·password 401.

## 보안 점검 항목
- 쿠키 서명 HMAC-SHA256, 비교는 `hmac.compare_digest`. 쓰기는 `Content-Type: application/json` 만(교차 출처 폼 차단) + SameSite=Strict.
- 로그인 없이 받는 것은 `login.html`·`login.js` 와 `/api/login` 뿐. 상태 이름은 허용 목록, 크기 256KB 상한.

## 시험
`python3 -m unittest tests/test_ability_ops_service.py`
