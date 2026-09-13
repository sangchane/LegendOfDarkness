---
파일: "lminig.txt"
아카이브: "cious.dat"
줄수: 66
바이트: 1056
인코딩: cp949
---

# lminig.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/cious|cious.dat]]

## 내용 (앞 40줄 / 전체 66줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 320 268
	<IMAGE>
		"mg_frame.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "START"
	<TYPE> 7
	<RECT> 5 5 64 23
	<IMAGE>
		"mgb_star.spf" 0
		"mgb_star.spf" 1
		"mgb_star.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "PAUSE"
	<TYPE> 7
	<RECT> 64 5 124 23
	<IMAGE>
		"mgb_paus.spf" 0
		"mgb_paus.spf" 1
		"mgb_paus.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "RESTART"
	<TYPE> 7
	<RECT> 124 5 185 23
	<IMAGE>
		"mgb_rest.spf" 0
		"mgb_rest.spf" 1
		"mgb_rest.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "RANKING"
	<TYPE> 7
	<RECT> 185 5 220 23
	<IMAGE>
		"mgb_rank.spf" 0
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/cious/cious.dat <폴더> lminig
iconv -f CP949 -t UTF-8 <폴더>/…/lminig.txt
```
