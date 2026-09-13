---
파일: "lnpcd2.txt"
아카이브: "setoa.dat"
줄수: 49
바이트: 779
인코딩: cp949
---

# lnpcd2.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 49줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 426 194
<ENDCONTROL>
<CONTROL>
	<NAME> "Content"
	<TYPE> 7
	<RECT> 13 6 413 166
<ENDCONTROL>
<CONTROL>
	<NAME> "TextInputContent"
	<TYPE> 7
	<RECT> 13 116 413 156
<ENDCONTROL>
<CONTROL>
	<NAME> "ExtraStatic"
	<TYPE> 7
	<RECT> 22 130 62 146
<ENDCONTROL>
<CONTROL>
	<NAME> "Text"
	<TYPE> 7
	<RECT> 72 126 404 146
<ENDCONTROL>
<CONTROL>
	<NAME> "Btn1"
	<TYPE> 3
	<RECT> 345 169 406 191
	<IMAGE>
		"_nbtn.spf" 3
		"_nbtn.spf" 4
		"_nbtn.spf" 5
<ENDCONTROL>
<CONTROL>
	<NAME> "TextMenu"
	<TYPE> 7
	<RECT> 13 41 413 59
<ENDCONTROL>
<CONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lnpcd2
iconv -f CP949 -t UTF-8 <폴더>/…/lnpcd2.txt
```
