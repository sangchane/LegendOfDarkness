---
파일: "test0.txt"
아카이브: "national.dat"
줄수: 2
바이트: 19
인코딩: cp949
---

# test0.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/national|national.dat]]

## 내용 (앞 40줄 / 전체 2줄)

```
Type S
lod00.pcx
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/national/national.dat <폴더> test0
iconv -f CP949 -t UTF-8 <폴더>/…/test0.txt
```
