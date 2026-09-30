# 원작 UI 재료

`stone.png` · `stone-dark.png`은 `docs/ui/mockups-451/assets/`의 추출 에셋을
바이트 그대로 복사했다. 밝은 돌은 원작 4.51 `msgsm.epf`, 어두운 돌은 `dlgback.epf`에서 왔다.
근거와 사용 규칙: `docs/original-ui-451.md`, 단일 자료 출처 `data/original-ui/451.json`.

`Greybox.cs`에서 Godot StyleBoxTexture로 반복하며, 기기 크기에 맞춰 그림 전체를 늘리지 않는다.
게임 창의 테두리·선택 탭·확정 버튼은 밝은 돌, 제목 띠는 어두운 돌을 쓴다.
로그인·생성은 외곽 돌 테두리와 투명 그림을 남기고 텍스트·입력은 단색이다.
생성창의 활성 만들기는 단색 채움, 외형 상단 탭은 밑줄로 구분한다.
본문·아이템 목록·입력칸은 단색, 터치 영역과 회전 배치는 기존 모바일 컨테이너를 유지한다.

## 투명 누끼 이미지

아래 PNG는 원작 그림을 입력으로 imagegen 배경 제거를 거쳐 만든 RGBA 에셋이다.
원작 PNG의 바이트 그대로 복사본이 아니며, 원본은 docs/ui/original-451/에 보존한다.

- `title-cutout.png`: `lod00.png`의 게임 타이틀·뒤 원형 심벌.
- `create-wizard-cutout.png`: `dlgcre00.png`의 마법사·지팡이·구슬.
- `create-circle-cutout.png`: `dlgcre02.png`의 첫 원형 문양. 사각 돌 모서리 제거.

모두 실제 알파 투명도를 확인했다. 생성창은 생성 전용 이미지 두 장을 사용한다.
로그인에는 title-cutout을 그대로 표시하며, 배경을 지우는 셰이더를 쓰지 않는다.
AtlasTexture 영역은 희미한 외곽 픽셀과 투명 여백을 제외해 모바일 크기에서 선명하게 표시한다.
