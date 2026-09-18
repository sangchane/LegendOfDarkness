---
파일: "sysmsg.epf"
무엇: "시스템 말 판"
크기: "443x123"
확인: "그림으로 확인"
---
# sysmsg.epf

**시스템 말 판** — 어두운 돌 띠

- 그림: `docs/ui/original-451/sysmsg.png` (443x123)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat sysmsg out.png 12 1 legend.pal
```
