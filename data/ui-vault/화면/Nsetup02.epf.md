---
파일: "Nsetup02.epf"
무엇: "접속 설정"
크기: "335x295"
확인: "그림으로 확인"
---
# Nsetup02.epf

**접속 설정** — Port · Speed · Tel 1~4 · OK · Cancel

- 그림: `docs/ui/original-451/Nsetup02.png` (335x295)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat Nsetup02 out.png 12 1 legend.pal
```
