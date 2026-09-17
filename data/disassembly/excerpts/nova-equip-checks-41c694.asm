; 장착 0x41c694 — 129 는 투핸드어택 필요 · 속성→자리 표 · 132 갑옷과 속성 10 은 함께 못 입음
; 출처: ~/Downloads/5.99 서버팩/Novaonline.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
;
  41c90b: call 0x45cecb <.text+0x5becb>
  41c910: add esp, 12
  41c913: xor al, al
  41c915: jmp 0x41c960 <.text+0x1b960>
  41c917: mov edx, dword ptr [ebp + 12]
  41c91a: xor eax, eax
  41c91c: mov al, byte ptr [edx + 27]
  41c91f: cmp eax, 129
  41c924: jne 0x41c95e <.text+0x1b95e>
  41c926: push 4754284
  ...
  41cb31: cmp dword ptr [ebp - 12], 13
  41cb35: ja 0x41d4e5 <.text+0x1c4e5>
  41cb3b: mov eax, dword ptr [ebp - 12]
  41cb3e: jmp dword ptr [4*eax + 4314497]
  41cb45: mov ecx, dword ptr [ebp - 4]
  41cb48: mov edx, dword ptr [ecx + 16]
  41cb4b: xor eax, eax
  41cb4d: mov al, byte ptr [edx + 114]
  41cb50: test eax, eax
  41cb52: je 0x41cb6c <.text+0x1bb6c>
  ...
  41cc35: mov ecx, dword ptr [eax + 16]
  41cc38: cmp dword ptr [ecx + 424], 0
  41cc3f: je 0x41cc6c <.text+0x1bc6c>
  41cc41: mov edx, dword ptr [ebp - 8]
  41cc44: mov eax, dword ptr [edx + 12]
  41cc47: xor ecx, ecx
  41cc49: mov cl, byte ptr [eax + 27]
  41cc4c: cmp ecx, 132
  41cc52: jne 0x41cc6c <.text+0x1bc6c>
  41cc54: push 4754504
  41cc59: push 3
  ...
  41d374: je 0x41d3aa <.text+0x1c3aa>
  41d376: mov eax, dword ptr [ebp - 4]
  41d379: mov ecx, dword ptr [eax + 16]
  41d37c: mov edx, dword ptr [ecx + 380]
  41d382: mov eax, dword ptr [edx + 12]
  41d385: xor ecx, ecx
  41d387: mov cl, byte ptr [eax + 27]
  41d38a: cmp ecx, 132
  41d390: jne 0x41d3aa <.text+0x1c3aa>
  41d392: push 4754540
  41d397: push 3
