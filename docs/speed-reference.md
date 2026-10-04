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

## 동작 중 이동·기술, 돌기 (2026-10-04)

세 가지 모두 **열쇠는 한 칸 `[obj+0x208]`(그 캐릭터가 지금 무엇을 하는 중인가)** 이다. 값: 0 = 쉼, 1 = 걷는 중, 3~8 = 몸 동작(`0x1A`) 중(동작 1→3, 6→4, 128+→5, 0x15→7, 0x16→8). 내 캐릭터(`UserPane`)도 남(`HumanPane`)과 같은 칸을 쓴다(0x1A 처리 vfunc+0xA4 가 둘 다 `0x4e1020`).
동작이 끝나는 곳은 `0x4e1c14`(`[obj+0x208]=0`, `[obj+0x20c]=-1`) 하나뿐이고, 동작 타이머 콜백이 프레임을 `(i+1)%N` 으로 돌리다 0 이 될 때 부른다(`0x4e157e`→`0x4e15cf`). 즉 **마지막 그림(N−1)이 한 간격(속도×10ms) 보인 뒤** 풀린다. 묶이는 시간 = 0x1A 받은 때부터 (N−1)×속도×10ms (예: 평타 N=3·속도 30 → 600ms).

| 항목 | 원작 동작 | 확신도 |
|---|---|---|
| 1. 몸 동작 중 이동 | 내 방향키 처리(`0x5c8ca4`)가 `[obj+0x208]` 이 0(쉼)일 때만 돌기·걷기를 하고, 3~8(동작 중)이면 **아무것도 하지 않고 돌아간다** — 걷기 `0x06`도 돌기 `0x11`도 **보내지 않는다**(그림만 막는 게 아니라 요청 자체를 안 함). 줄을 세워 두지도 않는다(키 이벤트는 버려짐). 키를 계속 누르고 있으면 OS 키 반복으로 다음 키 이벤트가 오므로, 동작이 풀린 직후의 첫 반복에서 걷기 시작한다. 막는 것은 **클라이언트**다 — Hades 서버 걷기 처리에는 "동작 중" 검사가 없다 | **확정**(분기), 풀린 뒤 이어 걷는 시점은 OS 키 반복 간격에 달림 |
| 2. 동작 중 다른 기술 | 0x1A 처리 `0x4e1020` 의 모든 갈래가 `[obj+0x208]!=0` 이면 `0x4e1209`(그냥 돌아감)로 간다 — 새 동작은 **무시**(덮어쓰기·줄 세우기 없음). 걷는 중(값 1)에 온 0x1A 도 같이 버려진다. 기술 요청 `0x3E`(`0x4f4d64`)는 기술 칸의 잠금(`[slot+0x32c]`·`[slot+0x32d]`, 재사용 대기로 추정)과 `0x4fa210` 만 보고 **몸 동작 상태는 안 본다** → 동작 중에도 보낸다. 서버(Hades)도 기술 요청을 동작 때문에 거절하지 않으므로 피해는 들어가고, 서버가 보낸 0x1A 는 클라이언트가 버려서 **"모션 없이 데미지만"** 이 된다. 스페이스 평타 `0x13`(`0x5c92c4`)도 동작 검사 없이 **100ms 넘게 지났을 때만** 보낸다(앞 절 "입력 확인 주기 100ms"는 사실 이 평타 연타 제한이다) | **확정** |
| 3. 방향키 짧게 = 돌기만 | 방향키 한 번(키 이벤트 1개)에 `0x5c8ca4` 가: 보는 방향과 **다르면 돌기 `0x11` 만 보내고**, 내 방향을 그 자리에서 바꾼 뒤(`0x51bfd0`→`[obj+0x204]`) 끝. **같으면 바로** 앞 칸이 비었나 보고(`0x540860`) 걷기 `0x06` + 걷기 시작. 그래서 짧게 누르면 돌기만, 누르고 있으면 **두 번째 키 이벤트(= Windows 키 반복의 첫 반복)** 에서 걷는다. 게임 안에 "몇 ms 기다렸다 걷기"·"반복 횟수" 상수는 **없다** — 기다림은 **OS 키보드 반복 지연**(Windows 기본 설정 1 ≈ 500ms, 사용자 설정 250~1000ms)이다. 입력은 **WM_KEYDOWN**(반복 포함; 반복 비트 0x40000000 은 아예 넘기지 않음)으로 받고, GetAsyncKeyState 폴링이 아니다. 걷는 중에는 마지막 단계(프레임 = `[obj+0x224]`−1 = 4)에 같은 방향 키 이벤트가 오면 "이어 걷기" 깃발(`[obj+0x10600]`)만 세우고, 한 칸이 끝날 때(`0x5c94e9`) 그 깃발로 다음 `0x06` 을 보낸다. 걷는 중 다른 방향 키는 무시 | 분기·입력 방식 **확정**, 지연 값(≈500ms)은 OS 기본값이라 **추정** |

