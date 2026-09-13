---
파일: "lback.txt"
아카이브: "setoa.dat"
줄수: 282
바이트: 4526
인코딩: cp949
---

# lback.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 282줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"backgrnd.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Portrait"
	<TYPE> 7
	<RECT> 570 332 634 406
	<IMAGE>
		"portrait.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Spelled"
	<TYPE> 7
	<RECT> 618 0 640 212
	<IMAGE>
		"spelled.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "HP"
	<TYPE> 7
	<RECT> 8 342 38 443
	<IMAGE>
		"orb001.epf" 0
		"orb001.epf" 17
<ENDCONTROL>
<CONTROL>
	<NAME> "MP"
	<TYPE> 7
	<RECT> 40 342 70 443
	<IMAGE>
		"orb002.epf" 0
		"orb002.epf" 17
<ENDCONTROL>
<CONTROL>
	<NAME> "HelpBtn"
	<TYPE> 7
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lback
iconv -f CP949 -t UTF-8 <폴더>/…/lback.txt
```
