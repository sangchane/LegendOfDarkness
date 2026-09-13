---
파일: "lbang.txt"
아카이브: "setoa.dat"
줄수: 29
바이트: 405
인코딩: cp949
---

# lbang.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 29줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 400 305
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 7 269 89 300
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "CANCEL"
	<TYPE> 3
	<RECT> 311 269 393 300
	<VALUE>
		8
<ENDCONTROL>
<CONTROL>
	<NAME> "Title"
	<TYPE> 7
	<RECT> 12 11 388 27
<ENDCONTROL>
<CONTROL>
	<NAME> "Text"
	<TYPE> 7
	<RECT> 6 31 393 265
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lbang
iconv -f CP949 -t UTF-8 <폴더>/…/lbang.txt
```
