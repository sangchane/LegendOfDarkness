---
파일: "lmerc2.txt"
아카이브: "setoa.dat"
줄수: 147
바이트: 2259
인코딩: cp949
---

# lmerc2.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 147줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 462 370
	<IMAGE>
		"mercback.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Description"
	<TYPE> 7
	<RECT> 114 13 406 61
<ENDCONTROL>
<CONTROL>
	<NAME> "Seller"
	<TYPE> 7
	<RECT> 3 5 93 74
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn1"
	<TYPE> 3
	<RECT> 21 301 103 332
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn2"
	<TYPE> 3
	<RECT> 113 301 195 332
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn3"
	<TYPE> 3
	<RECT> 230 301 312 332
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn4"
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lmerc2
iconv -f CP949 -t UTF-8 <폴더>/…/lmerc2.txt
```
