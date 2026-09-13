---
파일: "_nui_eqa.txt"
아카이브: "setoa.dat"
줄수: 222
바이트: 3423
인코딩: cp949
---

# _nui_eqa.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 222줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 599 306
	<IMAGE>
		"_nui_eqa.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "HEAD"
	<TYPE> 7
	<RECT> 136 50 168 82
	<IMAGE>
		"_nui_eqi.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "HEAD2"
	<TYPE> 7
	<RECT> 136 88 168 120
	<IMAGE>
		"_nui_eqi.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "EAR"
	<TYPE> 7
	<RECT> 85 60 117 92
	<IMAGE>
		"_nui_eqi.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "ARMOR"
	<TYPE> 7
	<RECT> 70 99 102 131
	<IMAGE>
		"_nui_eqi.spf" 3
<ENDCONTROL>
<CONTROL>
	<NAME> "ARMOR2"
	<TYPE> 7
	<RECT> 32 99 64 131
	<IMAGE>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui_eqa
iconv -f CP949 -t UTF-8 <폴더>/…/_nui_eqa.txt
```
