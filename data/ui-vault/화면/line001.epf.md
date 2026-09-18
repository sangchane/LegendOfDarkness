---
파일: "line001.epf"
무엇: "틀 조각 묶음"
크기: "236x156"
확인: "모양만 확인 — 쓰임은 미확정"
---
# line001.epf

**틀 조각 묶음** — 색색의 모서리·선 조각 — 창 틀을 조립하는 데 쓴 듯하나 확인 못 했다

- 그림: `docs/ui/original-451/line001.png` (236x156)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat line001 out.png 12 1 legend.pal
```
