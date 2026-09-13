---
파일: "llsprop.txt"
아카이브: "setoa.dat"
줄수: 36
바이트: 496
인코딩: cp949
---

# llsprop.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 36줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 328 271
	<IMAGE>
		"lsprop.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 288 4 324 27
	<IMAGE>
		"lconf.epf" 0
		"lconf.epf" 1
	<VALUE>
		0
<ENDCONTROL>
<CONTROL>
	<NAME> "Description"
	<TYPE> 7
	<RECT> 13 77 313 261
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "Title"
	<TYPE> 7
	<RECT> 75 37 307 49
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "Icon"
	<TYPE> 7
	<RECT> 19 18 51 50
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llsprop
iconv -f CP949 -t UTF-8 <폴더>/…/llsprop.txt
```
