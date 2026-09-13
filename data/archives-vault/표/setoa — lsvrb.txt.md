---
파일: "lsvrb.txt"
아카이브: "setoa.dat"
줄수: 46
바이트: 735
인코딩: cp949
---

# lsvrb.txt

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
	<RECT> 0 0 195 134
	<IMAGE>
		"bsvrtop.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerMidImage"
	<TYPE> 7
	<RECT> 0 134 195 154
	<IMAGE>
		"bsvrmid.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerBotImage"
	<TYPE> 7
	<RECT> 0 154 195 176
	<IMAGE>
		"bsvrbot.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerTop"
	<TYPE> 7
	<RECT> 28 58 94 70
<ENDCONTROL>
<CONTROL>
	<NAME> "ServerBottom"
	<TYPE> 7
	<RECT> 28 78 94 90
<ENDCONTROL>
<CONTROL>
	<NAME> "ExtraTop"
	<TYPE> 7
	<RECT> 100 58 166 70
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lsvrb
iconv -f CP949 -t UTF-8 <폴더>/…/lsvrb.txt
```
