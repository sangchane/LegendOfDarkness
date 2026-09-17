; 파일 이름 만들기 0x4e8514 — 성별 "MW" · 부위 글자 0x86b614 · 번호 점프표 0x86b5d8 · %03d · 기술 'a'+n 또는 %02d.epf
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
;
  4e8514: push edi
  4e8515: push esi
  4e8516: push ebp
  4e8517: push ebx
  4e8518: sub esp, 148
  4e851e: movsx ebp, byte ptr [esp + 180]
  4e8526: mov eax, dword ptr [esp + 168]
  4e852d: xor edx, edx
  4e852f: mov dword ptr [esp + 136], edx
  4e8536: test ebp, ebp
  4e8538: je 0x4e854e <.text+0xc154e>
  4e853a: cmp ebp, 10
  4e853d: je 0x4e854e <.text+0xc154e>
  4e853f: movzx esi, byte ptr [eax]
  4e8542: mov dl, byte ptr [esi + 8828452]
  4e8548: mov byte ptr [esp + 16], dl
  4e854c: jmp 0x4e8553 <.text+0xc1553>
  4e854e: mov byte ptr [esp + 16], 77
  4e8553: mov dl, byte ptr [ebp + 8828436]
  4e8559: mov byte ptr [esp + 17], dl
  4e855d: cmp ebp, 14
  4e8560: ja 0x4e85b1 <.text+0xc15b1>
  4e8562: mov edx, dword ptr [4*ebp + 8828376]
  4e8569: jmp edx
  4e856b: movzx edx, word ptr [eax + 6]
  4e856f: jmp 0x4e85b1 <.text+0xc15b1>
  4e8571: movzx edx, word ptr [eax + 14]
  4e8575: jmp 0x4e85b1 <.text+0xc15b1>
  4e8577: movzx edx, word ptr [eax + 10]
  4e857b: jmp 0x4e85b1 <.text+0xc15b1>
  4e857d: movzx edx, word ptr [eax + 18]
  ...
  4e85e1: cmp eax, 65535
  4e85e6: je 0x4e8822 <.text+0xc1822>
  4e85ec: je 0x4e85f9 <.text+0xc15f9>
  4e85ee: add al, 97
  4e85f0: mov byte ptr [esp + 21], al
  4e85f4: mov edx, 6
  4e85f9: lea eax, [esp + 16]
  ...
  4e8816: jmp 0x4e867f <.text+0xc167f>
  4e881b: mov dl, 0
  4e881d: jmp 0x4e8625 <.text+0xc1625>
  4e8822: movzx edx, byte ptr [esp + 172]
  4e882a: lea eax, [esp + 21]
  4e882e: mov dword ptr [esp], eax
  4e8831: mov dword ptr [esp + 4], 8829856
