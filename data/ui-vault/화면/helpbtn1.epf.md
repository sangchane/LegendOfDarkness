---
파일: "helpbtn1.epf"
무엇: "도움말 단추"
크기: "54x25"
확인: "그림으로 확인"
---
# helpbtn1.epf

**도움말 단추** — (?) 단추 둘

- 그림: `docs/ui/original-451/helpbtn1.png` (54x25)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat helpbtn1 out.png 12 1 legend.pal
```
