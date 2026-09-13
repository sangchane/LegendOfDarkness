---
파일: "_nhotkem.txt"
아카이브: "setoa.dat"
줄수: 19
바이트: 281
인코딩: cp949
---

# _nhotkem.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 19줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"_nhk_bk.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "MAIN"
	<TYPE> 7
	<RECT> 34 279 570 451
	<IMAGE>
		"_nhk_m.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "EX"
	<TYPE> 7
	<RECT> 36 43 572 268
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nhotkem
iconv -f CP949 -t UTF-8 <폴더>/…/_nhotkem.txt
```
