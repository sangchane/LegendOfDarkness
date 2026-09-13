---
파일: "_npw.txt"
아카이브: "setoa.dat"
줄수: 43
바이트: 662
인코딩: cp949
---

# _npw.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 43줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 217 170
	<IMAGE>
		"_npw.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Name"
	<TYPE> 7
	<RECT> 91 37 181 53
<ENDCONTROL>
<CONTROL>
	<NAME> "Password"
	<TYPE> 7
	<RECT> 91 61 181 77
<ENDCONTROL>
<CONTROL>
	<NAME> "NewPassword"
	<TYPE> 7
	<RECT> 91 85 181 101
<ENDCONTROL>
<CONTROL>
	<NAME> "Confirm"
	<TYPE> 7
	<RECT> 91 109 181 125
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 7
	<RECT> 31 130 93 150
	<IMAGE>
		"menubtn.epf" 0
		"menubtn.epf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 7
	<RECT> 117 130 179 150
	<IMAGE>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _npw
iconv -f CP949 -t UTF-8 <폴더>/…/_npw.txt
```
