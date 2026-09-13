---
파일: "_ngcdlg1.txt"
아카이브: "setoa.dat"
줄수: 97
바이트: 1580
인코딩: cp949
---

# _ngcdlg1.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 97줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 223 390
	<IMAGE>
		"_ngcdlg1.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "N_TOTAL_O"
	<TYPE> 7
	<RECT> 96 64 114 76
<ENDCONTROL>
<CONTROL>
	<NAME> "N_TOTAL_W"
	<TYPE> 7
	<RECT> 148 64 166 76
<ENDCONTROL>
<CONTROL>
	<NAME> "N_LEVEL_MIN"
	<TYPE> 7
	<RECT> 96 89 114 105
<ENDCONTROL>
<CONTROL>
	<NAME> "N_LEVEL_MAX"
	<TYPE> 7
	<RECT> 148 89 166 105
<ENDCONTROL>
<CONTROL>
	<NAME> "EXTRA"
	<TYPE> 7
	<RECT> 38 130 186 178
<ENDCONTROL>
<CONTROL>
	<NAME> "TITLE"
	<TYPE> 7
	<RECT> 82 35 184 51
<ENDCONTROL>
<CONTROL>
	<NAME> "N_CLASS0_O"
	<TYPE> 7
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _ngcdlg1
iconv -f CP949 -t UTF-8 <폴더>/…/_ngcdlg1.txt
```
