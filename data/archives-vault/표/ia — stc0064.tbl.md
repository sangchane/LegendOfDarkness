---
파일: "stc0064.tbl"
아카이브: "ia.dat"
줄수: 2
바이트: 24
인코딩: cp949
---

# stc0064.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/ia|ia.dat]]

## 내용 (앞 40줄 / 전체 2줄)

```
224 229 2
230 235 2
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/ia/ia.dat <폴더> stc0064
iconv -f CP949 -t UTF-8 <폴더>/…/stc0064.tbl
```
