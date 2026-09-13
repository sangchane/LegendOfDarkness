---
파일: "lfamily.txt"
아카이브: "setoa.dat"
줄수: 59
바이트: 887
인코딩: cp949
---

# lfamily.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 59줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 513 213
	<IMAGE>
		"family.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 418 11 500 42
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Self"
	<TYPE> 7
	<RECT> 203 118 311 130
<ENDCONTROL>
<CONTROL>
	<NAME> "Family"
	<TYPE> 7
	<RECT> 345 118 453 130
<ENDCONTROL>
<CONTROL>
	<NAME> "Text0"
	<TYPE> 7
	<RECT> 130 69 238 85
<ENDCONTROL>
<CONTROL>
	<NAME> "Text1"
	<TYPE> 7
	<RECT> 273 68 381 84
<ENDCONTROL>
<CONTROL>
	<NAME> "Text2"
	<TYPE> 7
	<RECT> 60 116 168 132
<ENDCONTROL>
<CONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lfamily
iconv -f CP949 -t UTF-8 <폴더>/…/lfamily.txt
```
