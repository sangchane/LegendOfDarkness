---
파일: "_nexch.txt"
아카이브: "setoa.dat"
줄수: 71
바이트: 1041
인코딩: cp949
---

# _nexch.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 71줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 421 296
	<IMAGE>
		"_nexch.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "MyID"
	<TYPE> 7
	<RECT> 27 39 197 51
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "YourID"
	<TYPE> 7
	<RECT> 222 39 392 51
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "MyExchange"
	<TYPE> 7
	<RECT> 30 76 199 220
<ENDCONTROL>
<CONTROL>
	<NAME> "YourExchange"
	<TYPE> 7
	<RECT> 224 76 394 220
<ENDCONTROL>
<CONTROL>
	<NAME> "MyMoney"
	<TYPE> 7
	<RECT> 64 228 196 244
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "YourMoney"
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nexch
iconv -f CP949 -t UTF-8 <폴더>/…/_nexch.txt
```
