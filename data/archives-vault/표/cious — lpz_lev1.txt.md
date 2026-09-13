---
파일: "lpz_lev1.txt"
아카이브: "cious.dat"
줄수: 22
바이트: 328
인코딩: cp949
---

# lpz_lev1.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/cious|cious.dat]]

## 내용 (앞 40줄 / 전체 22줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 88 40
<ENDCONTROL>
<CONTROL>
	<NAME> "TITLE"
	<TYPE> 7
	<RECT> 25 1 63 12
	<IMAGE>
		"pz_level.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "LEVEL1"
	<TYPE> 7
	<RECT> 25 14 43 38
<ENDCONTROL>
<CONTROL>
	<NAME> "LEVEL2"
	<TYPE> 7
	<RECT> 45 14 63 38
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/cious/cious.dat <폴더> lpz_lev1
iconv -f CP949 -t UTF-8 <폴더>/…/lpz_lev1.txt
```
