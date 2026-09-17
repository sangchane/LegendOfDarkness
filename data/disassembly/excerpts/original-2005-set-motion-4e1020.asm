; 사람 몸 동작 고르기 0x4e1020 (2005 = 5.99 클라이언트) — 4.51 과 같은 규칙: 기술 동작은 갑옷(U) 번호가 skill.tbl ST 에 있어야 재생
; 출처: sources/lodr.exe 안의 Legend.exe (2005-03-17 빌드) — 5.99 클라이언트 Legend.exe 와 md5 같음
; 도구: llvm-objdump -d --no-show-raw-insn --x86-asm-syntax=intel (NDK LLVM 18), 오프셋은 16진수
;
; [+0x3e1] 이면 무시 · [+0x369] 가 0 이면(괴물 모습) 0x4e1314 로 · 1 → 상태 3 · 6 → 상태 4 · 0x15 → 7
  4e1020: push edi
  4e1021: push esi
  4e1022: push ebp
  4e1023: push ebx
  4e1024: sub esp, 0xc
  4e1027: mov esi, ecx
  4e1029: mov al, byte ptr [esi + 0x3e1]
  4e102f: test al, al
  4e1031: jne 0x4e1209
  4e1037: movsx ebp, word ptr [esp + 0x24]
  4e103c: mov ebx, dword ptr [esp + 0x20]
  4e1040: mov al, byte ptr [esi + 0x369]
  4e1046: test al, al
  4e1048: je 0x4e1213
  4e104e: cmp ebx, 0x1
  4e1051: je 0x4e12bc
  4e1057: cmp ebx, 0x6
  4e105a: je 0x4e11d2
  4e1060: cmp ebx, 0x15
  4e1063: jne 0x4e10aa
  4e1065: mov eax, dword ptr [esi + 0x208]
  4e106b: test eax, eax
  4e106d: jne 0x4e1209
  ...
; 0x80 이상 → 기술. [객체+0x27a] = 겉모습(+0x268)+0x12 = 갑옷 U. 표 0x723200
  4e1124: cmp ebx, 0x80
  4e112a: jl 0x4e1209
  4e1130: mov eax, dword ptr [esi + 0x208]
  4e1136: test eax, eax
  4e1138: jne 0x4e1209
  4e113e: lea edx, [ebx - 0x80]
  4e1141: movsx ebx, dx
  4e1144: mov word ptr [esi + 0x22e], bx
  4e114b: lea ecx, [ebx + ebx]
  4e114e: add ecx, ecx
  4e1150: add ecx, ecx
  4e1152: add ecx, ecx
  4e1154: add ecx, ecx
  4e1156: add ecx, ebx
  4e1158: add ecx, ecx
  4e115a: add ecx, ecx
  4e115c: sub ecx, ebx
  4e115e: lea edx, [ecx + ecx]
  4e1161: movzx edi, word ptr [esi + 0x27a]
  4e1168: mov al, byte ptr [edi + 4*ecx + 0x72320c]
  4e116f: test al, al
  4e1171: je 0x4e1209
  4e1177: mov dword ptr [esi + 0x208], 0x5
  4e1181: mov ebx, dword ptr [edx + 2*ecx + 0x723200]
  4e1188: mov word ptr [esi + 0x230], bx
  4e118f: mov edi, dword ptr [edx + 2*ecx + 0x723204]
  4e1196: mov word ptr [esi + 0x222], di
  4e119d: movsx edx, word ptr [edx + 2*ecx + 0x723208]
  4e11a5: mov word ptr [esi + 0x22c], dx
  4e11ac: xor eax, eax
  4e11ae: mov byte ptr [esi + 0x20c], al
  4e11b4: mov ecx, dword ptr [0x734e54]
  4e11ba: push edx
  4e11bb: push ebp
  4e11bc: push eax
  4e11bd: push 0x1000001
  4e11c2: push esi
  4e11c3: call 0x4ac5b0
  4e11c8: add esp, 0xc
  4e11cb: pop ebx
  4e11cc: pop ebp
  4e11cd: pop esi
  4e11ce: pop edi
  4e11cf: ret 0x8
  ...
; 겉모습은 객체+0x268 (사람 그리기가 넘기는 곳)
  4e2666: movsx edx, byte ptr [esi + 0x204]
  4e266d: add esi, 0x268
  4e2673: mov eax, dword ptr [0x736a88]
  4e2678: xor edi, edi
  4e267a: push edi
  4e267b: push edi
  4e267c: push ebx
  4e267d: push ebp
  4e267e: mov ebx, dword ptr [esp + 0x38]
  4e2682: push ebx
  4e2683: push eax
  4e2684: movsx ebp, word ptr [esp + 0x3c]
  4e2689: push ebp
  4e268a: movsx eax, byte ptr [esp + 0x3c]
  4e268f: push eax
  4e2690: push esi
  4e2691: push edx
  4e2692: call 0x4e7790
