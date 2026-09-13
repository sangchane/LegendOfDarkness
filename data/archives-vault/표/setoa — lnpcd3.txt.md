---
파일: "lnpcd3.txt"
아카이브: "setoa.dat"
줄수: 120
바이트: 1884
인코딩: cp949
---

# lnpcd3.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 120줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 449 292
	<IMAGE>
		"nd_mback.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "TabPrev"
	<TYPE> 7
	<RECT> 147 34 165 50
	<IMAGE>
		"nd_mcp.spf" 0
		"nd_mcp.spf" 1
		"nd_mcp.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "TabNext"
	<TYPE> 7
	<RECT> 405 34 423 50
	<IMAGE>
		"nd_mcn.spf" 0
		"nd_mcn.spf" 1
		"nd_mcn.spf" 2
<ENDCONTROL>
<CONTROL>
	<NAME> "Tab0"
	<TYPE> 7
	<RECT> 165 34 225 50
	<IMAGE>
		"nd_mtab.spf" 0
		"nd_mtab.spf" 1
<ENDCONTROL>
<CONTROL>
	<NAME> "Tab1"
	<TYPE> 7
	<RECT> 225 34 285 50
	<IMAGE>
		"nd_mtab.spf" 0
		"nd_mtab.spf" 1
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lnpcd3
iconv -f CP949 -t UTF-8 <폴더>/…/lnpcd3.txt
```
