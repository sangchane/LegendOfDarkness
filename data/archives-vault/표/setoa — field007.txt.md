---
파일: "field007.txt"
아카이브: "setoa.dat"
줄수: 27
바이트: 713
인코딩: cp949
---

# field007.txt

원작 월드맵 한 장. 첫 줄이 팔레트, 그 뒤로 `이름 그림키 x y [EX x2 y2 번호]`.

아카이브: [[아카이브/setoa|setoa.dat]]

## 내용 (앞 40줄 / 전체 27줄)

```
fielde00.pal
ENDOFPALETTE
f00bm			f00bw		4
아벨			f001		324	269
밀레스			f002		389	190
동의우드랜드		f003		516	176
서의우드랜드		f004		155	171
남의우드랜드		f005		398	365
북의우드랜드		f006		255	96
피에트			f007		533	212
수오미			f008		345	112
운디네			f009		474	123
루어스마을		f010		102	232
뤼케시온		f011		317	398
백작부인의별장		f012		445	338
드라큐라의성		f013		148	366
루어스대평원		f014		78	155
야외배틀필드		f015		241	186
마인			f016		137	113
용자의공원		f017		384	245
타고르			f018		188	57
호엔			f019		331	209
로톤			f020		478	208
죽음의마을		f021		142	320
크리스마스마을		f022		257	33	f022a.epf	254	12	400	0
시장			f023		159	211
놀이동산		f024		374	336	f024a.epf	346	311	200	0
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat <폴더> field007
iconv -f CP949 -t UTF-8 <폴더>/…/field007.txt
```
