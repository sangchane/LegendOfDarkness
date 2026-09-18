---
파일: "option04.epf"
무엇: "작은 조각"
크기: "9x9"
확인: "모양만 확인 — 쓰임은 미확정"
---
# option04.epf

**작은 조각** — 설정창에 붙는 작은 그림

- 그림: `docs/ui/original-451/option04.png` (9x9)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat option04 out.png 12 1 legend.pal
```
