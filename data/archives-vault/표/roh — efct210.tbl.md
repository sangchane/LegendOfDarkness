---
파일: "efct210.tbl"
아카이브: "roh.dat"
줄수: 1
바이트: 40
인코딩: latin-1
---

# efct210.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/roh|roh.dat]]

## 내용 (앞 40줄 / 전체 1줄)

```
þÿþÿþÿþÿþÿþÿþÿþÿþÿþÿþÿþÿþÿþÿþÿþÿþÿþÿÿÿÿÿ
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/roh/roh.dat <폴더> efct210
iconv -f LATIN-1 -t UTF-8 <폴더>/…/efct210.tbl
```
