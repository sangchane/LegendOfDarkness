# 원작 UI — 5.01 이전(4.51) 과 그 뒤가 어떻게 다른가

사용자가 원하는 테마는 **5.01 이전**이다. 우리가 가진 5.01 이전 판은 **4.51(2001-12-07)** 하나다.

## 판 번호는 어디서 읽나

`Legend.dat` 안 `version.nfo` 24바이트가 그대로 적어 준다.

```
dt : 451
pg : 451
ed
```

그러니 판 번호는 `451` 꼴이고, "5.01" 은 `501` 이다. 우리가 가진 설치본은 **451** 과 **2005**(= 5.99 클라이언트,
md5 `347dc381…d128` 로 같은 파일 — `docs/disassembly.md`) 둘뿐이다. 501 판 자체는 없다.

## 5.01 앞뒤로 UI 체계가 통째로 갈린다

| | **4.51 (5.01 이전)** | **2005 (5.01 이후)** |
|---|---|---|
| 화면이 어디 있나 | `Legend.dat` 안 **EPF 그림** | `setoa.dat` 안 **`_n*.txt` 130장 + `.spf` 255장** |
| 배치를 정하는 것 | 실행 파일에 박혀 있다 | 표가 글로 적어 둔다 |
| 창 하나의 모양 | **통짜 그림 한 장** (`equip01.epf` = 장비창 전체) | 조각을 좌표로 쌓는다 |
| 그림 형식 | `.epf` (팔레트가 밖에 있다 — `legend.pal`) | `.spf` (팔레트를 제 안에 지고 다닌다) |

2005 판의 `_n*.txt` 는 이런 꼴이다 — 이름·종류·**픽셀 좌표**·그림 파일을 적은 선언형 배치표다.

```
<CONTROL>
    <NAME> "TAB_INTRO"
    <TYPE> 7
    <RECT> 576 23 638 47
    <IMAGE>  "_nui_tb2.spf" 0
<ENDCONTROL>
```

**4.51 에는 이 표가 없다.** `setoa.dat` 자체가 없다(`Legend.dat` · `seo.dat` · `khan.dat` 셋뿐). 그래서 4.51 UI 는
"창 한 개 = 그림 한 장"이고, 그 안의 칸 자리는 실행 파일이 알고 있다.

이것이 사용자가 말한 "**5.01 이후로 확 달라진다**" 의 정체다. 그림만 바뀐 게 아니라 **UI 를 만드는 방식이 바뀌었다.**

## 4.51 에 들어 있는 화면 (92장, `docs/ui/original-451/`)

한눈에 보기: `docs/ui/assets/original-451-contact-sheet.png`

| 무엇 | 파일 |
|---|---|
| 아래 상태바 (STR·INT·WIS·CON·DEX / HP·MP·EXP·GOLD·LEV / 무기·갑옷) | `stat001` · `statcon` |
| 체력·마력 구슬 (차오르는 칸 여러 장) | `orb001` · `orb002` |
| 장비창·인물창 | `equip01` ~ `equip06` |
| 소지품·기술·마법 아이콘판 | `Item001` · `item002`~`item007` · `skill001` · `spell001` · `spelled` |
| 로그인·비밀번호·서버 고르기 | `dlglogin` · `dlgpass` · `dlgpass1` · `svrdlg` · `svrbtn` |
| 캐릭터 만들기 | `dlgcre00` ~ `dlgcre03` · `crehlp.txt`(도움말 글) |
| 말풍선·시스템 말 | `msgtop` · `msgmid` · `msgbot` · `msgsm` · `sysmsg` |
| 상인창 | `mertop` · `mermid` · `merbot` |
| 교환·돈·우편·친구·길드 | `exchange` · `money` · `mail` · `friend` · `gset01` · `gbicon01` · `gbicon02` |
| 설정·단축키·상용구·도움말 | `option01`~`04` · `setup01`~`06` · `Nsetup02·03·05` · `macro01` · `help` · `helpbtn1·2` · `question` |
| 단추·틀·자잘한 것 | `butt001` · `buttonex` · `btn220` · `menubtn` · `menuok` · `menucncl` · `townBtn` · `panel01·02` · `line001` · `scroll` · `mouse` |
| 월드맵·사람 목록 | `lodmap` · `lodusr` · `tmuser` · `users01`~`04` · `nation`(문장 7개) |
| 게시판 | `dlgbbs01`~`03` · `dlgback` · `dlgbg001` |
| 제목·연출 | `legend` · `legends` · `legendi` · `levelup` · `portrait` · `clock01` · `woodbk` · `staff` |

