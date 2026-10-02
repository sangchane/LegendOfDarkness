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

2026-10-02 표의 줄을 모두 끝냈다.

결정(2026-10-02): 옛 `--hunt` 봇(`WorldView.Hunt.cs` 의 `HuntOnItsOwn`)은 합치지도 지우지도 않고 그대로 둔다.
`Flash.cs` 는 생성기(`scripts/`)가 바닥 줄·색을 미리 적어야 해서 남겼다.

## 밸런스 수치 — 코드에 박힌 숫자

2026-10-02 끝 — 무도가 기술 수치(마나·초·칸·체력 배율 등)는 모두 템플릿에 있다. `scripts/build-monk-skills.py` 가 쓰고 `MonkStrike` 는 읽기만 한다.

## 생성기 `scripts/`

| 무엇 | 지금 | 방향 |
|---|---|---|
| 폴더 | `lib/` `ops/` 까지 했다(2026-10-02). 나머지 생성기는 평평 | `gen/<도메인>/` 은 나중(가리키는 곳이 많다). `godot.sh`·`godot-dotnet/` 은 `mobile/client/dotnet` 링크가 가리켜 못 옮긴다 |
| `build-client-creatures` | `tools/pack-import/import.py` 를 `importlib` 으로 읽는다 | `tools/` 를 고칠 때 |
