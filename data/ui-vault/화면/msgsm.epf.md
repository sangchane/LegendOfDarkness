---
파일: "msgsm.epf"
무엇: "작은 돌판"
크기: "96x112"
확인: "그림으로 확인"
---
# msgsm.epf

**작은 돌판** — 글자 없는 밝은 돌 — 시안의 밝은 돌 재료

- 그림: `docs/ui/original-451/msgsm.png` (96x112)
- 확인: 그림으로 확인
- 아카이브: [[아카이브/Legend.dat|Legend.dat]]

## 시안이 이 그림을 쓴다

- [[재질/밝은돌|밝은돌]] — 확정 단추, 공격 버튼, 고른 탭

## 다시 그리는 법

```bash
DOTNET_ROLL_FORWARD=Major .tools/dotnet-9.0.317/dotnet tools/dat-extract/bin/Release/net8.0/dat-extract.dll epf Legend.dat msgsm out.png 12 1 legend.pal
```
