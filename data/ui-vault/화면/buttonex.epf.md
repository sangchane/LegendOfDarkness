---
파일: "buttonex.epf"
무엇: "단추 줄"
크기: "742x18"
확인: "모양만 확인 — 쓰임은 미확정"
---
# buttonex.epf

**단추 줄** — 가로로 늘어선 작은 단추들

- 그림: `docs/ui/original-451/buttonex.png` (742x18)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat buttonex out.png 12 1 legend.pal
```
