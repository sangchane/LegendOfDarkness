---
파일: "lset.txt"
아카이브: "setoa.dat"
줄수: 37
바이트: 530
인코딩: cp949
---

# lset.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 37줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 450 302
	<IMAGE>
		"gset01.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 349 258 431 289
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "TopText"
	<TYPE> 7
	<RECT> 40 42 210 54
	<COLOR>
		20
		24
<ENDCONTROL>
<CONTROL>
	<NAME> "BottomText"
	<TYPE> 7
	<RECT> 40 63 210 75
<ENDCONTROL>
<CONTROL>
	<NAME> "TopButton"
	<TYPE> 7
	<RECT> 16 38 35 57
<ENDCONTROL>
<CONTROL>
	<NAME> "RightText"
	<TYPE> 7
	<RECT> 251 42 421 54
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lset
iconv -f CP949 -t UTF-8 <폴더>/…/lset.txt
```
