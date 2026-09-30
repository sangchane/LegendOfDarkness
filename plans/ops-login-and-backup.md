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

## 보안 점검 항목
- 쿠키 서명 HMAC-SHA256, 비교는 `hmac.compare_digest`. 쓰기는 `Content-Type: application/json` 만(교차 출처 폼 차단) + SameSite=Strict.
- 로그인 없이 받는 것은 `login.html`·`login.js` 와 `/api/login` 뿐. 상태 이름은 허용 목록, 크기 256KB 상한.

## 시험
`python3 -m unittest tests/test_ability_ops_service.py`
