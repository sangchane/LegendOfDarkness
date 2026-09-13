---
파일: "luser.txt"
아카이브: "setoa.dat"
줄수: 53
바이트: 823
인코딩: cp949
---

# luser.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 53줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 460 303
	<IMAGE>
		"users01.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "CountryBtn"
	<TYPE> 7
	<RECT> 295 61 367 79
	<IMAGE>
		"users04.epf" 0
		"users04.epf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "MasterBtn"
	<TYPE> 7
	<RECT> 295 83 367 101
	<IMAGE>
		"users04.epf" 2
		"users04.epf" 3
<ENDCONTROL>
<CONTROL>
	<NAME> "Close"
	<TYPE> 3
	<RECT> 373 268 455 299
	<VALUE>
		12
<ENDCONTROL>
<CONTROL>
	<NAME> "TotalNum"
	<TYPE> 7
	<RECT> 378 39 440 51
<ENDCONTROL>
<CONTROL>
	<NAME> "CountryNum"
	<TYPE> 7
	<RECT> 378 65 440 77
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> luser
iconv -f CP949 -t UTF-8 <폴더>/…/luser.txt
```
