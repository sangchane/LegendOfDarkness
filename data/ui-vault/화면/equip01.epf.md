---
파일: "equip01.epf"
무엇: "장비창(구형)"
크기: "292x308"
확인: "그림으로 확인"
---
# equip01.epf

**장비창(구형)** — 돌판에 장비 칸과 종이인형. AC·DMG·HIT 와 Next Lev

- 그림: `docs/ui/original-451/equip01.png` (292x308)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat equip01 out.png 12 1 legend.pal
```
