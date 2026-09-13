---
파일: "lpz_set.txt"
아카이브: "cious.dat"
줄수: 244
바이트: 3452
인코딩: cp949
---

# lpz_set.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/cious|cious.dat]]

## 내용 (앞 40줄 / 전체 244줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 312 236
	<IMAGE>
		"pz_sback.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TITLE"
	<TYPE> 7
	<RECT> 76 4 234 33
	<IMAGE>
		"pz_sltt.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "LEVEL1"
	<TYPE> 4
	<RECT> 12 49 96 69
	<IMAGE>
		"pz_sl1.spf" 0
		"pz_sl1.spf" 1
	<VALUE>
		0
<ENDCONTROL>
<CONTROL>
	<NAME> "LEVEL2"
	<TYPE> 4
	<RECT> 12 86 99 106
	<IMAGE>
		"pz_sl2.spf" 0
		"pz_sl2.spf" 1
	<VALUE>
		0
<ENDCONTROL>
<CONTROL>
	<NAME> "LEVEL3"
	<TYPE> 4
	<RECT> 12 123 99 143
	<IMAGE>
		"pz_sl3.spf" 0
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/cious/cious.dat <폴더> lpz_set
iconv -f CP949 -t UTF-8 <폴더>/…/lpz_set.txt
```
