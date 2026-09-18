---
파일: "Nsetup05.epf"
무엇: "설정 메뉴(다른 판)"
크기: "172x308"
확인: "그림으로 확인"
---
# Nsetup05.epf

**설정 메뉴(다른 판)** — option01 과 같은 짜임

- 그림: `docs/ui/original-451/Nsetup05.png` (172x308)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat Nsetup05 out.png 12 1 legend.pal
```