### 근거 — 1·3. 내 방향키 → 돌기/걷기 (`0x5c8ca4`)
```
5c87ac  (키 0x63·0x76·0x78·0x7a·0x80~0x83 갈래, 표 0x890e1c) → 0x5c8c64 키→방향 → call 0x5c8ca4
5c8d00  mov edi, [esi+0x208]      ; 무엇을 하는 중인가
5c8d06  cmp edi, 1 ; je 0x5c8d86  ; 걷는 중 → 마지막 단계 검사
5c8d0b  test edi, edi ; jne 0x5c8d7d   ; 0 이 아니면(동작 중 3~8) 그냥 돌아감 — 아무것도 안 보냄
5c8d0f  movsx eax, byte [esi+0x204]; cmp ebp, eax ; je 0x5c8d3e   ; 같은 방향이면 걷기로
5c8d1d  call 0x5c8e04             ; 다른 방향: 0x11 돌기 보냄 (5c8e0b mov byte [esp],0x11)
5c8d25  call 0x51bfd0             ; 내 방향을 바로 바꿈([obj+0x204]=dir, 이것도 [0x208]==0 일 때만)
5c8d53  call 0x540860             ; 같은 방향: 앞 칸 비었나
5c8d69  call 0x5c8e34             ; 0x06 걷기 보냄 (5c8e3e mov byte [esp+8],0x6)
5c8d78  call 0x5c8eb4             ; 걷기 그림 시작
5c8d86  movsx edx, byte [esi+0x20c]; movsx eax, word [esi+0x224]; dec; cmp ; 걷는 중: 프레임 == N-1 ?
5c8ddd  mov byte [esi+0x10600], 1 ; 같은 방향이면 이어 걷기 깃발
5c94e9~5c952e                     ; 한 칸 끝: 깃발이 서 있으면 0x06 + 다음 걸음
```
키 입력 경로: 창 프로시저 `0x4eab70 cmp edi,0x100`(WM_KEYDOWN) → `0x4eae73` 스캔코드 `(lParam>>16)&0xff`(+0x80 확장키) → `0x4af1d0`/`0x4af210` 이 키 이벤트(종류 8)를 만들어 큐에 넣는다. lParam 의 반복 비트는 넘기지 않으므로 OS 반복도 모두 새 이벤트다. 키 뗌(WM_KEYUP→종류 9)은 게임 키 처리 `0x5c83a0` 가 버린다(`0x5c847b cmp al,8; jne`). 입력 관리자 초기화에 500ms·150ms 값(`0x4ae1ab mov [esi+0x44c],0x1f4` / `0x4ae1b5 mov [esi+0x450],0x96`)이 있으나, 이 칸을 읽는 곳을 찾지 못했다(쓰이지 않는 자체 반복으로 추정). GetAsyncKeyState 호출 3곳(`0x45a8ea`·`0x45c6d1`·`0x486eae`)과 `0x5f114b` 는 이 이동 경로에 없다.

