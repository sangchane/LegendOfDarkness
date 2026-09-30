# Godot 모바일 게임 UI

Godot 4 Control 노드로 폰 게임 UI를 만들 때 읽는다. 웹용 규칙(SKILL.md 3절의 Tailwind·React)은 적용하지 않고, dial과 "AI 티 안 나게"의 취지만 가져온다.
투박해지는 원인은 둘이다. 결과를 보지 않고 노드를 쓰는 것(4절 스크린샷으로 막는다), 그리고 틀린 곳만 없고 **게임다움**이 없는 것(1-2절로 막는다).
2절 하드룰만 지키면 "틀리지 않은" 화면이 될 뿐이다. 1-2절까지 해야 게임 화면이 된다.

## 1. 방향 먼저 (화면을 만들기 전에 한 번)

장르와 분위기(캐주얼·하드코어·귀여움·다크 판타지 등)를 한 줄로 정하고, 그에 맞춰 아래를 **Theme 리소스 하나**(`res://ui/theme.tres`)로 만든다.
프로젝트 설정 `gui/theme/custom`에 지정해 모든 Control이 따르게 한다.

- 색 5개: 배경 · 표면(패널) · 강조(주요 버튼) · 위험/경고 · 글자. 강조색은 한 화면에 한 곳.
- 폰트 1~2개: 한글이 들어가면 한글 글리프가 있는 폰트(Pretendard, Noto Sans KR 등)를 `res://`에 넣고 Theme 기본 폰트로. 숫자(점수·재화)는 굵게.
- 간격 단위 8px(기준 해상도 기준). 여백·간격은 8의 배수만.
- 버튼 StyleBoxFlat: 모서리 반경, 눌림(pressed)·비활성(disabled) 상태를 따로. 기본 회색 버튼을 그대로 두지 않는다.

노드마다 `theme_override_*`로 꾸미지 않는다. 한 곳을 바꾸면 전체가 따라와야 톤이 맞는다.

## 1-2. 게임다움 (하드룰보다 먼저 정하고, 스크린샷마다 확인)

- **참조 두 개**: 장르의 잘 만든 모바일 게임 두 개를 떠올려 한 줄씩 적는다(예: "캐주얼 퍼즐 — 두꺼운 외곽선 글자, 말랑한 둥근 버튼, 밝은 그라데이션").
  색·폰트·형태(둥근/각진/손그림)를 여기서 정한다. 사용자가 참조를 주면 그걸 쓴다.
- **색은 시안으로 고르게 한다**: 참조도 기존 아트도 없으면 색을 혼자 정하지 않는다. 레이아웃을 먼저 잡고, 색만 바꾼 시안 2~3개를 같은 화면으로 찍어
  한 장에 나란히 보여 주고 고르게 한다(확인 1회). 색은 취향이라 규칙으로 맞히기 어렵다.
- **색은 한 곳에만**: 팔레트를 Theme과 `res://ui/palette.gd` 상수 한 곳에 두고, 씬의 `theme_override_colors`나 그림 스크립트에 색을 직접 쓰지 않는다. 그래야 시안 교체가 파일 하나로 끝난다.
- **광고 게임 색 피하기**: 채도 최대의 보라·파랑 배경 + 형광 초록·노랑 버튼 조합은 모바일 광고 게임의 전형이라 싸 보인다.
  배경은 채도를 낮추고, 채도 높은 색은 강조 한 곳(주요 버튼)에만. 위계는 채도가 아니라 명도 대비로 만든다. 보조 버튼(닫기·설정)은 강조색을 쓰지 않는다.
- **자원**: 프로젝트에 있는 아트를 먼저 쓴다. 없으면 무료 자원을 `res://assets/ui/`에 받고 출처·라이선스를 `res://assets/LICENSES.md`에 적는다.
  - 버튼·패널·아이콘: Kenney(CC0, 표기 의무 없음) — UI Pack(버튼·패널·별·체크), Game Icons 등. kenney.nl/assets
  - 제목 폰트(한글): Google Fonts(OFL) — 주아(Jua)·검은고딕(Black Han Sans)·도현(Do Hyeon) 등. `github.com/google/fonts/tree/main/ofl/<이름>`
  - 코드로 그린 사각형과 기본 폰트만으로 끝내지 않는다.
- **배경은 단색 금지**: `GradientTexture2D` 세로 그라데이션 + 가장자리 어둡게(비네트), 또는 은은한 무늬나 게임 장면을 흐리게. 느린 배경 파티클(`CPUParticles2D`) 하나면 화면이 산다.
- **주인공 요소**: 화면 가운데에 시선을 잡는 것(로고, 캐릭터, 대표 오브젝트)을 둔다. 가운데가 비어 있으면 미완성처럼 보인다.
- **아이콘이 글자보다 먼저**: "코인 1234"가 아니라 코인 아이콘 + `1,234`. 설정·닫기·일시정지도 아이콘.
- **글자에 두께**: 제목·점수·주요 버튼 글자는 디스플레이 폰트 + `outline_size` 8~12(진한 테두리색) + 그림자. 본문은 읽기 좋은 폰트 그대로.
- **깊이**: 버튼은 아래쪽 두께(Kenney 버튼의 depth 이미지나 StyleBox 아래 테두리)와 그림자, 패널은 테두리 + 그림자. 평면 사각형만 쌓지 않는다.
- **살아 있음**: 대기 중에도 움직인다. 주요 버튼 1.0↔1.04 느린 펄스, 로고 위아래 몇 px 둥실, 재화 변경 시 숫자 튀기. 전부 은은하게(과하면 싸 보인다).
- **스크린샷 자문**: "앱스토어 상위 게임 옆에 놓아도 어색하지 않은가? 가장 싸 보이는 곳 한 곳은?"을 매번 묻고 그 한 곳을 고친다.

