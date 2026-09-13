---
파일: "palc.tbl"
아카이브: "khan.dat"
줄수: 9
바이트: 58
인코딩: cp949
---

# palc.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/khan|khan.dat]]

## 내용 (앞 40줄 / 전체 9줄)

```
3 4 1
15 18 2
34 3
61 4
66 5
67 5
68 5
103 6
135 7
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/khan/khan.dat <폴더> palc
iconv -f CP949 -t UTF-8 <폴더>/…/palc.tbl
```
