---
파일: "option03.epf"
무엇: "Exit Game 단추"
크기: "96x20"
확인: "그림으로 확인"
---
# option03.epf

**Exit Game 단추** — 단추 하나

- 그림: `docs/ui/original-451/option03.png` (96x20)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat option03 out.png 12 1 legend.pal
```
