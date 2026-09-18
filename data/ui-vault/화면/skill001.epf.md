---
파일: "skill001.epf"
무엇: "기술 아이콘판"
크기: "404x200"
확인: "그림으로 확인"
---
# skill001.epf

**기술 아이콘판** — 여러 칸

- 그림: `docs/ui/original-451/skill001.png` (404x200)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat skill001 out.png 12 1 legend.pal
```
