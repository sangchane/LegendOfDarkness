---
파일: "_noptdlg.txt"
아카이브: "setoa.dat"
줄수: 64
바이트: 996
인코딩: cp949
---

# _noptdlg.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 64줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 168 284
	<IMAGE>
		"_noptdlg.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "SoundRect"
	<TYPE> 7
	<RECT> 88 64 146 69
<ENDCONTROL>
<CONTROL>
	<NAME> "MusicRect"
	<TYPE> 7
	<RECT> 88 86 146 91
<ENDCONTROL>
<CONTROL>
	<NAME> "Tick"
	<TYPE> 7
	<RECT> 84 61 96 73
	<IMAGE>
		"option04.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Friends"
	<TYPE> 7
	<RECT> 9 103 75 119
	<IMAGE>
		"_noptbtn.spf" 4
		"_noptbtn.spf" 5
<ENDCONTROL>
<CONTROL>
	<NAME> "Macro"
	<TYPE> 7
	<RECT> 9 125 75 141
	<IMAGE>
		"_noptbtn.spf" 6
		"_noptbtn.spf" 7
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _noptdlg
iconv -f CP949 -t UTF-8 <폴더>/…/_noptdlg.txt
```
