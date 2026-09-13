---
파일: "lopt.txt"
아카이브: "setoa.dat"
줄수: 63
바이트: 971
인코딩: cp949
---

# lopt.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 63줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 172 302
	<IMAGE>
		"Nsetup05.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "CLOSE"
	<TYPE> 3
	<RECT> 73 257 155 288
	<VALUE>
		12
<ENDCONTROL>
<CONTROL>
	<NAME> "Friends"
	<TYPE> 7
	<RECT> 8 127 80 148
	<IMAGE>
		"option02.epf" 4
		"option02.epf" 5
<ENDCONTROL>
<CONTROL>
	<NAME> "Macro"
	<TYPE> 7
	<RECT> 8 152 80 173
	<IMAGE>
		"option02.epf" 6
		"option02.epf" 7
<ENDCONTROL>
<CONTROL>
	<NAME> "Setting"
	<TYPE> 7
	<RECT> 8 177 80 198
	<IMAGE>
		"option02.epf" 8
		"option02.epf" 9
<ENDCONTROL>
<CONTROL>
	<NAME> "ExitGame"
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lopt
iconv -f CP949 -t UTF-8 <폴더>/…/lopt.txt
```
