---
파일: "_ngcdlg0.txt"
아카이브: "setoa.dat"
줄수: 35
바이트: 550
인코딩: cp949
---

# _ngcdlg0.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 35줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 223 390
	<IMAGE>
		"_ngcdlg0.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "B_BTN0"
	<TYPE> 7
	<RECT> 156 42 186 62
	<IMAGE>
		"_ngcbtn1.spf" 0
		"_ngcbtn1.spf" 1
		"_ngcbtn1.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "USER0"
	<TYPE> 7
	<RECT> 53 47 125 59
<ENDCONTROL>
<CONTROL>
	<NAME> "USER1"
	<TYPE> 7
	<RECT> 53 69 125 81
<ENDCONTROL>
<CONTROL>
	<NAME> "BTN_OK"
	<TYPE> 7
	<RECT> 89 338 142 360
	<IMAGE>
		"_ngcbtn.spf" 12
		"_ngcbtn.spf" 13
		"_ngcbtn.spf" 14
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _ngcdlg0
iconv -f CP949 -t UTF-8 <폴더>/…/_ngcdlg0.txt
```
