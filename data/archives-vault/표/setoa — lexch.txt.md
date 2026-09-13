---
파일: "lexch.txt"
아카이브: "setoa.dat"
줄수: 67
바이트: 953
인코딩: cp949
---

# lexch.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 67줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 403 302
	<IMAGE>
		"exchange.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 17 264 99 295
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 3
	<RECT> 111 264 193 295
	<VALUE>
		8
<ENDCONTROL>
<CONTROL>
	<NAME> "MyID"
	<TYPE> 7
	<RECT> 12 52 182 64
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "YourID"
	<TYPE> 7
	<RECT> 215 52 385 64
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "MyExchange"
	<TYPE> 7
	<RECT> 16 89 185 233
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lexch
iconv -f CP949 -t UTF-8 <폴더>/…/lexch.txt
```
