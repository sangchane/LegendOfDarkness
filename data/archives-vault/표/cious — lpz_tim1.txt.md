---
파일: "lpz_tim1.txt"
아카이브: "cious.dat"
줄수: 39
바이트: 578
인코딩: cp949
---

# lpz_tim1.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/cious|cious.dat]]

## 내용 (앞 40줄 / 전체 39줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 88 48
<ENDCONTROL>
<CONTROL>
	<NAME> "TITLE"
	<TYPE> 7
	<RECT> 30 1 58 13
	<IMAGE>
		"pz_time.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "MIN2"
	<TYPE> 7
	<RECT> 0 16 21 46
<ENDCONTROL>
<CONTROL>
	<NAME> "MIN1"
	<TYPE> 7
	<RECT> 20 16 41 46
<ENDCONTROL>
<CONTROL>
	<NAME> "MIN_SEPARATOR"
	<TYPE> 7
	<RECT> 40 16 45 26
	<IMAGE>
		"pz_tmins.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "SEC2"
	<TYPE> 7
	<RECT> 46 16 67 46
<ENDCONTROL>
<CONTROL>
	<NAME> "SEC1"
	<TYPE> 7
	<RECT> 66 16 87 46
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/cious/cious.dat <폴더> lpz_tim1
iconv -f CP949 -t UTF-8 <폴더>/…/lpz_tim1.txt
```
