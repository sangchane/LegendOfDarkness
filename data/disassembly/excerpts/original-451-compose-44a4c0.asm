; 사람 그림 겹치기 0x44a4c0 — 순서표 0x4d3120[방향*12+i] · 부위 글자 0x50ba64 "SBNLHUDCHAWS" · 성별 0x50ba70 "MW" · 모든 조각 +27
; 출처: sources/lodr4.51.exe 안의 Legend.exe (2001-12-07 빌드, InstallShield 6 → unshield)
; 도구: llvm-objdump -d --no-show-raw-insn --x86-asm-syntax=intel (NDK LLVM 18), 오프셋은 16진수
;
; 바탕 111×85 (0..0x6e, 0..0x54)
  44a550: lea ecx, [ebp - 0x74]
  44a553: xor edx, edx
  44a555: push esp
  44a556: push 0x54
  44a558: push 0x6e
  44a55a: push edx
  44a55b: push edx
  44a55c: push ecx
  44a55d: call 0x440940
  ...
; 부위 i(ebx 0..11): 순서표에서 부위 번호를 꺼낸다. 동작 2(03 파일)면 0x44ab1a 로
  44a585: movsx edx, byte ptr [ebp + 0x8]
  44a589: xor ebx, ebx
  44a58b: mov dword ptr [ebp - 0x20], eax
  44a58e: lea ecx, [edx + edx]
  44a591: add ecx, ecx
  44a593: lea esi, [ecx + ecx]
  44a596: add esi, ecx
  44a598: mov dword ptr [ebp - 0x1c], esi
  44a59b: xor eax, eax
  44a59d: mov esi, dword ptr [ebp - 0x1c]
  44a5a0: movsx esi, byte ptr [eax + esi + 0x4d3120]
  44a5a8: movsx eax, byte ptr [ebp + 0x10]
  44a5ac: cmp eax, 0x2
  44a5af: je 0x44ab1a
  ...
; 방패(부위 0·11)는 번호 0xff 면 건너뛰고 늘 M
  44a5b5: test esi, esi
  44a5b7: je 0x44a5be
  44a5b9: cmp esi, 0xb
  44a5bc: jne 0x44a5d1
  44a5be: mov eax, dword ptr [ebp + 0xc]
  44a5c1: movzx edx, byte ptr [eax + 0xf]
  44a5c5: cmp edx, 0xff
  44a5cb: je 0x44a784
  44a5d1: test esi, esi
  44a5d3: jl 0x44a784
  44a5d9: movsx eax, byte ptr [ebp + 0x10]
  44a5dd: movzx edx, al
  44a5e0: mov dword ptr [ebp - 0x28], edx
  44a5e3: test esi, esi
  44a5e5: je 0x44a5fd
  44a5e7: cmp esi, 0xb
  44a5ea: je 0x44a5fd
  44a5ec: mov edi, dword ptr [ebp + 0xc]
  44a5ef: movzx eax, byte ptr [edi]
  44a5f2: mov dl, byte ptr [eax + 0x50ba70]
  44a5f8: mov byte ptr [ebp - 0x64], dl
  44a5fb: jmp 0x44a601
  44a5fd: mov byte ptr [ebp - 0x64], 0x4d
  44a601: mov al, byte ptr [esi + 0x50ba64]
  44a607: mov byte ptr [ebp - 0x63], al
  44a60a: mov edi, 0xffff
  44a60f: cmp esi, 0xb
  44a612: ja 0x44a69b
  44a618: mov eax, dword ptr [4*esi + 0x50ba34]
  ...
; 부위 → 겉모습 칸 (점프표 0x50ba34): S +0xf · B +0x3 · N +0x8 · L +0x6 · H +0x1 · U word +0xa · D +0xc · C +0xe · A word +0x4 · W +0xd
  44a61f: jmp eax
  44a621: mov eax, dword ptr [ebp + 0xc]
  44a624: movzx edx, byte ptr [eax + 0x3]
  44a628: mov dword ptr [ebp - 0x20], edx
  44a62b: jmp 0x44a69b
  44a62d: mov eax, dword ptr [ebp + 0xc]
  44a630: movzx edx, byte ptr [eax + 0x8]
  44a634: mov dword ptr [ebp - 0x20], edx
  44a637: jmp 0x44a69b
  44a639: mov eax, dword ptr [ebp + 0xc]
  44a63c: movzx edx, byte ptr [eax + 0x6]
  44a640: mov dword ptr [ebp - 0x20], edx
  44a643: jmp 0x44a69b
  44a645: mov edi, dword ptr [ebp + 0xc]
  44a648: movzx edi, word ptr [edi + 0xa]
  44a64c: xor eax, eax
  44a64e: mov dword ptr [ebp - 0x20], eax
  44a651: jmp 0x44a69b
  44a653: mov eax, dword ptr [ebp + 0xc]
  44a656: movzx edx, byte ptr [eax + 0xc]
  44a65a: mov dword ptr [ebp - 0x20], edx
  44a65d: jmp 0x44a69b
  44a65f: mov eax, dword ptr [ebp + 0xc]
  44a662: movzx edx, byte ptr [eax + 0xe]
  44a666: mov dword ptr [ebp - 0x20], edx
  44a669: jmp 0x44a69b
  44a66b: mov eax, dword ptr [ebp + 0xc]
  44a66e: movzx edx, byte ptr [eax + 0x1]
  44a672: mov dword ptr [ebp - 0x20], edx
  44a675: jmp 0x44a69b
  44a677: mov edi, dword ptr [ebp + 0xc]
  44a67a: movzx edi, word ptr [edi + 0x4]
  44a67e: xor eax, eax
  44a680: mov dword ptr [ebp - 0x20], eax
  44a683: jmp 0x44a69b
  44a685: mov eax, dword ptr [ebp + 0xc]
  44a688: movzx edx, byte ptr [eax + 0xd]
  44a68c: mov dword ptr [ebp - 0x20], edx
  44a68f: jmp 0x44a69b
  44a691: mov eax, dword ptr [ebp + 0xc]
  44a694: movzx edx, byte ptr [eax + 0xf]
  44a698: mov dword ptr [ebp - 0x20], edx
  ...
