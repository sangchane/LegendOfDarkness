---
파일: "lpopup.txt"
아카이브: "setoa.dat"
줄수: 27
바이트: 390
인코딩: cp949
---

# lpopup.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 27줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 82 68
	<IMAGE>
		"popupbox.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Box0"
	<TYPE> 6
	<RECT> 4 4 78 18
<ENDCONTROL>
<CONTROL>
	<NAME> "Box1"
	<TYPE> 6
	<RECT> 4 22 78 36
<ENDCONTROL>
<CONTROL>
	<NAME> "Box2"
	<TYPE> 6
	<RECT> 4 36 78 50
<ENDCONTROL>
<CONTROL>
	<NAME> "Box3"
	<TYPE> 6
	<RECT> 4 50 78 64
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lpopup
iconv -f CP949 -t UTF-8 <폴더>/…/lpopup.txt
```