글꼴도 함께 있다 — `han00.fnt` · `han01.fnt`(한글, 각 57,624바이트) · `eng00.fnt` · `eng01.fnt`.

## 4.51 UI 가 쓰는 색

창·단추·말풍선 14장에서 실제로 센 것(`legend.pal` 로 그린 뒤, 게임이 안 그리는 0번 칸 `#141720` 은 뺐다).
**140가지밖에 안 쓴다.**

창은 **밝은 돌 틀 + 어두운 내용 칸** 두 겹이다. 이 구분이 색 이름보다 중요하다 — 글자는 늘 밝은 돌 위의 검정(음각)이거나
어두운 칸 위의 밝은 글씨다.

| 쓰임 | 색 | 몫 |
|---|---|---|
| **밝은 돌 틀**(창 테두리·단추 얼굴) | `#97978b` · `#837b6f` · `#abab9f` · `#636357` | 17% |
| 틀의 밝은 모서리 | `#979797` · `#878787` · `#777777` | 12% |
| **어두운 내용 칸**(글자·목록이 들어가는 안쪽) | `#333333` · `#1f1f1f` · `#434343` | 31% |
| 그림자·음각 | `#030303` · `#0f0f0f` · `#171723` · `#232333` | 18% |
| 올리브 | `#4b4f43` | 4% |

색조가 **회색–흙빛 한 갈래**다. 파랑·초록은 아이콘(`nation` 문장, `orb002` 마력 구슬)에만 있고 창에는 없다.

## 이 테마로 무엇을 만들기로 했나 (2026-09-18)

사용자 결정: **5.01 이전(4.51) 테마**, 애플·토스급 직관성. 3안을 그려 보고 **안 C**(틀은 원작, 속은 깨끗하게)를 골랐다.

- **시안(눌러볼 것):** `docs/ui/mockups-451/index.html` — 세로 게임 화면 3상태 · 장비창 · 소지품 · NPC 대화 · 상점 · 대화 창 · 로그인
- 3안 비교: `docs/ui/mockups-451/three-ways.html`
- 사진: `docs/ui/assets/mockups-451-adopted.png`(전체) · `-three-ways.png`(3안) · `-fight.png` · `-gear.png` · `-pack.png`
- **단일 출처: `data/original-ui/451.json`** — 판·아카이브·화면 92·재질·색·치수·규칙·시안·구현대상.
  새로 알게 된 것은 여기에 적고 볼트·그래프를 다시 만든다
- **볼트(Obsidian): `data/ui-vault/`** — `python scripts/build-ui-vault.py` (저장소에 들어 있다 — 그냥 연다)
- **그래프: `python scripts/build-ui-graph.py`** → `data/original-ui/graph/graph.html` (무시 목록).
  묻는 법: `graphify query "체력 마력 아이콘 왜 납작하게" --graph data/original-ui/graph/graph.json --budget 700`

### 규칙 (구현이 따를 것)

