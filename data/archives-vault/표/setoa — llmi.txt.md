---
파일: "llmi.txt"
아카이브: "setoa.dat"
줄수: 33
바이트: 497
인코딩: cp949
---

# llmi.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 33줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 600 436
	<IMAGE>
		"lmiback.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "MAP"
	<TYPE> 7
	<RECT> 14 14 584 284
	<IMAGE>
		"tmapv01.epf" 0
		"tmapv02.epf" 0
		"tmapv03.epf" 0
		"tmapv04.epf" 0
		"tmapv05.epf" 0
		"tmapv06.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "MAPNAME"
	<TYPE> 5
	<RECT> 229 291 369 303
	<COLOR>
		20	0
<ENDCONTROL>
<CONTROL>
	<NAME> "MAPDESC"
	<TYPE> 5
	<RECT> 19 314 581 420
	<COLOR>
		20	0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llmi
iconv -f CP949 -t UTF-8 <폴더>/…/llmi.txt
```
