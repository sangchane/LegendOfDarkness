; 키 입력 경로 (2005 = 5.99 클라이언트 Legend.exe) — 윈도우 키 메시지 → 스캔 코드 → 게임 키 번호 → 게임 화면 분기표
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe · 맥 objdump, 오프셋 10진수 · ";;" 는 임포트·문자열 주석
;
; 0x4eae73 WM_KEYDOWN(256): 키 번호 = lParam 비트 16~23(스캔 코드), 확장 키(비트 24)면 +128. 가상 키(VK)는 안 쓴다.
;   WM_SYSKEYDOWN(260)·WM_SYSKEYUP(261)에서 VK 115(F4)+Alt 는 종료 깃발. 입력 관리자 [0x6892c0] 의 0x4af1d0 으로 넘긴다.
; 0x4ae013 입력 관리자 초기화: 수식키 표 [+52+스캔] Alt(0x38·0xb8)=1 · Ctrl(0x1d·0x9d)=2 · Shift(0x2a·0x36)=4,
;   보통 글자표 [+308] ← 0x68f1a0(256바이트), Shift 글자표 [+564] ← 0x68f0a0. 자체 반복 칸 지연 500 · 간격 150(읽는 곳 못 찾음).
; 0x4af210: Shift 가 눌려 있으면 Shift 표, 아니면 보통 표로 바꿔 사건(종류 8, +16 키 번호, +17 수식키)을 만든다.
;   키 번호: 글자·숫자는 ASCII, F1~F10 = 133~142, F11 143, F12 144, ←128 ↑129 →130 ↓131, Del 132, Home 145, End 146, PgUp 147, PgDn 148.
; 0x5c8478 게임 화면: 키 번호 9~142 를 표 0x890e1c(=8982044) 로 가른다. 0x5c8c64: z·← 서(3), x·↓ 남(2), v·→ 동(1), c·↑ 북(0).

  4ae016: mov al, 1
  4ae018: mov byte ptr [edi + 108], al
  4ae01b: mov byte ptr [edi + 236], al
  4ae021: mov dl, 2
  4ae023: mov byte ptr [edi + 81], dl
  4ae026: mov byte ptr [edi + 209], dl
  4ae02c: mov cl, 4
  4ae02e: mov byte ptr [edi + 94], cl
  4ae031: mov byte ptr [edi + 106], cl
  4ae034: add edi, 308
  4ae03a: mov esi, 6877600
  4ae03f: mov ecx, 64
  4ae044: rep  movsd dword ptr es:[edi], dword ptr [esi]
  4ae046: mov edi, dword ptr [ebp - 44]
  4ae049: add edi, 564
  4ae04f: mov esi, 6877344
  4ae054: mov ecx, 64
  4ae059: rep  movsd dword ptr es:[edi], dword ptr [esi]
  ...
  4af272: mov edx, dword ptr [ebp - 32]
  4af275: mov ebx, dword ptr [ebp - 28]
  4af278: mov al, byte ptr [edx + ebx + 820]
  4af27f: or al, -128
  4af281: mov byte ptr [edx + ebx + 820], al
  4af288: movzx ecx, byte ptr [ebx + 1076]
  4af28f: movzx esi, byte ptr [edx + ebx + 52]
  4af294: or ecx, esi
  4af296: movsx eax, cl
  4af299: mov byte ptr [ebx + 1076], al
  4af29f: test al, 4
  4af2a1: je 0x4af2af <.text+0x882af>
  4af2a3: mov esi, ebx
  4af2a5: movzx ebx, byte ptr [edx + esi + 564]
  4af2ad: jmp 0x4af2b9 <.text+0x882b9>
  4af2af: mov esi, ebx
  4af2b1: movzx ebx, byte ptr [edx + esi + 308]
  4af2b9: mov esi, dword ptr [ebp + 16]
  ...
  4eae73: mov eax, ebp
  4eae75: and eax, 16711680
  4eae7a: sar eax, 16
  4eae7d: mov dword ptr [esp + 28], eax
  4eae81: test ebp, 16777216
  4eae87: je 0x4eae95 <.text+0xc3e95>
  4eae89: add eax, 128
  4eae8e: movzx edx, al
  4eae91: mov dword ptr [esp + 28], edx
  4eae95: mov eax, dword ptr [esp + 28]
  4eae99: test eax, eax
  4eae9b: je 0x4eaefe <.text+0xc3efe>
  4eae9d: cmp eax, 28
  4eaea0: je 0x4eaf29 <.text+0xc3f29>
  4eaea6: cmp eax, 156
  4eaeab: je 0x4eaf29 <.text+0xc3f29>
  4eaead: mov eax, dword ptr [7686340]
  4eaeb2: test eax, eax
  4eaeb4: je 0x4eaeda <.text+0xc3eda>
  4eaeb6: mov ebp, dword ptr [6853312]
  4eaebc: test ebp, ebp
  4eaebe: je 0x4eaf29 <.text+0xc3f29>
  4eaec0: call dword ptr [8665844]   ;; GetMessageTime
  4eaec6: mov esi, dword ptr [esi + 40]
  4eaec9: push ebx
  4eaeca: push eax
  4eaecb: push esi
  4eaecc: mov edx, dword ptr [esp + 40]
  4eaed0: push edx
  4eaed1: mov ecx, ebp
  4eaed3: call 0x4b03c0 <.text+0x893c0>
  4eaed8: jmp 0x4eaf29 <.text+0xc3f29>
  ...
  5c8478: mov al, byte ptr [esi + 12]
  5c847b: cmp al, 8
  5c847d: jne 0x5c8829 <.text+0x1a1829>
  5c8483: movzx eax, byte ptr [esi + 16]
  5c8487: lea edx, [eax - 9]
  5c848a: cmp edx, 133
  5c8490: ja 0x5c8825 <.text+0x1a1825>
  5c8496: mov edx, dword ptr [4*eax + 8982044]
  5c849d: jmp edx
  ...
  5c8c64: movzx edx, word ptr [esp + 4]
  5c8c69: lea eax, [edx - 99]
  5c8c6c: cmp eax, 49
  5c8c6f: ja 0x5c8c8a <.text+0x1a1c8a>
  5c8c71: mov eax, dword ptr [4*edx + 8982324]
  5c8c78: jmp eax
  5c8c7a: mov eax, 3
  5c8c7f: ret 4
  5c8c82: mov eax, 2
  5c8c87: ret 4
  5c8c8a: xor eax, eax
  5c8c8c: ret 4
  5c8c8f: mov eax, 1
  5c8c94: ret 4
