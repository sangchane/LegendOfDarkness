---
파일: "lpz_main.txt"
아카이브: "cious.dat"
줄수: 34
바이트: 531
인코딩: cp949
---

# lpz_main.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/cious|cious.dat]]

## 내용 (앞 40줄 / 전체 34줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 312 236
	<IMAGE>
		"pz_back.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Puzzle"
	<TYPE> 7
	<RECT> 12 13 222 223
<ENDCONTROL>
<CONTROL>
	<NAME> "TimeArea"
	<TYPE> 7
	<RECT> 224 10 312 58
<ENDCONTROL>
<CONTROL>
	<NAME> "LevelArea"
	<TYPE> 7
	<RECT> 224 61 312 101
<ENDCONTROL>
<CONTROL>
	<NAME> "NiyePop"
	<TYPE> 7
	<RECT> 224 100 312 226
<ENDCONTROL>
<CONTROL>
	<NAME> "Balloon"
	<TYPE> 7
	<RECT> 229 100 306 155
	<IMAGE>
		"pz_balun.spf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/cious/cious.dat <폴더> lpz_main
iconv -f CP949 -t UTF-8 <폴더>/…/lpz_main.txt
```
