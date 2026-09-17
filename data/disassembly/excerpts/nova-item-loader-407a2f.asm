; 아이템 로더 0x407a2f — 공격모션 item+0x1B · 공격속도 item+0x1C
; 출처: ~/Downloads/5.99 서버팩/Novaonline.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
;
  407a29: call 0x476da4 <.text+0x75da4>
  407a2e: push eax
  407a2f: call dword ptr [4722748]
  407a35: cmp esi, esp
  407a37: call 0x476da4 <.text+0x75da4>
  ...
  408028: call 0x477436 <.text+0x76436>
  40802d: add esp, 4
  408030: mov ecx, dword ptr [ebp - 8]
  408033: mov byte ptr [ecx + 27], al
  408036: jmp 0x4090af <.text+0x80af>
  ...
  408059: call 0x477436 <.text+0x76436>
  40805e: add esp, 4
  408061: mov ecx, dword ptr [ebp - 8]
  408064: mov byte ptr [ecx + 28], al
  408067: jmp 0x4090af <.text+0x80af>
