---
파일: "ltermset.txt"
아카이브: "setoa.dat"
줄수: 148
바이트: 2134
인코딩: cp949
---

# ltermset.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 148줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 258 258
	<IMAGE>
		"setup12.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 156 208 238 239
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Lan"
	<TYPE> 7
	<RECT> 164 46 249 70
	<IMAGE>
		"setup13.epf" 0
		"setup13.epf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Modem"
	<TYPE> 7
	<RECT> 13 46 98 70
	<IMAGE>
		"setup13.epf" 2
		"setup13.epf" 3
<ENDCONTROL>
<CONTROL>
	<NAME> "Port"
	<TYPE> 7
	<RECT> 13 73 59 93
	<IMAGE>
		"setup14.epf" 0
		"setup14.epf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Speed"
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> ltermset
iconv -f CP949 -t UTF-8 <폴더>/…/ltermset.txt
```
