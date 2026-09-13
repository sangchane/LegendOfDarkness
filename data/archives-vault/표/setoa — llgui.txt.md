---
파일: "llgui.txt"
아카이브: "setoa.dat"
줄수: 41
바이트: 650
인코딩: cp949
---

# llgui.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 41줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 566 436
	<IMAGE>
		"lsbackm.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "GUI"
	<TYPE> 7
	<RECT> 15 17 551 419
	<IMAGE>
		"lg_main.epf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "DESC"
	<TYPE> 7
	<RECT> 27 29 520 261
<ENDCONTROL>
<CONTROL>
	<NAME> "DESC_TITLE"
	<TYPE> 7
	<RECT> 27 29 520 41
<ENDCONTROL>
<CONTROL>
	<NAME> "DESC_CONTENT"
	<TYPE> 7
	<RECT> 27 45 520 261
<ENDCONTROL>
<CONTROL>
	<NAME> "STATUS"
	<TYPE> 7
	<RECT> 88 295 459 385
	<IMAGE>
		"lg_stat.epf" 0
		"lg_item.epf" 0
		"lg_skill.epf" 0
		"lg_spell.epf" 0
		"lg_chat.epf" 0
		"lg_stat2.epf" 0
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> llgui
iconv -f CP949 -t UTF-8 <폴더>/…/llgui.txt
```
