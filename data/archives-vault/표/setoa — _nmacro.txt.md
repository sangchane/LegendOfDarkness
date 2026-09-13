---
파일: "_nmacro.txt"
아카이브: "setoa.dat"
줄수: 39
바이트: 567
인코딩: cp949
---

# _nmacro.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 39줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 447 284
	<IMAGE>
		"_nmacro.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TextTop"
	<TYPE> 7
	<RECT> 40 40 425 56
	<COLOR>
		18
<ENDCONTROL>
<CONTROL>
	<NAME> "TextBottom"
	<TYPE> 7
	<RECT> 40 61 425 77
	<COLOR>
		18
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 7
	<RECT> 13 256 74 278
	<IMAGE>
		"_nbtn.spf" 6
		"_nbtn.spf" 7
		"_nbtn.spf" 8
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 7
	<RECT> 369 256 430 278
	<IMAGE>
		"_nbtn.spf" 3
		"_nbtn.spf" 4
		"_nbtn.spf" 5
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nmacro
iconv -f CP949 -t UTF-8 <폴더>/…/_nmacro.txt
```
