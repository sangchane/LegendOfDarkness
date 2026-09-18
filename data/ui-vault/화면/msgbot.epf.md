---
파일: "msgbot.epf"
무엇: "말풍선 아래"
크기: "454x63"
확인: "그림으로 확인"
---
# msgbot.epf

**말풍선 아래** — 세 조각 중 아래

- 그림: `docs/ui/original-451/msgbot.png` (454x63)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat msgbot out.png 12 1 legend.pal
```
