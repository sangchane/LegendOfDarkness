; 단축키 갈래 (2005 = 5.99 클라이언트 Legend.exe) — 게임 화면·오른쪽 판이 키 번호로 하는 일
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe · 맥 objdump, 오프셋 10진수
;
; 0x5c8574 숫자줄: 1~9 → 1~9, 0 → 10, - → 11, = → 12. 수식키 없음: 판(0x4ca570)이 기술·마법 쪽이면 그 칸을 쓰고,
;   아니면 0x5c9034 → 0~9 만 매크로 줄([obj+67132+64n])을 채팅으로 보낸다. Ctrl=+0x1000 · Alt=+0x2000 · Ctrl+Alt=+0x3000 을
;   붙여 감정표현 표 0x869880(=8820864, 64바이트 줄) 의 +0 단축키와 맞으면 0x1D 패킷에 줄의 +4 번호(0~35)를 실어 보낸다.
; 0x4ca570: 판 쪽 [+424] 1 → 왼쪽 판 n칸, 2 → 오른쪽 판 n칸, 3 → 1~6 왼쪽·7~12 오른쪽(n−6).
; 0x4c86cd 오른쪽 판 탭: Esc · A=0 · S=1 · D=2 · F=3 · G=4 · H=5 (대소문자 같음). 0x4cac07: Q=0 · W=1 · E=2 · R=3 판 쪽, T=0x50fa70,
;   F3(135)·F4(136) 은 창(1620바이트) 하나씩 연다.
; 게임 화면 표 0x890e1c 의 나머지: Tab 9, Space 32(평타 0x5c92c4), ! 33(외치기 "! "), " 34(귓속말 입력칸), Enter 13·' 39(채팅 ": "),
;   ` 96(0x44 패킷 두 번), b 98(1초에 한 번, 발밑 줍기 0x4f4270), j 106(0x2D 패킷), r 114(Ctrl+R=0x38 새로고침),
;   F1 133(안내서), F5 137(0x38 새로고침), F9 141, F10 142(친구 목록). F12 144 는 0x5f0f30 이 화면을 lod%03d.bmp 로 찍는다.

  4c86cd: movzx edx, byte ptr [ebp + 16]
  4c86d1: lea eax, [edx - 27]
  4c86d4: cmp eax, 88
  4c86d7: ja 0x4c8899 <.text+0xa1899>
  4c86dd: mov eax, dword ptr [4*edx + 8797364]
  4c86e4: jmp eax
  4c86e6: mov ecx, dword ptr [esi + 512]
  4c86ec: cmp ecx, dword ptr [esi + 484]
  4c86f2: je 0x4c88b2 <.text+0xa18b2>
  4c86f8: jmp 0x4c8899 <.text+0xa1899>
  4c86fd: movsx edi, byte ptr [ebp + 17]
  4c8701: lea eax, [esi + 88]
  4c8704: push eax
  4c8705: mov ecx, esi
  4c8707: call 0x5e0850 <.text+0x1b9850>
  ...
  4cac07: movzx edx, byte ptr [ebx + 16]
  4cac0b: lea eax, [edx - 69]
  4cac0e: cmp eax, 67
  4cac11: ja 0x4cacb4 <.text+0xa3cb4>
  4cac17: mov eax, dword ptr [4*edx + 8797644]
  4cac1e: jmp eax
  4cac20: mov eax, 2
  4cac25: mov byte ptr [esi + 424], al
  4cac2b: push eax
  4cac2c: mov ecx, esi
  4cac2e: call 0x4cad60 <.text+0xa3d60>
  4cac33: mov eax, 1
  4cac38: jmp 0x4cacba <.text+0xa3cba>
  4cac3d: xor eax, eax
  4cac3f: mov byte ptr [esi + 424], al
  4cac45: push eax
  4cac46: mov ecx, esi
  4cac48: call 0x4cad60 <.text+0xa3d60>
  4cac4d: mov eax, 1
  4cac52: jmp 0x4cacba <.text+0xa3cba>
  4cac54: mov eax, 3
  ...
  5c8574: movsx edx, al
  5c8577: mov dword ptr [ebp - 24], edx
  5c857a: xor eax, eax
  5c857c: mov dword ptr [ebp - 28], eax
  5c857f: cmp edx, 48
  5c8582: je 0x5c88ea <.text+0x1a18ea>
  5c8588: mov eax, edx
  5c858a: cmp eax, 49
  5c858d: jl 0x5c859c <.text+0x1a159c>
  5c858f: cmp eax, 57
  5c8592: jg 0x5c859c <.text+0x1a159c>
  5c8594: lea edx, [eax - 48]
  5c8597: mov dword ptr [ebp - 28], edx
  5c859a: jmp 0x5c85ae <.text+0x1a15ae>
  5c859c: cmp eax, 45
  5c859f: je 0x5c88dd <.text+0x1a18dd>
  5c85a5: cmp eax, 61
  5c85a8: je 0x5c88d0 <.text+0x1a18d0>
  5c85ae: mov al, byte ptr [esi + 17]
  5c85b1: mov dl, al
  5c85b3: and dl, 3
  5c85b6: jne 0x5c8601 <.text+0x1a1601>
  5c85b8: test al, 4
  5c85ba: jne 0x5c882b <.text+0x1a182b>
  5c85c0: mov ecx, dword ptr [7556644]
  5c85c6: mov eax, dword ptr [ebp - 28]
  5c85c9: movzx edx, al
  5c85cc: push edx
  5c85cd: call 0x4ca570 <.text+0xa3570>
  5c85d2: movzx edx, al
  5c85d5: test edx, edx
  5c85d7: jne 0x5c882b <.text+0x1a182b>
  5c85dd: mov eax, dword ptr [ebp - 28]
  5c85e0: cmp eax, 10
  5c85e3: je 0x5c85ef <.text+0x1a15ef>
  5c85e5: cmp eax, 10
  5c85e8: jl 0x5c85f4 <.text+0x1a15f4>
  5c85ea: jmp 0x5c882b <.text+0x1a182b>
  5c85ef: xor eax, eax
  5c85f1: mov dword ptr [ebp - 28], eax
  5c85f4: push eax
  5c85f5: mov ecx, ebx
  5c85f7: call 0x5c9034 <.text+0x1a2034>
  ...
  5c8601: cmp dl, 2
  5c8604: je 0x5c88c4 <.text+0x1a18c4>
  5c860a: cmp dl, 1
  5c860d: je 0x5c88b8 <.text+0x1a18b8>
  5c8613: cmp dl, 3
  5c8616: je 0x5c88ac <.text+0x1a18ac>
  5c861c: mov ebx, 8820864
  5c8621: mov eax, dword ptr [8820864]
  5c8626: test eax, eax
  5c8628: jl 0x5c882b <.text+0x1a182b>
  5c862e: mov edx, dword ptr [8820864]
  5c8634: mov eax, dword ptr [ebp - 24]
  5c8637: cmp eax, edx
  5c8639: jne 0x5c865a <.text+0x1a165a>
  5c863b: mov al, byte ptr [ebx + 4]
  5c863e: mov byte ptr [ebp - 68], 29
  5c8642: mov byte ptr [ebp - 67], al
  5c8645: mov byte ptr [ebp - 66], 0
  5c8649: mov ecx, dword ptr [7556672]
  5c864f: lea edx, [ebp - 68]
  5c8652: push 2
  5c8654: push edx
  5c8655: call 0x5fdf00 <.text+0x1d6f00>
  5c865a: add ebx, 64
  ...
  5c88ac: or dword ptr [ebp - 24], 12288
  5c88b3: jmp 0x5c861c <.text+0x1a161c>
  5c88b8: or dword ptr [ebp - 24], 8192
  5c88bf: jmp 0x5c861c <.text+0x1a161c>
  5c88c4: or dword ptr [ebp - 24], 4096
  5c88cb: jmp 0x5c861c <.text+0x1a161c>
  5c88d0: mov eax, 12
  5c88d5: mov dword ptr [ebp - 28], eax
  5c88d8: jmp 0x5c85ae <.text+0x1a15ae>
  5c88dd: mov eax, 11
  5c88e2: mov dword ptr [ebp - 28], eax
  5c88e5: jmp 0x5c85ae <.text+0x1a15ae>
  5c88ea: mov eax, 10
  5c88ef: mov dword ptr [ebp - 28], eax
  5c88f2: jmp 0x5c85ae <.text+0x1a15ae>
  ...
  5f0f38: cmp al, 8
  5f0f3a: jne 0x5f0f47 <.text+0x1c9f47>
  5f0f3c: movzx eax, byte ptr [edx + 16]
  5f0f40: cmp eax, 144
  5f0f45: je 0x5f0f4f <.text+0x1c9f4f>
  5f0f47: xor eax, eax
  5f0f49: add esp, 4
  5f0f4c: ret 4
  5f0f4f: call 0x5f0f64 <.text+0x1c9f64>
