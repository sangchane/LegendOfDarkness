---
파일: "llmap.txt"
아카이브: "setoa.dat"
줄수: 15
바이트: 222
인코딩: cp949
---

# llmap.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 15줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 566 436
	<IMAGE>
		"lsbackm.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Map"
	<TYPE> 7
	<RECT> 15 15 551 422
	<IMAGE>
		"lemap.spf" 0
		"lwmap.epf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llmap
iconv -f CP949 -t UTF-8 <폴더>/…/llmap.txt
```
