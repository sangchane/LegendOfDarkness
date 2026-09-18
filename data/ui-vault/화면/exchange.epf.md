---
파일: "exchange.epf"
무엇: "교환창"
크기: "410x308"
확인: "그림으로 확인"
---
# exchange.epf

**교환창** — 두 사람 몫의 칸이 나란히, 아래에 손 그림 둘

- 그림: `docs/ui/original-451/exchange.png` (410x308)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat exchange out.png 12 1 legend.pal
```
