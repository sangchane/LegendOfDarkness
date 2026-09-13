---
파일: "llevent.txt"
아카이브: "setoa.dat"
줄수: 38
바이트: 589
인코딩: cp949
---

# llevent.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 38줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 566 436
	<IMAGE>
		"leback.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "EventBase"
	<TYPE> 7
	<RECT> 15 42 531 118
	<IMAGE>
		"leback1.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "List"
	<TYPE> 7
	<RECT> 14 19 551 417
<ENDCONTROL>
<CONTROL>
	<NAME> "EventIcon"
	<TYPE> 7
	<RECT> 30 50 62 82
	<IMAGE>
		"leicon.epf" 0
		"leicon.epf" 1
		"leicon.epf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "EventDesc"
	<TYPE> 7
	<RECT> 79 69 523 117
<ENDCONTROL>
<CONTROL>
	<NAME> "EventTitle"
	<TYPE> 7
	<RECT> 81 50 311 62
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llevent
iconv -f CP949 -t UTF-8 <폴더>/…/llevent.txt
```
