---
파일: "staff.epf"
무엇: "운영자 그림"
크기: "640x480"
확인: "그림으로 확인"
---
# staff.epf

**운영자 그림** — 색이 화려한 그림 한 장

- 그림: `docs/ui/original-451/staff.png` (640x480)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat staff out.png 12 1 legend.pal
```
