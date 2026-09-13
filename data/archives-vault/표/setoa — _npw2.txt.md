---
파일: "_npw2.txt"
아카이브: "setoa.dat"
줄수: 28
바이트: 425
인코딩: cp949
---

# _npw2.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 28줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 217 170
	<IMAGE>
		"_npw2.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Birth"
	<TYPE> 7
	<RECT> 64 77 154 93
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 7
	<RECT> 28 131 90 151
	<IMAGE>
		"menubtn.epf" 0
		"menubtn.epf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 7
	<RECT> 114 129 176 149
	<IMAGE>
		"menubtn.epf" 2
		"menubtn.epf" 3
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _npw2
iconv -f CP949 -t UTF-8 <폴더>/…/_npw2.txt
```
