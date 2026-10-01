# 기술·마법 연출 운영 화면 작업

1. [x] 생성기 계약 테스트 작성 — 현재 템플릿 운영키와 미디어 카탈로그를 검증한다.
2. [x] 현재 기술·마법 및 이펙트·사운드 카탈로그 생성기를 구현하고 산출물을 만든다.
3. [x] 서버 override 실패 테스트 작성 — 기본값, 치환, 범위, 파일 갱신을 고정한다.
4. [x] 서버 실행 범위와 패킷 직전 override를 구현한다.
5. [x] 운영 API 실패 테스트 작성 — 인증, revision, 검증, 원자 저장, 정적 경로를 고정한다.
6. [x] 표준 라이브러리 운영 API와 클라우드 설치·배포 스크립트를 구현한다.
7. [x] 모바일 UI 계약 테스트 작성 — 기술/마법 탭, 단일 편집판, 상태, 44px, `100dvh`를 고정한다.
8. [x] 기술·마법 목록을 현재 데이터로 교체하고 단일 편집판·미디어 선택·적용 흐름을 구현한다.
9. [x] 생성기·API·대시보드·서버 관련 시험과 빌드를 실행한다.
10. [x] 390×844·데스크톱 시각 QA 뒤 결함을 수정한다.
11. [ ] 클라우드 운영 화면을 HTTPS로 배포하고 저장 왕복을 확인한다.
12. [ ] 게임 서버 배포 충돌 여부를 확인한 뒤 runtime override를 반영하고 실제 패킷을 검증한다.

## 결과

- 현재 Hades 템플릿 731개(기술 315, 마법 416)를 단일 목록으로 생성했고, 이펙트 131개와 사운드 165개를 운영 후보로 제공한다.
- 이펙트·속도·사운드는 항목 선택 뒤 같은 편집 시트에서 바꾸며, API revision 충돌과 읽기 전용 상태를 구분한다.
- 서버는 `/home/ubuntu/lod-ops/data/ability-presentation-overrides.json`을 패킷 직전에 읽도록 클라우드에 반영됐다.
- 클라우드의 nginx·운영 API·게임 서버는 모두 active이고 내부 HTTPS 페이지는 200, health는 정상 응답이다.
- 외부 `443/tcp`는 인스턴스 iptables까지 열렸지만 Oracle VCN 인바운드 규칙이 없어 외부 모바일 접속이 타임아웃된다.
  Oracle 콘솔에서 해당 인스턴스 서브넷의 Security List/NSG에 TCP 443 ingress를 추가한 뒤 10~12번의 외부 모바일·실제 패킷 검증을 마친다.
- 로그인 정보는 저장소 밖 `~/LOD-backups/cloud/ability-ops-credentials.txt`에 권한 600으로 보관했다.

## 10번 시각 점검 결과 (2026-09-27, 로컬 헤드리스)

로컬 운영 API(`scripts/ops/ability-ops-service.py --root docs --catalog data/game-data/ability-operations.json
--overrides <임시파일> --password-file <user:pass 임시파일> --port 8799`)에 헤드리스 크롬으로 390×844·1440×900 을 찍었다.
목록 → 항목 → 편집판 → 적용 → 되돌리기까지 PUT 200 왕복, 가로 넘침 0, 44px 미만 조작부 0.

고친 결함:
- 사운드 후보가 0개였다 — 화면이 `media["사운드"]` 를 읽었는데 카탈로그 키는 `소리`다.
- 이펙트 그림 칸이 원본 크기만큼 길쭉했다(transform 축소는 자리를 줄이지 않는다) — 그림 크기 자체를 44px 안으로 줄였다.
- 닫기(×) 단추가 스타일 없이 25×29px 였다 — `ability-editor-close` 클래스를 달았다.
- 한 쪽뿐인데도 쪽 넘김이 보였고(38px) — `[hidden]` 을 지키고 44px 로 올렸다.
- 편집판을 다시 열면 앞 항목의 스크롤 위치에서 열렸다 — 열 때 맨 위로.

