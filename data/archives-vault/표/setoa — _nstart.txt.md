---
파일: "_nstart.txt"
아카이브: "setoa.dat"
줄수: 73
바이트: 1132
인코딩: cp949
---

# _nstart.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 73줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"_nstart.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Create"
	<TYPE> 7
	<RECT> 68 265 153 300
	<IMAGE>
		"_nsb1.spf" 0
		"_nsb1.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Continue"
	<TYPE> 7
	<RECT> 60 302 167 337
	<IMAGE>
		"_nsb2.spf" 0
		"_nsb2.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Password"
	<TYPE> 7
	<RECT> 71 335 190 372
	<IMAGE>
		"_nsb3.spf" 0
		"_nsb3.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Credit"
	<TYPE> 7
	<RECT> 485 265 568 300
	<IMAGE>
		"_nsb4.spf" 0
		"_nsb4.spf" 1
<ENDCONTROL>
<CONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nstart
iconv -f CP949 -t UTF-8 <폴더>/…/_nstart.txt
```
