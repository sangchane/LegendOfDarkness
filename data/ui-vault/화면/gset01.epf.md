---
파일: "gset01.epf"
무엇: "길드 설정"
크기: "454x308"
확인: "그림으로 확인"
---
# gset01.epf

**길드 설정** — 여러 줄이 든 돌판

- 그림: `docs/ui/original-451/gset01.png` (454x308)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat gset01 out.png 12 1 legend.pal
```
