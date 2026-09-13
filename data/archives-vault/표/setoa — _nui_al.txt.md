---
파일: "_nui_al.txt"
아카이브: "setoa.dat"
줄수: 50
바이트: 770
인코딩: cp949
---

# _nui_al.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 50줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 599 306
	<IMAGE>
		"_nui_al.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "NEXT"
	<TYPE> 7
	<RECT> 527 276 565 291
	<IMAGE>
		"_nui_bn.spf" 0
		"_nui_bn.spf" 1
		"_nui_bn.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "PREV"
	<TYPE> 7
	<RECT> 33 275 71 290
	<IMAGE>
		"_nui_bp.spf" 0
		"_nui_bp.spf" 1
		"_nui_bp.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "PG1"
	<TYPE> 7
	<RECT> 79 278 121 290
<ENDCONTROL>
<CONTROL>
	<NAME> "PG2"
	<TYPE> 7
	<RECT> 477 278 519 290
<ENDCONTROL>
<CONTROL>
	<NAME> "PGA1"
	<TYPE> 7
	<RECT> 36 34 264 274
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui_al
iconv -f CP949 -t UTF-8 <폴더>/…/_nui_al.txt
```
