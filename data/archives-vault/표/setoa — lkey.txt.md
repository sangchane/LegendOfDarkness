---
파일: "lkey.txt"
아카이브: "setoa.dat"
줄수: 121
바이트: 1853
인코딩: cp949
---

# lkey.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 121줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 536 402
	<IMAGE>
		"lsbackm.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "CONTENT"
	<TYPE> 7
	<RECT> 15 15 551 422
<ENDCONTROL>
<CONTROL>
	<NAME> "HOTKEY"
	<TYPE> 7
	<RECT> 11 230 529 402
	<IMAGE>
		"khotkey.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "DESC"
	<TYPE> 7
	<RECT> 0 0 536 230
	<IMAGE>
		"kdesc00.epf" 0
		"kdesc01.epf" 0
		"kdesc02.epf" 0
		"kdesc03.epf" 0
		"kdesc04.epf" 0
		"kdesc05.epf" 0
		"kdesc06.epf" 0
		"kdesc07.epf" 0
		"kdesc08.epf" 0
		"kdesc09.epf" 0
		"kdesc10.epf" 0
		"kdesc11.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "KEY00"
	<TYPE> 7
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lkey
iconv -f CP949 -t UTF-8 <폴더>/…/lkey.txt
```
