---
파일: "lbdlist.txt"
아카이브: "setoa.dat"
줄수: 28
바이트: 382
인코딩: cp949
---

# lbdlist.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 28줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 616 308
	<IMAGE>
		"dlgbbs01.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "View"
	<TYPE> 3
	<RECT> 517 14 599 45
	<VALUE>
		1
<ENDCONTROL>
<CONTROL>
	<NAME> "Quit"
	<TYPE> 3
	<RECT> 517 256 599 287
	<VALUE>
		2
<ENDCONTROL>
<CONTROL>
	<NAME> "BoardList"
	<TYPE> 6
	<RECT> 19 23 499 278
	<VALUE>
		0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lbdlist
iconv -f CP949 -t UTF-8 <폴더>/…/lbdlist.txt
```
