---
파일: "_nui_fm.txt"
아카이브: "setoa.dat"
줄수: 67
바이트: 1036
인코딩: cp949
---

# _nui_fm.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 67줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 599 306
	<IMAGE>
		"_nui_fm.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Self"
	<TYPE> 7
	<RECT> 266 125 338 137
<ENDCONTROL>
<CONTROL>
	<NAME> "Family"
	<TYPE> 7
	<RECT> 385 125 457 137
<ENDCONTROL>
<CONTROL>
	<NAME> "Text0"
	<TYPE> 7
	<RECT> 212 63 284 79
<ENDCONTROL>
<CONTROL>
	<NAME> "Text1"
	<TYPE> 7
	<RECT> 318 63 390 79
<ENDCONTROL>
<CONTROL>
	<NAME> "Text2"
	<TYPE> 7
	<RECT> 147 123 219 139
<ENDCONTROL>
<CONTROL>
	<NAME> "Text3"
	<TYPE> 7
	<RECT> 147 149 219 165
<ENDCONTROL>
<CONTROL>
	<NAME> "Text4"
	<TYPE> 7
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui_fm
iconv -f CP949 -t UTF-8 <폴더>/…/_nui_fm.txt
```
