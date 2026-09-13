---
파일: "paper1.txt"
아카이브: "national.dat"
줄수: 1
바이트: 29
인코딩: cp949
---

# paper1.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/national|national.dat]]

## 내용 (앞 40줄 / 전체 1줄)

```
10009 80 150 42 0 519 243
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/national/national.dat <폴더> paper1
iconv -f CP949 -t UTF-8 <폴더>/…/paper1.txt
```
