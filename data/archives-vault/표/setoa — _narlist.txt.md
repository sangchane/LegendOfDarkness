---
파일: "_narlist.txt"
아카이브: "setoa.dat"
줄수: 75
바이트: 1171
인코딩: cp949
---

# _narlist.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 75줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 581 290
	<IMAGE>
		"_nmaill.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ArticleList"
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
	<NAME> "New"
	<TYPE> 7
	<RECT> 507 87 568 109
	<IMAGE>
		"_nbtn.spf" 33
		"_nbtn.spf" 34
		"_nbtn.spf" 35
<ENDCONTROL>
<CONTROL>
	<NAME> "Delete"
	<TYPE> 7
	<RECT> 507 113 568 135
	<IMAGE>
		"_nbtn.spf" 9
		"_nbtn.spf" 10
		"_nbtn.spf" 11
<ENDCONTROL>
<CONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _narlist
iconv -f CP949 -t UTF-8 <폴더>/…/_narlist.txt
```
