---
파일: "_nsvr.txt"
아카이브: "setoa.dat"
줄수: 46
바이트: 733
인코딩: cp949
---

# _nsvr.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 46줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerTopImage"
	<TYPE> 7
	<RECT> 0 0 421 133
	<IMAGE>
		"_nsvr1.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerMidImage"
	<TYPE> 7
	<RECT> 0 133 421 153
	<IMAGE>
		"_nsvr2.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerBotImage"
	<TYPE> 7
	<RECT> 0 153 421 176
	<IMAGE>
		"_nsvr3.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerTop"
	<TYPE> 7
	<RECT> 28 37 98 49
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerBottom"
	<TYPE> 7
	<RECT> 28 57 98 69
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerATop"
	<TYPE> 7
	<RECT> 108 37 394 49
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nsvr
iconv -f CP949 -t UTF-8 <폴더>/…/_nsvr.txt
```
