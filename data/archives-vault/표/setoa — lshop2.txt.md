---
파일: "lshop2.txt"
아카이브: "setoa.dat"
줄수: 63
바이트: 855
인코딩: cp949
---

# lshop2.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 63줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 272 100
	<IMAGE>
		"shop02.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Item"
	<TYPE> 7
	<RECT> 9 20 41 52
<ENDCONTROL>
<CONTROL>
	<NAME> "Name"
	<TYPE> 7
	<RECT> 52 23 222 35
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "Number"
	<TYPE> 7
	<RECT> 228 23 263 35
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "Buy"
	<TYPE> 7
	<RECT> 49 37 145 53
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "Sell"
	<TYPE> 7
	<RECT> 49 37 246 53
	<COLOR>
		20
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lshop2
iconv -f CP949 -t UTF-8 <폴더>/…/lshop2.txt
```
