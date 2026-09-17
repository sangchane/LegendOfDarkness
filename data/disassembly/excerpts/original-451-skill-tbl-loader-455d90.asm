; Skill.tbl 읽기 0x455d90 — 줄마다 NO FN SI FC ST... → 표 0x4de6e0(기술당 0x20C 바이트)
; 출처: sources/lodr4.51.exe 안의 Legend.exe (2001-12-07 빌드, InstallShield 6 → unshield)
; 도구: llvm-objdump -d --no-show-raw-insn --x86-asm-syntax=intel (NDK LLVM 18), 오프셋은 16진수
;
; 파일 이름 "Skill.tbl"(0x50ef40)
  455d9e: call 0x433d30
  455da3: mov ecx, eax
  455da5: push 0x50ef40
  455daa: call 0x433680
  455daf: mov edi, eax
  ...
; [NO].FN · [NO].SI · [NO].FC+1, 허용 목록(0x80 dword)을 0 으로
  455fe8: mov ebp, dword ptr [esp + 0x4208]
  455fef: lea esi, [ebp + ebp]
  455ff3: add esi, esi
  455ff5: add esi, esi
  455ff7: add esi, esi
  455ff9: add esi, esi
  455ffb: add esi, ebp
  455ffd: add esi, esi
  455fff: add esi, esi
  456001: sub esi, ebp
  456003: lea edi, [esi + esi]
  456006: mov eax, dword ptr [esp + 0x420c]
  45600d: mov dword ptr [edi + 2*esi + 0x4de6e0], eax
  456014: mov edx, dword ptr [esp + 0x4210]
  45601b: mov dword ptr [edi + 2*esi + 0x4de6e4], edx
  456022: mov ecx, dword ptr [esp + 0x4214]
  456029: add ecx, 0x1
  45602c: mov dword ptr [edi + 2*esi + 0x4de6e8], ecx
  456033: lea edi, [edi + 2*esi + 0x4de6ec]
  45603a: xor eax, eax
  45603c: mov ecx, 0x80
  456041: rep  stosd dword ptr es:[edi], eax
  ...
; 다섯째 값부터(ST) 옷 번호마다 허용[옷] = 1
  456043: mov edi, 0x4
  456048: cmp ebx, 0x4
  45604b: jle 0x455df9
  456051: lea eax, [ebx - 0x4]
  456054: cmp eax, 0x6
  456057: jl 0x4560f0
  45605d: lea ecx, [ebp + ebp]
  456061: add ecx, ecx
  456063: add ecx, ecx
  456065: add ecx, ecx
  456067: add ecx, ecx
  456069: add ecx, ebp
  45606b: add ecx, ecx
  45606d: add ecx, ecx
  45606f: sub ecx, ebp
  456071: lea esi, [ecx + ecx]
  456074: add esi, esi
  456076: lea edx, [ebx - 0x6]
  456079: lea esi, [esi + eiz]
  456080: mov ebp, dword ptr [esp + 4*edi + 0x4208]
  456087: mov al, 0x1
  456089: mov byte ptr [ebp + 4*ecx + 0x4de6ec], al
  456090: mov ebp, dword ptr [esp + 4*edi + 0x420c]
  456097: mov byte ptr [ebp + 4*ecx + 0x4de6ec], al
  45609e: mov ebp, dword ptr [esp + 4*edi + 0x4210]
  4560a5: mov byte ptr [ebp + 4*ecx + 0x4de6ec], al
  4560ac: mov ebp, dword ptr [esp + 4*edi + 0x4214]
  4560b3: mov byte ptr [ebp + 4*ecx + 0x4de6ec], al
  4560ba: mov ebp, dword ptr [esp + 4*edi + 0x4218]
  4560c1: mov byte ptr [ebp + 4*ecx + 0x4de6ec], al
  4560c8: add edi, 0x5
  4560cb: cmp edi, edx
  4560cd: jle 0x456080
  4560cf: mov eax, dword ptr [esp + 4*edi + 0x4208]
  4560d6: mov byte ptr [eax + esi + 0x4de6ec], 0x1
  4560de: add edi, 0x1
  4560e1: cmp edi, ebx
  4560e3: jl 0x4560cf
