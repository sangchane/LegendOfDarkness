---
파일: "lgcdlg1.txt"
아카이브: "setoa.dat"
줄수: 96
바이트: 1514
인코딩: cp949
---

# lgcdlg1.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 96줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 175 390
	<IMAGE>
		"gc_dlg1.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TITLE"
	<TYPE> 7
	<RECT> 44 47 158 63
	<COLOR>
		18
<ENDCONTROL>
<CONTROL>
	<NAME> "N_TOTAL_O"
	<TYPE> 7
	<RECT> 61 76 79 88
<ENDCONTROL>
<CONTROL>
	<NAME> "N_TOTAL_W"
	<TYPE> 7
	<RECT> 113 76 131 88
<ENDCONTROL>
<CONTROL>
	<NAME> "N_LEVEL_MIN"
	<TYPE> 7
	<RECT> 59 101 77 117
<ENDCONTROL>
<CONTROL>
	<NAME> "N_LEVEL_MAX"
	<TYPE> 7
	<RECT> 111 101 129 117
<ENDCONTROL>
<CONTROL>
	<NAME> "EXTRA"
	<TYPE> 7
	<RECT> 12 142 160 190
	<COLOR>
		18
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lgcdlg1
iconv -f CP949 -t UTF-8 <폴더>/…/lgcdlg1.txt
```
