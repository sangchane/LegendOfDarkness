; 몸 동작 0x1A 받기 — 디스패처 0x4611b0 → 0x463824 → 사람 객체 가상함수 +0x68(0x449300)
; 출처: sources/lodr4.51.exe 안의 Legend.exe (2001-12-07 빌드, InstallShield 6 → unshield)
; 도구: llvm-objdump -d --no-show-raw-insn --x86-asm-syntax=intel (NDK LLVM 18), 오프셋은 16진수
;
; 디스패처: 버퍼[0] 이 opcode, 점프표 0x510a54
  4611d9: mov eax, dword ptr [ebp + 0x8]
  4611dc: mov edx, dword ptr [eax + 0x14]
  4611df: mov dword ptr [ebp - 0x18], edx
  4611e2: movzx edx, byte ptr [edx]
  4611e5: lea eax, [edx - 0x3]
  4611e8: cmp eax, 0x48
  4611eb: ja 0x461291
  4611f1: mov eax, dword ptr [4*edx + 0x510a54]
  4611f8: jmp eax
  ...
; 0x1A 칸 → 0x463824
  4614b8: mov eax, dword ptr [ebp - 0x18]
  4614bb: push eax
  4614bc: mov ecx, esi
  4614be: call 0x463824
  ...
; [1..4] id(u32 빅엔디언) · [5] 동작 · [6..7] 속도(u16) ×10 → 객체[+0x68](동작, 속도×10). 소리 바이트 없음
  463824: push edi
  463825: push esi
  463826: push ebp
  463827: push ebx
  463828: sub esp, 0x14
  46382b: mov ebp, ecx
  46382d: mov edi, dword ptr [esp + 0x28]
  463831: lea eax, [edi + 0x1]
  463834: mov dword ptr [esp], eax
  463837: call 0x498970
  46383c: mov esi, eax
  46383e: lea edx, [edi + 0x5]
  463841: mov dword ptr [esp], edx
  463844: call 0x498920
  463849: movzx ebx, al
  46384c: lea edx, [edi + 0x6]
  46384f: mov dword ptr [esp], edx
  463852: call 0x498930
  463857: movzx edx, ax
  46385a: lea edi, [edx + edx]
  46385d: add edi, edi
  46385f: add edi, edx
  463861: add edi, edi
  463863: movsx edx, di
  463866: mov dword ptr [esp + 0xc], edx
  46386a: mov ecx, dword ptr [ebp + 0x150]
  463870: push esi
  463871: call 0x481270
  463876: test eax, eax
  463878: je 0x463889
  46387a: mov edx, dword ptr [esp + 0xc]
  46387e: push edx
  46387f: push ebx
  463880: mov ebx, dword ptr [eax]
  463882: mov edx, dword ptr [ebx + 0x68]
  463885: mov ecx, eax
  463887: call edx
  463889: mov eax, 0x1
  46388e: add esp, 0x14
  463891: pop ebx
  463892: pop ebp
  463893: pop esi
  463894: pop edi
  463895: ret 0x4
