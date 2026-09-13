---
파일: "lmail.txt"
아카이브: "setoa.dat"
줄수: 88
바이트: 1141
인코딩: cp949
---

# lmail.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 88줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 616 308
	<IMAGE>
		"dlgbbs02.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Prev"
	<TYPE> 3
	<RECT> 517 14 599 45
	<VALUE>
		4
<ENDCONTROL>
<CONTROL>
	<NAME> "Next"
	<TYPE> 3
	<RECT> 517 48 599 79
	<VALUE>
		11
<ENDCONTROL>
<CONTROL>
	<NAME> "New"
	<TYPE> 3
	<RECT> 517 82 599 113
	<VALUE>
		3
<ENDCONTROL>
<CONTROL>
	<NAME> "Reply"
	<TYPE> 3
	<RECT> 517 116 599 147
	<VALUE>
		10
<ENDCONTROL>
<CONTROL>
	<NAME> "Delete"
	<TYPE> 3
	<RECT> 517 150 599 181
	<VALUE>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lmail
iconv -f CP949 -t UTF-8 <폴더>/…/lmail.txt
```
