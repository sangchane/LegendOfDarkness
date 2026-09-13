---
파일: "lgcpane.txt"
아카이브: "setoa.dat"
줄수: 12
바이트: 177
인코딩: cp949
---

# lgcpane.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 12줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 103 19
	<IMAGE>
		"gc_pane2.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TITLE"
	<TYPE> 7
	<RECT> 3 3 100 16
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lgcpane
iconv -f CP949 -t UTF-8 <폴더>/…/lgcpane.txt
```
