---
파일: "equip05.epf"
무엇: "사람 아이콘 넷"
크기: "148x27"
확인: "그림으로 확인"
---
# equip05.epf

**사람 아이콘 넷** — 남·여 따위를 고르는 작은 그림

- 그림: `docs/ui/original-451/equip05.png` (148x27)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat equip05 out.png 12 1 legend.pal
```
