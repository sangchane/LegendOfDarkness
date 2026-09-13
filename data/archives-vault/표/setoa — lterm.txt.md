---
파일: "lterm.txt"
아카이브: "setoa.dat"
줄수: 66
바이트: 1021
인코딩: cp949
---

# lterm.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 66줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"setup01.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Hangul"
	<TYPE> 7
	<RECT> 187 371 253 383
<ENDCONTROL>
<CONTROL>
	<NAME> "Port"
	<TYPE> 7
	<RECT> 261 371 344 383
<ENDCONTROL>
<CONTROL>
	<NAME> "Speed"
	<TYPE> 7
	<RECT> 351 371 402 383
<ENDCONTROL>
<CONTROL>
	<NAME> "Tel"
	<TYPE> 7
	<RECT> 409 371 491 383
<ENDCONTROL>
<CONTROL>
	<NAME> "Num"
	<TYPE> 7
	<RECT> 499 371 605 383
<ENDCONTROL>
<CONTROL>
	<NAME> "Help"
	<TYPE> 7
	<RECT> 140 345 612 357
<ENDCONTROL>
<CONTROL>
	<NAME> "Main"
	<TYPE> 7
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lterm
iconv -f CP949 -t UTF-8 <폴더>/…/lterm.txt
```
