; 이펙트 0x29 — 패킷 0x4638a4 · 만들기 0x482f00 ("Efct%03d.epf") · 한 칸 넘기기 0x483870 (Effect.tbl 순서, 속도 = 한 칸 간격)
; 출처: sources/lodr4.51.exe 안의 Legend.exe (2001-12-07 빌드, InstallShield 6 → unshield)
; 도구: llvm-objdump -d --no-show-raw-insn --x86-asm-syntax=intel (NDK LLVM 18), 오프셋은 16진수
;
; [1..4] 대상 id · [5] 이펙트(byte) · [6..7] 속도. id 0 이면 [8..9] x · [10..11] y. 0xfa~0xfd 는 화면 효과
  4638a4: push edi
  4638a5: push esi
  4638a6: push ebp
  4638a7: push ebx
  4638a8: sub esp, 0x1c
  4638ab: mov edi, ecx
  4638ad: mov esi, dword ptr [esp + 0x30]
  4638b1: lea eax, [esi + 0x1]
  4638b4: mov dword ptr [esp], eax
  4638b7: call 0x498970
  4638bc: mov ebp, eax
  4638be: lea edx, [esi + 0x5]
  4638c1: mov dword ptr [esp], edx
  4638c4: call 0x498920
  4638c9: mov dword ptr [esp + 0x14], eax
  4638cd: lea edx, [esi + 0x6]
  4638d0: mov dword ptr [esp], edx
  4638d3: call 0x498930
  4638d8: movsx ebx, ax
  4638db: test ebp, ebp
  4638dd: je 0x4638e9
  4638df: xor edx, edx
  4638e1: mov dword ptr [esp + 0x10], edx
  4638e5: xor edx, edx
  4638e7: jmp 0x463909
  4638e9: lea edx, [esi + 0x8]
  4638ec: mov dword ptr [esp], edx
  4638ef: call 0x498930
  4638f4: movzx edx, ax
  4638f7: mov dword ptr [esp + 0x10], edx
  4638fb: lea esi, [esi + 0xa]
  4638fe: mov dword ptr [esp], esi
  463901: call 0x498930
  463906: movzx edx, ax
  463909: mov ecx, dword ptr [esp + 0x14]
  46390d: movzx eax, cl
  463910: cmp eax, 0xfa
  463915: je 0x4639a3
  46391b: cmp eax, 0xfb
  463920: je 0x463987
  463922: cmp eax, 0xfc
  463927: jne 0x463938
  463929: mov ecx, dword ptr [0x4f0328]
  46392f: push ebx
  463930: push ebx
  463931: call 0x493230
  463936: jmp 0x463994
  463938: cmp eax, 0xfd
  46393d: jne 0x46394e
  46393f: mov ecx, dword ptr [0x4f0328]
  463945: push ebx
  463946: push ebx
  463947: call 0x493320
  46394c: jmp 0x463994
  ...
; 이펙트 번호-1 로 만든다
  46394e: mov dword ptr [esp + 0x8], edx
  463952: mov edx, dword ptr [esp + 0x10]
  463956: mov dword ptr [esp + 0xc], edx
  46395a: add esp, -0x14
  46395d: mov dword ptr [esp], ebp
  463960: dec al
  463962: mov byte ptr [esp + 0x4], al
  463966: mov word ptr [esp + 0x8], bx
  46396b: lea esi, [esp + 0xc]
  46396f: lea ebp, [esp + 0x1c]
  463973: mov ebx, dword ptr [ebp]
  463976: mov dword ptr [esi], ebx
  463978: mov eax, dword ptr [ebp + 0x4]
  46397b: mov dword ptr [esi + 0x4], eax
  46397e: mov ecx, edi
  463980: call 0x4639c4
  ...
