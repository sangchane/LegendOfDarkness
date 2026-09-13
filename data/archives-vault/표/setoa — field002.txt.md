---
파일: "field002.txt"
아카이브: "setoa.dat"
줄수: 0
바이트: 0
인코딩: cp949
---

# field002.txt

원작 월드맵 한 장. 첫 줄이 팔레트, 그 뒤로 `이름 그림키 x y [EX x2 y2 번호]`.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 0줄)

```

```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> field002
iconv -f CP949 -t UTF-8 <폴더>/…/field002.txt
```
