---
파일: "woodbk.epf"
무엇: "나무 바탕"
크기: "360x180"
확인: "그림으로 확인"
---
# woodbk.epf

**나무 바탕** — 이어 붙이는 나무 무늬 — 창고·가방 바탕

- 그림: `docs/ui/original-451/woodbk.png` (360x180)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 시안이 이 그림을 쓴다

- [[재질/나무|나무]] — 쓰지 않기로 했다

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat woodbk out.png 12 1 legend.pal
```
