# LOD 클라이언트 greybox

승인된 와이어프레임(`docs/mobile-test-v1-wireframes.md` v0.3)의 화면을 무채색으로만 세워 배치·도달성·글자를
판단하기 위한 프로젝트다. 색상·아트·최종 UX는 범위 밖이다.

## 왜 800×360인가

기준 뷰포트가 곧 논리 단위(dp)다. 요즘 폰의 가로 논리 크기가 대략 800×360dp이므로 이 값을 쓰면 코드의
`48`이 화면에서 실제 48dp가 된다. 1280×720처럼 잡으면 같은 숫자가 기기에서 3분의 1 크기로 줄어 손가락
크기 판단이 틀어진다.

## 한글 글꼴

Godot 기본 글꼴에는 한글이 없다. 지금은 윈도우의 맑은 고딕을 불러오고, 실제로 쓰인 글꼴 이름을 화면 아래에
표시한다. **안드로이드와 iOS에는 그 경로가 없으므로** 라이선스가 되는 한글 글꼴을 프로젝트에 넣어야 하며,
그 전까지는 화면이 "글꼴 없음 — 한글이 깨집니다"로 알려준다.

## 실행

```powershell
$env:DOTNET_ROOT = '<저장소>\.tools\dotnet-9.0.317'
& "$env:DOTNET_ROOT\dotnet.exe" build mobile\client\LodClient.csproj -c Debug
& '<저장소>\.tools\godot-4.6-mono\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64_console.exe' --path mobile\client
```

C# 코드를 고쳤으면 **다시 빌드해야 한다.** Godot는 프로젝트를 직접 실행할 때 C#을 다시 컴파일하지 않는다.

## 화면 없이 결과 보기

첫 프레임을 PNG로 저장하고 종료한다. 에디터나 기기 없이 확인할 수 있고, 나중에 화면 회귀 검사에도 쓸 수 있다.

```powershell
... --path mobile\client -- --shot out.png
```
