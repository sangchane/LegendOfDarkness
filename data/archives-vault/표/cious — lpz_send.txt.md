---
파일: "lpz_send.txt"
아카이브: "cious.dat"
줄수: 31
바이트: 465
인코딩: cp949
---

# lpz_send.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/cious|cious.dat]]

## 내용 (앞 40줄 / 전체 31줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 312 236
	<IMAGE>
		"pz_rback.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "YES"
	<TYPE> 7
	<RECT> 81 184 127 208
	<IMAGE>
		"pz_ryes.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "NO"
	<TYPE> 7
	<RECT> 184 184 221 208
	<IMAGE>
		"pz_rno.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "LEVEL"
	<TYPE> 7
	<RECT> 57 105 95 143
<ENDCONTROL>
<CONTROL>
	<NAME> "TIME"
	<TYPE> 7
	<RECT> 106 109 244 139
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/cious/cious.dat <폴더> lpz_send
iconv -f CP949 -t UTF-8 <폴더>/…/lpz_send.txt
```
