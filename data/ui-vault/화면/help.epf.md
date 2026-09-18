---
파일: "help.epf"
무엇: "도움말 판"
크기: "2056x418"
확인: "모양만 확인 — 쓰임은 미확정"
---
# help.epf

**도움말 판** — 여러 줄이 든 돌판

- 그림: `docs/ui/original-451/help.png` (2056x418)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat help out.png 12 1 legend.pal
```
