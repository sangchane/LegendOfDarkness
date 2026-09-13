---
파일: "_nui.txt"
아카이브: "setoa.dat"
줄수: 68
바이트: 1071
인코딩: cp949
---

# _nui.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 68줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
<ENDCONTROL>
<CONTROL>
	<NAME> "CONTENT"
	<TYPE> 7
	<RECT> 1 2 600 308
	<IMAGE>
		"_nui_eq.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB_INTRO"
	<TYPE> 7
	<RECT> 576 23 638 47
	<IMAGE>
		"_nui_tb2.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB_INTRO_E"
	<TYPE> 7
	<RECT> 590 24 629 40
	<IMAGE>
		"_nui_tb1.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB_LEGEND"
	<TYPE> 7
	<RECT> 576 46 638 70
	<IMAGE>
		"_nui_tb2.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB_SKILL"
	<TYPE> 7
	<RECT> 576 69 638 93
	<IMAGE>
		"_nui_tb2.spf" 2
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui
iconv -f CP949 -t UTF-8 <폴더>/…/_nui.txt
```
