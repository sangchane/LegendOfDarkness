---
파일: "_tcoord.txt"
아카이브: "national.dat"
줄수: 28
바이트: 1146
인코딩: cp949
---

# _tcoord.txt

맵 번호·한글명·영문명·월드맵 위치(x,y)·가로·세로. 팩의 맵 번호와 일치한다.

아카이브: [[아카이브/national|national.dat]]

## 내용 (앞 40줄 / 전체 28줄)

```
남뤼케시온 2007 South Rucesion -165,135 50 50
노비스 6700 Novis -122,100 70 70
동뤼케시온 2008 East Rucesion - 183,132 50 50
동밀레스 2001 East Mileth -65,71 100 100
동아벨 2012 East Abel -124,103 70 78
로톤 665 Roton -73,73 100 100
루어스 664 Loures - 71,83 100 100
뤼케시온 505 Rucesion -165,123 50 50
마인 666 Mine -71,72 100 100
밀레스 500 Mileth -63,78 100 100
북밀레스  2002 North Mileth -67,74 100 100
북아벨 2013 North Abel 120 101 70 78
서뤼케시온 2006 West Rucesion 163 125 50 50
서밀레스 2003 West Mileth -63,73 100 100
서아벨 2011 West Abel -98,108 70 78
수오미 503 Suomi - 74,71 100 100
아벨 502 Abel -104,100 70 78
오렌섬 6228 Oren island -120,102 133 200
우드랜드대기실 995 (통합) Woodland -102,88 30 30
우드랜드대기실 600 (동) Woodland -103,90 40 35
우드랜드대기실 700 (북) Woodland -136,105 40 35
우드랜드대기실 441 (서) Woodland -103,90 40 35
운디네 504 Undine -61,73 100 100
타고르 662 Tagor  - 69,69 100 100
피에트 501 Piet -141,105 70 70
호엔 663 Hoen   - 72,76 100 100
홀리루딘성 667 Holyrudin  -71,78 100 100
우드랜드대기실 995 102 88 30 30
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/national/national.dat <폴더> _tcoord
iconv -f CP949 -t UTF-8 <폴더>/…/_tcoord.txt
```
