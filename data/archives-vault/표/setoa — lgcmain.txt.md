---
파일: "lgcmain.txt"
아카이브: "setoa.dat"
줄수: 40
바이트: 605
인코딩: cp949
---

# lgcmain.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 40줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 220 400
<ENDCONTROL>
<CONTROL>
	<NAME> "DLGFRAME"
	<TYPE> 7
	<RECT> 0 0 175 390
	<IMAGE>
		"gc_dlg0.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB0"
	<TYPE> 7
	<RECT> 167 24 217 53
	<IMAGE>
		"gc_tab00.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB1"
	<TYPE> 7
	<RECT> 167 52 217 81
	<IMAGE>
		"gc_tab10.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB0_S"
	<TYPE> 7
	<RECT> 175 28 204 51
	<IMAGE>
		"gc_tab01.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB1_S"
	<TYPE> 7
	<RECT> 175 53 204 76
	<IMAGE>
		"gc_tab11.spf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lgcmain
iconv -f CP949 -t UTF-8 <폴더>/…/lgcmain.txt
```
