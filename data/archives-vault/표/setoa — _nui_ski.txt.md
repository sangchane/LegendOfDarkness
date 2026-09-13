---
파일: "_nui_ski.txt"
아카이브: "setoa.dat"
줄수: 27
바이트: 399
인코딩: cp949
---

# _nui_ski.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 27줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 211 43
	<IMAGE>
		"_nui_ski.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TILE"
	<TYPE> 7
	<RECT> 7 7 39 39
<ENDCONTROL>
<CONTROL>
	<NAME> "NAME"
	<TYPE> 7
	<RECT> 48 7 204 19
<ENDCONTROL>
<CONTROL>
	<NAME> "LEVEL"
	<TYPE> 7
	<RECT> 48 27 108 39
<ENDCONTROL>
<CONTROL>
	<NAME> "E_BTN"
	<TYPE> 7
	<RECT> 127 22 158 36
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui_ski
iconv -f CP949 -t UTF-8 <폴더>/…/_nui_ski.txt
```
