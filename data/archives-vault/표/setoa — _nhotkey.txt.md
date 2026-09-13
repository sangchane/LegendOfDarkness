---
파일: "_nhotkey.txt"
아카이브: "setoa.dat"
줄수: 112
바이트: 1655
인코딩: cp949
---

# _nhotkey.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 112줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 536 172
	<IMAGE>
		"_nhk_m.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "C00"
	<TYPE> 7
	<RECT> 62 87 229 114
	<IMAGE>
		"_nhkk00.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "C01"
	<TYPE> 7
	<RECT> 54 59 221 86
	<IMAGE>
		"_nhkk01.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "C021"
	<TYPE> 7
	<RECT> 75 115 186 142
	<IMAGE>
		"_nhkk021.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "C022"
	<TYPE> 7
	<RECT> 444 115 527 170
	<IMAGE>
		"_nhkk022.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "C03"
	<TYPE> 7
	<RECT> 342 59 430 114
	<IMAGE>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nhotkey
iconv -f CP949 -t UTF-8 <폴더>/…/_nhotkey.txt
```
