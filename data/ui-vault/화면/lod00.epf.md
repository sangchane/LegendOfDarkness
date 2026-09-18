---
파일: "lod00.epf"
무엇: "제목 화면"
크기: "640x480"
확인: "그림으로 확인"
---
# lod00.epf

**제목 화면** — DARKNESS 로고와 그림

- 그림: `docs/ui/original-451/lod00.png` (640x480)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat lod00 out.png 12 1 legend.pal
```
