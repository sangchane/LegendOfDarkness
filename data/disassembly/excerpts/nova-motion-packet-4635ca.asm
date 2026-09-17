; 0x1A 몸 동작 패킷 만들기 0x4635ca
; 출처: ~/Downloads/5.99 서버팩/Novaonline.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
;
  4635ca: push ebp
  4635cb: mov ebp, esp
  4635cd: mov eax, dword ptr [ebp + 8]
  4635d0: mov ecx, dword ptr [4*eax + 4903424]
  4635d7: mov edx, dword ptr [ebp + 8]
  4635da: mov eax, dword ptr [4*edx + 4903424]
  4635e1: mov ecx, dword ptr [ecx + 4]
  4635e4: mov edx, dword ptr [eax + 8]
  4635e7: mov byte ptr [ecx + edx], -86
  4635eb: mov eax, dword ptr [ebp + 8]
  4635ee: mov ecx, dword ptr [4*eax + 4903424]
  4635f5: mov edx, dword ptr [ebp + 8]
  4635f8: mov eax, dword ptr [4*edx + 4903424]
  4635ff: mov ecx, dword ptr [ecx + 4]
  463602: mov edx, dword ptr [eax + 8]
  463605: mov byte ptr [ecx + edx + 1], 0
  46360a: mov eax, dword ptr [ebp + 8]
  46360d: mov ecx, dword ptr [4*eax + 4903424]
  463614: mov edx, dword ptr [ebp + 8]
  463617: mov eax, dword ptr [4*edx + 4903424]
  46361e: mov ecx, dword ptr [ecx + 4]
  463621: mov edx, dword ptr [eax + 8]
  463624: mov byte ptr [ecx + edx + 2], 9
  463629: mov eax, dword ptr [ebp + 8]
  46362c: mov ecx, dword ptr [4*eax + 4903424]
  463633: mov edx, dword ptr [ebp + 8]
  463636: mov eax, dword ptr [4*edx + 4903424]
  46363d: mov ecx, dword ptr [ecx + 4]
  463640: mov edx, dword ptr [eax + 8]
  463643: mov byte ptr [ecx + edx + 3], 26
  463648: mov eax, dword ptr [ebp + 8]
  46364b: mov ecx, dword ptr [4*eax + 4903424]
  463652: mov edx, dword ptr [ebp + 8]
  463655: mov eax, dword ptr [4*edx + 4903424]
  46365c: mov ecx, dword ptr [ecx + 4]
  46365f: mov edx, dword ptr [eax + 8]
  463662: mov byte ptr [ecx + edx + 4], 0
  463667: mov eax, dword ptr [ebp + 8]
  46366a: mov ecx, dword ptr [4*eax + 4903424]
  463671: mov edx, dword ptr [ebp + 8]
  463674: mov eax, dword ptr [4*edx + 4903424]
