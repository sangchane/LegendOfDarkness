---
파일: "dlgback.epf"
무엇: "어두운 돌 바탕"
크기: "64x64"
확인: "그림으로 확인"
---
# dlgback.epf

**어두운 돌 바탕** — 64x64 이어 붙이는 어두운 돌 — 시안의 어두운 돌 재료

- 그림: `docs/ui/original-451/dlgback.png` (64x64)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 시안이 이 그림을 쓴다

- [[재질/어두운돌|어두운돌]] — 창 바깥 틀, 제목줄

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat dlgback out.png 12 1 legend.pal
```