; 번호 0 이면 안 그린다. 기술이면 'a'+기술, 아니면 %02d.epf(모션+1)
  44a6c3: mov edx, 0x5
  44a6c8: mov eax, dword ptr [ebp - 0x18]
  44a6cb: test eax, eax
  44a6cd: jne 0x44a6d7
  44a6cf: cmp edi, 0xffff
  44a6d5: je 0x44a740
  44a6d7: test edi, edi
  44a6d9: je 0x44a740
  44a6db: movzx eax, word ptr [ebp + 0x1c]
  44a6df: cmp eax, 0xffff
  44a6e4: je 0x44aa99
  44a6ea: movzx eax, word ptr [ebp + 0x1c]
  44a6ee: cmp eax, 0xffff
  44a6f3: je 0x44a703
  44a6f5: movzx eax, word ptr [ebp + 0x1c]
  44a6f9: add al, 0x61
  44a6fb: mov byte ptr [ebp - 0x5f], al
  44a6fe: mov edx, 0x6
  44a703: lea eax, [ebp - 0x64]
  44a706: add edx, eax
  44a708: push 0x50bf60
  44a70d: push edx
  44a70e: call 0x4b6ea8
  44a713: add esp, 0x8
  ...
  44aa99: lea edx, [ebp - 0x5f]
  44aa9c: mov eax, dword ptr [ebp - 0x28]
  44aa9f: add eax, 0x1
  44aaa2: push esp
  44aaa3: push eax
  44aaa4: push 0x50c000
  44aaa9: push edx
  44aaaa: call 0x4b6ea8
  44aaaf: add esp, 0x10
  44aab2: jmp 0x44a716
  ...
; EPF 칸 사각형을 +27(0x1b) 만큼 옮긴다 — 부위 가리지 않음
  44a74a: mov edi, dword ptr [ebp - 0xb8]
  44a750: mov ecx, dword ptr [ebp - 0xbc]
  44a756: mov edx, dword ptr [ebp - 0xb0]
  44a75c: mov eax, dword ptr [ebp - 0xb4]
  44a762: push esp
  44a763: push eax
  44a764: push edx
  44a765: push ecx
  44a766: push edi
  44a767: lea eax, [ebp - 0x54]
  44a76a: push eax
  44a76b: call 0x440940
  44a770: add esp, 0x18
  44a773: lea eax, [ebp - 0x54]
  44a776: push esp
  44a777: push 0x0
  44a779: push 0x1b
  44a77b: push eax
  44a77c: call 0x440b30
  ...
; 그리기 점프표 0x50ba00[부위+1]: 뒷방패(0)는 방향 0·3 만
  44a792: add esi, 0x1
  44a795: cmp esi, 0xc
  44a798: ja 0x44a9ca
  44a79e: mov eax, dword ptr [4*esi + 0x50ba00]
  44a7a5: jmp eax
  44a7a7: movsx eax, byte ptr [ebp + 0x8]
  44a7ab: test eax, eax
  44a7ad: jne 0x44a7e5
  44a7af: mov eax, dword ptr [ebp + 0xc]
  44a7b2: movzx edx, byte ptr [eax + 0xf]
  44a7b6: cmp edx, 0xff
  44a7bc: je 0x44a9eb
  44a7c2: lea ecx, [ebp - 0xc4]
  44a7c8: lea edx, [ebp - 0xbc]
  44a7ce: lea eax, [ebp - 0x54]
  44a7d1: push 0x0
  44a7d3: push 0x1
  44a7d5: push eax
  44a7d6: push edx
  44a7d7: push ecx
  44a7d8: mov ecx, dword ptr [ebp - 0x24]
  44a7db: call 0x445b40
  44a7e0: jmp 0x44a9eb
  44a7e5: movsx eax, byte ptr [ebp + 0x8]
  44a7e9: cmp eax, 0x3
  44a7ec: je 0x44a7af
  ...
; 앞방패(11)는 방향 1·2 만
  44a9ae: movsx eax, byte ptr [ebp + 0x8]
  44a9b2: cmp eax, 0x1
  44a9b5: je 0x44aad8
  44a9bb: movsx eax, byte ptr [ebp + 0x8]
  44a9bf: cmp eax, 0x2
  44a9c2: je 0x44aad8
  ...
  44aad8: mov eax, dword ptr [ebp + 0xc]
  44aadb: movzx edx, byte ptr [eax + 0xf]
  44aadf: cmp edx, 0xff
  ...
; 감정표현(동작 2)은 방패 둘과 무기 W 를 뺀다
  44ab1a: test esi, esi
  44ab1c: je 0x44a9eb
  44ab22: cmp esi, 0xa
  44ab25: je 0x44a9eb
  44ab2b: cmp esi, 0xb
  44ab2e: je 0x44a9eb
  44ab34: jmp 0x44a5b5
  ...
; 다음 부위
  44a9eb: add ebx, 0x1
  44a9ee: movsx eax, bl
  44a9f1: cmp eax, 0xb
  44a9f4: jle 0x44a59d
