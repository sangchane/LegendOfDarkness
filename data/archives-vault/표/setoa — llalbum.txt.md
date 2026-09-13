---
파일: "llalbum.txt"
아카이브: "setoa.dat"
줄수: 52
바이트: 783
인코딩: cp949
---

# llalbum.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 52줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 566 436
	<IMAGE>
		"leback.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "List"
	<TYPE> 7
	<RECT> 14 19 551 417
<ENDCONTROL>
<CONTROL>
	<NAME> "AlbumBase"
	<TYPE> 7
	<RECT> 30 21 95 119
	<IMAGE>
		"album.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "AlbumBaseX"
	<TYPE> 7
	<RECT> 100 21 165 119
	<IMAGE>
		"album.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "AlbumBaseY"
	<TYPE> 7
	<RECT> 30 120 95 218
	<IMAGE>
		"album.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Del"
	<TYPE> 7
	<RECT> 34 100 61 116
	<IMAGE>
		"albumd.epf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llalbum
iconv -f CP949 -t UTF-8 <폴더>/…/llalbum.txt
```
