# mobile/ — 스코프 작업 지침

이 폴더의 파일을 다룰 때만 로드된다(온디맨드). 공통 행동규칙은 루트 `CLAUDE.md`,
프로젝트 표준·네비게이션은 `AGENTS.md`, 다음-할일은 `NEXT.md`를 따른다.

## 목표
모바일 클라이언트 — 알맹이 `src/Lod.Mobile.Core`(엔진 없이 시험) · 화면 `client`(Godot 4.6 + C#) · 봇 `bots`.

## 소유 경로
`mobile/src` `mobile/client` `mobile/bots` `mobile/tests`. 서버(`sources/wren11/...`)는 이 폴더 밖이다.

## 핵심 관례
- **큰 파일을 고칠 땐 먼저 `plans/split-when-touched.md`** — 그 파일을 어떻게 나눌지 적어 둔 지도. 고치는 작업 앞에 해당 줄 하나만 나눈다(나누기 커밋 따로).
- **모바일 클라이언트 — 빌드·실행·인자·함정: `docs/mobile-client.md`** (클라이언트를 만지면 여기부터)
- 원작 스프라이트 방향·프레임 구간: `docs/original-sprite-animation.md`
- 무도가 1~10 기술 모션·이펙트·사운드 근거와 구현 계약: `docs/martial-artist-skill-presentation.md`
- 화면 배치(세로·가로): `docs/mobile-test-v1-wireframes.md` · 눌러볼 화면: `docs/index.html` (그림은 `docs/ui/assets/`)
- 화면을 만들거나 고치면 UI 테마 규칙표 `docs/original-ui-451.md` 부터(`docs/CLAUDE.md`).

### 함정 — 시간 버린 것들

- **고도는 `scripts/godot.sh` 로 연다.** 시스템 `/Applications/Godot.app` 은 **C# 이 없는 판**이라 화면이
  비어 나오고, 그냥 열면 `dotnet` 을 못 찾아 ".NET SDK 를 설치하라" 고 한다.
- **`--screen game` 은 로그인을 건너뛴다.** 서버에 안 붙고 자리표(체력 99999)만 보여 준다 —
  **그걸로 찍은 화면은 확인이 아니다.** `--login <계정>:<암호>` 만 주고 찍는다. 시험 계정은 `nov`·`monk`·`watch`,
  암호는 모두 `1234`.
- **헤드리스로는 화면을 못 찍는다**(그릴 것이 없어 `GetImage()` 가 빈 값이다). 창을 띄워 찍는다.
- **캐릭터 파일에는 템플릿이 통째로 복사돼 있다.** 기술·아이템 템플릿만 고치면 이미 배운 것에는 반영되지
  않는다 — `aislings/*.json` 안의 사본도 함께 고친다. 고치기 전에 **서버를 멈춘다**(안 그러면 덮어쓴다).

- **[남음/되돌릴 것]** `mobile/client/export_presets.cfg` 의 `application/app_store_team_id` 는 `""` 로
  되돌려 두었다. 아이패드 빌드를 다시 구우려면 `HQC44HA87V` 를 넣고, **끝나면 다시 비운다**.
  `mobile/client/{server,login,hunt}.cfg` 는 커밋되지 않는다(`.gitignore`).

## 검증
- `dotnet test mobile/tests/Lod.Mobile.Core.Tests/Lod.Mobile.Core.Tests.csproj` (알맹이, 몇 초)
- 서버와 붙는 시험: `dotnet test tests/hades-characterization/Hades.Characterization.Tests.csproj`
- 앱 설치: `scripts/ios-build.sh install`
