---
파일: "_nui_pi.txt"
아카이브: "setoa.dat"
줄수: 28
바이트: 423
인코딩: cp949
---

# _nui_pi.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 28줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 200 200
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 7
	<RECT> 10 172 71 194
	<IMAGE>
		"_nbtn.spf" 3
		"_nbtn.spf" 4
		"_nbtn.spf" 5
<ENDCONTROL>
<CONTROL>
	<NAME> "CANCEL"
	<TYPE> 7
	<RECT> 129 173 190 195
	<IMAGE>
		"_nbtn.spf" 6
		"_nbtn.spf" 7
		"_nbtn.spf" 8
<ENDCONTROL>
<CONTROL>
	<NAME> "TEXT"
	<TYPE> 7
	<RECT> 11 7 191 167
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui_pi
iconv -f CP949 -t UTF-8 <폴더>/…/_nui_pi.txt
```
