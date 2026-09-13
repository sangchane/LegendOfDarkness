---
파일: "_nmoney.txt"
아카이브: "setoa.dat"
줄수: 35
바이트: 526
인코딩: cp949
---

# _nmoney.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 35줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 239 126
	<IMAGE>
		"_nmoney.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Title"
	<TYPE> 7
	<RECT> 74 40 224 52
<ENDCONTROL>
<CONTROL>
	<NAME> "Text"
	<TYPE> 7
	<RECT> 25 60 211 76
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 7
	<RECT> 88 82 149 104
	<IMAGE>
		"_nbtn.spf" 3
		"_nbtn.spf" 4
		"_nbtn.spf" 5
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 7
	<RECT> 153 82 214 104
	<IMAGE>
		"_nbtn.spf" 6
		"_nbtn.spf" 7
		"_nbtn.spf" 8
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nmoney
iconv -f CP949 -t UTF-8 <폴더>/…/_nmoney.txt
```
