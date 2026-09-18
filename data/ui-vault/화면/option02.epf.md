---
파일: "option02.epf"
무엇: "설정 단추 줄"
크기: "428x20"
확인: "그림으로 확인"
---
# option02.epf

**설정 단추 줄** — 가로로 늘어선 단추들

- 그림: `docs/ui/original-451/option02.png` (428x20)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat option02 out.png 12 1 legend.pal
```
