---
파일: "lldi.txt"
아카이브: "setoa.dat"
줄수: 72
바이트: 1107
인코딩: cp949
---

# lldi.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 72줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 566 436
	<IMAGE>
		"ldimback.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "PREV"
	<TYPE> 7
	<RECT> 15 15 37 66
	<IMAGE>
		"ldiprev.epf" 0
		"ldiprev.epf" 1
		"ldiprev.epf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "NEXT"
	<TYPE> 7
	<RECT> 529 15 551 66
	<IMAGE>
		"ldinext.epf" 0
		"ldinext.epf" 1
		"ldinext.epf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "TOPLEFT"
	<TYPE> 7
	<RECT> 37 15 119 32
<ENDCONTROL>
<CONTROL>
	<NAME> "BOTTOMLEFT"
	<TYPE> 7
	<RECT> 37 32 119 49
<ENDCONTROL>
<CONTROL>
	<NAME> "TOPRIGHT"
	<TYPE> 7
	<RECT> 119 15 201 32
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lldi
iconv -f CP949 -t UTF-8 <폴더>/…/lldi.txt
```
