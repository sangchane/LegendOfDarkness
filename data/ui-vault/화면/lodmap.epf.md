---
파일: "lodmap.epf"
무엇: "맵 불러오는 중"
크기: "304x76"
확인: "그림으로 확인"
---
# lodmap.epf

**맵 불러오는 중** — Loading Map

- 그림: `docs/ui/original-451/lodmap.png` (304x76)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat lodmap out.png 12 1 legend.pal
```
