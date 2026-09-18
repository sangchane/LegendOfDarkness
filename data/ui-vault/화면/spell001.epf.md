---
파일: "spell001.epf"
무엇: "마법 아이콘판"
크기: "404x404"
확인: "그림으로 확인"
---
# spell001.epf

**마법 아이콘판** — 여러 칸

- 그림: `docs/ui/original-451/spell001.png` (404x404)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat spell001 out.png 12 1 legend.pal
```
