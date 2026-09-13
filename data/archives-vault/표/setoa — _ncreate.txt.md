---
파일: "_ncreate.txt"
아카이브: "setoa.dat"
줄수: 131
바이트: 2050
인코딩: cp949
---

# _ncreate.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 131줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"_ncreate.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "HUMAN"
	<TYPE> 7
	<RECT> 256 184 366 268
<ENDCONTROL>
<CONTROL>
	<NAME> "Male"
	<TYPE> 7
	<RECT> 245 133 289 177
	<IMAGE>
		"_ncbg.spf" 2
		"_ncbg.spf" 3
		"_ncbg.spf" 3
<ENDCONTROL>
<CONTROL>
	<NAME> "Female"
	<TYPE> 7
	<RECT> 350 133 394 177
	<IMAGE>
		"_ncbg.spf" 0
		"_ncbg.spf" 1
		"_ncbg.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "NAME"
	<TYPE> 7
	<RECT> 23 177 137 193
<ENDCONTROL>
<CONTROL>
	<NAME> "PASSWD"
	<TYPE> 7
	<RECT> 23 235 137 251
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _ncreate
iconv -f CP949 -t UTF-8 <폴더>/…/_ncreate.txt
```
