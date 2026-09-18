---
파일: "spelled.epf"
무엇: "마법 칸 줄"
크기: "22x230"
확인: "그림으로 확인"
---
# spelled.epf

**마법 칸 줄** — 세로로 늘어선 빈 칸

- 그림: `docs/ui/original-451/spelled.png` (22x230)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat spelled out.png 12 1 legend.pal
```
