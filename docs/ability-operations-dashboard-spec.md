# 기술·마법 연출 운영 화면 명세

## 배경(Context)

`docs/index.html`의 기술·마법 화면은 서버가 보내는 연출을 읽고 미리 보여 주지만, 잘못된 이펙트와 사운드를
브라우저에서 바로 고쳐 운영에 반영할 수는 없다. 현재 한글 이름과 아이콘 수정도 브라우저 저장소에만 남고,
사람이 복사한 글을 다시 개발 작업으로 옮겨야 한다. 휴대전화에서 원작과 비교하며 고치는 흐름에는 단계가 너무 많다.

## 현재 상태(Current State)

| 위치 | 현재 역할 | 한계 |
|---|---|---|
| `docs/index.html` 기술·마법 화면 | 613개 원작 계보와 Hades 연출 표시 | 기술/마법이 필터 칩이고 운영 저장 기능이 없다 |
| `docs/abilities.js` | 카드·샌드백 미리보기·소리 재생 | 브라우저 로컬 이름 수정만 가능하다 |
| `scripts/build-ability-page-data.py` | 템플릿·스크립트에서 실제 전송 번호 추출 | 현재 Hades 템플릿 전체를 운영 목록으로 내지 않는다 |
| `NetworkClient.FlushAndSend` | 모든 서버 패킷의 직렬화 경계 | 기술·마법별 연출 override가 없다 |
| `scripts/cloud-server.sh` | 게임 서버 배포 | 정적 대시보드와 인증 운영 API를 다루지 않는다 |

## 제안 변경(Proposed Change)

1. 현재 Hades 기술·마법 템플릿을 기준으로 운영 목록을 생성한다.
2. 기술/마법을 화면 최상단의 두 탭으로 나누고, 검색·직업 필터 뒤 항목을 한 번 탭하면 같은 화면의 하단 편집판을 연다.
3. 이펙트 목록은 실제 클라이언트가 가진 그림을 미리 보여 주고, 사운드는 번호별로 바로 재생한다.
4. 이펙트·속도·사운드는 한 편집판에서 바꾸고 고정 하단 버튼 한 번으로 저장한다. 별도 단계·깊은 상세 화면은 만들지 않는다.
5. 서버는 기술/마법 스크립트 실행 범위를 표시하고 패킷 전송 직전에 override를 적용한다. 변경 파일을 1초 이내 다시 읽어
   게임 서버 재시작 없이 다음 사용부터 반영한다.
6. 클라우드에는 HTTPS와 Basic 인증 뒤 정적 페이지와 운영 API를 같은 출처로 서비스한다. 운영값은 게임 배포 경로 밖에
   원자적으로 저장해 다음 배포에도 남긴다.

### 구현 세부(Implementation Details)

- override 키: `skill:<템플릿 Name>` 또는 `spell:<템플릿 Name>`.
- override 값: `effect`(1~999), `speed`(1~255), `sound`(0~255). 빠진 값은 서버 기본값을 유지한다.
- `AbilityPresentationOverrides.Begin(kind, name)`이 실행 범위를 만들고 `NetworkClient.FlushAndSend`가
  `ServerFormat29`, `ServerFormat13`, `ServerFormat19`를 직렬화하기 전에 값을 바꾼다.
- 이펙트는 기존 패킷의 0이 아닌 시전자/대상 칸을 같은 번호로 바꾼다. 둘 다 0이면 대상 칸에 넣는다. 위치 의미는 보존한다.
- 운영 API는 카탈로그에 있는 키만 허용하고 JSON 크기·숫자 범위를 제한한다. `revision` 비교로 오래 열린 화면의 덮어쓰기를 막는다.
- UI는 API가 없으면 읽기 전용 상태를 명시하고 기존 미리보기는 계속 동작한다.
- 디자인 dial: `VISUAL_DENSITY=8`, `MOTION_INTENSITY=2`, `DESIGN_VARIANCE=3`. 44px 터치 영역,
  `100dvh`, 숫자 고정폭, 4.51 계열의 어두운 돌/밝은 돌 토큰을 사용하되 목록 면은 무늬 없이 유지한다.

## 수용 기준(Acceptance Criteria)

- 기술/마법 탭 전환이 한 번의 탭으로 끝나고 현재 서버 템플릿만 표시한다.
- 휴대전화 폭 390px에서 검색, 직업 필터, 목록, 편집판, 적용 버튼이 가로 스크롤 없이 동작한다.
- 항목을 탭한 뒤 이펙트·속도·사운드를 같은 편집판에서 바꾸고 한 번 눌러 저장할 수 있다.
- 이펙트 후보는 그림과 번호, 사운드 후보는 재생과 번호를 제공한다.
- API 미연결·인증 실패·저장 충돌·저장 성공 상태가 서로 다르게 보인다.
- 잘못된 키와 범위 밖 값은 저장되지 않는다.
- 저장 후 1초 안에 서버가 새 값을 읽고 해당 기술/마법의 다음 패킷에 적용한다.
- override 파일은 게임 서버 배포 뒤에도 남는다.
- 대시보드 테스트, 운영 API 테스트, 서버 characterization 테스트, 서버 빌드가 통과한다.
- 실제 모바일 크기 화면을 캡처해 레이아웃을 확인한다.

## 테스트 계획(Testing Plan)

| 층 | 검사 |
|---|---|
| 생성기 | 현재 템플릿 수·고유 운영키·미디어 카탈로그 파일 존재 |
| 서버 단위 | 기본값 유지, effect/speed/sound 치환, 잘못된 파일 무시, 파일 갱신 재읽기 |
| 서버 통합 | 기술과 마법 사용 패킷에 override가 적용됨 |
| API | 인증, 목록, 저장, 삭제, 범위 검사, revision 충돌, 경로 탈출 차단 |
| 프론트 | 탭·편집판·읽기 전용/오류 상태·44px 컨트롤·모바일 CSS |
| 시각 QA | 390×844 및 데스크톱 화면 캡처 |

## 검증 방법(Verification)

- `node --test tests/docs-dashboard.test.js tests/ability-operations-ui.test.js`
- `python3 -m unittest tests/test_ability_ops_service.py`
- 로컬 .NET SDK로 서버 프로젝트 빌드와 관련 characterization 필터 실행
- 로컬 정적/API 서비스를 띄워 브라우저 모바일 뷰를 캡처
- 클라우드 HTTPS 주소에서 health와 저장 왕복을 확인한 뒤 실제 기술 1개를 시험한다.

## 롤백 계획(Rollback)

- 운영 API에서 해당 항목의 override를 삭제하면 즉시 서버 기본값으로 돌아간다.
- 서비스는 별도 systemd/nginx 설정이므로 중지·삭제해도 게임 포트에는 영향이 없다.
- 서버 코드 롤백 시 override 파일은 읽히지 않을 뿐 그대로 보존된다.

## 범위 밖(Out of Scope)

- 피해식·마나·쿨다운·습득 레벨 편집
- 몸동작 번호 편집
- 여러 운영자 권한 등급과 감사 로그 검색 UI
- 원작 이펙트의 정답 자동 판정

## 참조 파일(Files Reference)

- `docs/index.html`, `docs/abilities.js`, `docs/dashboard.css`
- `scripts/build-ability-page-data.py`, `scripts/build-client-effects.py`
- `sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Network/NetworkClient.cs`
- `sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Network/Game/GameServerHandlers.cs`
- `sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Types/Aisling.cs`

명세 자기평가: **9/10**. 클라우드 외부 443 인바운드는 Oracle VCN 설정 상태에 따라 수동 허용이 필요할 수 있다.
