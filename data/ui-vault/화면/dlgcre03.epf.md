---
파일: "dlgcre03.epf"
무엇: "캐릭터 만들기(다른 판)"
크기: "127x15"
확인: "그림으로 확인"
---
# dlgcre03.epf

**캐릭터 만들기(다른 판)** — dlgcre00 과 같은 짜임

- 그림: `docs/ui/original-451/dlgcre03.png` (127x15)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat dlgcre03 out.png 12 1 legend.pal
```
