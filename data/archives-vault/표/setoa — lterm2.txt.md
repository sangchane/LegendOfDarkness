---
파일: "lterm2.txt"
아카이브: "setoa.dat"
줄수: 20
바이트: 304
인코딩: cp949
---

# lterm2.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 20줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 640 480
	<IMAGE>
		"term.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Main"
	<TYPE> 7
	<RECT> 80 117 560 369
<ENDCONTROL>
<CONTROL>
	<NAME> "Exit"
	<TYPE> 7
	<RECT> 509 417 601 461
	<IMAGE>
		"termexit.spf" 0
		"termexit.spf" 1
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lterm2
iconv -f CP949 -t UTF-8 <폴더>/…/lterm2.txt
```
