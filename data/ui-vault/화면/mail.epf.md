---
파일: "mail.epf"
무엇: "우편 아이콘"
크기: "44x14"
확인: "그림으로 확인"
---
# mail.epf

**우편 아이콘** — 봉투와 빨간 점

- 그림: `docs/ui/original-451/mail.png` (44x14)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat mail out.png 12 1 legend.pal
```
