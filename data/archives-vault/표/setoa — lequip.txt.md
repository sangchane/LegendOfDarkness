---
파일: "lequip.txt"
아카이브: "setoa.dat"
줄수: 211
바이트: 3146
인코딩: cp949
---

# lequip.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 211줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 266 298
	<IMAGE>
		"equip01.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "WEAPON"
	<TYPE> 7
	<RECT> 50 169 83 202
	<IMAGE>
		"equip07.epf" 6
<ENDCONTROL>
<CONTROL>
	<NAME> "ARMOR"
	<TYPE> 7
	<RECT> 50 130 83 163
	<IMAGE>
		"equip07.epf" 3
<ENDCONTROL>
<CONTROL>
	<NAME> "SHIELD"
	<TYPE> 7
	<RECT> 183 169 216 202
	<IMAGE>
		"equip07.epf" 7
<ENDCONTROL>
<CONTROL>
	<NAME> "HEAD"
	<TYPE> 7
	<RECT> 116 81 148 113
	<IMAGE>
		"equip07.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "EAR"
	<TYPE> 7
	<RECT> 65 91 98 124
	<IMAGE>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lequip
iconv -f CP949 -t UTF-8 <폴더>/…/lequip.txt
```
