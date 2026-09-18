---
파일: "dlglogin.epf"
무엇: "로그인 창"
크기: "116x158"
확인: "그림으로 확인"
---
# dlglogin.epf

**로그인 창** — NAME · PASSWORD · OK · CANCEL

- 그림: `docs/ui/original-451/dlglogin.png` (116x158)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat dlglogin out.png 12 1 legend.pal
```
