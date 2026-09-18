---
파일: "statcon.epf"
무엇: "상태바 조각"
크기: "56x14"
확인: "그림으로 확인"
---
# statcon.epf

**상태바 조각** — stat001 과 함께 쓰는 작은 칸

- 그림: `docs/ui/original-451/statcon.png` (56x14)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat statcon out.png 12 1 legend.pal
```
