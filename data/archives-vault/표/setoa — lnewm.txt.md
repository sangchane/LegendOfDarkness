---
파일: "lnewm.txt"
아카이브: "setoa.dat"
줄수: 57
바이트: 725
인코딩: cp949
---

# lnewm.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 57줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 616 308
	<IMAGE>
		"dlgbbs03.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Send"
	<TYPE> 3
	<RECT> 518 67 600 98
	<VALUE>
		13
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 3
	<RECT> 518 101 600 132
	<VALUE>
		8
<ENDCONTROL>
<CONTROL>
	<NAME> "Receiver"
	<TYPE> 5
	<RECT> 96 15 318 27
	<VALUE>
		0
	<COLOR>
		20
		255
<ENDCONTROL>
<CONTROL>
	<NAME> "ReceiverEdit"
	<TYPE> 6
	<RECT> 96 13 320 29
	<VALUE>
		0
	<COLOR>
		255
		31
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lnewm
iconv -f CP949 -t UTF-8 <폴더>/…/lnewm.txt
```
