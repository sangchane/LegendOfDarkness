# 장비창 위치·종이인형 오류 TDD 증거

- 사용자 여정: 장비창을 열었을 때 세로 화면에서는 상단 HUD 아래 전폭으로, 가로 화면에서는 상단 HUD 아래 오른쪽 열로 나타나며 Godot 오류 없이 사용할 수 있어야 한다.
- RED: `scripts/check-layout.ps1`가 Godot 런타임 오류를 실패로 판정하도록 보강한 뒤 실행했다. 장비창을 연 6개 화면 크기 모두 `Condition "p_name.is_empty()" is true`로 실패했다.
- 리뷰 RED: 가짜 Godot 실행기로 검사기를 검증하자 무출력, 종료 코드 7, `SCRIPT ERROR:` 세 경우를 모두 성공으로 오판했다(5개 중 3개 실패).
- 원인: `GearGrid.ShowDoll`이 빈 이름으로 `Actor`를 만들고, `Actor`가 그 값을 Godot `Node.Name`에 대입했다.
- GREEN: 종이인형의 내부 노드 이름을 `PaperDoll`로 지정했다. 휴대용 .NET SDK 9.0.317 빌드는 경고·오류 0, 레이아웃 검사는 12개 조합 모두 통과했다.

| 보장 | 검사 | 종류 | 결과 |
|---|---|---|---|
| 세로 360×640, 360×780, 360×800에서 닫힘/열림 레이아웃이 화면 경계를 벗어나지 않는다 | `scripts/check-layout.ps1` | 통합 | PASS |
| 가로 640×360, 800×360, 840×360에서 닫힘/열림 레이아웃이 화면 경계를 벗어나지 않는다 | `scripts/check-layout.ps1` | 통합 | PASS |
| 장비창을 열어도 무시되지 않은 Godot 런타임 오류가 없다 | `scripts/check-layout.ps1` | 회귀 | PASS |
| 완료 표식 누락, 비정상 종료, 스크립트 오류를 성공으로 오판하지 않는다 | `node --test tests/check-layout-script.test.js` | 단위 | 5 PASS |
| 기존 모바일 코어 동작이 유지된다 | `dotnet test mobile/tests/Lod.Mobile.Core.Tests/Lod.Mobile.Core.Tests.csproj --no-restore` | 단위·통합 | 114 PASS |

## 커버리지와 남은 사각지대

코어 테스트의 XPlat Coverage는 line 61.05%, branch 54.04%로 기존 프로젝트 목표 80%에 못 미친다. 이번 변경은 Godot 클라이언트 UI라 코어 커버리지 대상에 포함되지 않으며, 실제 실행 기반 회귀 검사로 검증했다. 현재 검사는 패널 경계와 런타임 오류를 검증하지만 장비 링을 끝까지 손가락으로 스크롤하는 입력 흐름까지 자동화하지는 않는다.
