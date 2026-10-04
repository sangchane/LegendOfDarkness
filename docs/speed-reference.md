# 속도 기준표 — 원작·영상·우리

5.99 원작 클라이언트 `Legend.exe`(PE32, 패킹 없음, RTTI 이름 남아 있음)를 정적 분석해 걷기·몸 동작·이펙트·게임 틱의 속도 상수를 주소와 함께 적은 표다. 영상 칸은 유튜브 영상 검산 값이다(아래 「영상 검산」).

분석 대상: `~/Downloads/5.99 클라이언트/Legend.exe` (3,555,391바이트, ImageBase 0x400000, .text 엔트로피 6.45 — UPX 등 패킹 없음).
도구: llvm `objdump -d -M intel --print-imm-hex`, pefile. 주소는 모두 가상주소(VA)다.

## 표

| 항목 | 원작 값 | 출처 (exe VA / 저장소) | 영상 | 우리 값 |
|---|---|---|---|---|
| **걷기 한 칸 — 남(서버 `0x0C`)** | 한 틱 **100ms** × **5틱 = 500ms**. 틱 1~4에 반 타일(가로 28·세로 14px)을 **4번**에 나눠 옮기고(가로 7·7·7·7, 세로 3·4·4·3), 5번째 틱에 타일 좌표를 확정한다. **확정** | `0x54ab5a push 0x64` → vfunc+0xA0(`HumanPane 0x4e2784`·`LivingObjectPane 0x51bed0`) → `0x4e27f7` 타이머 등록(지연=100) → 콜백 `0x4e1662` `(프레임+1) % [obj+0x224]`, `[obj+0x224]=5`(`0x4e366f`). 픽셀 표 `0x6a07e0`/`0x6a08e0` 4단계 행 | 따로 재지 못함(파티원이 같이 걸어 구분 안 됨) | `StepSeconds = 0.44`s (`Tuning.cs:13`) — 남·나 구분 없음 |
| **걷기 한 칸 — 나(내 캐릭터)** | 한 틱 **114ms**(`[0x6aace4]=114`, 쓰는 곳 없음 = 상수) × 4틱 = **456ms** + 첫 지연 `[0x776a0c]`. 4단계(설정 `[cfg+0x538]>0`이면 8단계·틱 57ms, 합은 같음). 값 114는 **확정**, 첫 지연이 0이라는 것은 **추정**(BSS, 쓰는 코드를 못 찾음) | `0x5c8f7a mov eax,[0x6aace4]` → `/(단계수/4)` → `0x5c9011` 등록(지연=[0x776a0c], p2=114) → 콜백 `0x5c941a` 재등록 지연=p2(`0x5c95d5`). 단계→그림 표 `0x6aa5a0` 4단계 행 = 1,2,3,4,(0=확정) | **0.41~0.44s** (옛 클라이언트 두 영상, 화면 밀림으로 잼) — 원작 0.456s와 맞음 | 위와 같음 0.44s (원작 0.456s와 3.5% 차이) |
| **몸 동작 속도 해석 (`0x1A`)** | **속도 × 10 = 한 프레임 ms** (속도 단위 1/100초, **한 프레임 간격**이지 전체 길이가 아니다). 프레임은 `(i+1) % N` 으로 돌고 0으로 돌아오면 끝 → 실제로 보이는 그림은 1..N−1, 전체 ≈ (N−1)×속도×10ms. N: 평타(동작 1)=3, 동작 6=2, 동작 0x15=3, 동작 0x16=3(이것만 간격 ÷3), 128+ 은 skill.tbl 줄의 프레임 수. **확정** | `0x54ac33 movsx edx,[pkt+0x18]` → `0x54ac37~0x54ac3e` ×10 → vfunc+0xA4 `HumanPane 0x4e1020` → 등록(지연 0, p1=속도×10, p2=N) → 콜백 `0x4e153d`·재등록 지연=p1 `0x4e15e6`. ÷3: `0x4e10d5 imul 0x55555556`. 몬스터(`MonsterPane2 0x59e570`)는 **패킷 속도를 무시**하고 동작 1만, 한 프레임 **300ms 고정**(`0x59e95d mov esi,0x12c`) | 못 잼(영상 압축·겹침으로 프레임 구분 불가) | `speed/100/Count` = **전체 길이**로 해석(`BodyMotion.cs:125`) ÷0.7 ×`MotionSlowdown=1.1`(`Tuning.cs:19`). 예: 하데스 평타 30 → 우리 0.236s/그림, 원작 0.300s/그림. 0x16의 ÷3 은 우리 `Faster: 3`(`BodyMotion.cs:63`)과 일치 |
| **이펙트 속도 (`0x29`)** | 속도 = **한 프레임 ms 그대로**(×10 없음). 단, 효과 자료에 자기 값(`[entry+0x28]`)이 있으면 그것이 **패킷 속도를 덮어쓴다**. 프레임 = (이펙트 시계 − 시작) / 속도, 이펙트 시계는 **50ms** 마다 갱신 → 실제 해상도 50ms. 해석 **확정**, `[entry+0x28]`이 EFA 머리의 프레임 간격이라는 것은 **추정** | `0x54acec movsx edi,[pkt+0x1e]` → `0x54b090` → 생성자 `0x49e924 mov [obj+0x200],esi` → `0x49db79 call 0x49c7c0`(0이 아니면 `0x49db82` 덮어씀) → 등록 지연=속도 `0x49dc30` → 콜백 `0x49e9e2~0x49e9ed` `(시계−시작)/속도`. 시계 `0x49ee9b`, 50ms `0x49eea4 push 0x32` | 못 잼(영상 압축·겹침으로 프레임 구분 불가) | `Clamp(speed,30,300)/1000` s/프레임(`EffectSheet.cs:32`) — 단위는 원작과 같음. 자료 값 덮어쓰기·50ms 계단은 없음 |
| **게임 틱** | **고정 프레임 틱 없음** — 시각순 타이머 큐 하나(`[0x734e54]`)로 돈다. 매 회 `timeGetTime`→`[mgr+0x30]`, 다음 일까지 20ms 넘게 남으면 `Sleep(5)`, 한 번에 최대 40개 처리. 큐 구조·Sleep 은 **확정**, "그리기도 이 큐에 묶임"은 **추정**(전용 그리기 주기를 못 찾음) | `0x4acd6b call timeGetTime` / `0x4acd77 lea ecx,[eax+0x14]` / `0x4acd7f push 5; call Sleep` / `0x4acdc9 mov ebx,0x28` | 못 잼(영상 압축·겹침으로 프레임 구분 불가) | Godot 프레임마다(고정 틱 없음) |
| (참고) 돌기 후 걷기 대기 | **못 찾음** — 내 캐릭터 입력 쪽(`0x5c8e34`·`0x5c92c4`)까지 봤다. 입력 확인 주기로 보이는 100ms 비교(`0x5c92d3 cmp eax,0x64`)만 있고 "돌기만 하고 기다리는" 상수는 확인 못 함 | — | 못 잼(영상 압축·겹침으로 프레임 구분 불가) | `TurnHoldSeconds = 0.2`s (`Tuning.cs:16`) |

