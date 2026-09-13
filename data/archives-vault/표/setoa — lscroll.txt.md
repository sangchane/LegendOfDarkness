---
파일: "lscroll.txt"
아카이브: "setoa.dat"
줄수: 7
바이트: 101
인코딩: cp949
---

# lscroll.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 7줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 16 16
	<IMAGE>
		"scroll.epf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lscroll
iconv -f CP949 -t UTF-8 <폴더>/…/lscroll.txt
```
