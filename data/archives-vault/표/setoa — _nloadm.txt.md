---
파일: "_nloadm.txt"
아카이브: "setoa.dat"
줄수: 28
바이트: 415
인코딩: cp949
---

# _nloadm.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 28줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 310 87
	<IMAGE>
		"_nloadm.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Head"
	<TYPE> 7
	<RECT> 23 44 28 53
	<IMAGE>
		"_nloadb0.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Body"
	<TYPE> 7
	<RECT> 28 44 283 53
	<IMAGE>
		"_nloadb1.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Tail"
	<TYPE> 7
	<RECT> 283 44 288 53
	<IMAGE>
		"_nloadb2.spf" 0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nloadm
iconv -f CP949 -t UTF-8 <폴더>/…/_nloadm.txt
```
