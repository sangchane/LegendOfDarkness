---
파일: "_tncoord.txt"
아카이브: "national.dat"
줄수: 2
바이트: 65
인코딩: cp949
---

# _tncoord.txt

_tcoord 와 같은 꼴. 대평원 계열 두 줄.

아카이브: [[아카이브/national|national.dat]]

## 내용 (앞 40줄 / 전체 2줄)

```
0 대평원 10009 Noam - 44,67 80 150
1 아슬론 10008 54 70 150 80
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/national/national.dat <폴더> _tncoord
iconv -f CP949 -t UTF-8 <폴더>/…/_tncoord.txt
```
