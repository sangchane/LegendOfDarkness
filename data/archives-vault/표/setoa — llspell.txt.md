---
파일: "llspell.txt"
아카이브: "setoa.dat"
줄수: 46
바이트: 687
인코딩: cp949
---

# llspell.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 46줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 566 436
	<IMAGE>
		"lsbackm.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "InContent"
	<TYPE> 7
	<RECT> 15 15 551 422
	<IMAGE>
		"lsback.epf" 0
		"lsback2.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Property"
	<TYPE> 7
	<RECT> 116 91 444 362
	<IMAGE>
		"lsprop.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "BTN1"
	<TYPE> 7
	<RECT> 456 22 496 62
	<IMAGE>
		"lsu.epf" 0
		"lsu.epf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "BTN2"
	<TYPE> 7
	<RECT> 456 66 496 106
	<IMAGE>
		"lsu.epf" 2
		"lsu.epf" 3
<ENDCONTROL>
<CONTROL>
	<NAME> "BTN3"
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llspell
iconv -f CP949 -t UTF-8 <폴더>/…/llspell.txt
```
