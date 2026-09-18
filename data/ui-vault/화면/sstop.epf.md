---
파일: "sstop.epf"
무엇: "판 조각"
크기: "254x80"
확인: "모양만 확인 — 쓰임은 미확정"
---
# sstop.epf

**판 조각** — 가로로 긴 조각

- 그림: `docs/ui/original-451/sstop.png` (254x80)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat sstop out.png 12 1 legend.pal
```
