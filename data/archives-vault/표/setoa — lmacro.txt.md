---
파일: "lmacro.txt"
아카이브: "setoa.dat"
줄수: 34
바이트: 466
인코딩: cp949
---

# lmacro.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 34줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 450 302
	<IMAGE>
		"macro01.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 349 258 431 289
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 3
	<RECT> 15 258 97 289
	<VALUE>
		8
<ENDCONTROL>
<CONTROL>
	<NAME> "TextTop"
	<TYPE> 7
	<RECT> 40 40 425 56
	<COLOR>
		20
		255
<ENDCONTROL>
<CONTROL>
	<NAME> "TextBottom"
	<TYPE> 7
	<RECT> 40 61 425 77
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lmacro
iconv -f CP949 -t UTF-8 <폴더>/…/lmacro.txt
```
