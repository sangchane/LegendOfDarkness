---
파일: "lshop1.txt"
아카이브: "setoa.dat"
줄수: 35
바이트: 462
인코딩: cp949
---

# lshop1.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 35줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 214 114
	<IMAGE>
		"shop01.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Edit"
	<TYPE> 7
	<RECT> 61 46 151 62
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 129 80 211 111
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 3
	<RECT> 3 80 85 111
	<VALUE>
		8
<ENDCONTROL>
<CONTROL>
	<NAME> "Text"
	<TYPE> 7
	<RECT> 10 23 202 35
	<COLOR>
		20
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lshop1
iconv -f CP949 -t UTF-8 <폴더>/…/lshop1.txt
```
