---
파일: "lgcdlg0.txt"
아카이브: "setoa.dat"
줄수: 35
바이트: 544
인코딩: cp949
---

# lgcdlg0.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 35줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 175 390
	<IMAGE>
		"gc_dlg0.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "BTN_OK"
	<TYPE> 7
	<RECT> 61 351 113 377
	<IMAGE>
		"gc_gbtn3.spf" 0
		"gc_gbtn3.spf" 1
		"gc_gbtn3.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "USER0"
	<TYPE> 7
	<RECT> 19 54 91 66
<ENDCONTROL>
<CONTROL>
	<NAME> "B_BTN0"
	<TYPE> 7
	<RECT> 136 50 166 70
	<IMAGE>
		"gc_btnu.spf" 0
		"gc_btnu.spf" 1
		"gc_btnu.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "USER1"
	<TYPE> 7
	<RECT> 19 76 91 88
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lgcdlg0
iconv -f CP949 -t UTF-8 <폴더>/…/lgcdlg0.txt
```
