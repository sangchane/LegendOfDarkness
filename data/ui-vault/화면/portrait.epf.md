---
파일: "portrait.epf"
무엇: "초상 틀"
크기: "243x106"
확인: "모양만 확인 — 쓰임은 미확정"
---
# portrait.epf

**초상 틀** — 작은 그림 셋

- 그림: `docs/ui/original-451/portrait.png` (243x106)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat portrait out.png 12 1 legend.pal
```
