---
파일: "lpz_tim2.txt"
아카이브: "cious.dat"
줄수: 49
바이트: 726
인코딩: cp949
---

# lpz_tim2.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/cious|cious.dat]]

## 내용 (앞 40줄 / 전체 49줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 138 30
<ENDCONTROL>
<CONTROL>
	<NAME> "MIN2"
	<TYPE> 7
	<RECT> 0 0 21 30
<ENDCONTROL>
<CONTROL>
	<NAME> "MIN1"
	<TYPE> 7
	<RECT> 20 0 41 30
<ENDCONTROL>
<CONTROL>
	<NAME> "MIN_SEPARATOR"
	<TYPE> 7
	<RECT> 40 0 45 10
	<IMAGE>
		"pz_tmins.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "SEC2"
	<TYPE> 7
	<RECT> 46 0 67 30
<ENDCONTROL>
<CONTROL>
	<NAME> "SEC1"
	<TYPE> 7
	<RECT> 66 0 87 30
<ENDCONTROL>
<CONTROL>
	<NAME> "SEC_SEPARATOR"
	<TYPE> 7
	<RECT> 86 0 96 10
	<IMAGE>
		"pz_tsecs.spf" 0
<ENDCONTROL>
<CONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/cious/cious.dat <폴더> lpz_tim2
iconv -f CP949 -t UTF-8 <폴더>/…/lpz_tim2.txt
```
