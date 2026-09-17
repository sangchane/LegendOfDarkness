; 평타 0x4160f7 — 잠금 char+0x18 · 450ms · 무기→갑옷 공격모션/공격속도 · 0x1A 보내기 · 소리 1
; 출처: ~/Downloads/5.99 서버팩/Novaonline.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
;
  4160f7: push ebp
  4160f8: mov ebp, esp
  4160fa: sub esp, 328
  416100: push esi
  416101: push edi
  ...
  4161e4: mov edx, dword ptr [ecx + 16]
  4161e7: cmp dword ptr [edx + 24], 0
  4161eb: je 0x4161f2 <.text+0x151f2>
  4161ed: jmp 0x416b30 <.text+0x15b30>
  4161f2: mov eax, dword ptr [ebp - 28]
  ...
  41628e: call dword ptr [4722768]
  416294: cmp edi, esp
  416296: call 0x476da4 <.text+0x75da4>
  41629b: cmp esi, eax
  41629d: jae 0x416b16 <.text+0x15b16>
  4162a3: mov esi, esp
  4162a5: call dword ptr [4722768]
  ...
  4162ca: mov ecx, dword ptr [eax + 16]
  4162cd: mov edx, dword ptr [ecx + 376]
  4162d3: mov dword ptr [ebp - 44], edx
  4162d6: mov eax, dword ptr [ebp - 28]
  4162d9: mov ecx, dword ptr [eax + 16]
  4162dc: mov edx, dword ptr [ecx + 380]
  4162e2: mov dword ptr [ebp - 8], edx
  4162e5: call 0x476d82 <.text+0x75d82>
  4162ea: cdq
  4162eb: mov ecx, 100
  4162f0: idiv ecx
  4162f2: mov dword ptr [ebp - 20], edx
  ...
  4163a4: mov eax, dword ptr [edx + 12]
  4163a7: xor ecx, ecx
  4163a9: mov cl, byte ptr [eax + 27]
  4163ac: mov dword ptr [ebp - 308], ecx
  4163b2: jmp 0x4163be <.text+0x153be>
  4163be: mov edx, dword ptr [ebp - 44]
  4163c1: mov eax, dword ptr [edx + 12]
  4163c4: xor ecx, ecx
  4163c6: mov cl, byte ptr [eax + 28]
  4163c9: push ecx
  4163ca: mov edx, dword ptr [ebp - 308]
  4163d0: push edx
  4163d1: mov eax, dword ptr [ebp + 8]
  ...
  4163e9: xor eax, eax
  4163eb: mov al, byte ptr [edx + 28]
  4163ee: mov dword ptr [ecx + 24], eax
  4163f1: jmp 0x4164c6 <.text+0x154c6>
  4163f6: mov ecx, dword ptr [ebp - 44]
  ...
  416885: mov cx, word ptr [eax + 38]
  416889: test ecx, ecx
  41688b: jne 0x4168a2 <.text+0x158a2>
  41688d: push 0
  41688f: push 1
  416891: mov edx, dword ptr [ebp + 8]
  416894: push edx
  ...
  416903: mov cl, byte ptr [eax + 27]
  416906: mov dword ptr [ebp - 328], ecx
  41690c: jmp 0x416918 <.text+0x15918>
  416918: push 20
  41691a: mov edx, dword ptr [ebp - 328]
  416920: push edx
  416921: mov eax, dword ptr [ebp + 8]
  416924: push eax
