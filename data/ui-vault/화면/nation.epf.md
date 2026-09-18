---
파일: "nation.epf"
무엇: "나라 문장 일곱"
크기: "446x51"
확인: "그림으로 확인"
---
# nation.epf

**나라 문장 일곱** — 방패 모양 색색의 문장

- 그림: `docs/ui/original-451/nation.png` (446x51)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat nation out.png 12 1 legend.pal
```
