---
파일: "svrdlg.epf"
무엇: "서버 고르기"
크기: "181x181"
확인: "그림으로 확인"
---
# svrdlg.epf

**서버 고르기** — SERVER 제목과 목록

- 그림: `docs/ui/original-451/svrdlg.png` (181x181)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat svrdlg out.png 12 1 legend.pal
```
