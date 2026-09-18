---
파일: "menubtn.epf"
무엇: "메뉴 단추 줄"
크기: "602x21"
확인: "그림으로 확인"
---
# menubtn.epf

**메뉴 단추 줄** — 가로로 늘어선 글자 단추들

- 그림: `docs/ui/original-451/menubtn.png` (602x21)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat menubtn out.png 12 1 legend.pal
```
