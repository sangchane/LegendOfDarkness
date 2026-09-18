---
파일: "menuok.epf"
무엇: "OK 단추"
크기: "89x16"
확인: "그림으로 확인"
---
# menuok.epf

**OK 단추** — 3상태

- 그림: `docs/ui/original-451/menuok.png` (89x16)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat menuok out.png 12 1 legend.pal
```
