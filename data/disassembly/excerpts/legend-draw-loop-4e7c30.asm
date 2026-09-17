; 그리기 순환 0x4e7c30 — 0x736f98[slot] 순서로 부위를 그린다 · 앞방패(부위 10)는 dir 1·2 만
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
;
  4e7c30: lea esi, [eax + eax]
  4e7c33: add esi, esi
  4e7c35: sub esi, eax
  4e7c37: lea esi, [eax + 4*esi]
  4e7c3a: lea ebp, [esi + esi]
  4e7c3d: mov dword ptr [esp + 140], ebp
  4e7c44: lea ebx, [ebp + 2*esi + 7564000]
  4e7c4b: mov dword ptr [esp + 136], ebx
  4e7c52: lea ebp, [eax + eax]
  4e7c55: lea ebx, [ebp + ebp]
  4e7c59: add ebx, ebx
  4e7c5b: lea edx, [ebx + 8*eax + 7564832]
  4e7c62: mov dword ptr [esp + 112], edx
  ...
  4e8419: jne 0x4e819f <.text+0xc119f>
  4e841f: jmp 0x4e7ca0 <.text+0xc0ca0>
  4e8424: lea esi, [esi]
  4e842a: lea edi, [edi]
  4e8430: movsx eax, byte ptr [esp + 192]
  4e8438: cmp eax, 1
  4e843b: je 0x4e8470 <.text+0xc1470>
  4e843d: movsx eax, byte ptr [esp + 192]
  4e8445: cmp eax, 2
