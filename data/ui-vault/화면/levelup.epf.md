---
파일: "levelup.epf"
무엇: "레벨 올랐음"
크기: "52x18"
확인: "모양만 확인 — 쓰임은 미확정"
---
# levelup.epf

**레벨 올랐음** — 작은 아이콘 셋

- 그림: `docs/ui/original-451/levelup.png` (52x18)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat levelup out.png 12 1 legend.pal
```
