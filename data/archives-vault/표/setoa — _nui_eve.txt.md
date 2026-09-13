---
파일: "_nui_eve.txt"
아카이브: "setoa.dat"
줄수: 37
바이트: 551
인코딩: cp949
---

# _nui_eve.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 37줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 335 258
	<IMAGE>
		"_nui_eve.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ICON"
	<TYPE> 7
	<RECT> 15 11 47 43
<ENDCONTROL>
<CONTROL>
	<NAME> "NAME"
	<TYPE> 7
	<RECT> 59 32 319 44
<ENDCONTROL>
<CONTROL>
	<NAME> "LEV"
	<TYPE> 7
	<RECT> 57 16 117 28
<ENDCONTROL>
<CONTROL>
	<NAME> "MUST"
	<TYPE> 7
	<RECT> 80 66 320 78
<ENDCONTROL>
<CONTROL>
	<NAME> "REWARD"
	<TYPE> 7
	<RECT> 71 84 311 96
<ENDCONTROL>
<CONTROL>
	<NAME> "DESC"
	<TYPE> 7
	<RECT> 14 117 320 249
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui_eve
iconv -f CP949 -t UTF-8 <폴더>/…/_nui_eve.txt
```
