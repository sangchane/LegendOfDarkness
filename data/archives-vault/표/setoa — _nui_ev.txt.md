---
파일: "_nui_ev.txt"
아카이브: "setoa.dat"
줄수: 37
바이트: 580
인코딩: cp949
---

# _nui_ev.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 37줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 599 306
	<IMAGE>
		"_nui_ev.spf" 0
		"_nui_ev0.spf" 0
		"_nui_ev1.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "EV1"
	<TYPE> 7
	<RECT> 32 33 265 272
<ENDCONTROL>
<CONTROL>
	<NAME> "EV2"
	<TYPE> 7
	<RECT> 331 33 564 272
<ENDCONTROL>
<CONTROL>
	<NAME> "NEXT"
	<TYPE> 7
	<RECT> 495 281 567 296
	<IMAGE>
		"_nui_bn.spf" 0
		"_nui_bn.spf" 1
		"_nui_bn.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "PREV"
	<TYPE> 7
	<RECT> 34 281 106 296
	<IMAGE>
		"_nui_bp.spf" 0
		"_nui_bp.spf" 1
		"_nui_bp.spf" 2
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui_ev
iconv -f CP949 -t UTF-8 <폴더>/…/_nui_ev.txt
```
