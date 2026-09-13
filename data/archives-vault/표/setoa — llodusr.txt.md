---
파일: "llodusr.txt"
아카이브: "setoa.dat"
줄수: 28
바이트: 414
인코딩: cp949
---

# llodusr.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 28줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 298 71
	<IMAGE>
		"lodusr.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Head"
	<TYPE> 7
	<RECT> 17 35 22 44
	<IMAGE>
		"loading0.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Body"
	<TYPE> 7
	<RECT> 22 35 277 44
	<IMAGE>
		"loading1.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Tail"
	<TYPE> 7
	<RECT> 277 35 282 44
	<IMAGE>
		"loading2.epf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llodusr
iconv -f CP949 -t UTF-8 <폴더>/…/llodusr.txt
```
