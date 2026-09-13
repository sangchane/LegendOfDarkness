---
파일: "paper2.txt"
아카이브: "national.dat"
줄수: 1
바이트: 28
인코딩: cp949
---

# paper2.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/national|national.dat]]

## 내용 (앞 40줄 / 전체 1줄)

```
10008 150 80 56 12 463 243
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/national/national.dat <폴더> paper2
iconv -f CP949 -t UTF-8 <폴더>/…/paper2.txt
```
