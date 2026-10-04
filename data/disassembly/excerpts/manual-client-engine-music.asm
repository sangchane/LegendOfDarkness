; 음악 (2005 = 5.99 클라이언트 Legend.exe) — ".\music\N.mp3" 를 열 때 확장자를 "mus" 로 바꿔 music\N.mus(내용은 MP3) 를 스트림으로 튼다
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe · 맥 objdump, 오프셋 10진수
;
; 0x430e9e 음악 관리자: AIL_set_file_callbacks(열기=0x431280 …), 켜짐 [+84]=1, 목표 볼륨 [+86]=64, 지금 볼륨 [+85]=0.
; 0x431280 열기 콜백: 이름 끝 3글자를 "mus" 로 덮고 fopen("rb"). 그래서 music 폴더에는 1.mus~64.mus(ID3 머리, MP3)만 있다.
; 0x430f64 곡 바꾸기: 경로를 [+12]에 적고 [+76]=1(바꿀 곡 있음), 200ms 타이머 등록.
; 0x431004 200ms 마다: 지금 볼륨을 목표로 (차이 / 5) 만큼, 차이 < 10 이면 바로 맞춘다. 바꿀 곡이 있으면 목표를 0 으로 보고 줄이다가
;   0 이 되면 닫고 AIL_open_stream → loop_count 0(무한 반복) → 볼륨 → AIL_start_stream, 그 뒤 원래 목표로 다시 올린다(엇갈려 줄고 늚).
; 0x603680 음악 볼륨 = 단계 × 20 을 목표로. 로그인 화면은 ".\Music\1.mp3"(0x50f61a), 0x57e671 은 ".\music\64.mp3".

  430ec5: push 4395952
  430eca: push 4395856
  430ecf: push 4395824
  430ed4: push 4395648
  430ed9: call dword ptr [8666204]   ;; _AIL_set_file_callbacks@16
  430edf: mov edx, dword ptr [ebp - 36]
  430ee2: mov byte ptr [edx + 84], 1
  430ee6: mov byte ptr [edx + 86], 64
  430eea: xor eax, eax
  430eec: mov byte ptr [edx + 85], al
  430eef: mov dword ptr [edx + 88], eax
  ...
  430f70: mov edi, dword ptr [esp + 16]
  430f74: lea esi, [ebx + 12]
  430f77: mov dl, byte ptr [edi]
  430f79: add edi, 1
  430f7c: mov byte ptr [esi], dl
  430f7e: add esi, 1
  430f81: test dl, dl
  430f83: jne 0x430f77 <.text+0x9f77>
  430f85: mov dword ptr [ebx + 76], 1
  430f8c: mov edx, dword ptr [7556692]
  430f92: test edx, edx
  430f94: je 0x430fa8 <.text+0x9fa8>
  430f96: xor eax, eax
  430f98: push eax
  430f99: push eax
  430f9a: push 200
  430f9f: push eax
  430fa0: push ebx
  430fa1: mov ecx, edx
  430fa3: call 0x4ac9b0 <.text+0x859b0>
  ...
  43102b: movzx ebx, byte ptr [ebp + 85]
  43102f: mov eax, dword ptr [esp + 4]
  431033: cmp ebx, eax
  431035: jge 0x431071 <.text+0xa071>
  431037: mov esi, eax
  431039: sub esi, ebx
  43103b: cmp esi, 10
  43103e: jge 0x431046 <.text+0xa046>
  431040: mov ebx, eax
  431042: mov edx, ebx
  431044: jmp 0x43105d <.text+0xa05d>
  431046: mov eax, 1717986919
  43104b: mov edx, esi
  43104d: imul edx
  43104f: sar edx
  431051: sar esi, 31
  431054: sub edx, esi
  431056: add ebx, edx
  ...
  431133: mov edx, dword ptr [ebp + 80]
  431136: lea eax, [ebp + 12]
  431139: push 0
  43113b: push eax
  43113c: push edx
  43113d: call dword ptr [8666240]   ;; _AIL_open_stream@12
  431143: mov dword ptr [ebp + 88], eax
  431146: test eax, eax
  431148: je 0x43118a <.text+0xa18a>
  43114a: push 0
  43114c: push eax
  43114d: call dword ptr [8666200]   ;; _AIL_set_stream_loop_count@8
  431153: mov al, byte ptr [ebp + 84]
  431156: test al, al
  431158: je 0x431160 <.text+0xa160>
  43115a: movzx eax, byte ptr [ebp + 85]
  43115e: jmp 0x431162 <.text+0xa162>
  431160: xor eax, eax
  431162: mov edx, dword ptr [ebp + 88]
  431165: push eax
  431166: push edx
  431167: call dword ptr [8666196]   ;; _AIL_set_stream_volume@8
  43116d: mov eax, dword ptr [ebp + 88]
  431170: push eax
  431171: call dword ptr [8666236]   ;; _AIL_start_stream@4
  431177: mov dword ptr [ebp + 76], 0
  ...
  4312bf: lea esi, [esp + 8]
  4312c3: mov edi, dword ptr [esp + 96]
  4312c7: mov dl, byte ptr [edi]
  4312c9: add edi, 1
  4312cc: mov byte ptr [esi], dl
  4312ce: add esi, 1
  4312d1: test dl, dl
  4312d3: jne 0x4312c7 <.text+0xa2c7>
  4312d5: lea esi, [esp + ebx + 5]
  4312d9: mov edi, 8684896   ;; "mus"
  4312de: mov dl, byte ptr [edi]
  4312e0: add edi, 1
  4312e3: mov byte ptr [esi], dl
  4312e5: add esi, 1
  4312e8: test dl, dl
  4312ea: jne 0x4312de <.text+0xa2de>
  4312ec: lea eax, [esp + 8]
  4312f0: mov dword ptr [esp], eax
  4312f3: mov dword ptr [esp + 4], 8678336   ;; "rb"
  4312fb: call 0x66ea67 <.text+0x247a67>
  431300: test eax, eax
  431302: je 0x431316 <.text+0xa316>
  431304: mov dword ptr [ebp], eax
  ...
  603687: test ecx, ecx
  603689: je 0x60369f <.text+0x1dc69f>
  60368b: mov eax, dword ptr [esp + 8]
  60368f: lea edx, [eax + 4*eax]
  603692: add edx, edx
  603694: add edx, edx
  603696: movzx eax, dl
  603699: push eax
  60369a: call 0x4311f0 <.text+0xa1f0>
