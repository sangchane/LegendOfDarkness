---
파일: "scroll.epf"
무엇: "굴림대"
크기: "92x12"
확인: "모양만 확인 — 쓰임은 미확정"
---
# scroll.epf

**굴림대** — 작은 가로 조각

- 그림: `docs/ui/original-451/scroll.png` (92x12)
- 확인: 모양만 확인 — 쓰임은 미확정
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat scroll out.png 12 1 legend.pal
```
