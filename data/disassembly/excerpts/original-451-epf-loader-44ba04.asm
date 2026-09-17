; EPF 칸 읽기 0x44ba04 — u16 칸 수, 6바이트 건너뛰고 u32 표 위치. 머리말 너비·높이는 안 쓴다. "Efct" 이름만 <이름>.tbl 에서 칸별 x·y
; 출처: sources/lodr4.51.exe 안의 Legend.exe (2001-12-07 빌드, InstallShield 6 → unshield)
; 도구: llvm-objdump -d --no-show-raw-insn --x86-asm-syntax=intel (NDK LLVM 18), 오프셋은 16진수
;
  44ba2d: mov ebx, dword ptr [esp + 0xac]
  44ba34: mov eax, dword ptr [esp + 0xa0]
  44ba3b: push eax
  44ba3c: mov ecx, ebx
  44ba3e: call 0x433680
  44ba43: mov edi, eax
  44ba45: test edi, edi
  44ba47: je 0x44bc51
  44ba4d: lea edx, [esp + 0x18]
  44ba51: push 0x2
  44ba53: push edx
  44ba54: push edi
  44ba55: mov ecx, ebx
  44ba57: call 0x4337b0
  44ba5c: push 0x1
  44ba5e: push 0x6
  44ba60: push edi
  44ba61: mov ecx, ebx
  44ba63: call 0x433b60
  44ba68: lea edx, [esp + 0x1c]
  44ba6c: push 0x4
  44ba6e: push edx
  44ba6f: push edi
  44ba70: mov ecx, ebx
  44ba72: call 0x4337b0
  44ba77: push edi
  44ba78: mov ecx, ebx
  44ba7a: call 0x433890
  44ba7f: mov dword ptr [esp + 0x84], eax
  44ba86: mov edx, dword ptr [esp + 0x1c]
  44ba8a: push 0x1
  44ba8c: push edx
  44ba8d: push edi
  44ba8e: mov ecx, ebx
  44ba90: call 0x433b60
  44ba95: push edi
  44ba96: mov ecx, ebx
  44ba98: call 0x433890
  44ba9d: mov esi, eax
  44ba9f: movsx edx, word ptr [esp + 0xa4]
  44baa7: test edx, edx
  44baa9: jl 0x44baba
  ...
; 칸 번호가 칸 수 이상이면 빈 칸
  44baab: movsx edx, word ptr [esp + 0xa4]
  44bab3: cmp dx, word ptr [esp + 0x18]
  ...
; 표 16바이트: top · left · bottom · right(i16) · 시작 · 끝(u32)
  44bb07: movsx ebp, word ptr [esp + 0xa4]
  44bb0f: lea ebp, [ebp + ebp]
  44bb13: mov edx, dword ptr [esp + 0xa8]
  44bb1a: lea edx, [edx + 0x8]
  44bb1d: mov dword ptr [esp], edx
  44bb20: movsx edx, word ptr [esi + 8*ebp + 0x2]
  44bb25: mov dword ptr [esp + 0x4], edx
  44bb29: movsx edx, word ptr [esi + 8*ebp]
  44bb2d: mov dword ptr [esp + 0x8], edx
  44bb31: movsx edx, word ptr [esi + 8*ebp + 0x6]
  44bb36: mov dword ptr [esp + 0xc], edx
  44bb3a: movsx edx, word ptr [esi + 8*ebp + 0x4]
  44bb3f: mov dword ptr [esp + 0x10], edx
  44bb43: call 0x440940
  44bb48: movsx eax, word ptr [esi + 8*ebp + 0x6]
  44bb4d: movsx edx, word ptr [esi + 8*ebp + 0x2]
  44bb52: sub eax, edx
  44bb54: mov ecx, dword ptr [esp + 0xa8]
  44bb5b: mov dword ptr [ecx + 0x4], eax
  44bb5e: mov eax, dword ptr [esi + 8*ebp + 0x8]
  44bb62: mov edx, dword ptr [esp + 0x84]
  44bb69: add eax, edx
  44bb6b: mov dword ptr [ecx], eax
  44bb6d: mov eax, dword ptr [esi + 8*ebp + 0x18]
  44bb71: sub eax, dword ptr [esi + 8*ebp + 0xc]
  44bb75: mov dword ptr [ecx + 0x18], eax
  44bb78: add edx, dword ptr [esi + 8*ebp + 0xc]
  44bb7c: mov dword ptr [ecx + 0x1c], edx
  ...
; "Efct" 가 들어간 이름이면 .epf → .tbl, 칸번호*4 에서 i16 두 개
  44bb87: mov eax, dword ptr [esp + 0xa0]
  44bb8e: mov dword ptr [esp], eax
  44bb91: mov dword ptr [esp + 0x4], 0x50bfa0
  44bb99: call 0x4b73d0
  44bb9e: test eax, eax
  44bba0: je 0x44bc44
  44bba6: lea esi, [esp + 0x20]
  44bbaa: mov edi, dword ptr [esp + 0xa0]
  44bbb1: mov dl, byte ptr [edi]
  44bbb3: add edi, 0x1
  44bbb6: mov byte ptr [esi], dl
  44bbb8: add esi, 0x1
  44bbbb: test dl, dl
  44bbbd: jne 0x44bbb1
  44bbbf: lea eax, [esp + 0x20]
  44bbc3: mov dword ptr [esp], eax
  44bbc6: mov dword ptr [esp + 0x4], 0x2e
  44bbce: call 0x4b7060
  44bbd3: mov edi, 0x50bf80
  44bbd8: mov esi, eax
  44bbda: mov dl, byte ptr [edi]
  44bbdc: add edi, 0x1
  44bbdf: mov byte ptr [esi], dl
  44bbe1: add esi, 0x1
  44bbe4: test dl, dl
  44bbe6: jne 0x44bbda
  44bbe8: lea eax, [esp + 0x20]
  44bbec: push eax
  44bbed: mov ecx, ebx
  44bbef: call 0x433680
  44bbf4: mov ebp, eax
  44bbf6: test ebp, ebp
  44bbf8: je 0x44bc44
  44bbfa: movsx edx, word ptr [esp + 0xa4]
  44bc02: lea esi, [edx + edx]
  44bc05: add esi, esi
  44bc07: push 0x1
  44bc09: push esi
  44bc0a: push ebp
  44bc0b: mov ecx, ebx
  44bc0d: call 0x433b60
  44bc12: mov edx, dword ptr [esp + 0xa8]
  44bc19: lea esi, [edx + 0x24]
  44bc1c: push 0x2
  44bc1e: push esi
  44bc1f: push ebp
  44bc20: mov ecx, ebx
  44bc22: call 0x4337b0
  44bc27: mov edx, dword ptr [esp + 0xa8]
  44bc2e: lea esi, [edx + 0x20]
  44bc31: push 0x2
  44bc33: push esi
  44bc34: push ebp
  44bc35: mov ecx, ebx
  44bc37: call 0x4337b0
  44bc3c: push ebp
  44bc3d: mov ecx, ebx
