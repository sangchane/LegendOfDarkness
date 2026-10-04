; manual-client-engine-balloon-name — 원작 2005(=5.99) Legend.exe
; 말풍선(0x0D): 사람·괴물(+0x1AC 종류 1·2)에만 뜨고, 3000ms 타이머(id 1)로 사라진다. 글 69바이트 넘으면 66+"..." 로 자른다.
; 말풍선 글 색 = legend.pal 번호: 보통 255 · 외침(1) 69 · 주문(2) 88. 이름표 색: 테두리 31, 사람 20, 깃발(+0x1FC=1) 40, 괴물·NPC(+0x200=1) 128.
; 오프셋은 맥 objdump 10진수. 주소는 16진수.
; --- 0x0D 처리: 종류 1·2 일 때만 BalloonPane(1476바이트) 생성
54a974  call 0x5d1190 <.text+0x1aa190>
54a979  mov dword ptr [ebp - 24], eax
54a97c  test eax, eax
54a97e  je 0x54a98e <.text+0x12398e>
54a980  mov al, byte ptr [eax + 428]
54a986  cmp al, 2
54a988  je 0x54a99e <.text+0x12399e>
54a98a  cmp al, 1
54a98c  je 0x54a99e <.text+0x12399e>
54a98e  mov ecx, dword ptr [ebp - 12]
54a991  mov dword ptr fs:[0], ecx
54a998  mov esp, ebp
54a99a  pop ebp
54a99b  ret 12
54a99e  push 0
54a9a0  push 1476
54a9a5  call 0x51cdb0 <.text+0xf5db0>
54a9aa  mov dword ptr [ebp - 32], eax
54a9ad  add esp, 8
; --- BalloonPane 생성자: 종류별 색, 69바이트 자르기, 3000ms 타이머
42d59e  mov eax, dword ptr [ebp - 28]
42d5a1  cmp eax, 2
42d5a4  je 0x42d7c8 <.text+0x67c8>
42d5aa  cmp eax, 1
42d5ad  je 0x42d7bb <.text+0x67bb>
42d5b3  mov eax, 255
42d5b8  mov dword ptr [ebp - 28], eax
42d64b  movsx eax, bx
42d64e  cmp eax, 69
42d651  jle 0x42d67d <.text+0x667d>
42d653  xor ebx, ebx
42d655  xor eax, eax
42d657  movzx eax, byte ptr [ecx + 8*eax + 432]
42d65f  push eax
42d660  call dword ptr [8665508]   ;; IsDBCSLeadByte
42d666  test eax, eax
42d668  je 0x42d66d <.text+0x666d>
42d66a  add ebx, 1
42d66d  add ebx, 1
42d670  movsx eax, bl
42d673  cmp eax, 66
42d676  jge 0x42d6cd <.text+0x66cd>
42d678  mov ecx, dword ptr [ebp - 44]
42d67b  jmp 0x42d657 <.text+0x6657>
42d67d  call 0x42efc0 <.text+0x7fc0>
42d682  mov ecx, dword ptr [7556692]
42d688  mov edx, dword ptr [ebp - 44]
42d68b  xor eax, eax
42d68d  push eax
42d68e  push eax
42d68f  push 3000
42d694  push 1
42d696  push edx
42d697  call 0x4ac5b0 <.text+0x855b0>
42d69c  call dword ptr [8666000]   ;; timeGetTime
42d6a2  mov edx, dword ptr [ebp - 44]
42d6a5  mov dword ptr [edx + 1464], eax
42d7bb  mov eax, 69
42d7c0  mov dword ptr [ebp - 28], eax
42d7c3  jmp 0x42d5bb <.text+0x65bb>
42d7c8  mov eax, 88
42d7cd  mov dword ptr [ebp - 28], eax
42d7d0  jmp 0x42d5bb <.text+0x65bb>
42d7d5  mov eax, 1
; --- 이름표 그리기: 테두리 31 → 본색 20/40/128
5d43e2  mov dl, byte ptr [esi + 508]
5d43e8  cmp dl, 1
5d43eb  je 0x5d44ce <.text+0x1ad4ce>
5d43f1  push 20
5d43f3  mov ecx, esi
5d43f5  call 0x48a8e0 <.text+0x638e0>
5d43fa  mov edx, dword ptr [esi + 512]
5d4400  cmp edx, 1
5d4403  je 0x5d44c0 <.text+0x1ad4c0>
5d4409  test edx, edx
5d440b  jne 0x5d4455 <.text+0x1ad455>
5d44c0  push 128
5d44c5  mov ecx, esi
5d44c7  call 0x48a8e0 <.text+0x638e0>
5d44cc  jmp 0x5d4455 <.text+0x1ad455>
5d44ce  push 40
5d44d0  mov ecx, esi
5d44d2  call 0x48a8e0 <.text+0x638e0>
; --- 괴물(+0x1AC=2)이면 이름표 모드 1(색 128)
54a47d  mov esi, dword ptr [ebp - 32]
54a480  mov al, byte ptr [esi + 428]
54a486  cmp al, 2
54a488  je 0x54a4af <.text+0x1234af>
54a48a  mov ebx, dword ptr [ebp - 44]
54a48d  mov esi, dword ptr [ebp - 48]
54a490  mov edi, dword ptr [ebp - 52]
54a4af  push 1
54a4b1  mov ecx, dword ptr [ebp - 24]
54a4b4  call 0x5d4220 <.text+0x1ad220>
54a4b9  jmp 0x54a48a <.text+0x12348a>
54a4bb  xor ebx, ebx