참고 저장소의 후보 값(원작과 비교용): dark-ages-ts 걷기 415ms(`sources/FallenDev/dark-ages-ts/apps/client/src/game-objects/map-entity.ts:11`), 몸 동작 `speed*20`ms 전체(`.../paper-doll/paper-doll-container.ts:91-92`) — **원작과 다르다**. ETDA 걷기 뒤 50ms(`sources/wren11/ETDA/BotCore/Actions/GameActions.cs:211`), da 봇 걷기 간격 750ms(`sources/wren11/da/Operations/GameConstants.h:7`)는 봇 쪽 값이라 클라이언트 속도 근거가 아니다.

## 영상 검산 (2026-10-04)

방법: `yt-dlp` 로 받아 게임 지도 부분만 잘라, 이웃한 두 프레임 사이 지도가 밀린 픽셀을 위상상관(numpy FFT)으로 잼.
걷는 동안 화면이 캐릭터를 따라 밀리므로, 밀린 거리 ÷ 반 타일(28px × 영상 배율) = 걸은 칸 수.

| 영상 | 클라이언트 | 구간 | 결과 |
|---|---|---|---|
| [어둠/프리어둠 옛날게임](https://youtu.be/VtMBZxB1QDI) 10:00~ (30fps, 배율 892/640) | 옛 클라이언트 | 프레임 361~430 오른쪽, 444~486 왼쪽, 4920~5010 | 0.44 · 0.41 · 0.43 s/칸 |
| [A walk through Plamit](https://youtu.be/s3HE-qKJPIQ) 0:00~ (29.97fps, 배율 1.5) | 미국판 옛 클라이언트 | 프레임 411~461 | 0.44 s/칸 |
| [무도가 호러캐슬](https://youtu.be/lMW_gSUK3GI) (30fps) | 옛 클라이언트 | 프레임 4683~4786 (21칸) | 0.167 s/칸 — **빨리 감은 영상으로 보고 뺌**(움직임 4프레임+멈춤 1프레임 꼴은 원작 4단계+확정 1틱과 같음) |
| [무도가 3차 스킬](https://youtu.be/Drkt1x4vm4Q), [초심 방송](https://youtu.be/bt-zS5CzZV8) | 요즘 리마스터 클라이언트 | — | 판이 달라 뺌 |

결론: 걷기는 영상 0.41~0.44s, exe 0.456s, 우리 0.44s — **이미 맞다**. 몸 동작은 영상으로 잴 수 없어 exe 값(속도×10ms = 한 장)을 따른다.
배율 오차(±3%)와 30fps(±33ms) 때문에 영상 값은 검산용이다.

## 근거

### 1. 서버 패킷 분기 (`0x547430` 부근)
패킷은 미리 객체로 풀린 뒤 opcode 비교 사슬로 나뉜다(점프 테이블 없음). opcode 는 `call 0x63b800`.
```
5473db  cmp eax, 0x33  → 0x54a4e4      54740e  cmp eax, 0xc   → 0x54ab04 (걷기)
54741f  cmp eax, 0x11  → 0x54aba4      547430  cmp eax, 0x1a  → 0x54ac24 (몸 동작)
54744e  cmp eax, 0x29  → 0x54aca0 (이펙트)
```
가상함수 대상은 RTTI 로 확인했다: vtable `0x86a144`=`HumanPane`, `0x891544`=`UserPane`(+0xA0 `0x4e2740`, +0xA4 `0x4e1020`), `0x874884`=`LivingObjectPane`(`0x51bed0`/`0x51c030`), `0x88ab44`=`MonsterPane2`(`0x59e3c0`/`0x59e570`).

### 2. 타이머 큐 — 모든 속도의 단위
`0x4ac5b0(obj, id, 지연ms, p1, p2)`: 항목 = {obj+0x134, id, 발화시각 = `[mgr+0x30]`+지연, p1, p2}, 발화시각 순으로 끼워 넣는다.
```
4ac5ce  mov edx, [esp+0x3c]      ; p1
4ac5d2  mov esi, [esp+0x38]      ; 지연
4ac5e2  add esi, [edi+0x30]      ; + 지금 시각(timeGetTime)
4ac60f  cmp edx, [eax+0x8]       ; 발화시각 순 정렬
```

### 3. 걷기 (남)
```
54ab5a  push 0x64                ; 0x0C 핸들러: 속도 100 고정
4e27f7  push edx / push eax / push edx / push 0x1000000   ; 등록(지연=100, p1=방향, p2=100)
4e1679  movsx eax, byte [ebp+0xd8] ; 콜백: 프레임+1
4e1683  movsx edi, word [ebp+0xf0] ; % [obj+0x224] (=5)
4e1698  jne 0x4e1729             ; 0 아니면 재등록(지연=p2=100)·픽셀 이동(vfunc+0xB8)
4e16c2  call 0x540820            ; 0이면 방향→칸 이동 확정
4e366f  mov word [ebp+0x224], di ; di=5 (HumanPane 초기값)
```
픽셀 이동 `0x51cbb0`: 칸 차이를 `0x53ec90`(가로 ×28, 세로 ×14)로 바꾼 뒤 `0x51caa0`/`0x51cad0` 표에서 한 틱 이동량을 읽는다.
```
51cac3  movsx eax, word [ecx+4*edx+0x6a07e0]   ; 가로 표[단계수][프레임]
0x6a07e0 4단계: 7,7,7,7,7   0x6a08e0 4단계: 4,3,4,4,3   (프레임 1~4 합 = 28, 14)
```

### 4. 걷기 (나)
```
5c8f7a  mov eax, [0x6aace4]      ; = 114 (.data 초기값, 쓰는 곳 없음)
5c8f84  mov [ebx+0x1118c], 4     ; 단계수 4 (설정 [cfg+0x538]>0 이면 8)
5c8fb9  idiv esi                 ; 114 / (단계수/4)
5c9008  mov edi, [0x776a0c]      ; 첫 지연 (BSS)
5c9011  push 0x1000000 → 0x4ac5b0
5c9455  mov cl, [eax+4*edx+0x6aa5a0] ; 단계→그림 4단계 행: 0,1,2,3,4
5c95d5  push ebx/eax/ebx         ; 재등록 지연 = p2 = 114
```

### 5. 몸 동작 (`0x1A`)
```
54ac33  movsx edx, word [edx+0x18]   ; 속도
54ac37  lea ebx,[edx+edx]; add ebx,ebx; add ebx,edx; add ebx,ebx  ; ×10
54ac7d  call [vtable+0xa4](동작, 속도×10)
4e12e2  movsx edx, word [esi+0x226]  ; 동작 1: N = [obj+0x226] (=3, 0x4e3682)
4e12ec  push 0x1000001 (지연 0, p1=속도×10, p2=N)
4e1574  (프레임+1) cdq; idiv [p2]     ; 콜백: % N
4e157e  test eax,eax; jne 0x4e15d6   ; 0 아니면 재등록 지연 = p1 (0x4e15e6)
4e15cf  call 0x4e1c14                ; 0이면 동작 끝
4e113e  lea edx,[ebx-0x80]           ; 128+: skill.tbl 줄(0x723200, 한 줄 524바이트), +8 이 N
```

### 6. 이펙트 (`0x29`)
```
54acec  movsx edi, word [eax+0x1e]   ; 속도 (×10 없음)
54afc6  mov word [esp+0x8], di → call 0x54b090 → 0x49e8b0
49e924  mov [ecx+0x200], esi         ; 속도 그대로 저장
49db79  call 0x49c7c0                ; 효과 자료의 [entry+0x28]
49db82  mov [edi+0x200], eax         ; 0 아니면 패킷 속도를 덮어씀
49e9e2  mov eax,[esi+0x108]; neg; add eax,ebx; idiv ecx  ; (시계−시작)/속도
49ee9b  mov [0x6e8d40], eax          ; 이펙트 시계, 0x49eea4 push 0x32 (50ms 주기)
```

### 7. 게임 틱
```
4acd6b  call timeGetTime
4acd74  mov [edx+0x30], eax          ; 큐의 지금 시각
4acd77  lea ecx,[eax+0x14]; cmp ecx,[edx+0x34]   ; 다음 일까지 20ms 넘게 남았나
4acd7f  push 5; call Sleep
4acdc9  mov ebx, 0x28                ; 한 번에 최대 40개
```

## 확인 방법
```sh
cd ~/Downloads/"5.99 클라이언트"
objdump -d --no-show-raw-insn --print-imm-hex -M intel Legend.exe > /tmp/dis.txt
grep -n "^  54ac37:" /tmp/dis.txt   # 이후 위 주소를 그대로 찾는다
python3 -c "import pefile,struct;pe=pefile.PE('Legend.exe');print(struct.unpack('<i',pe.get_data(0x6aace4-0x400000,4)))"  # (114,)
```

## 못 본 것
- 내 캐릭터 첫 걷기 지연 `[0x776a0c]` 의 실제 값(쓰는 코드를 못 찾음 — 0으로 추정).
- 효과 자료 `[entry+0x28]` 이 어느 파일의 어느 칸인지(EFA 머리의 프레임 간격으로 추정).
- 돌기 뒤 걷기까지 기다리는 원작 상수.
- 실행으로 검증하지 않았다(맥, wine 없음) — 모두 정적 분석이다.
