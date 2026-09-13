---
파일: "lmain.txt"
아카이브: "setoa.dat"
줄수: 74
바이트: 1134
인코딩: cp949
---

# lmain.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 74줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"lod00.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Create"
	<TYPE> 7
	<RECT> 480 222 592 264
	<IMAGE>
		"lod01.epf" 0
		"lod01.epf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Continue"
	<TYPE> 7
	<RECT> 480 255 592 297
	<IMAGE>
		"lod01.epf" 2
		"lod01.epf" 3
<ENDCONTROL>
<CONTROL>
	<NAME> "Password"
	<TYPE> 7
	<RECT> 480 290 592 332
	<IMAGE>
		"lod01.epf" 4
		"lod01.epf" 5
<ENDCONTROL>
<CONTROL>
	<NAME> "Credit"
	<TYPE> 7
	<RECT> 480 323 592 365
	<IMAGE>
		"lod01.epf" 6
		"lod01.epf" 7
<ENDCONTROL>
<CONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lmain
iconv -f CP949 -t UTF-8 <폴더>/…/lmain.txt
```
