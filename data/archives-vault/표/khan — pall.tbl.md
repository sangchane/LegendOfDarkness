---
파일: "pall.tbl"
아카이브: "khan.dat"
줄수: 5
바이트: 37
인코딩: cp949
---

# pall.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/khan|khan.dat]]

## 내용 (앞 40줄 / 전체 5줄)

```
018 1 
019 1 
020 1 
021 1 
233 2
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/khan/khan.dat <폴더> pall
iconv -f CP949 -t UTF-8 <폴더>/…/pall.tbl
```
