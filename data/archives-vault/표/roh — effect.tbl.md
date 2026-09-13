---
파일: "effect.tbl"
아카이브: "roh.dat"
줄수: 395
바이트: 6871
인코딩: cp949
---

# effect.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/roh|roh.dat]]

## 내용 (앞 40줄 / 전체 395줄)

```
394
0 1 2 3 
0 1 
0 1 2 3 4 5 
0 1 2 3 4 5 6 7 
0 1 2 3 4 
0 1 2 3 4 5 6 
0 1 2 3 4 
0 1 2 3 4 5 
0 1 2 
0 1 2 3 
0 1 2 3 4 
0 1 2 3 4 5 
0 1 2 1 2 3 
0 1 2 3 
0 1 2 1 2 3 
0 1 2 1 2 
0 1 2 1 2 
0 1 2 3 4 
0 1 2 
0 1 2 3 4 5 6 7 
0 1 0 1 
0 1 
0 1 2 1 2 
0 1 2 3 4 5 6 7 
0 1 2 3 
0 1 2 
0 1 2 3 
0 1 2 3 
0 1 2 3 
0 1 2 3 4 
0 1 2 3 4 
0 1 2 3 
0 1 2 3 
0 1 2 3 4 
0 1 2 3 4 5 6 
0 1 2 3 4 0 1 2 3 4 
0 1 2 3 4 5 
0 1 2 3 4 5 
0 1 2 3 4 5 
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/roh/roh.dat <폴더> effect
iconv -f CP949 -t UTF-8 <폴더>/…/effect.tbl
```