## 2. 하드룰

- **늘이기 설정**: `display/window/stretch/mode = canvas_items`, `aspect = expand`. 세로 게임이면 기준 해상도 720×1280(가로 게임은 1280×720)과 `display/window/handheld/orientation`을 맞춘다.
  expand여야 20:9 폰과 4:3 태블릿에서 검은 띠 없이 UI가 넓어진다.
- **배치는 컨테이너와 앵커로**: 좌표를 직접 넣은 `layout_mode = 0` + offset 배치를 쓰지 않는다. 화면 비율이 바뀌면 어긋난다.
  화면 가장자리 UI는 앵커 프리셋, 묶음은 VBox/HBox/Grid/MarginContainer.
- **노치·홈 바(안전 영역)**: 화면 가장자리에 붙는 UI는 전부 아래 SafeArea를 붙인 MarginContainer 안에 둔다. 배경 그림만 가장자리까지 채운다.
- **터치 크기**: 누르는 것은 최소 48dp. 기준 폭 720이면 1dp ≈ 2px이므로 `custom_minimum_size` 96×96 이상. 버튼끼리는 16px 이상 띄운다.
- **글자 크기**(기준 폭 720): 본문 32px 이상, 보조 26px 이상, 제목·점수 44px 이상. 폰에서 이보다 작으면 읽히지 않는다.
- **엄지 영역**: 자주 누르는 버튼(공격·확인·다음)은 화면 아래 1/3. 위쪽은 정보(재화·점수·일시정지).
- **반응감**: 누르면 반드시 반응한다. 눌림 크기 0.95 → 1.0 Tween(0.08초), 효과음, 중요한 동작은 `Input.vibrate_handheld(20)`. 화면 전환은 0.15~0.25초 페이드나 슬라이드.

```gdscript
# res://ui/safe_area.gd — 가장자리 UI를 감싸는 MarginContainer에 붙여 노치·홈 바만큼 여백을 준다.
extends MarginContainer

func _ready() -> void:
	_apply()
	get_viewport().size_changed.connect(_apply)

func _apply() -> void:
	var screen := Vector2(DisplayServer.screen_get_size())
	var safe := Rect2(DisplayServer.get_display_safe_area())
	if Engine.has_meta("safe_area_preview"):  # dev:ui 스크린샷 스크립트가 넣는 가짜 노치
		var p: Dictionary = Engine.get_meta("safe_area_preview")
		screen = p.screen
		safe = Rect2(0, p.top, screen.x, screen.y - p.top - p.bottom)
	var k := get_viewport_rect().size / screen  # 화면 픽셀 → UI 좌표
	add_theme_constant_override("margin_left", int(safe.position.x * k.x))
	add_theme_constant_override("margin_top", int(safe.position.y * k.y))
	add_theme_constant_override("margin_right", int((screen.x - safe.end.x) * k.x))
	add_theme_constant_override("margin_bottom", int((screen.y - safe.end.y) * k.y))
```

## 3. 흔한 투박함과 고치는 법

| 보이는 것 | 원인 | 고침 |
|---|---|---|
| 회색 기본 버튼, 기본 폰트 | Theme 없음 | 1절 Theme |
| 글자가 작고 흐림 | 기준 해상도 대비 폰트 크기 부족 | 2절 글자 크기 |
| 태블릿에서 UI가 한쪽에 몰림 | 좌표 배치, aspect keep | 컨테이너·앵커, aspect expand |
| 상단 재화가 노치에 가림 | 안전 영역 무시 | SafeArea |
| 누르는 게 헷갈림 | 상태별 스타일·반응 없음 | pressed 스타일, Tween, 소리 |
| 정보가 한 줄로 늘어섬 | 위계 없음 | 크기·굵기·색 3단계, 강조 한 곳 |
| "광고 게임 같다" | 채도 최대 배경 + 형광 강조, 강조색 여러 곳 | 배경 채도 낮추기, 강조 한 곳, 색 시안 고르기 |
| "중학생이 만든 것 같다" | 단색 배경·기본 폰트·아이콘 없음·빈 가운데·정지 화면 | 1-2절 전부 |

## 4. 화면 확인 (고칠 때마다)

스킬 폴더의 `scripts/godot_shots.gd`로 폰 두 종과 태블릿 한 종을 찍어 Read로 본다. 명령은 SKILL.md "Godot 모바일" 절에 있다.

- 빨간 띠(노치·홈 바) 아래에 글자나 버튼이 있으면 SafeArea가 빠졌거나 예전 버전이다(스크립트가 넣는 `safe_area_preview` 메타를 읽는 위 코드여야 한다).
- 세 장을 나란히 보고 겹침, 잘림, 한쪽 쏠림, 너무 작은 글자·버튼을 찾는다.
- 고치고 다시 찍는다. 세 번 돌려도 같은 문제가 남으면 사용자에게 화면을 보여 주고 방향을 묻는다.
- 스크립트는 `--script`로 돌아서 autoload를 직접 붙인다. 게임 진행 상태가 필요한 화면(결과창 등)은 그 상태를 만드는 테스트용 씬을 따로 두고 `scene=`으로 찍는다.
- 창이 뜨는 환경이 필요하다(`--headless`는 그리지 않는다). 실제 기기의 노치 위치는 기종마다 다르므로 마지막 확인은 실기기로 한다.
