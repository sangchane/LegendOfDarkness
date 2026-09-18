---
파일: "equip03.epf"
무엇: "장비창 조각"
크기: "150x294"
확인: "모양만 확인 — 쓰임은 미확정"
---
# equip03.epf

**장비창 조각** — 긴 세로 칸

- 그림: `docs/ui/original-451/equip03.png` (150x294)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat equip03 out.png 12 1 legend.pal
```
