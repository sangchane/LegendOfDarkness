---
파일: "legends.epf"
무엇: "직업 아이콘 여덟"
크기: "188x20"
확인: "그림으로 확인"
---
# legends.epf

**직업 아이콘 여덟** — 색색의 작은 그림

- 그림: `docs/ui/original-451/legends.png` (188x20)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat legends out.png 12 1 legend.pal
```
