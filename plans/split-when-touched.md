# 고칠 때 나누기 — 큰 파일·섞인 폴더 정리 지도

2026-10-02 코드 리뷰(함수 하나에 기능 하나 · 폴더 구조 · 예외 처리 · 주석)에서 나온 것 가운데 **기능은 그대로이고 구조만 바꾸는 일**을 모았다.
~~고칠 때 하나씩~~ → **사용자 결정(2026-10-02): 지금 한다.** 큰 파일은 최근 14일 중 8~10일을 고치는 파일이라 미룰 이유가 없다.
순서: `GameScreen` → `WorldClient` → `WorldView` → `Main` → 기술 수치 빼기 → 나머지. 파일 하나 = 커밋 하나(동작 그대로). 나눈 뒤 이 표에서 그 줄을 지운다.

## 규칙
- 나누기 커밋은 동작을 바꾸지 않는다 — 시험이 그대로 통과해야 한다(`dotnet test mobile/tests/...`, 서버는 `tests/hades-characterization`).
- 처음에는 `partial class` 로 파일만 나눠도 된다. 공개 이름(`world.Pack` 같은 것)은 그대로 두어 화면 쪽이 안 바뀌게.
- Godot 스크립트를 옮기면 `.tscn` 의 `res://src/…` 경로와 `.cs.uid` 파일도 같이 옮긴다.
- 주석은 새로 쓰거나 고치는 것부터 한국어로. 변경 이력은 주석 말고 커밋·WORKLOG 에.

## 화면 `mobile/client/src`

| 파일 | 지금 | 나누는 경계 |
|---|---|---|
| `Flash.cs` | 이펙트 첫 사용 때 메인 스레드에서 `GetPixel` 전체 훑기(끊김) | 바닥 줄·색을 생성기가 `effects.txt` 에 미리 |

결정(2026-10-02): 옛 `--hunt` 봇(`WorldView.Hunt.cs` 의 `HuntOnItsOwn`)은 합치지도 지우지도 않고 그대로 둔다.
`Flash.cs` 는 생성기(`scripts/`)가 바닥 줄·색을 미리 적어야 해서 남겼다.

## 서버 포크 `sources/wren11/Dark-Ages-Private-Server` (우리 코드만)

| 파일 | 지금 | 나누는 경계 |
|---|---|---|
| `Types/Companions.cs` (880줄) | 짝 맺기·돌려보내기·레벨 맞춤·옷·주기·깨우기·알림, 정적 사전 6개 | `CompanionPairing` · `CompanionKit` · `CompanionStatus`, 사전은 상태 클래스 하나로 |
| `GameServerHandlers.cs` `FormatF2Handler`(100줄) | 검증·사기·팔기·메뉴 | `HandleBuy` · `HandleSell`. `Value / 1.6` 6곳 → `ShopPricing.Offer(item)` |
| `Network/ClientFormats/Undefined.cs:766-805` | 쓰는 패킷 `ClientFormatF2` 가 빈 껍데기 파일에 | `ClientFormatF2.cs` 로(`BulkTradeLine` 같이) |
| `Types/AbilityPresentationOverrides.cs` | 설정 로더가 `Types/` 에. 깨지면 로그 없이 기본값 | `Infrastructure/` 로, catch 에 경고 한 줄 |

## 밸런스 수치 — 코드에 박힌 숫자

| 어디 | 지금 | 방향 |
|---|---|---|
| 서버 기술 스크립트 `database/server/scripts/Skills/Monk/*.cs` | 2026-10-02: 한 방(`MonkStrike.Use`, 14개)의 배율은 템플릿 `AttackPercent`·`EndurancePercent` 로 옮겼다(시험 `MonkStrikeNumbersTests`). 남은 것: `Step`(허공답보)·`UseCross`·`UseVitality`·`UseWolf`·`UseStrengthAndEndurance`·`Afflict`·`Empower` 의 수치와 마나(`Spend`) | 필요할 때 같은 방식으로 템플릿 필드를 더한다 |
| `MonkStrike.cs:72` 등 | `damage / 4 * 3` 같은 이름 없는 식 | 이름 붙은 상수로(무엇을 줄이는지 주석) |

## 생성기 `scripts/`

| 무엇 | 지금 | 방향 |
|---|---|---|
| 공용 코드 | `ROOT` 78곳, `read` 18곳, `.tools/dotnet-9.0.317` 9곳, `run()` 5곳, git 포인터 4곳, `ensure_graphify_python` 3곳 복사 | `scripts/lib/`(밑줄 이름 모듈) — 고치는 스크립트부터 옮겨 쓴다. `importlib` 우회도 이걸로 없어진다 |
| `drops_of` | 4개 파일, 걸러내는 조건이 서로 다름 | 한 함수로, 조건을 인자로 |
| 큰 함수 | 100줄 넘는 것 17개(`build-server-pack-vault.py` `build` 284줄 …) | 읽기 / 계산 / 쓰기로 |
| 폴더 | 103개 평평 | `lib/` `ops/` 부터(가리키는 곳 166개라 `gen/<도메인>/` 은 나중) |

## 작은 것(고치는 김에)
- 엉뚱한 멤버에 붙은 설명: 서버 `Companions.cs:340`(+"1초마다"→0.5초).
- 레벨업 점수: 서버 갱신이 오기 전 같은 점수에 요청이 여러 번 갈 수 있다(서버가 남은 점수로 막는지 확인).
