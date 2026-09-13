---
파일: "_nui_sk.txt"
아카이브: "setoa.dat"
줄수: 17
바이트: 257
인코딩: cp949
---

# _nui_sk.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 17줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 599 306
	<IMAGE>
		"_nui_sk.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "SPELL"
	<TYPE> 7
	<RECT> 32 33 265 272
<ENDCONTROL>
<CONTROL>
	<NAME> "SKILL"
	<TYPE> 7
	<RECT> 331 33 564 272
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> _nui_sk
iconv -f CP949 -t UTF-8 <폴더>/…/_nui_sk.txt
```
