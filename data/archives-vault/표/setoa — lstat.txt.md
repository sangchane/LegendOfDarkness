---
파일: "lstat.txt"
아카이브: "setoa.dat"
줄수: 148
바이트: 2281
인코딩: cp949
---

# lstat.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 148줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
<ENDCONTROL>
<CONTROL>
	<NAME> "Status"
	<TYPE> 7
	<RECT> 0 0 444 108
	<IMAGE>
		"stat001.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ExtraStatus"
	<TYPE> 7
	<RECT> 0 117 444 225
	<IMAGE>
		"stat002.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "s_Str"
	<TYPE> 7
	<RECT> 45 10 66 22
<ENDCONTROL>
<CONTROL>
	<NAME> "s_Int"
	<TYPE> 7
	<RECT> 45 29 66 41
<ENDCONTROL>
<CONTROL>
	<NAME> "s_Wis"
	<TYPE> 7
	<RECT> 45 48 66 60
<ENDCONTROL>
<CONTROL>
	<NAME> "s_Con"
	<TYPE> 7
	<RECT> 45 67 66 79
<ENDCONTROL>
<CONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lstat
iconv -f CP949 -t UTF-8 <폴더>/…/lstat.txt
```
