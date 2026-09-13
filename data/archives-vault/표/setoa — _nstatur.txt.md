---
파일: "_nstatur.txt"
아카이브: "setoa.dat"
줄수: 89
바이트: 1366
인코딩: cp949
---

# _nstatur.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 89줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
<ENDCONTROL>
<CONTROL>
	<NAME> "Status"
	<TYPE> 7
	<RECT> 0 0 444 46
	<IMAGE>
		"_nstatur.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ExtraStatus"
	<TYPE> 7
	<RECT> 0 61 444 107
	<IMAGE>
		"_nstater.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "s_nextLev"
	<TYPE> 7
	<RECT> 374 28 430 40
<ENDCONTROL>
<CONTROL>
	<NAME> "s_Gold"
	<TYPE> 7
	<RECT> 55 28 115 40
<ENDCONTROL>
<CONTROL>
	<NAME> "s_HPMax"
	<TYPE> 7
	<RECT> 230 11 282 23
<ENDCONTROL>
<CONTROL>
	<NAME> "s_HP"
	<TYPE> 7
	<RECT> 172 11 224 23
<ENDCONTROL>
<CONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nstatur
iconv -f CP949 -t UTF-8 <폴더>/…/_nstatur.txt
```
