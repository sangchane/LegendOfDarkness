---
파일: "larre.txt"
아카이브: "setoa.dat"
줄수: 33
바이트: 440
인코딩: cp949
---

# larre.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 33줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 514 114
<ENDCONTROL>
<CONTROL>
	<NAME> "Edit"
	<TYPE> 7
	<RECT> 61 46 451 62
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 3
	<RECT> 429 80 511 111
	<VALUE>
		8
<ENDCONTROL>
<CONTROL>
	<NAME> "Send"
	<TYPE> 3
	<RECT> 337 80 419 111
	<VALUE>
		13
<ENDCONTROL>
<CONTROL>
	<NAME> "Text"
	<TYPE> 7
	<RECT> 10 23 502 35
	<COLOR>
		20
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> larre
iconv -f CP949 -t UTF-8 <폴더>/…/larre.txt
```
