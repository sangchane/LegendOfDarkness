---
파일: "lemot.txt"
아카이브: "setoa.dat"
줄수: 30
바이트: 462
인코딩: cp949
---

# lemot.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 30줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 150 56
	<IMAGE>
		"emotdlg.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Emot0"
	<TYPE> 7
	<RECT> 3 9 21 27
	<IMAGE>
		"emot000.epf" 0
		"emot000.epf" 1
		"emot000.epf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "Emot1"
	<TYPE> 7
	<RECT> 21 9 39 27
	<IMAGE>
		"emot000.epf" 3
		"emot000.epf" 4
		"emot000.epf" 5
<ENDCONTROL>
<CONTROL>
	<NAME> "Description"
	<TYPE> 7
	<RECT> 6 30 143 42
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lemot
iconv -f CP949 -t UTF-8 <폴더>/…/lemot.txt
```
