---
파일: "_nui_alb.txt"
아카이브: "setoa.dat"
줄수: 40
바이트: 600
인코딩: cp949
---

# _nui_alb.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 40줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 76 120
<ENDCONTROL>
<CONTROL>
	<NAME> "PIC"
	<TYPE> 7
	<RECT> 6 6 70 78
	<IMAGE>
		"_nui_alb.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "PICRECT"
	<TYPE> 7
	<RECT> 14 15 62 71
<ENDCONTROL>
<CONTROL>
	<NAME> "BBACK"
	<TYPE> 7
	<RECT> 8 84 69 104
	<IMAGE>
		"_nui_alc.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "SAVE"
	<TYPE> 7
	<RECT> 10 86 38 102
	<IMAGE>
		"_nui_ba.spf" 0
		"_nui_ba.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "REMOVE"
	<TYPE> 7
	<RECT> 39 86 67 102
	<IMAGE>
		"_nui_ba.spf" 2
		"_nui_ba.spf" 3
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui_alb
iconv -f CP949 -t UTF-8 <폴더>/…/_nui_alb.txt
```
