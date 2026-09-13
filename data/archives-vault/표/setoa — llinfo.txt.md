---
파일: "llinfo.txt"
아카이브: "setoa.dat"
줄수: 294
바이트: 4436
인코딩: cp949
---

# llinfo.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 294줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"lback.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "MainTab"
	<TYPE> 7
	<RECT> 10 35 60 455
<ENDCONTROL>
<CONTROL>
	<NAME> "TabLegend"
	<TYPE> 7
	<RECT> 19 52 54 81
	<IMAGE>
		"lbtn01.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TabSpell"
	<TYPE> 7
	<RECT> 19 83 54 112
	<IMAGE>
		"lbtn11.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TabSkill"
	<TYPE> 7
	<RECT> 19 114 54 143
	<IMAGE>
		"lbtn21.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TabEvent"
	<TYPE> 7
	<RECT> 19 145 54 174
	<IMAGE>
		"lbtn31.epf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llinfo
iconv -f CP949 -t UTF-8 <폴더>/…/llinfo.txt
```
