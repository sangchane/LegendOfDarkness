---
파일: "llegend2.txt"
아카이브: "setoa.dat"
줄수: 47
바이트: 732
인코딩: cp949
---

# llegend2.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 47줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 566 436
	<IMAGE>
		"llback.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ProfileText"
	<TYPE> 7
	<RECT> 96 350 543 414
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "Portrait"
	<TYPE> 7
	<RECT> 20 341 84 415
	<IMAGE>
		"portrait.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Change"
	<TYPE> 7
	<RECT> 491 309 527 332
	<IMAGE>
		"llchange.epf" 0
		"llchange.epf" 1
		"llchange.epf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "PortraitPic"
	<TYPE> 7
	<RECT> 28 350 76 406
<ENDCONTROL>
<CONTROL>
	<NAME> "LegendList"
	<TYPE> 7
	<RECT> 18 21 552 301
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llegend2
iconv -f CP949 -t UTF-8 <폴더>/…/llegend2.txt
```
