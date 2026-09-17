; 능력치 재계산 0x45d4be — 공격력 0x8C(140) · 마법 공격력 0x98(152) · 치명타 0x9C(156) · 에나르마 0x156(342) · 수페라에나르마 0x158(344) · 피닉스모드 0x7A(122) · 소수신공 0x7C(124)
; 출처: ~/Downloads/5.99 서버팩/Novaonline.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
; 식 전체는 Pack599.AttackPower·MagicPower·Critical 주석에 옮겨 적었다(포만도 보정은 하데스에 없어 뺐다).
;
  45d4be: push ebp
  45d4bf: mov ebp, esp
  45d4c1: sub esp, 16
  ...
  45d665: mov ecx, dword ptr [eax + 12]
  45d668: mov edx, dword ptr [ebp + 8]
  45d66b: mov eax, dword ptr [edx + 152]
  45d671: add eax, dword ptr [ecx + 92]
  45d674: mov ecx, dword ptr [ebp + 8]
  45d677: mov dword ptr [ecx + 152], eax
  45d67d: mov edx, dword ptr [ebp - 16]
  ...
  45d7e5: add edx, dword ptr [ecx + 128]
  45d7eb: shr edx
  45d7ed: mov eax, dword ptr [eax + 140]
  45d7f3: add eax, edx
  45d7f5: mov ecx, dword ptr [ebp + 8]
  45d7f8: mov edx, dword ptr [ecx + 16]
  45d7fb: mov dword ptr [edx + 140], eax
  45d801: mov eax, dword ptr [ebp + 8]
  ...
  45d80d: mov edx, dword ptr [ebp - 16]
  45d810: mov edx, dword ptr [edx + 12]
  45d813: mov eax, dword ptr [eax + 124]
  45d816: add eax, dword ptr [edx + 132]
  45d81c: shr eax
  45d81e: mov ecx, dword ptr [ecx + 152]
  45d824: add ecx, eax
  45d826: mov edx, dword ptr [ebp + 8]
  45d829: mov eax, dword ptr [edx + 16]
  45d82c: mov dword ptr [eax + 152], ecx
  45d832: mov ecx, dword ptr [ebp + 8]
  ...
  45d838: mov eax, dword ptr [ebp - 16]
  45d83b: mov ecx, dword ptr [eax + 12]
  45d83e: mov edx, dword ptr [edx + 156]
  45d844: add edx, dword ptr [ecx + 140]
  45d84a: mov eax, dword ptr [ebp + 8]
  45d84d: mov ecx, dword ptr [eax + 16]
  45d850: mov dword ptr [ecx + 156], edx
  45d856: mov edx, dword ptr [ebp + 8]
  ...
  45d85c: mov ecx, dword ptr [ebp + 8]
  45d85f: mov edx, dword ptr [ecx + 16]
  45d862: mov eax, dword ptr [eax + 140]
  45d868: add eax, dword ptr [edx + 228]
  45d86e: mov ecx, dword ptr [ebp + 8]
  45d871: mov edx, dword ptr [ecx + 16]
  45d874: mov dword ptr [edx + 140], eax
  45d87a: mov eax, dword ptr [ebp + 8]
  ...
  45d880: mov edx, dword ptr [ebp + 8]
  45d883: mov eax, dword ptr [edx + 16]
  45d886: mov ecx, dword ptr [ecx + 152]
  45d88c: add ecx, dword ptr [eax + 232]
  45d892: mov edx, dword ptr [ebp + 8]
  45d895: mov eax, dword ptr [edx + 16]
  45d898: mov dword ptr [eax + 152], ecx
  45d89e: jmp 0x45d5e1 <.text+0x5c5e1>
  ...
  45d8cd: mov edx, dword ptr [ebp + 8]
  45d8d0: mov eax, dword ptr [edx + 16]
  45d8d3: mov ecx, dword ptr [eax + 156]
  45d8d9: add ecx, 10
  45d8dc: mov edx, dword ptr [ebp + 8]
  45d8df: mov eax, dword ptr [edx + 16]
  45d8e2: mov dword ptr [eax + 156], ecx
  45d8e8: push 4784968
  ...
  45d912: mov edx, dword ptr [ebp + 8]
  45d915: mov eax, dword ptr [edx + 16]
  45d918: mov ecx, dword ptr [eax + 156]
  45d91e: add ecx, 20
  45d921: mov edx, dword ptr [ebp + 8]
  45d924: mov eax, dword ptr [edx + 16]
  45d927: mov dword ptr [eax + 156], ecx
  45d92d: mov ecx, dword ptr [ebp + 8]
  ...
  45d940: mov ecx, dword ptr [ebp + 8]
  45d943: mov edx, dword ptr [ecx + 16]
  45d946: mov eax, dword ptr [edx + 156]
  45d94c: add eax, 40
  45d94f: mov ecx, dword ptr [ebp + 8]
  45d952: mov edx, dword ptr [ecx + 16]
  45d955: mov dword ptr [edx + 156], eax
  45d95b: mov eax, dword ptr [ebp + 8]
  45d95e: mov ecx, dword ptr [eax + 16]
  45d961: cmp dword ptr [ecx + 156], 100
  45d968: jbe 0x45d97a <.text+0x5c97a>
  ...
  45d9a4: mov edx, dword ptr [ebp + 8]
  45d9a7: mov eax, dword ptr [edx + 16]
  45d9aa: mov ecx, dword ptr [eax + 140]
  45d9b0: add ecx, 10
  45d9b3: mov edx, dword ptr [ebp + 8]
  45d9b6: mov eax, dword ptr [edx + 16]
  45d9b9: mov dword ptr [eax + 140], ecx
  45d9bf: mov ecx, dword ptr [ebp + 8]
  ...
  45d9ca: mov cl, byte ptr [eax + 163]
  45d9d0: imul ecx, ecx, 10
  45d9d3: mov edx, dword ptr [edx + 140]
  45d9d9: add edx, ecx
  45d9db: mov eax, dword ptr [ebp + 8]
  45d9de: mov ecx, dword ptr [eax + 16]
  45d9e1: mov dword ptr [ecx + 140], edx
  45d9e7: mov edx, dword ptr [ebp + 8]
  ...
  45da0b: mov edx, dword ptr [ebp + 8]
  45da0e: mov eax, dword ptr [edx + 16]
  45da11: mov ecx, dword ptr [eax + 140]
  45da17: add ecx, 25
  45da1a: mov edx, dword ptr [ebp + 8]
  45da1d: mov eax, dword ptr [edx + 16]
  45da20: mov dword ptr [eax + 140], ecx
  45da26: mov ecx, dword ptr [ebp + 8]
  ...
  45da46: mov ecx, dword ptr [ebp + 8]
  45da49: mov edx, dword ptr [ecx + 16]
  45da4c: mov eax, dword ptr [edx + 140]
  45da52: add eax, 70
  45da55: mov ecx, dword ptr [ebp + 8]
  45da58: mov edx, dword ptr [ecx + 16]
  45da5b: mov dword ptr [edx + 140], eax
  45da61: mov eax, dword ptr [ebp + 8]
  ...
  45da81: mov eax, dword ptr [ebp + 8]
  45da84: mov ecx, dword ptr [eax + 16]
  45da87: mov edx, dword ptr [ecx + 140]
  45da8d: add edx, 210
  45da93: mov eax, dword ptr [ebp + 8]
  45da96: mov ecx, dword ptr [eax + 16]
  45da99: mov dword ptr [ecx + 140], edx
  45da9f: mov edx, dword ptr [ebp + 8]
  ...
  45daaf: mov edx, dword ptr [ebp + 8]
  45dab2: mov eax, dword ptr [edx + 16]
  45dab5: mov ecx, dword ptr [eax + 140]
  45dabb: add ecx, 260
  45dac1: mov edx, dword ptr [ebp + 8]
  45dac4: mov eax, dword ptr [edx + 16]
  45dac7: mov dword ptr [eax + 140], ecx
  45dacd: mov ecx, dword ptr [ebp + 8]
  ...
  45dad8: mov cl, byte ptr [eax + 164]
  45dade: imul ecx, ecx, 10
  45dae1: mov edx, dword ptr [edx + 152]
  45dae7: add edx, ecx
  45dae9: mov eax, dword ptr [ebp + 8]
  45daec: mov ecx, dword ptr [eax + 16]
  45daef: mov dword ptr [ecx + 152], edx
  45daf5: mov edx, dword ptr [ebp + 8]
  ...
  45db07: mov esi, 11
  45db0c: idiv esi
  45db0e: mov ecx, dword ptr [ecx + 156]
  45db14: add ecx, eax
  45db16: mov edx, dword ptr [ebp + 8]
  45db19: mov eax, dword ptr [edx + 16]
  45db1c: mov dword ptr [eax + 156], ecx
  45db22: mov ecx, dword ptr [ebp + 8]
  45db25: mov edx, dword ptr [ecx + 16]
  45db28: cmp dword ptr [edx + 140], 0
  45db2f: jae 0x45db41 <.text+0x5cb41>
  ...
  45db41: mov edx, dword ptr [ebp + 8]
  45db44: mov eax, dword ptr [edx + 16]
  45db47: cmp dword ptr [eax + 152], 0
  45db4e: jae 0x45db60 <.text+0x5cb60>
  ...
  45db84: mov eax, dword ptr [ebp + 8]
  45db87: mov ecx, dword ptr [eax + 16]
  45db8a: cmp dword ptr [ecx + 140], 100
  45db91: jbe 0x45dbb7 <.text+0x5cbb7>
  45db93: mov edx, dword ptr [ebp + 8]
  45db96: mov eax, dword ptr [edx + 16]
  45db99: mov eax, dword ptr [eax + 140]
  45db9f: xor edx, edx
  ...
  45dbab: mov edx, dword ptr [ebp + 8]
  45dbae: mov ecx, dword ptr [edx + 16]
  45dbb1: mov dword ptr [ecx + 140], eax
  45dbb7: mov edx, dword ptr [ebp + 8]
  ...
  45dbdb: mov edx, dword ptr [ebp + 8]
  45dbde: mov eax, dword ptr [edx + 16]
  45dbe1: cmp dword ptr [eax + 152], 100
  45dbe8: jbe 0x45dc0e <.text+0x5cc0e>
  45dbea: mov ecx, dword ptr [ebp + 8]
  45dbed: mov edx, dword ptr [ecx + 16]
  45dbf0: mov eax, dword ptr [edx + 152]
  45dbf6: xor edx, edx
  ...
  45dc02: mov edx, dword ptr [ebp + 8]
  45dc05: mov ecx, dword ptr [edx + 16]
  45dc08: mov dword ptr [ecx + 152], eax
  45dc0e: mov edx, dword ptr [ebp + 8]
