---
파일: "MobTile.tbl"
아카이브: "Legend.dat"
줄수: 195
바이트: 6310
인코딩: cp949
---

# MobTile.tbl

**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다.

아카이브: [[아카이브/Legend|Legend.dat]]

## 내용 (앞 40줄 / 전체 195줄)

```
;NO		: Server 쪽에서 오는 몬스터의 번호
;
;Sgs		: 그림 파일이 몇 개의 세그먼트로 이루어져 있는가
;
;SFCnt		: 서기 동작의 프레임 수
;
;WFCnt		: 걷기 동작의 프레임 수
;
;AFCnt		: 공격 동작의 프레임 수
;
;fStop		: 해당 몬스터가 가만이 서 있을 수 있는가?
;			(말벌 같은 것은 계속 날개짓을 해야하므로 fStop이 0이 되어야 한다.)
;
;fChgDir	: 해당 몬스터가 방향을 전환할 수 있는가?
;			(보통의 NPC는 한 방향만 보고 있으므로 0으로 세팅하면 된다.)
;
;SMDly		: 계속 움직여야 하는 동물들의 각 동작사이의 Delay
;
;BMDlyMn	: NPC들의 동작이 이루어지는 간격 (Min)
;			(쉴새 없이 움직이는 놈들은 이 값을 SMDly와 같게 해 준다.)
;
;BMDlyMx	: NPC들의 동작이 이루어지는 간격 (Max)
;			(쉴새 없이 움직이는 놈들은 이 값을 SMDly와 같게 해 준다.)
;
;			위의 두 값, BMDlyMn과 BMDlyMx 사이에서 동작간 Delay가 Random하게 결정된다.
;
;SMCnt		: 제자리 동작의 프레임 수 (예를 들어 쌍절곤을 계속해서 돌리는 놈은 계속 돌리는
;		  동작에 두 프레임이 소요되므로 2로 써 주면 된다.)
;		  보통은 0으로 써 주기 바란다. 0으로 써 주면 동작 프레임 전체를 계속적으로 반복하며
;		  0이 아닌 숫자를 써 주면 써 준 숫자만큼의 프레임만을 반복하다가 가끔씩 남은 프레임
;		  으로 에니메이션된다.
;SMFR		: SMCnt에 지정된 프레임에서 다른 프레임으로 넘어가는 비율을 정한다. 예를 들어
;		  쌍절곤 돌리는 놈에게 SMFR을 10으로 지정하면 10번에 한 번 정도 꼴로 자기 머리를
;		  친다. (SMFR은 Stop Motion Failure Ratio의 약자이다.)
;NO	Sgs	SFCnt	WFCnt	AFCnt	fStop	fChgDir	SMDly	BMDlyMn	BMDlyMx	SMCnt	SMFR
1	1	2	2	2	0	1	1000	1000	1000	0	0
2	1	1	4	2	1	1	0	0	0	0	0
3	1	5	5	2	0	0	1000	1000	1000	0	0
4	1	2	2	2	0	1	1000	1000	1000	0	0
5	1	1	2	2	1	1	0	0	0	0	0
```

## 꺼내는 법

```bash
dotnet run --project tools/dat-extract -c Release -- \
  dump sources/wren11/Dark-Ages-Private-Server/database/archives/legend/Legend.dat <폴더> MobTile
iconv -f CP949 -t UTF-8 <폴더>/…/MobTile.tbl
```