더한 것(가장 작게):
- 「노바 표와 다름」 보기 — 생성기가 노바 팩 표(`data/game-data/ability-effects.json`)의 첫 레벨 이펙트·소리를
  `노바` 로 싣고, 서버 기본과 다르면 `노바와다름`(현재 32개). 카드에 주황 표시, 편집판 맨 위에 서버 기본·노바 표·지금 운영 비교표와
  「노바 표 값 넣기」(넣기만 하고 저장은 [운영에 반영]).
- 「최근 바꾼 것」 보기 — 운영 API 가 항목별 저장 시각 `changedAt` 을 남긴다(게임 서버는 `abilities` 만 읽으므로 무해). 최근순 정렬, 카드에 시각.
- 되돌리기 — 하단 [원래 값으로] 한 번(운영값을 지워 서버 기본으로). 바꾼 적 없는 항목에서는 꺼진다.

## 11·12번 — 443 이 열린 뒤 맥 세션이 할 일

로그인은 `~/LOD-backups/cloud/ability-ops-credentials.txt`(저장소 밖, 권한 600). 값은 어디에도 붙여 넣지 않는다.
**저장 왕복 시험은 로컬에서 끝냈다. 클라우드에서는 읽기만 확인한다** — 클라우드의 override 파일은 실제 운영값이다.

11. 대시보드 올리기와 확인
   1. `LOD_CLOUD_IP=161.33.43.117 scripts/ops/cloud-dashboard.sh deploy` — docs/ 와 운영 API(`changedAt` 포함)를 올리고
      API 만 재시작한다. 게임 서버는 재시작하지 않는다(접속자 안 끊김).
   2. `LOD_CLOUD_IP=161.33.43.117 scripts/ops/cloud-dashboard.sh status` → `active active`, 443·8787 LISTEN.
   3. `C=$(cat ~/LOD-backups/cloud/ability-ops-credentials.txt)`
      - `curl -sk -o /dev/null -w '%{http_code}' https://161.33.43.117/` → 401 (인증 없이는 막힘)
      - `curl -sk -u "$C" https://161.33.43.117/api/health` → `{"ok": true}`
      - `curl -sk -u "$C" https://161.33.43.117/api/ability-overrides` → `revision`·`abilities`·`changedAt` 이 온다
      - `curl -sk -u "$C" https://161.33.43.117/ability-operations-data.js | grep -c 노바와다름` → 1 이상(새 자료가 올라감)
   4. 헤드리스 390×844 로 `https://161.33.43.117/?view=abilities` 를 인증 붙여 찍는다(자체 서명 인증서라
      `ignoreHTTPSErrors`). 상태 배지가 「운영 연결됨」, 「노바 표와 다름」 숫자가 보이면 끝. [운영에 반영]은 누르지 않는다.
   5. 휴대전화: 자체 서명 인증서 경고를 한 번 넘기고 로그인 → 같은 화면 확인(사용자).
12. 실제 패킷 확인 — **사용자 동의를 받은 뒤에만**(운영값을 실제로 바꾸는 일)
   1. 게임 서버(systemd `lod`)에 `LOD_ABILITY_OVERRIDES` 가 걸려 있는지 확인:
      `ssh -i ~/.ssh/lod_oracle ubuntu@161.33.43.117 'systemctl show lod -p Environment'`.
      다른 세션이 게임 서버를 배포 중이면 기다린다(배포하면 접속자가 끊긴다).
   2. 사용자와 정한 기술 하나(예: 쿠로토)를 휴대전화 화면에서 이펙트만 바꿔 [운영에 반영] → 1초 뒤 게임에서 그 기술을 쓴다.
      앱/패킷 기록(ServerFormat29 의 대상 그림 번호)이 새 번호인지 본다. 게임 서버 재시작 없이 바뀌어야 한다.
   3. 바로 [원래 값으로] 를 눌러 되돌리고, 다시 쓰면 서버 기본 번호로 돌아오는지 본다.
      `curl -sk -u "$C" https://161.33.43.117/api/ability-overrides` 에 그 키가 없어야 끝.
