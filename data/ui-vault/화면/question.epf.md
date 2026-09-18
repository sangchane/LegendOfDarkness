---
파일: "question.epf"
무엇: "(?) 아이콘"
크기: "46x21"
확인: "그림으로 확인"
---
# question.epf

**(?) 아이콘** — 물음표 단추 둘

- 그림: `docs/ui/original-451/question.png` (46x21)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat question out.png 12 1 legend.pal
```
