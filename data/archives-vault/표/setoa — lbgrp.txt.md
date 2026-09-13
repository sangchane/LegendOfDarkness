---
파일: "lbgrp.txt"
아카이브: "setoa.dat"
줄수: 48
바이트: 713
인코딩: cp949
---

# lbgrp.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 48줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 461 192
	<IMAGE>
		"equip03.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TextTopLeft"
	<TYPE> 7
	<RECT> 22 57 132 69
	<COLOR>
		20
		255
<ENDCONTROL>
<CONTROL>
	<NAME> "TextTopMiddle"
	<TYPE> 7
	<RECT> 164 57 274 69
<ENDCONTROL>
<CONTROL>
	<NAME> "TextTopRight"
	<TYPE> 7
	<RECT> 306 57 416 69
<ENDCONTROL>
<CONTROL>
	<NAME> "TextBottomLeft"
	<TYPE> 7
	<RECT> 22 77 132 89
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 359 11 441 42
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "BtnTopLeft"
	<TYPE> 3
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lbgrp
iconv -f CP949 -t UTF-8 <폴더>/…/lbgrp.txt
```
