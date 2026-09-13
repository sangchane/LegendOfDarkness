---
파일: "lshopba2.txt"
아카이브: "setoa.dat"
줄수: 83
바이트: 1224
인코딩: cp949
---

# lshopba2.txt

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 83줄)

```
<CONTROL>
	<NAME> "Noname"
	<TYPE> 0
	<RECT> 0 0 424 259
	<IMAGE>
		"sb_back2.spf" 0
<ENDCONTROL>
<CONTROL>
	<NAME> "Get"
	<TYPE> 3
	<RECT> 32 217 93 239
	<IMAGE>
		"_nbtn.spf" 39
		"_nbtn.spf" 40
		"_nbtn.spf" 41
	<VALUE>
		0
<ENDCONTROL>
<CONTROL>
	<NAME> "Close"
	<TYPE> 3
	<RECT> 330 217 391 239
	<IMAGE>
		"_nbtn.spf" 36
		"_nbtn.spf" 37
		"_nbtn.spf" 38
	<VALUE>
		0
<ENDCONTROL>
<CONTROL>
	<NAME> "Prev"
	<TYPE> 3
	<RECT> 113 219 186 234
	<IMAGE>
		"sb_prev2.spf" 0
		"sb_prev2.spf" 1
		"sb_prev2.spf" 2
	<VALUE>
		0
<ENDCONTROL>
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> lshopba2
iconv -f CP949 -t UTF-8 <폴더>/…/lshopba2.txt
```
