---
파일: "_ngcmain.txt"
아카이브: "setoa.dat"
줄수: 40
바이트: 606
인코딩: cp949
---

# _ngcmain.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 40줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
<ENDCONTROL>
<CONTROL>
	<NAME> "DLGFRAME"
	<TYPE> 7
	<RECT> 0 0 223 390
	<IMAGE>
		"_ngcdlg0.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB0"
	<TYPE> 7
	<RECT> 190 32 262 59
	<IMAGE>
		"_ngcbtnb.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB1"
	<TYPE> 7
	<RECT> 190 59 262 86
	<IMAGE>
		"_ngcbtnb.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB0_S"
	<TYPE> 7
	<RECT> 190 35 259 55
	<IMAGE>
		"_ngcbtns.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "TAB1_S"
	<TYPE> 7
	<RECT> 190 62 259 82
	<IMAGE>
		"_ngcbtns.spf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _ngcmain
iconv -f CP949 -t UTF-8 <폴더>/…/_ngcmain.txt
```
