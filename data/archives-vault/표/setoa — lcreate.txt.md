---
파일: "lcreate.txt"
아카이브: "setoa.dat"
줄수: 140
바이트: 2243
인코딩: cp949
---

# lcreate.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 140줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"dlgcre00.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 7
	<RECT> 407 386 506 430
	<IMAGE>
		"dlgcre04.epf" 1
		"dlgcre04.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 7
	<RECT> 506 386 605 430
	<IMAGE>
		"dlgcre04.epf" 3
		"dlgcre04.epf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "Male"
	<TYPE> 7
	<RECT> 406 68 449 111
	<IMAGE>
		"dlgcre01.epf" 0
		"dlgcre01.epf" 1
		"dlgcre01.epf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "Female"
	<TYPE> 7
	<RECT> 477 68 520 111
	<IMAGE>
		"dlgcre01.epf" 3
		"dlgcre01.epf" 4
		"dlgcre01.epf" 5
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lcreate
iconv -f CP949 -t UTF-8 <폴더>/…/lcreate.txt
```
