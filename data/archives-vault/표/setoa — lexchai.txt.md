---
파일: "lexchai.txt"
아카이브: "setoa.dat"
줄수: 38
바이트: 532
인코딩: cp949
---

# lexchai.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 38줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 413 277
<ENDCONTROL>
<CONTROL>
	<NAME> "Close"
	<TYPE> 3
	<RECT> 75 226 157 257
	<VALUE>
		12
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 3
	<RECT> 256 226 338 257
	<VALUE>
		8
<ENDCONTROL>
<CONTROL>
	<NAME> "StaticText"
	<TYPE> 7
	<RECT> 64 46 342 104
	<COLOR>
		255
<ENDCONTROL>
<CONTROL>
	<NAME> "TextEdit"
	<TYPE> 7
	<RECT> 102 129 306 145
	<COLOR>
		20
<ENDCONTROL>
<CONTROL>
	<NAME> "List"
	<TYPE> 7
	<RECT> 96 69 313 201
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lexchai
iconv -f CP949 -t UTF-8 <폴더>/…/lexchai.txt
```
