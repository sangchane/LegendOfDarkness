---
파일: "dlgpass.epf"
무엇: "암호 바꾸기"
크기: "260x164"
확인: "그림으로 확인"
---
# dlgpass.epf

**암호 바꾸기** — NAME · PASSWORD · New PASSWORD · CONFIRM · OK · CANCEL

- 그림: `docs/ui/original-451/dlgpass.png` (260x164)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat dlgpass out.png 12 1 legend.pal
```
