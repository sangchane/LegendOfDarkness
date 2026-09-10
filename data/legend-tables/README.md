# `Legend.dat` 표 모음

원작 클라이언트가 **동작 구간·몬스터 행동·색**을 정할 때 보는 글자 표들이다. 그림이 아니라 규칙이고,
전부 합쳐 50KB이므로 여기 그대로 둔다.

## 왜 여기 있나

`Legend.dat`은 **이 작업공간의 게임 자료 폴더에 없다.** 개인 서버 배포본에는 안 들어 있고, 설치된
원작 클라이언트에만 있다.

```
C:\Program Files (x86)\KRU\Dark Ages\Legend.dat   (13MB)
C:\Program Files (x86)\KRU\Dark Ages\cious.dat    ( 8MB)   ← 마찬가지로 없음
```

13MB 아카이브를 통째로 들이는 대신, 실제로 쓰는 표만 뽑아 두었다. 그림(이야기 삽화 186장)과
음악(165곡)은 필요해질 때 위 경로에서 뽑는다.

## 다시 뽑는 법

```
dat-extract dump "C:/Program Files (x86)/KRU/Dark Ages/Legend.dat" <출력폴더> .tbl
```

## 무엇이 들어 있나

| 파일 | 내용 |
|---|---|
| `skill.tbl` | **스킬 동작 구간.** `NO` 스킬번호 · `FN` 직업 그림(b=1 성직·c=2 전사·d=3 도가·e=4 도적·f=5 마법) · `SI` 시작 칸 · `FC` 한 방향 프레임 수 · `ST` 쓸 수 있는 옷 번호. 한 스킬 = `SI..SI+FC-1`(등) + `SI+FC..SI+2FC-1`(앞) |
| `skill_e.tbl` `skill_i.tbl` | 같은 표의 다른 판본. `NO`·`FN`·`SI`·`FC`는 셋 다 같고 `ST`만 다르다 |
| `MobTile.tbl` | **몬스터 행동.** 서기·걷기·공격 프레임 수, 가만히 서 있을 수 있는지, 방향을 바꿀 수 있는지, 동작 간 지연. 107행 |
| `itempal.tbl` | 아이템 번호 구간 → 팔레트 번호 |
| `color.tbl` `color0.tbl` | 색 표 |
| `emo32~40.tbl` | 감정표현 표 (각 32바이트) |

파일은 모두 CP949 글자로 되어 있고, `;`로 시작하는 줄은 주석이다. **원작 개발자가 한글 주석을
남겨 두었으므로 해석은 파일 자체를 읽는 것이 가장 정확하다.**

읽는 법과 우리 코드에서 쓰는 방식: [`../../docs/original-sprite-animation.md`](../../docs/original-sprite-animation.md)
