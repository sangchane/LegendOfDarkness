---
파일: "lmerd.txt"
아카이브: "setoa.dat"
줄수: 66
바이트: 947
인코딩: cp949
---

# lmerd.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 66줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 448 366
	<IMAGE>
		"dlgbg001.epf" 0
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
	<NAME> "Btn1"
	<TYPE> 3
	<RECT> 11 327 93 358
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn2"
	<TYPE> 3
	<RECT> 101 327 183 358
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn3"
	<TYPE> 3
	<RECT> 265 327 347 358
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn4"
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lmerd
iconv -f CP949 -t UTF-8 <폴더>/…/lmerd.txt
```
