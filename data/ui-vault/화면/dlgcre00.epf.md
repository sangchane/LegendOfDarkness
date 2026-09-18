---
파일: "dlgcre00.epf"
무엇: "캐릭터 만들기"
크기: "640x480"
확인: "그림으로 확인"
---
# dlgcre00.epf

**캐릭터 만들기** — 인물 그림·머리·색 고르기와 능력치 칸

- 그림: `docs/ui/original-451/dlgcre00.png` (640x480)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat dlgcre00 out.png 12 1 legend.pal
```
