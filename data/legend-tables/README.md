# `Legend.dat` 표 모음

> **계열: Hades (7.18).** `data/` 에는 배포 팩(5.99·혼든) 자료도 있고 그쪽은 한글 표다 —
> 섞으면 어느 원작인지 알 수 없게 된다. `data/README.md` 참고.

원작 클라이언트가 **동작 구간·몬스터 행동·색**을 정할 때 보는 글자 표들이다. 그림이 아니라 규칙이고,
전부 합쳐 50KB이므로 여기 그대로 둔다.

## 왜 여기 있나

`Legend.dat`은 **저장소 안에 있다** — 게임 자료 폴더가 아니라 서버 저장소 쪽이다 (2026-09-10 정정,
그 전에는 "설치 클라이언트에만 있다"고 잘못 적혀 있었다).

```
sources/wren11/Dark-Ages-Private-Server/database/archives/legend/Legend.dat   (13MB)
```

안에 `.tbl` 16개 · 이야기 삽화 `.epf` 186장 · 음악 `.mp3` 165곡이 들어 있다. **표 16개는 여기 있는
것과 같은 것들이다** — 13MB를 매번 여는 대신 규칙 표만 뽑아 둔 것이다. 그림과 음악은 필요해질 때
위 경로에서 뽑는다:

```powershell
dat-extract list sources/wren11/Dark-Ages-Private-Server/database/archives/legend/Legend.dat
```

`cious.dat`(8MB)는 여전히 없고 설치 클라이언트에만 있다.

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
