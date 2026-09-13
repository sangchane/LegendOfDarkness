---
파일: "mpt0018.tbl"
아카이브: "seo.dat"
줄수: 1
바이트: 9
인코딩: cp949
---

# mpt0018.tbl

같은 꼴의 7.18 계보 색표.

아카이브: [[아카이브/seo|seo.dat]]

## 내용 (앞 40줄 / 전체 1줄)

```
206 219 2
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/seo/seo.dat <폴더> mpt0018
iconv -f CP949 -t UTF-8 <폴더>/…/mpt0018.tbl
```
