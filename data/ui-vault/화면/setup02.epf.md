---
파일: "setup02.epf"
무엇: "설정창"
크기: "676x350"
확인: "그림으로 확인"
---
# setup02.epf

**설정창** — 왼쪽 목록 + 오른쪽 내용

- 그림: `docs/ui/original-451/setup02.png` (676x350)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat setup02 out.png 12 1 legend.pal
```
