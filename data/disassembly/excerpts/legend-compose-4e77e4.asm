; 사람 그림 겹치기 0x4e77e4 — 방향 줄(15×dir)을 0x69c200 에서 0x736f98 로 옮기고, 감정표현(모션 2)은 방패·무기·P 를 뺀다, ff 는 빈 자리, 뒷방패(부위 0)는 dir 0·3 만
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
;
  4e77e4: push edi
  4e77e5: push esi
  4e77e6: push ebp
  4e77e7: push ebx
  4e77e8: sub esp, 172
  ...
  4e784b: xor eax, eax
  4e784d: xor edx, edx
  4e784f: lea ecx, [esi + esi]
  4e7852: add ecx, ecx
  4e7854: add ecx, ecx
  4e7856: add ecx, ecx
  4e7858: sub ecx, esi
  ...
  4e78c1: mov dword ptr [esp + 136], edx
  4e78c8: xor esi, esi
  4e78ca: mov dword ptr [4*eax + 7565284], esi
  4e78d1: mov edi, dword ptr [esp + 144]
  4e78d8: mov dl, byte ptr [eax + edi + 6930944]
  4e78df: mov byte ptr [eax + 7565208], dl
  4e78e5: mov dword ptr [ebx + 2*ebp + 7564000], esi
  4e78ec: movsx eax, byte ptr [esp + 200]
  4e78f4: cmp eax, 2
  4e78f7: je 0x4e84e0 <.text+0xc14e0>
  4e78fd: mov eax, dword ptr [esp + 108]
  4e7901: movsx esi, byte ptr [eax]
  ...
  4e7918: cmp eax, 255
  4e791d: je 0x4e7a09 <.text+0xc0a09>
  4e7923: test esi, esi
  4e7925: jl 0x4e7a09 <.text+0xc0a09>
  4e792b: mov edx, dword ptr [esp + 140]
  4e7932: mov al, byte ptr [edx + 12]
  4e7935: cmp al, 1
  ...
  4e7a02: mov edi, dword ptr [esp + 108]
  4e7a06: movsx esi, byte ptr [edi]
  4e7a09: test esi, esi
  4e7a0b: jne 0x4e7a60 <.text+0xc0a60>
  4e7a0d: movsx eax, byte ptr [esp + 192]
  4e7a15: test eax, eax
  4e7a17: je 0x4e7a40 <.text+0xc0a40>
  4e7a19: movsx eax, byte ptr [esp + 192]
  4e7a21: cmp eax, 3
