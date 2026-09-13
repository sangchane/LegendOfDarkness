---
파일: "lmanu.txt"
아카이브: "setoa.dat"
줄수: 79
바이트: 1077
인코딩: cp949
---

# lmanu.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 79줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 288 259
	<IMAGE>
		"manufac.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 203 225 285 256
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "CANCEL"
	<TYPE> 3
	<RECT> 3 225 85 256
	<VALUE>
		8
<ENDCONTROL>
<CONTROL>
	<NAME> "PREV"
	<TYPE> 3
	<RECT> 5 100 14 114
	<IMAGE>
		"manuprev.spf" 0
		"manuprev.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "NEXT"
	<TYPE> 3
	<RECT> 60 100 69 114
	<IMAGE>
		"manunext.spf" 0
		"manunext.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "NUM"
	<TYPE> 5
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lmanu
iconv -f CP949 -t UTF-8 <폴더>/…/lmanu.txt
```
