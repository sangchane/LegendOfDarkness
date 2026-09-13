---
파일: "lnpcnid.txt"
아카이브: "setoa.dat"
줄수: 58
바이트: 891
인코딩: cp949
---

# lnpcnid.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 58줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 426 132
<ENDCONTROL>
<CONTROL>
	<NAME> "Content"
	<TYPE> 7
	<RECT> 13 6 413 104
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn1"
	<TYPE> 3
	<RECT> 345 107 406 129
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
	<NAME> "Id"
	<TYPE> 7
	<RECT> 95 33 395 53
<ENDCONTROL>
<CONTROL>
	<NAME> "IdLabel"
	<TYPE> 7
	<RECT> 27 39 91 55
<ENDCONTROL>
<CONTROL>
	<NAME> "PasswordLabel"
	<TYPE> 7
	<RECT> 27 61 91 77
<ENDCONTROL>
<CONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lnpcnid
iconv -f CP949 -t UTF-8 <폴더>/…/lnpcnid.txt
```
