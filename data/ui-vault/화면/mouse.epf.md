---
파일: "mouse.epf"
무엇: "마우스 커서"
크기: "52x21"
확인: "그림으로 확인"
---
# mouse.epf

**마우스 커서** — 화살표와 모래시계

- 그림: `docs/ui/original-451/mouse.png` (52x21)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat mouse out.png 12 1 legend.pal
```
