---
파일: "panel02.epf"
무엇: "판 띠"
크기: "446x111"
확인: "모양만 확인 — 쓰임은 미확정"
---
# panel02.epf

**판 띠** — 가로로 긴 어두운 돌 띠

- 그림: `docs/ui/original-451/panel02.png` (446x111)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat panel02 out.png 12 1 legend.pal
```