; 객체: +0x108 이펙트(0부터) · +0x10c 속도. 파일 이름은 번호+1
  482f42: mov ebx, dword ptr [ebp + 0x10]
  482f45: movzx edx, byte ptr [ebp + 0xc]
  482f49: mov ecx, dword ptr [ebp + 0x8]
  482f4c: mov eax, dword ptr [ebp - 0x3c]
  482f4f: mov dword ptr [eax], 0x517484
  482f55: mov dword ptr [eax + 0x104], ecx
  482f5b: mov byte ptr [eax + 0x108], dl
  482f61: mov byte ptr [eax + 0x109], 0x0
  482f68: mov dword ptr [eax + 0x10c], ebx
  482f6e: mov esi, dword ptr [ebp + 0x14]
  482f71: mov dword ptr [eax + 0x110], esi
  482f77: mov edi, dword ptr [ebp + 0x18]
  482f7a: mov dword ptr [eax + 0x114], edi
  482f80: mov ecx, dword ptr [0x4f0304]
  482f86: test ecx, ecx
  482f88: je 0x483032
  482f8e: add eax, 0x118
  482f93: add edx, 0x1
  482f96: push esp
  482f97: push edx
  482f98: push 0x5176a0
  482f9d: push eax
  482f9e: call 0x4b6ea8
  ...
; 처음 한 칸을 속도 간격으로 건다
  483400: push edi
  483401: push esi
  483402: push esi
  483403: mov edi, ecx
  483405: mov ecx, edi
  483407: call 0x483484
  48340c: mov ecx, dword ptr [0x4f0330]
  483412: xor edx, edx
  483414: mov eax, dword ptr [edi + 0x10c]
  48341a: push edx
  48341b: push edx
  48341c: push eax
  48341d: push edx
  48341e: push edi
  48341f: call 0x42e090
  ...
; 칸 넘기기: 단계+1 → Effect.tbl 줄[이펙트][단계], 0xff 면 끝. 아니면 다시 속도 간격으로
  483870: push esi
  483871: push ebp
  483872: push esi
  483873: mov ebp, ecx
  483875: mov eax, dword ptr [esp + 0x10]
  483879: test eax, eax
  48387b: jne 0x483902
  483881: mov al, byte ptr [ebp + 0x109]
  483887: add al, 0x1
  483889: mov byte ptr [ebp + 0x109], al
  48388f: lea edx, [ebp + 0x38]
  483892: push edx
  483893: mov esi, dword ptr [ebp]
  483896: mov eax, dword ptr [esi + 0x58]
  483899: mov ecx, ebp
  48389b: call eax
  48389d: mov eax, dword ptr [0x4f0304]
  4838a2: movzx esi, byte ptr [ebp + 0x108]
  4838a9: movzx edx, byte ptr [ebp + 0x109]
  4838b0: mov ecx, dword ptr [eax + 0x10]
  4838b3: movsx esi, word ptr [ecx + 2*esi]
  4838b7: add esi, dword ptr [eax + 0x14]
  4838ba: movzx eax, byte ptr [edx + esi]
  4838be: cmp eax, 0xff
  4838c3: je 0x48390f
  4838c5: mov ecx, dword ptr [0x4f0330]
  4838cb: xor edx, edx
  4838cd: mov eax, dword ptr [ebp + 0x10c]
  4838d3: push edx
  4838d4: push edx
  4838d5: push eax
  4838d6: push edx
  4838d7: push ebp
  4838d8: call 0x42e090
  4838dd: mov ecx, dword ptr [0x4f0330]
  4838e3: call 0x454920
  4838e8: test eax, eax
  4838ea: je 0x483902
  4838ec: mov edx, dword ptr [0x4f0334]
  4838f2: mov dword ptr [ebp + 0x8], edx
  4838f5: mov eax, 0x1
  4838fa: add esp, 0x4
  4838fd: pop ebp
  4838fe: pop esi
  4838ff: ret 0xc
  483902: mov eax, 0x1
  483907: add esp, 0x4
  48390a: pop ebp
  48390b: pop esi
  48390c: ret 0xc
