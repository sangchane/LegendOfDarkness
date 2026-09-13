---
파일: "_nlogin.txt"
아카이브: "setoa.dat"
줄수: 33
바이트: 505
인코딩: cp949
---

# _nlogin.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 33줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 217 170
	<IMAGE>
		"_nlogin.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Name"
	<TYPE> 7
	<RECT> 65 57 153 73
<ENDCONTROL>
<CONTROL>
	<NAME> "Password"
	<TYPE> 7
	<RECT> 65 99 153 115
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 7
	<RECT> 28 130 90 150
	<IMAGE>
		"menubtn.epf" 0
		"menubtn.epf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 7
	<RECT> 116 130 178 150
	<IMAGE>
		"menubtn.epf" 2
		"menubtn.epf" 3
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nlogin
iconv -f CP949 -t UTF-8 <폴더>/…/_nlogin.txt
```
