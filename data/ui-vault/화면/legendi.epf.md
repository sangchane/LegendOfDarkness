---
파일: "legendi.epf"
무엇: "아이콘 둘"
크기: "72x27"
확인: "모양만 확인 — 쓰임은 미확정"
---
# legendi.epf

**아이콘 둘** — 작은 그림

- 그림: `docs/ui/original-451/legendi.png` (72x27)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat legendi out.png 12 1 legend.pal
```
