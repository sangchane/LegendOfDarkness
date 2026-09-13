---
파일: "_nbdlist.txt"
아카이브: "setoa.dat"
줄수: 30
바이트: 461
인코딩: cp949
---

# _nbdlist.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 30줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 581 290
	<IMAGE>
		"_nmaill.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "BoardList"
	<TYPE> 7
	<RECT> 19 18 499 273
<ENDCONTROL>
<CONTROL>
	<NAME> "View"
	<TYPE> 7
	<RECT> 507 35 568 57
	<IMAGE>
		"_nbtn.spf" 15
		"_nbtn.spf" 16
		"_nbtn.spf" 17
<ENDCONTROL>
<CONTROL>
	<NAME> "Quit"
	<TYPE> 7
	<RECT> 507 245 568 267
	<IMAGE>
		"_nbtn.spf" 0
		"_nbtn.spf" 1
		"_nbtn.spf" 2
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nbdlist
iconv -f CP949 -t UTF-8 <폴더>/…/_nbdlist.txt
```
