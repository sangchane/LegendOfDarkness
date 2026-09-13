---
파일: "_nbk_l.txt"
아카이브: "setoa.dat"
줄수: 332
바이트: 5449
인코딩: cp949
---

# _nbk_l.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 332줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"_nbk_l.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "SZ_ID"
	<TYPE> 7
	<RECT> 6 418 78 430
<ENDCONTROL>
<CONTROL>
	<NAME> "NUM_HP"
	<TYPE> 7
	<RECT> 14 443 74 455
<ENDCONTROL>
<CONTROL>
	<NAME> "NUM_MP"
	<TYPE> 7
	<RECT> 14 462 74 474
<ENDCONTROL>
<CONTROL>
	<NAME> "SZ_ZONE"
	<TYPE> 7
	<RECT> 543 437 633 449
<ENDCONTROL>
<CONTROL>
	<NAME> "SZ_XY"
	<TYPE> 7
	<RECT> 587 462 635 474
<ENDCONTROL>
<CONTROL>
	<NAME> "SZ_WEIGHT"
	<TYPE> 7
	<RECT> 532 462 586 474
<ENDCONTROL>
<CONTROL>
	<NAME> "MAP"
	<TYPE> 7
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nbk_l
iconv -f CP949 -t UTF-8 <폴더>/…/_nbk_l.txt
```
