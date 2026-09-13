---
파일: "lssbook.txt"
아카이브: "setoa.dat"
줄수: 60
바이트: 873
인코딩: cp949
---

# lssbook.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 60줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
<ENDCONTROL>
<CONTROL>
	<NAME> "TopImage"
	<TYPE> 7
	<RECT> 0 0 246 89
	<IMAGE>
		"sstop.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "MidImage"
	<TYPE> 7
	<RECT> 0 89 246 114
	<IMAGE>
		"sstext.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "BotImage"
	<TYPE> 7
	<RECT> 0 114 246 159
	<IMAGE>
		"ssbottom.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Text"
	<TYPE> 7
	<RECT> 26 91 222 107
<ENDCONTROL>
<CONTROL>
	<NAME> "OK"
	<TYPE> 3
	<RECT> 12 116 94 147
	<VALUE>
		6
<ENDCONTROL>
<CONTROL>
	<NAME> "Cancel"
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lssbook
iconv -f CP949 -t UTF-8 <폴더>/…/lssbook.txt
```
