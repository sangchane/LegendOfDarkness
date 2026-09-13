---
파일: "lshop0.txt"
아카이브: "setoa.dat"
줄수: 76
바이트: 1108
인코딩: cp949
---

# lshop0.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 76줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 214 200
	<IMAGE>
		"shop00.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Title"
	<TYPE> 7
	<RECT> 40 9 100 21
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "ItemTopLeft"
	<TYPE> 7
	<RECT> 14 60 46 92
<ENDCONTROL>
<CONTROL>
	<NAME> "ItemTopRight"
	<TYPE> 7
	<RECT> 52 60 84 92
<ENDCONTROL>
<CONTROL>
	<NAME> "ItemBottomLeft"
	<TYPE> 7
	<RECT> 14 114 46 146
<ENDCONTROL>
<CONTROL>
	<NAME> "Money"
	<TYPE> 7
	<RECT> 17 43 137 55
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "WithdrawMoney"
	<TYPE> 7
	<RECT> 141 42 153 55
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lshop0
iconv -f CP949 -t UTF-8 <폴더>/…/lshop0.txt
```