### 근거 — 2. 동작 중 새 동작 무시 (`0x4e1020`)
```
4e12bc  mov eax,[esi+0x208]; test eax,eax; jne 0x4e1209   ; 동작 1(평타)
4e11d2  mov eax,[esi+0x208]; test eax,eax; jne 0x4e1209   ; 동작 6
4e1065  ...                                     jne 0x4e1209   ; 동작 0x15
4e10af  ...                                     jne 0x4e1209   ; 동작 0x16
4e1130  mov eax,[esi+0x208]; test eax,eax; jne 0x4e1209   ; 동작 128+
4e1209  add esp,0xc; ...; ret 8                            ; 아무것도 안 하고 끝
4e15c7~4e15cf  프레임이 0 으로 돌면 call 0x4e1c14          ; 0x4e1c17 mov [ebp+0x208],0 — 여기서만 풀림
4f4d64  call 0x4fa210 ; 0 이면 4f4d7e mov byte [esp+8],0x3e → 0x5fdf00 보냄   ; 몸 동작 상태 안 봄
4f4ec3  mov al,[edi+0x32c]; test; jne 끝 / [edi+0x32d] 도 같음                  ; 기술 칸 잠금만 봄
5c92c7  call timeGetTime; sub eax,[edi+0x105f4]; cmp eax,0x64; jbe 건너뜀    ; 평타 0x13 은 100ms 넘게 지나야
```

### 근거 — 서버(Hades)는 막지 않는다
- 걷기 `0x06`: `sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Network/Game/GameServerHandlers.cs:296-330` — 잠·얼음·마비, 해골(Skulled), 새로고침 중, 걷기 속도 제한(`IsSpeedHacking` = 마지막 걸음 뒤 275ms 안, `GameClient.cs:56`·`Lorule.Config/LoruleConfig.json:16`)만 거절한다. 몸 동작 중 검사 없음. 주문 영창 중이면 영창을 끊는다(`:317`, `CancelCastingWhenWalking: true`).
- 기술 `0x3E`: 같은 파일 `:1886-1945` — 죽음·해골·잠·얼음, `skill.CanUse()`(= 재사용 대기 끝남, `Types/Skill.cs:26,123`)만 본다. 몸 동작 중 검사 없음. 쓰면 다음 사용까지 템플릿 쿨다운 또는 `GlobalBaseSkillDelay` 500ms(`LoruleConfig.json:64`).
- 0x1A 는 `Scope.NearbyAislings` 로 자기 자신에게도 간다(`Types/Sprite.cs:304-309`, 범위 안 모두에게 보냄). 그래서 내 캐릭터의 동작 그림도 서버 0x1A 로만 시작하고, 클라이언트는 기술을 누를 때 그림을 먼저 그리지 않는다(`0x4f4d64` 에 그림 호출 없음).
- 결론: **세 가지 모두 클라이언트가 만드는 동작**이다. 원작 서버(노바 5.99)가 따로 막는지는 보지 않았다.

### 이 절에서 못 본 것
- 스캔코드 → 내부 키 번호 표(`[입력관리자+0x134]`)는 실행 중에 채워져서, 내부 키 0x63·0x76·0x78·0x7a / 0x80~0x83 이 정확히 어느 물리 키(방향키·숫자패드)인지는 확인하지 못했다. 방향은 `0x5c8c64` 표(0x890f34)대로 0x63→0, 0x76→1, 0x78→2, 0x7a→3, 0x80→3, 0x81→0, 0x82→1, 0x83→2.
- `[obj+0x3e1]`(물 속, `0x891660` = "수영" 기술 검사)이 서 있으면 걷기·동작 모두 다른 길로 간다 — 이번 질문과 무관해 따라가지 않았다.
- 실행 검증 없음(맥, wine 없음).
