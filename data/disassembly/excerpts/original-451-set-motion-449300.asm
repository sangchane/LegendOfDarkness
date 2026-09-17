; 사람 몸 동작 고르기 0x449300 — 기술 동작(0x80+)은 입은 갑옷(U) 번호가 Skill.tbl ST 에 있어야 재생한다
; 출처: sources/lodr4.51.exe 안의 Legend.exe (2001-12-07 빌드, InstallShield 6 → unshield)
; 도구: llvm-objdump -d --no-show-raw-insn --x86-asm-syntax=intel (NDK LLVM 18), 오프셋은 16진수
;
; 동작 번호 점프표 0x50b4bc: 1 → 상태 3(02 파일) · 6 → 상태 4(03 파일) · 9~17 → 앞모습일 때만 감정 · 21 → 상태 7 · 22 → 상태 8 · 2~5·7·8·18~20 → 무시
  449300: push edi
  449301: push esi
  449302: push ebp
  449303: push ebx
  449304: push esi
  449305: mov esi, ecx
  449307: movsx ebp, word ptr [esp + 0x1c]
  44930c: mov edx, dword ptr [esp + 0x18]
  449310: lea eax, [edx - 0x1]
  449313: cmp eax, 0x15
  449316: ja 0x449466
  44931c: mov eax, dword ptr [4*edx + 0x50b4bc]
  449323: jmp eax
  449325: mov eax, dword ptr [esi + 0x110]
  44932b: test eax, eax
  44932d: jne 0x44953a
  449333: mov dword ptr [esi + 0x110], 0x3
  44933d: xor eax, eax
  44933f: mov byte ptr [esi + 0x114], al
  449345: mov ecx, dword ptr [0x4f0330]
  44934b: movsx edx, word ptr [esi + 0x136]
  449352: push edx
  449353: push ebp
  449354: push eax
  449355: push 0x1
  449357: push esi
  449358: call 0x42e090
  44935d: add esp, 0x4
  449360: pop ebx
  449361: pop ebp
  449362: pop esi
  449363: pop edi
  449364: ret 0x8
  ...
; 23~33 → 앞모습일 때만 감정. 0x80 이상 → 기술
  449466: cmp edx, 0x17
  449469: jl 0x44953a
  44946f: cmp edx, 0x21
  449472: jg 0x44949d
  449474: mov eax, dword ptr [esi + 0x110]
  44947a: test eax, eax
  44947c: jne 0x44953a
  449482: mov al, byte ptr [esi + 0x10c]
  449488: cmp al, 0x1
  44948a: je 0x4495b3
  449490: cmp al, 0x2
  449492: je 0x4495b3
  449498: jmp 0x44953a
  ...
; 기술: 번호 = 동작-0x80. [객체+0x152] = 겉모습+0xa = 갑옷 U 번호. 표[기술].허용[갑옷] 이 0 이면 아무것도 안 한다
  44949d: cmp edx, 0x80
  4494a3: jl 0x44953a
  4494a9: mov eax, dword ptr [esi + 0x110]
  4494af: test eax, eax
  4494b1: jne 0x44953a
  4494b7: lea edx, [edx - 0x80]
  4494ba: movsx ebx, dx
  4494bd: mov word ptr [esi + 0x13e], bx
  4494c4: lea ecx, [ebx + ebx]
  4494c7: add ecx, ecx
  4494c9: add ecx, ecx
  4494cb: add ecx, ecx
  4494cd: add ecx, ecx
  4494cf: add ecx, ebx
  4494d1: add ecx, ecx
  4494d3: add ecx, ecx
  4494d5: sub ecx, ebx
  4494d7: lea edx, [ecx + ecx]
  4494da: movzx edi, word ptr [esi + 0x152]
  4494e1: mov al, byte ptr [edi + 4*ecx + 0x4de6ec]
  4494e8: test al, al
  4494ea: je 0x44953a
  4494ec: mov dword ptr [esi + 0x110], 0x5
  4494f6: mov ebx, dword ptr [edx + 2*ecx + 0x4de6e0]
  4494fd: mov word ptr [esi + 0x140], bx
  449504: mov edi, dword ptr [edx + 2*ecx + 0x4de6e4]
  44950b: mov word ptr [esi + 0x132], di
  449512: movsx edx, word ptr [edx + 2*ecx + 0x4de6e8]
  44951a: mov word ptr [esi + 0x13c], dx
  449521: xor eax, eax
  449523: mov byte ptr [esi + 0x114], al
  449529: mov ecx, dword ptr [0x4f0330]
  44952f: push edx
  449530: push ebp
  449531: push eax
  449532: push 0x1
  449534: push esi
  449535: call 0x42e090
  ...
; 생성자 0x4490d0: 겉모습 16바이트를 +0x148 에 담는다(그래서 +0x152 = 겉모습+0xa)
  44912d: mov eax, dword ptr [esi]
  44912f: mov dword ptr [edx + 0x148], eax
  449135: mov ecx, dword ptr [esi + 0x4]
  449138: mov dword ptr [edx + 0x14c], ecx
  44913e: mov ebx, dword ptr [esi + 0x8]
  449141: mov dword ptr [edx + 0x150], ebx
  449147: mov esi, dword ptr [esi + 0xc]
