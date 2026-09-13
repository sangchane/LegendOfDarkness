---
파일: "lsvr.txt"
아카이브: "setoa.dat"
줄수: 46
바이트: 733
인코딩: cp949
---

# lsvr.txt

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
	<RECT> 0 0 413 146
	<IMAGE>
		"svrtop.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerMidImage"
	<TYPE> 7
	<RECT> 0 146 413 166
	<IMAGE>
		"svrmid.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerBotImage"
	<TYPE> 7
	<RECT> 0 166 413 183
	<IMAGE>
		"svrbot.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerTop"
	<TYPE> 7
	<RECT> 21 50 91 62
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerBottom"
	<TYPE> 7
	<RECT> 21 70 91 82
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerATop"
	<TYPE> 7
	<RECT> 102 50 388 62
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lsvr
iconv -f CP949 -t UTF-8 <폴더>/…/lsvr.txt
```
