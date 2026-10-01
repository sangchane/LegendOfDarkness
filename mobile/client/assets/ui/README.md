# 원작 UI 재료

`stone.png` · `stone-dark.png`은 `docs/ui/mockups-451/assets/`의 추출 에셋을
바이트 그대로 복사했다. 밝은 돌은 원작 4.51 `msgsm.epf`, 어두운 돌은 `dlgback.epf`에서 왔다.
근거와 사용 규칙: `docs/original-ui-451.md`, 단일 자료 출처 `data/original-ui/451.json`.

`Greybox.cs`에서 Godot StyleBoxTexture로 반복하며, 기기 크기에 맞춰 그림 전체를 늘리지 않는다.
게임 창의 테두리·선택 탭·확정 버튼은 밝은 돌, 제목 띠는 어두운 돌을 쓴다.
로그인·생성은 외곽 돌 테두리와 투명 그림을 남기고 텍스트·입력은 단색이다.
생성창의 활성 만들기는 단색 채움, 외형 상단 탭은 밑줄로 구분한다.
본문·아이템 목록·입력칸은 단색, 터치 영역과 회전 배치는 기존 모바일 컨테이너를 유지한다.

## 통째로 쓰는 원작 그림

- `equip-panel.png`: `docs/ui/original-451/equip01.png` 를 (22,0)부터 오른쪽·아래 끝까지 자른 것(270×302).
  왼쪽 쇠고리 바깥만 뺐고(위 이름·칭호 줄은 사용자가 되살림 2026-10-01), 왼쪽 위 바탕 귀퉁이(남색·그림자)는 투명으로 바꿨다.
  칸 좌표는 이 자른 그림 기준으로 `GearLayout.cs` 에 있다. 늘리지 않고 정수배로만 보인다.

- `close.png`·`close-pressed.png`: `butt001.png` 의 Close 단추(보통·누름), 68×20.
- `equip-group.png`: `equip05.png` 그대로 — 사람 하나/둘(그룹 받기 꺼짐/켜짐), 34×27 네 칸.
- `pack-floor.png`: `panel02.png` 안쪽 바닥의 고른 띠 (30,32)~(420,56), 390×24 — 소지품 칸 뒤에 되풀이해 깐다.

## 투명 누끼 이미지

아래 PNG는 원작 그림을 입력으로 imagegen 배경 제거를 거쳐 만든 RGBA 에셋이다.
원작 PNG의 바이트 그대로 복사본이 아니며, 원본은 docs/ui/original-451/에 보존한다.

- `title-cutout.png`: `lod00.png`의 게임 타이틀·뒤 원형 심벌.
- `create-wizard-cutout.png`: `dlgcre00.png`의 마법사·지팡이·구슬.
- `create-circle-cutout.png`: `dlgcre02.png`의 첫 원형 문양. 사각 돌 모서리 제거.

모두 실제 알파 투명도를 확인했다. 생성창은 생성 전용 이미지 두 장을 사용한다.
로그인에는 title-cutout을 그대로 표시하며, 배경을 지우는 셰이더를 쓰지 않는다.
AtlasTexture 영역은 희미한 외곽 픽셀과 투명 여백을 제외해 모바일 크기에서 선명하게 표시한다.
