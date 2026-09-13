---
파일: "_nsett.txt"
아카이브: "setoa.dat"
줄수: 51
바이트: 762
인코딩: cp949
---

# _nsett.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 51줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 447 284
	<IMAGE>
		"_nsett.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TopText"
	<TYPE> 7
	<RECT> 40 42 210 54
	<COLOR>
		20
		24
<ENDCONTROL>
<CONTROL>
	<NAME> "BottomText"
	<TYPE> 7
	<RECT> 40 63 210 75
<ENDCONTROL>
<CONTROL>
	<NAME> "TopButton"
	<TYPE> 7
	<RECT> 15 40 31 56
	<IMAGE>
		"_nsettb.spf" 0
		"_nsettb.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "RightText"
	<TYPE> 7
	<RECT> 251 42 421 54
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 7
	<RECT> 13 256 74 278
	<IMAGE>
		"_nbtn.spf" 6
		"_nbtn.spf" 7
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nsett
iconv -f CP949 -t UTF-8 <폴더>/…/_nsett.txt
```
