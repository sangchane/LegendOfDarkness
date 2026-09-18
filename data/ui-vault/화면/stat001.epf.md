---
파일: "stat001.epf"
무엇: "아래 상태바"
크기: "444x108"
확인: "그림으로 확인"
---
# stat001.epf

**아래 상태바** — STR·INT·WIS·CON·DEX / HP·MP·EXP·GOLD·LEV / 무기·갑옷 칸

- 그림: `docs/ui/original-451/stat001.png` (444x108)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat stat001 out.png 12 1 legend.pal
```
