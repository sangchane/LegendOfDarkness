---
파일: "lagre.txt"
아카이브: "setoa.dat"
줄수: 31
바이트: 448
인코딩: cp949
---

# lagre.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 31줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 444 412
	<IMAGE>
		"legend.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "CONTROL"
	<TYPE> 7
	<RECT> 189 18 284 30
<ENDCONTROL>
<CONTROL>
	<NAME> "AGREEMENTTEXT"
	<TYPE> 7
	<RECT> 15 62 429 362
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 63 377 145 408
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "CANCEL"
	<TYPE> 3
	<RECT> 293 377 375 408
	<VALUE>
		8
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lagre
iconv -f CP949 -t UTF-8 <폴더>/…/lagre.txt
```
