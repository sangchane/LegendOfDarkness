---
파일: "staff.tbl"
아카이브: "national.dat"
줄수: 19
바이트: 284
인코딩: cp949
---

# staff.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/national|national.dat]]

## 내용 (앞 40줄 / 전체 19줄)

```
제작}
(주) 넥슨}
어둠의전설팀}
}
개발년도}
1997년 10월 어둠의전설 발표}
1998년 01월 어둠의전설 상용화}
}
게임문의}
넥슨고객지원센터}
국번없이 1588-7702번}
}
이용문의,정액관련 도우미 이메일}
doumi@nexon.co.kr}
}
게임이용관련 도우미 이메일}
lod-help@nexon.co.kr}
}
#
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/national/national.dat <폴더> staff
iconv -f CP949 -t UTF-8 <폴더>/…/staff.tbl
```
