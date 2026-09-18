---
파일: "dlgbbs01.epf"
무엇: "게시판"
크기: "616x308"
확인: "그림으로 확인"
---
# dlgbbs01.epf

**게시판** — 제목줄과 본문 칸

- 그림: `docs/ui/original-451/dlgbbs01.png` (616x308)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat dlgbbs01 out.png 12 1 legend.pal
```
