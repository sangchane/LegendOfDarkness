---
파일: "mpt0056.tbl"
아카이브: "seo.dat"
줄수: 3
바이트: 31
인코딩: cp949
---

# mpt0056.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/seo|seo.dat]]

## 내용 (앞 40줄 / 전체 3줄)

```
176 184 2
192 207 2
208 216 2
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/seo/seo.dat <폴더> mpt0056
iconv -f CP949 -t UTF-8 <폴더>/…/mpt0056.tbl
```
