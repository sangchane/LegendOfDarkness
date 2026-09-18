---
파일: "gbicon02.epf"
무엇: "길드 아이콘"
크기: "74x23"
확인: "그림으로 확인"
---
# gbicon02.epf

**길드 아이콘** — 작은 그림 셋

- 그림: `docs/ui/original-451/gbicon02.png` (74x23)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat gbicon02 out.png 12 1 legend.pal
```
