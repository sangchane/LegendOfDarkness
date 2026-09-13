---
파일: "_nbk_s.txt"
아카이브: "setoa.dat"
줄수: 340
바이트: 5515
인코딩: cp949
---

# _nbk_s.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 340줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"_nbk_s.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ORB_HP"
	<TYPE> 7
	<RECT> 6 331 39 454
	<IMAGE>
		"_norb_hp.spf" 21
<ENDCONTROL>
<CONTROL>
	<NAME> "ORB_MP"
	<TYPE> 7
	<RECT> 39 331 72 454
	<IMAGE>
		"_norb_mp.spf" 21
<ENDCONTROL>
<CONTROL>
	<NAME> "SZ_ID"
	<TYPE> 7
	<RECT> 563 338 635 350
<ENDCONTROL>
<CONTROL>
	<NAME> "SZ_SERVER"
	<TYPE> 7
	<RECT> 564 461 636 473
<ENDCONTROL>
<CONTROL>
	<NAME> "NUM_HP"
	<TYPE> 7
	<RECT> 6 316 66 328
<ENDCONTROL>
<CONTROL>
	<NAME> "NUM_MP"
	<TYPE> 7
	<RECT> 6 463 66 475
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nbk_s
iconv -f CP949 -t UTF-8 <폴더>/…/_nbk_s.txt
```
