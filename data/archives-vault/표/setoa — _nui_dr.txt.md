---
파일: "_nui_dr.txt"
아카이브: "setoa.dat"
줄수: 19
바이트: 294
인코딩: cp949
---

# _nui_dr.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 19줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 599 306
	<IMAGE>
		"_nui_dr.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "LegendList"
	<TYPE> 7
	<RECT> 38 33 562 270
<ENDCONTROL>
<CONTROL>
	<NAME> "LegendIcon"
	<TYPE> 7
	<RECT> 44 56 65 76
	<IMAGE>
		"_nui_leg.spf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui_dr
iconv -f CP949 -t UTF-8 <폴더>/…/_nui_dr.txt
```
