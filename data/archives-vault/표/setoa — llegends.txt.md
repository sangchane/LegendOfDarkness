---
파일: "llegends.txt"
아카이브: "setoa.dat"
줄수: 34
바이트: 488
인코딩: cp949
---

# llegends.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 34줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 444 412
	<IMAGE>
		"legend.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Title"
	<TYPE> 7
	<RECT> 161 18 279 30
	<COLOR>
		255
		31
<ENDCONTROL>
<CONTROL>
	<NAME> "LegendList"
	<TYPE> 7
	<RECT> 14 61 429 361
<ENDCONTROL>
<CONTROL>
	<NAME> "Close"
	<TYPE> 3
	<RECT> 319 376 401 407
	<VALUE>
		12
<ENDCONTROL>
<CONTROL>
	<NAME> "LegendImage"
	<TYPE> 7
	<RECT> 358 20 379 40
	<IMAGE>
		"legends.epf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llegends
iconv -f CP949 -t UTF-8 <폴더>/…/llegends.txt
```
