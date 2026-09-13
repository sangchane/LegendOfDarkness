---
파일: "lnpcd4.txt"
아카이브: "setoa.dat"
줄수: 34
바이트: 518
인코딩: cp949
---

# lnpcd4.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 34줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 426 102
<ENDCONTROL>
<CONTROL>
	<NAME> "Content"
	<TYPE> 7
	<RECT> 13 6 413 74
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn1"
	<TYPE> 3
	<RECT> 345 77 406 99
	<IMAGE>
		"_nbtn.spf" 3
		"_nbtn.spf" 4
		"_nbtn.spf" 5
<ENDCONTROL>
<CONTROL>
	<NAME> "Prolog"
	<TYPE> 7
	<RECT> 23 12 403 28
<ENDCONTROL>
<CONTROL>
	<NAME> "TextInput"
	<TYPE> 7
	<RECT> 23 30 403 50
<ENDCONTROL>
<CONTROL>
	<NAME> "Epilog"
	<TYPE> 7
	<RECT> 23 52 403 68
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lnpcd4
iconv -f CP949 -t UTF-8 <폴더>/…/lnpcd4.txt
```
