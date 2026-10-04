; 아이템 넣기 0x41b06e — 최대개수(+0x12) 가 0 이면 44 로 친다. 타입(+0x10) 0 은 한 칸에 하나, 아니면 같은 이름 칸에 최대개수까지 겹친다. 가방은 char+0x1B0 의 60칸
; 출처: Novaonline.exe (5.99 서버팩, md5 d36015ac…aa71) · macOS objdump -d --x86-asm-syntax=intel · 오프셋은 10진수
; ;; 뒤는 문자열(CP949)·가져온 함수 이름 주석

; ---- 0x41b08d-0x41b0e4 ----
41b08d  mov	eax, dword ptr [ebp + 16]
41b090  mov	ecx, dword ptr [eax + 12]
41b093  xor	edx, edx
41b095  mov	dl, byte ptr [ecx + 18]
41b098  test	edx, edx
41b09a  je	0x41b0af <.text+0x1a0af>
41b09c  mov	eax, dword ptr [ebp + 16]
41b09f  mov	ecx, dword ptr [eax + 12]
41b0a2  xor	edx, edx
41b0a4  mov	dl, byte ptr [ecx + 18]
41b0a7  cmp	edx, 300
41b0ad  jle	0x41b0b9 <.text+0x1a0b9>
41b0af  mov	eax, dword ptr [ebp + 16]
41b0b2  mov	ecx, dword ptr [eax + 12]
41b0b5  mov	byte ptr [ecx + 18], 44
41b0b9  mov	edx, dword ptr [ebp + 16]
41b0bc  mov	eax, dword ptr [edx + 12]
41b0bf  xor	ecx, ecx
41b0c1  mov	cl, byte ptr [eax + 16]
41b0c4  test	ecx, ecx
41b0c6  jne	0x41b1c2 <.text+0x1a1c2>
41b0cc  mov	dword ptr [ebp - 8], 0
41b0d3  mov	edx, dword ptr [ebp - 8]
41b0d6  mov	dword ptr [ebp - 12], edx
41b0d9  jmp	0x41b0e4 <.text+0x1a0e4>
41b0db  mov	eax, dword ptr [ebp - 12]
41b0de  add	eax, 1
41b0e1  mov	dword ptr [ebp - 12], eax
41b0e4  cmp	dword ptr [ebp - 12], 60

; ---- 0x41b323-0x41b360 ----
41b323  mov	eax, dword ptr [ebp + 16]
41b326  mov	ecx, dword ptr [eax + 12]
41b329  xor	edx, edx
41b32b  mov	dl, byte ptr [ecx + 18]
41b32e  cmp	dword ptr [ebp - 4], edx
41b331  jl	0x41b37c <.text+0x1a37c>
41b333  movsx	eax, byte ptr [ebp + 24]
41b337  test	eax, eax
41b339  jne	0x41b375 <.text+0x1a375>
41b33b  mov	ecx, dword ptr [ebp + 16]
41b33e  mov	edx, dword ptr [ecx + 12]
41b341  xor	eax, eax
41b343  mov	al, byte ptr [edx + 18]
41b346  push	eax
41b347  mov	ecx, dword ptr [ebp + 16]
41b34a  mov	edx, dword ptr [ecx + 12]
41b34d  mov	eax, dword ptr [edx]
41b34f  push	eax
41b350  push	4753380   ;; "%s %u개 이상 가질 수 없습니다"
41b355  push	4837472
41b35a  call	0x4779c0 <.text+0x769c0>
41b35f  add	esp, 16

; ---- 0x41afaf-0x41affe ----
41afaf  push	ebp
41afb0  mov	ebp, esp
41afb2  sub	esp, 8
41afb5  mov	dword ptr [ebp - 8], 3435973836
41afbc  mov	dword ptr [ebp - 4], 3435973836
41afc3  mov	dword ptr [ebp - 4], 0
41afca  mov	eax, dword ptr [ebp - 4]
41afcd  mov	dword ptr [ebp - 8], eax
41afd0  jmp	0x41afdb <.text+0x19fdb>
41afd2  mov	ecx, dword ptr [ebp - 8]
41afd5  add	ecx, 1
41afd8  mov	dword ptr [ebp - 8], ecx
41afdb  cmp	dword ptr [ebp - 8], 60
41afdf  jge	0x41aff8 <.text+0x19ff8>
41afe1  mov	edx, dword ptr [ebp - 8]
41afe4  mov	eax, dword ptr [ebp + 8]
41afe7  cmp	dword ptr [eax + 4*edx], 0
41afeb  jne	0x41aff6 <.text+0x19ff6>
41afed  mov	ecx, dword ptr [ebp - 4]
41aff0  add	ecx, 1
41aff3  mov	dword ptr [ebp - 4], ecx
41aff6  jmp	0x41afd2 <.text+0x19fd2>
41aff8  mov	eax, dword ptr [ebp - 4]
41affb  mov	esp, ebp
41affd  pop	ebp
41affe  ret
