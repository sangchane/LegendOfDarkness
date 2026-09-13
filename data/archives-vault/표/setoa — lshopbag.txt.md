---
파일: "lshopbag.txt"
아카이브: "setoa.dat"
줄수: 81
바이트: 1184
인코딩: cp949
---

# lshopbag.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 81줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 388 245
	<IMAGE>
		"sb_back.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Get"
	<TYPE> 3
	<RECT> 22 217 81 241
	<IMAGE>
		"sb_get.spf" 0
		"sb_get.spf" 1
	<VALUE>
		0
<ENDCONTROL>
<CONTROL>
	<NAME> "Close"
	<TYPE> 3
	<RECT> 311 217 364 241
	<IMAGE>
		"sb_close.spf" 0
		"sb_close.spf" 1
	<VALUE>
		0
<ENDCONTROL>
<CONTROL>
	<NAME> "Prev"
	<TYPE> 3
	<RECT> 90 221 163 236
	<IMAGE>
		"sb_prev.spf" 0
		"sb_prev.spf" 1
		"sb_prev.spf" 2
	<VALUE>
		0
<ENDCONTROL>
<CONTROL>
	<NAME> "Next"
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lshopbag
iconv -f CP949 -t UTF-8 <폴더>/…/lshopbag.txt
```
