---
파일: "palu.tbl"
아카이브: "khan.dat"
줄수: 9
바이트: 81
인코딩: cp949
---

# palu.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/khan|khan.dat]]

## 내용 (앞 40줄 / 전체 9줄)

```
333 1
340 2
365 5 -2
366 5 -2
367 5 -2
368 5 -2
367 6 -1
368 6 -1
372 7
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/khan/khan.dat <폴더> palu
iconv -f CP949 -t UTF-8 <폴더>/…/palu.tbl
```
