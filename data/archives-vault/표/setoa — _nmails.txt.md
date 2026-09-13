---
파일: "_nmails.txt"
아카이브: "setoa.dat"
줄수: 57
바이트: 786
인코딩: cp949
---

# _nmails.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 57줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 581 290
	<IMAGE>
		"_nmails.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Receiver"
	<TYPE> 7
	<RECT> 96 13 318 25
	<COLOR>
		20
		255
<ENDCONTROL>
<CONTROL>
	<NAME> "Title"
	<TYPE> 7
	<RECT> 96 37 484 53
	<COLOR>
		255
		31
<ENDCONTROL>
<CONTROL>
	<NAME> "Content"
	<TYPE> 7
	<RECT> 21 69 501 273
	<COLOR>
		20
		31
<ENDCONTROL>
<CONTROL>
	<NAME> "Send"
	<TYPE> 7
	<RECT> 507 61 568 83
	<IMAGE>
		"_nbtn.spf" 30
		"_nbtn.spf" 31
		"_nbtn.spf" 32
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nmails
iconv -f CP949 -t UTF-8 <폴더>/…/_nmails.txt
```
