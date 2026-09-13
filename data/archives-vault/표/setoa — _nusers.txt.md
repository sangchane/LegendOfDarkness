---
파일: "_nusers.txt"
아카이브: "setoa.dat"
줄수: 60
바이트: 952
인코딩: cp949
---

# _nusers.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 60줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 459 303
	<IMAGE>
		"_nusers.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "UsersList"
	<TYPE> 7
	<RECT> 13 24 272 276
<ENDCONTROL>
<CONTROL>
	<NAME> "TotalNum"
	<TYPE> 7
	<RECT> 378 39 440 51
<ENDCONTROL>
<CONTROL>
	<NAME> "CountryNum"
	<TYPE> 7
	<RECT> 378 65 440 77
<ENDCONTROL>
<CONTROL>
	<NAME> "UsersList"
	<TYPE> 7
	<RECT> 13 24 273 276
<ENDCONTROL>
<CONTROL>
	<NAME> "Emoticon"
	<TYPE> 7
	<RECT> 296 14 307 25
	<IMAGE>
		"emot001.epf" 0
		"emot001.epf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "CountryBtn"
	<TYPE> 7
	<RECT> 296 62 362 78
	<IMAGE>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nusers
iconv -f CP949 -t UTF-8 <폴더>/…/_nusers.txt
```
