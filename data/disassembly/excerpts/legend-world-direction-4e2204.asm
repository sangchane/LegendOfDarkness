; 세계 그리기 — 방향 바이트 [obj+0x204] 로 칸 묶음과 좌우 뒤집기를 정하고 0x4e7790 을 부른다
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
;
  4e21f5: lea esi, [esi + eiz]
  4e21f9: lea edi, [edi + eiz]
  4e2200: mov eax, dword ptr [esp + 4]
  4e2204: push edi
  4e2205: push esi
  4e2206: push ebp
  4e2207: push ebx
  4e2208: sub esp, 16
  4e220b: mov ebx, ecx
  4e220d: mov edx, dword ptr [esp + 44]
  ...
  4e268a: movsx eax, byte ptr [esp + 60]
  4e268f: push eax
  4e2690: push esi
  4e2691: push edx
  4e2692: call 0x4e7790 <.text+0xc0790>
  4e2697: mov ecx, dword ptr [7563912]
  4e269d: call 0x48bc80 <.text+0x64c80>
