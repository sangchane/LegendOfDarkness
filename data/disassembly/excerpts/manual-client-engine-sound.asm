; 효과음 (2005 = 5.99 클라이언트 Legend.exe) — Miles(Mss32) 22050Hz·16비트·2채널, 번호 → "%d.mp3" 다음 "%d.wav", 볼륨 = 단계 × 20
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe · 맥 objdump, 오프셋 10진수
;
; 0x603270: AIL_set_redist_directory(".\music") → AIL_startup → AIL_open_digital_driver(22050, 16, 2, 1), 실패하면 마지막 인자 0 으로 다시.
; 0x54c444 패킷 0x19(소리): 값 ≥ 128 이면 음악 (값−128), 그중 100 은 "바꾸지 않음". < 128 이면 효과음 번호.
; 0x603364 효과음 n: 이미 연 소리 목록(같은 번호면 그 핸들 재사용)에 없으면 "%d.mp3" 를 아카이브 관리자(0x4bddb0→0x4bd600)에서 찾고,
;   없으면 "%d.wav". 번호마다 샘플 핸들 하나를 만들어(AIL_allocate_sample_handle) 목록에 매달아 둔다 — 동시 재생 수 상한 상수 없음.
;   5.99 아카이브에서 이 이름은 Legend.dat 에만 있다(163개, 0.mp3~).
; 0x603584 볼륨 설정: 바이트 [+20] = 단계 × 20 → 모든 샘플에 AIL_set_sample_volume. 0x603554 읽기 = [+20] / 20.
;   Legend.cfg "Sound Volume" 을 0x46f54c 가 이 함수로 넣는다. 설정창 끌대(0x5d6fe4)는 0~10 단계, 0 이면 소리 끔(0x603630).

  54c451: movsx edx, word ptr [eax + 16]
  54c455: cmp edx, 128
  54c45b: jl 0x54c48f <.text+0x12548f>
  54c45d: add edx, -128
  54c460: cmp edx, 100
  54c463: je 0x54c49b <.text+0x12549b>
  54c465: lea eax, [esp + 16]
  54c469: mov dword ptr [esp], eax
  54c46c: mov dword ptr [esp + 4], 8898016   ;; ".\music\%d.mp3"
  54c474: mov dword ptr [esp + 8], edx
  54c478: call 0x66ee00 <.text+0x247e00>
  54c47d: mov ecx, dword ptr [6863616]
  54c483: lea eax, [esp + 16]
  54c487: push eax
  54c488: call 0x6036b0 <.text+0x1dc6b0>
  54c48d: jmp 0x54c49b <.text+0x12549b>
  54c48f: mov ecx, dword ptr [6863616]
  54c495: push edx
  54c496: call 0x603330 <.text+0x1dc330>
  ...
  603275: call dword ptr [8666216]   ;; _AIL_set_redist_directory@4
  60327b: call 0x6526e0 <.text+0x22b6e0>
  603280: call dword ptr [8666224]   ;; _AIL_startup@0
  603286: test eax, eax
  603288: jne 0x60328f <.text+0x1dc28f>
  60328a: mov eax, dword ptr [ebp - 36]
  60328d: jmp 0x6032af <.text+0x1dc2af>
  60328f: push 1
  603291: push 2
  603293: push 16
  603295: push 22050
  60329a: call dword ptr [8666220]   ;; _AIL_open_digital_driver@16
  6032a0: mov edx, eax
  ...
  6033a8: lea eax, [esp + 16]
  6033ac: mov dword ptr [esp], eax
  6033af: mov dword ptr [esp + 4], 9040640   ;; "%d.mp3"
  6033b7: mov dword ptr [esp + 8], edi
  6033bb: call 0x66ee00 <.text+0x247e00>
  6033c0: call 0x4bddb0 <.text+0x96db0>
  6033c5: lea edx, [esp + 16]
  6033c9: push edx
  6033ca: mov ecx, eax
  6033cc: call 0x4bd600 <.text+0x96600>
  6033d1: mov ebx, eax
  6033d3: test ebx, ebx
  6033d5: je 0x6034da <.text+0x1dc4da>
  ...
  603436: mov dword ptr [ebp], 1
  60343d: mov word ptr [ebp + 4], di
  603441: mov edx, dword ptr [esi + 12]
  603444: push edx
  603445: call dword ptr [8666212]   ;; _AIL_allocate_sample_handle@4
  60344b: mov dword ptr [ebp + 8], eax
  60344e: push eax
  60344f: call dword ptr [8666188]   ;; _AIL_init_sample@4
  ...
  6034ac: mov edx, dword ptr [ebp + 8]
  6034af: movzx esi, byte ptr [esi + 20]
  6034b3: push esi
  6034b4: push edx
  6034b5: call dword ptr [8666184]   ;; _AIL_set_sample_volume@8
  6034bb: mov eax, dword ptr [ebp + 8]
  6034be: push eax
  6034bf: call dword ptr [8666228]   ;; _AIL_start_sample@4
  ...
  6034da: lea eax, [esp + 16]
  6034de: mov dword ptr [esp], eax
  6034e1: mov dword ptr [esp + 4], 9040672   ;; "%d.wav"
  6034e9: mov dword ptr [esp + 8], edi
  6034ed: call 0x66ee00 <.text+0x247e00>
  6034f2: call 0x4bddb0 <.text+0x96db0>
  6034f7: lea edx, [esp + 16]
  6034fb: push edx
  6034fc: mov ecx, eax
  6034fe: call 0x4bd600 <.text+0x96600>
  603503: mov ebx, eax
  603505: test ebx, ebx
  603507: jne 0x6033db <.text+0x1dc3db>
  ...
  603562: movzx esi, byte ptr [eax + 20]
  603566: mov eax, 1717986919
  60356b: mov edx, esi
  60356d: imul edx
  60356f: sar edx, 3
  603572: sar esi, 31
  603575: sub edx, esi
  603577: movzx eax, dl
  ...
  603590: movzx edx, byte ptr [esp + 16]
  603595: lea edx, [edx + 4*edx]
  603598: add edx, edx
  60359a: add edx, edx
  60359c: movzx edx, dl
  60359f: mov byte ptr [eax + 20], dl
  6035a2: mov ecx, dword ptr [eax + 12]
