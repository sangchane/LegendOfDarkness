---
파일: "lrs_res.txt"
아카이브: "cious.dat"
줄수: 26
바이트: 402
인코딩: cp949
---

# lrs_res.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/cious|cious.dat]]

## 내용 (앞 40줄 / 전체 26줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 312 236
	<IMAGE>
		"mg_res1.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Score"
	<TYPE> 7
	<RECT> 51 67 261 105
<ENDCONTROL>
<CONTROL>
	<NAME> "Replay"
	<TYPE> 7
	<RECT> 42 173 115 193
	<IMAGE>
		"mg_rplay.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "ShowRanking"
	<TYPE> 7
	<RECT> 200 171 271 193
	<IMAGE>
		"mg_rrank.spf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/cious/cious.dat <폴더> lrs_res
iconv -f CP949 -t UTF-8 <폴더>/…/lrs_res.txt
```
