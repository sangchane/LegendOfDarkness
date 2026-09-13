---
파일: "lnpcd.txt"
아카이브: "setoa.dat"
줄수: 95
바이트: 1462
인코딩: cp949
---

# lnpcd.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 95줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
<ENDCONTROL>
<CONTROL>
	<NAME> "UIScreen"
	<TYPE> 7
	<RECT> 0 254 640 372
<ENDCONTROL>
<CONTROL>
	<NAME> "MessageDialog"
	<TYPE> 7
	<RECT> 0 372 640 480
	<IMAGE>
		"nd_talk.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "NPCTile"
	<TYPE> 7
	<RECT> 0 254 132 372
	<IMAGE>
		"nd_npcbg.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "MenuDialog"
	<TYPE> 7
	<RECT> 132 0 640 372
<ENDCONTROL>
<CONTROL>
	<NAME> "Name"
	<TYPE> 7
	<RECT> 20 382 408 398
<ENDCONTROL>
<CONTROL>
	<NAME> "Text"
	<TYPE> 7
	<RECT> 26 400 606 449
<ENDCONTROL>
<CONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lnpcd
iconv -f CP949 -t UTF-8 <폴더>/…/lnpcd.txt
```
