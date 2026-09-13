---
파일: "gndattr.tbl"
아카이브: "seo.dat"
줄수: 112
바이트: 2997
인코딩: cp949
---

# gndattr.tbl

땅 속성(지나갈 수 있나 따위)으로 보이나 칸 뜻은 확인 못 했다.

아카이브: [[아카이브/seo|seo.dat]]

## 내용 (앞 40줄 / 전체 112줄)

```
/*
	ground tile attribute table :
	[ ATTR_gnd_paint : ( r, g, b, a ), h ]  
			: ( r, g, b, a )의 컬러로 지정된 ground tile위의 object의 높이 h 까지를 색칠한다.
*/
[ set_attr :
	[ ATTR_gnd_paint :
			( 11, 31, 39, 178 ), 20 ]
  apply_to :
	16503 16505 16507 16509 16511 16513
	( 16521, 16526 ) (16536, 16571 )
]
[ set_attr :
	[ ATTR_gnd_paint :
			( 52, 100, 212, 178 ), 10 ]
  apply_to :
	16659 16660 16663 16665 16667 16671 16680 16682 16685 16686 16697 16698
	16700 16702 ( 16707, 16710 ) 16715 16717 16719 16721 16728 16730 16732 16733 16735
]
[ set_attr :
	[ ATTR_gnd_paint :
			( 52, 100, 212, 178 ), 14 ]
  apply_to :
	( 16623, 16654 )
]
[ set_attr :
	[ ATTR_gnd_paint :
			( 0, 0, 0, 0 ), 1 ]
  apply_to :
	( 16977, 17008 ) ( 17021, 17028 ) 17041 17045 17049 17053 17060 17064 17068 17072 17085 17089
	17108 ( 17136, 17140 ) 17143 17144 17147 17148 17152 17156 17160 ( 17163, 17169 ) 17173 17174 17177 17178
	( 17181, 17190 ) 17193 17197
]
[ set_attr :
	[ ATTR_gnd_paint :
			( 0, 0, 0, 0 ), 1 ]
  apply_to :
	( 756, 763 ) ( 765, 768 )
]
[ set_attr :
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/seo/seo.dat <폴더> gndattr
iconv -f CP949 -t UTF-8 <폴더>/…/gndattr.tbl
```
