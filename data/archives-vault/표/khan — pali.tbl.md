---
파일: "pali.tbl"
아카이브: "khan.dat"
줄수: 19
바이트: 177
인코딩: cp949
---

# pali.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/khan|khan.dat]]

## 내용 (앞 40줄 / 전체 19줄)

```
002 2 -2
003 2 -2
004 2 -2
005 3 -2
006 3 -2
008 4
009 4
010 4
011 4
015 6 -2
019 7 -2
020 7 -2
021 7 -2
022 7 -2
021 8 -1
022 8 -1
035 9
037 10
040 11
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/khan/khan.dat <폴더> pali
iconv -f CP949 -t UTF-8 <폴더>/…/pali.tbl
```
