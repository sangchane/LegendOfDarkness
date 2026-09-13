---
파일: "lmsg.txt"
아카이브: "setoa.dat"
줄수: 87
바이트: 1224
인코딩: cp949
---

# lmsg.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 87줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
<ENDCONTROL>
<CONTROL>
	<NAME> "TopImage"
	<TYPE> 7
	<RECT> 0 0 448 90
	<IMAGE>
		"msgtop.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "MidImage"
	<TYPE> 7
	<RECT> 0 90 448 120
	<IMAGE>
		"msgmid.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "BotImage"
	<TYPE> 7
	<RECT> 0 120 448 189
	<IMAGE>
		"msgbot.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Description"
	<TYPE> 7
	<RECT> 124 20 422 89
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn1"
	<TYPE> 3
	<RECT> 11 148 93 179
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn2"
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lmsg
iconv -f CP949 -t UTF-8 <폴더>/…/lmsg.txt
```
