---
파일: "lpasswd.txt"
아카이브: "setoa.dat"
줄수: 43
바이트: 664
인코딩: cp949
---

# lpasswd.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 43줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 175 176
	<IMAGE>
		"dlgpass.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Name"
	<TYPE> 7
	<RECT> 70 44 160 60
<ENDCONTROL>
<CONTROL>
	<NAME> "Password"
	<TYPE> 7
	<RECT> 70 68 160 84
<ENDCONTROL>
<CONTROL>
	<NAME> "NewPassword"
	<TYPE> 7
	<RECT> 70 92 160 108
<ENDCONTROL>
<CONTROL>
	<NAME> "Confirm"
	<TYPE> 7
	<RECT> 70 116 160 132
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 7
	<RECT> 12 139 74 159
	<IMAGE>
		"menubtn.epf" 0
		"menubtn.epf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 7
	<RECT> 97 139 159 159
	<IMAGE>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lpasswd
iconv -f CP949 -t UTF-8 <폴더>/…/lpasswd.txt
```
