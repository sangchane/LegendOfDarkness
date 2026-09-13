---
파일: "lmerc.txt"
아카이브: "setoa.dat"
줄수: 79
바이트: 1157
인코딩: cp949
---

# lmerc.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 79줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 448 366
<ENDCONTROL>
<CONTROL>
	<NAME> "TopImage"
	<TYPE> 7
	<RECT> 0 0 448 120
	<IMAGE>
		"mertop.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "MidImage"
	<TYPE> 7
	<RECT> 0 120 448 150
	<IMAGE>
		"mermid.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "BotImage"
	<TYPE> 7
	<RECT> 0 150 448 209
	<IMAGE>
		"merbot.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Description"
	<TYPE> 7
	<RECT> 126 21 418 90
<ENDCONTROL>
<CONTROL>
	<NAME> "Seller"
	<TYPE> 7
	<RECT> 3 5 115 105
<ENDCONTROL>
<CONTROL>
	<NAME> "TextButton"
	<TYPE> 7
	<RECT> 37 126 406 144
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lmerc
iconv -f CP949 -t UTF-8 <폴더>/…/lmerc.txt
```
