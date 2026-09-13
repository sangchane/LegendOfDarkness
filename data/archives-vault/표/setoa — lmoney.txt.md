---
파일: "lmoney.txt"
아카이브: "setoa.dat"
줄수: 31
바이트: 432
인코딩: cp949
---

# lmoney.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 31줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 226 117
	<IMAGE>
		"money.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 53 77 135 108
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
	<TYPE> 3
	<RECT> 139 77 221 108
	<VALUE>
		8
<ENDCONTROL>
<CONTROL>
	<NAME> "Text"
	<TYPE> 7
	<RECT> 17 53 203 69
<ENDCONTROL>
<CONTROL>
	<NAME> "Title"
	<TYPE> 7
	<RECT> 68 33 218 45
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lmoney
iconv -f CP949 -t UTF-8 <폴더>/…/lmoney.txt
```