| | 무엇 | 왜 |
|---|---|---|
| **돌이 두 가지다** | **어두운 돌** `dlgback.epf` 평균 `rgb(40,36,38)` → 넓은 면(창 틀·제목줄)<br>**밝은 돌** `msgsm.epf` 평균 `rgb(125,119,108)` → 작은 면(확정 단추·공격 버튼·고른 탭) | 원작도 그렇게 나눠 쓴다. **밝은 돌에 작은 글자를 넓게 얹으면 먼저 무너진다** — 시안에서 실제로 안 읽혔다 |
| 글자 색은 돌이 정한다 | 어두운 돌 위 → 밝은 글자 `#abab9f` + 그림자 · 밝은 돌 위 → 음각 `#100f0b` + 아래 흰 선 1px | 음각은 밝은 돌에서만 성립한다 |
| 돌을 안 쓰는 곳 | 목록 · 격자 칸 · 입력칸 · 보조 단추 · 방향판 | 무늬 위 글자는 작은 화면에서 먼저 무너진다 |
| 글꼴 | 제목줄·확정 단추만 세리프, 나머지 산세리프, **숫자는 고정폭** | 자릿수가 흔들리면 값이 안 읽힌다 |
| 속 | `#0f0f0f` 96% · 칸 `#1f1f24` · 테두리 `#303036` | 금빛 바닥 위에서 글자가 살아남는 값 |
| 체력·마력 색 | `#c8783c` · `#5a6fa8` — 원작 구슬에서 뽑았다 | 창에는 색이 없어 강조색은 구슬에서만 나온다 |
| 체력·마력 아이콘 | **납작한 원 하나(18px), 그림 파일 없음** | 원작 구슬(86×85 · 16단계)은 18px 로 줄이면 뭉갠다 (사용자 지시: 더 심플하게) |
| 치수 | 틀 5 · 속 12 · 틈 8 · 누르는 곳 44 이상 · 모서리 12(속에만) | 돌 틀은 각져야 원작처럼 보인다 |
| **체력 표시를 두 번 그리지 않는다** | 머리 위 = 백분율 막대(맞을 때만, `HealthBar.cs`) · 위 판 = 정확한 숫자(늘) | 맥이 머리 위 막대를 넣었으므로(`2faede52`) 위 판의 긴 막대를 뺐다 |

**배치는 하나도 바꾸지 않는다.** 지금 클라이언트가 잡아 둔 자리(위 상태판 · 기록 줄 · 방향판 · 공격 둘레 부채꼴 ·
아래에 붙는 창 · 18자리 고리)를 그대로 쓴다. 그래서 손대는 곳은 `mobile/client/src/Greybox.cs` 와 그것을 부르는 곳들이다.

### 시안이 쓰는 재료 (`docs/ui/mockups-451/assets/`)

`stone.png`(밝은 돌) · `stone-dark.png`(어두운 돌) · `wood.png` · `orb-hp/mp-full|mid.png`(원작 구슬, 참고용) ·
`slot0`~`slot13.png`(장비 빈 자리 14개) · `item0`~`item11.png` · `doll.png` · `crest.png` · `world.png`(바탕).

---

## 다시 뽑는 법

설치본 `sources/lodr4.51.exe` 는 InstallShield 6 이라 일반 압축 도구로 안 열린다.

```bash
bz x -y sources/lodr4.51.exe                      # → Disk1/data1.cab
docker run --rm -v <폴더>:/w debian:bookworm-slim \
  sh -c "apt-get update && apt-get install -y unshield && cd /w/Disk1 && unshield -d /w/out x data1.cab"
# → out/Program_Executable_Files/{Legend,seo,khan}.dat
```

그림은 저장소 도구로 그린다. **`dat-extract` 는 net8 로 빌드돼 있는데 여기 런타임은 net9 뿐이라
`DOTNET_ROLL_FORWARD=Major` 가 필요하다.**

```bash
export DOTNET_ROLL_FORWARD=Major
DOTNET=.tools/dotnet-9.0.317/dotnet
TOOL=tools/dat-extract/bin/Release/net8.0/dat-extract.dll
$DOTNET $TOOL list Legend.dat .epf                          # 이름 전부
$DOTNET $TOOL epf  Legend.dat equip01 out.png 12 1 legend.pal   # 그리기 (팔레트를 반드시 준다)
```

**EPF 는 팔레트를 제 안에 안 지고 있다** — UI 는 `legend.pal` 이다. 안 주면 색이 엉뚱하게 나온다.

## 아직 안 본 것

- `seo.dat` 의 `.hpf` 12,702장(바닥 타일) · `khan.dat` 의 EPF 6,527장(사람 그림) — UI 아니라 안 봤다
- 창 안의 **칸 자리**(어느 픽셀이 장비 한 칸인가)는 그림에 안 적혀 있다. 4.51 `Legend.exe` 를 더 읽어야 한다
  (`docs/disassembly.md` 의 4.51 주소들이 출발점)
- `lod.lft`(3.4MB) · `dlgframe.upf` 가 무엇인지 안 봤다
- 7.18(`sources/DarkAges718single.exe`) · 7.41(LOD_ 쪽) 은 안 풀었다 — 5.01 이후라 이번 테마와 무관
