---
파일: "msgmid.epf"
무엇: "말풍선 가운데"
크기: "454x30"
확인: "그림으로 확인"
---
# msgmid.epf

**말풍선 가운데** — 세로로 늘어나는 가운데 조각

- 그림: `docs/ui/original-451/msgmid.png` (454x30)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat msgmid out.png 12 1 legend.pal
```
