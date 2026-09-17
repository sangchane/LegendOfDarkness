; EPF 읽기 0x4e8854 — 칸 수(u16)를 읽고 6바이트 건너뛴 뒤 표 위치(u32)만 읽는다(바탕 크기 칸은 안 읽음)
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
;
  4e8854: push edi
  4e8855: push esi
  4e8856: push ebp
  4e8857: push ebx
  4e8858: sub esp, 156
  ...
  4e88b2: push esi
  4e88b3: mov ecx, ebx
  4e88b5: call 0x4bd800 <.text+0x96800>
  4e88ba: push 1
  4e88bc: push 6
  4e88be: push esi
  4e88bf: mov ecx, ebx
  4e88c1: call 0x4bd940 <.text+0x96940>
  4e88c6: lea edx, [esp + 40]
