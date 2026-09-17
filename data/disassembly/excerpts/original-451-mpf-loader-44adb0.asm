; 괴물 그림 0x44adb0 — "mns%03d.mpf"(번호-0x4000). 칸 = 시작 + 단계 % 칸 수, 표를 칸번호×16 으로 바로 읽는다(빈 칸도 번호를 차지)
; 출처: sources/lodr4.51.exe 안의 Legend.exe (2001-12-07 빌드, InstallShield 6 → unshield)
; 도구: llvm-objdump -d --no-show-raw-insn --x86-asm-syntax=intel (NDK LLVM 18), 오프셋은 16진수
;
  44ade6: movzx eax, word ptr [ebp + 0x8]
  44adea: lea ecx, [ebp - 0x48]
  44aded: lea edx, [eax - 0x4000]
  44adf3: push esp
  44adf4: push edx
  44adf5: push 0x50c020
  44adfa: push ecx
  44adfb: call 0x4b6ea8
  44ae00: add esp, 0x10
  44ae03: call 0x433d30
  44ae08: lea edx, [ebp - 0x48]
  44ae0b: push edx
  44ae0c: mov ecx, eax
  44ae0e: call 0x433680
  ...
; 머리말 → 표(0x11 뒤) → 칸 수×16 뒤 그림
  44ae3b: call 0x433d30
  44ae40: push esi
  44ae41: mov ecx, eax
  44ae43: call 0x433890
  44ae48: mov dword ptr [ebp - 0x54], eax
  44ae4b: call 0x433d30
  44ae50: push 0x1
  44ae52: push 0x11
  44ae54: push esi
  44ae55: mov ecx, eax
  44ae57: call 0x433b60
  44ae5c: call 0x433d30
  44ae61: push esi
  44ae62: mov ecx, eax
  44ae64: call 0x433890
  44ae69: mov dword ptr [ebp - 0x50], eax
  44ae6c: call 0x433d30
  44ae71: mov ebx, eax
  44ae73: lea ecx, [ebp - 0x54]
  44ae76: call 0x47cb60
  44ae7b: movzx edx, al
  44ae7e: add edx, edx
  44ae80: add edx, edx
  44ae82: add edx, edx
  44ae84: add edx, edx
  44ae86: push 0x1
  44ae88: push edx
  44ae89: push esi
  44ae8a: mov ecx, ebx
  44ae8c: call 0x433b60
  44ae91: call 0x433d30
  44ae96: push esi
  44ae97: mov ecx, eax
  44ae99: call 0x433890
  ...
; 걷기(상태 1): 시작 [머리말+9] + 단계 % [머리말+0xa] · 공격(상태 3): [+0xb] + 단계 % [+0xc]
  44ae9e: movsx edi, word ptr [ebp + 0x14]
  44aea2: mov dl, byte ptr [ebp + 0xc]
  44aea5: mov dword ptr [ebp - 0x4c], eax
  44aea8: test dl, dl
  44aeaa: je 0x44af31
  44aeb0: cmp dl, 0x2
  44aeb3: je 0x44af31
  44aeb5: cmp dl, 0x1
  44aeb8: jne 0x44aef3
  44aeba: lea ecx, [ebp - 0x54]
  44aebd: call 0x47cb90
  44aec2: mov ebx, eax
  44aec4: lea ecx, [ebp - 0x54]
  44aec7: call 0x47cba0
  44aecc: movzx ebx, bl
  44aecf: movzx ecx, al
  44aed2: mov eax, edi
  44aed4: cdq
  44aed5: idiv ecx
  44aed7: add ebx, edx
  44aed9: mov dl, byte ptr [ebp + 0x10]
  44aedc: cmp dl, 0x1
  44aedf: je 0x44b00e
  44aee5: mov dl, byte ptr [ebp + 0x10]
  44aee8: cmp dl, 0x2
  44aeeb: je 0x44b00e
  44aef1: jmp 0x44af67
  44aef3: cmp dl, 0x3
  44aef6: jne 0x44af67
  44aef8: lea ecx, [ebp - 0x54]
  44aefb: call 0x47cbb0
  44af00: mov ebx, eax
  44af02: lea ecx, [ebp - 0x54]
  44af05: call 0x47cbc0
  44af0a: movzx ebx, bl
  44af0d: movzx ecx, al
  44af10: mov eax, edi
  44af12: cdq
  44af13: idiv ecx
  44af15: add ebx, edx
  44af17: mov dl, byte ptr [ebp + 0x10]
  44af1a: cmp dl, 0x1
  44af1d: je 0x44b040
  44af23: mov dl, byte ptr [ebp + 0x10]
  44af26: cmp dl, 0x2
  44af29: je 0x44b040
  44af2f: jmp 0x44af67
  ...
  44af67: mov dword ptr [ebp - 0x1c], esi
  44af6a: mov eax, dword ptr [ebp + 0x18]
  44af6d: mov edi, dword ptr [ebp - 0x50]
  44af70: add ebx, ebx
  44af72: lea esi, [eax + 0x8]
  44af75: movsx ecx, word ptr [edi + 8*ebx]
  44af79: movsx edx, word ptr [edi + 8*ebx + 0x2]
  44af7e: movsx eax, word ptr [edi + 8*ebx + 0x4]
  44af83: movsx edi, word ptr [edi + 8*ebx + 0x6]
  44af88: push esp
  44af89: push edi
  44af8a: push eax
  44af8b: push edx
  44af8c: push ecx
  44af8d: push esi
  44af8e: mov esi, dword ptr [ebp - 0x1c]
