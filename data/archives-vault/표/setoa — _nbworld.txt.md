---
파일: "_nbworld.txt"
아카이브: "setoa.dat"
줄수: 97
바이트: 1506
인코딩: cp949
---

# _nbworld.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 97줄)

```
<CONTROL>
	<NAME> "NUMBER"
	<TYPE> 7
	<RECT> 0 0 599 438
	<IMAGE>
		"bw_num.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"_nbswbk.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "CHECK_FLAG"
	<TYPE> 7
	<RECT> 552 407 566 417
	<IMAGE>
		"bw_check.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "CHECK_TERRITORY"
	<TYPE> 7
	<RECT> 552 429 566 439
	<IMAGE>
		"bw_check.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TOPPOS"
	<TYPE> 7
	<RECT> 0 17 56 45
<ENDCONTROL>
<CONTROL>
	<NAME> "FLAG"
	<TYPE> 7
	<RECT> 554 410 563 419
<ENDCONTROL>
<CONTROL>
	<NAME> "TERRITORY"
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nbworld
iconv -f CP949 -t UTF-8 <폴더>/…/_nbworld.txt
```
