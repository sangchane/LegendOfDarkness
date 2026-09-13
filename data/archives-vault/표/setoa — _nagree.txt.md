---
파일: "_nagree.txt"
아카이브: "setoa.dat"
줄수: 39
바이트: 571
인코딩: cp949
---

# _nagree.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 39줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 444 377
	<IMAGE>
		"_nagree.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "CONTROL"
	<TYPE> 7
	<RECT> 198 7 293 19
<ENDCONTROL>
<CONTROL>
	<NAME> "AGREEMENTTEXT"
	<TYPE> 7
	<RECT> 15 39 429 339
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 110 352 171 374
	<IMAGE>
		"_nbtn.spf" 3
		"_nbtn.spf" 4
		"_nbtn.spf" 5
	<VALUE>
		0
<ENDCONTROL>
<CONTROL>
	<NAME> "CANCEL"
	<TYPE> 3
	<RECT> 275 352 336 374
	<IMAGE>
		"_nbtn.spf" 6
		"_nbtn.spf" 7
		"_nbtn.spf" 8
	<VALUE>
		0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nagree
iconv -f CP949 -t UTF-8 <폴더>/…/_nagree.txt
```
