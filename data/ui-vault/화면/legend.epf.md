---
파일: "legend.epf"
무엇: "작은 띠"
크기: "4785x418"
확인: "모양만 확인 — 쓰임은 미확정"
---
# legend.epf

**작은 띠** — 가로로 긴 조각 — 무엇에 쓰는지 확인 못 했다

- 그림: `docs/ui/original-451/legend.png` (4785x418)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat legend out.png 12 1 legend.pal
```
