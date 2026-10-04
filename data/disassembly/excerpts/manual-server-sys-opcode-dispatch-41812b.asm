; 게임 서버 opcode 분기 — 받은 패킷 [3] 바이트 - 5 를 바이트표 0x419171 → 점프표 0x4190bd 로 보낸다(0..118 → opcode 0x05..0x7B). 분기 목록은 docs/exe-manual/04-server-systems.md
; 출처: Novaonline.exe (5.99 서버팩, md5 d36015ac…aa71) · macOS objdump -d --x86-asm-syntax=intel · 오프셋은 10진수
; ;; 뒤는 문자열(CP949)·가져온 함수 이름 주석

; ---- 0x41812b-0x4181a0 ----
41812b  mov	eax, dword ptr [ebp + 8]
41812e  mov	ecx, dword ptr [4*eax + 4903424]
418135  mov	edx, dword ptr [ebp + 8]
418138  mov	eax, dword ptr [4*edx + 4903424]
41813f  mov	ecx, dword ptr [ecx + 12]
418142  mov	edx, dword ptr [eax + 20]
418145  xor	eax, eax
418147  mov	al, byte ptr [ecx + edx + 3]
41814b  mov	dword ptr [ebp - 16], eax
41814e  mov	ecx, dword ptr [ebp - 16]
418151  sub	ecx, 5
418154  mov	dword ptr [ebp - 16], ecx
418157  cmp	dword ptr [ebp - 16], 118
41815b  ja	0x419020 <.text+0x18020>
418161  mov	eax, dword ptr [ebp - 16]
418164  xor	edx, edx
418166  mov	dl, byte ptr [eax + 4297073]
41816c  jmp	dword ptr [4*edx + 4296893]
418173  mov	ecx, dword ptr [ebp + 8]
418176  push	ecx
418177  call	0x41fc41 <.text+0x1ec41>
41817c  add	esp, 4
41817f  jmp	0x41904e <.text+0x1804e>
418184  mov	edx, dword ptr [ebp - 12]
418187  xor	eax, eax
418189  mov	al, byte ptr [edx + 337]
41818f  test	eax, eax
418191  je	0x4181d8 <.text+0x171d8>
418193  mov	ecx, dword ptr [ebp - 12]
418196  00	mov	dword ptr [ecx + 352], 0
4181a0  mov	edx, dword ptr [ebp - 12]

; ---- 0x418fc1-0x419050 ----
418fc1  jmp	0x41904e <.text+0x1804e>
418fc6  mov	eax, dword ptr [ebp + 8]
418fc9  push	eax
418fca  call	0x46b84e <.text+0x6a84e>
418fcf  add	esp, 4
418fd2  jmp	0x41904e <.text+0x1804e>
418fd4  jmp	0x41904e <.text+0x1804e>
418fd6  mov	ecx, dword ptr [ebp + 8]
418fd9  push	ecx
418fda  call	0x46fbfd <.text+0x6ebfd>
418fdf  add	esp, 4
418fe2  jmp	0x41904e <.text+0x1804e>
418fe4  mov	edx, dword ptr [ebp + 8]
418fe7  push	edx
418fe8  call	0x46202d <.text+0x6102d>
418fed  add	esp, 4
418ff0  jmp	0x41904e <.text+0x1804e>
418ff2  mov	eax, dword ptr [ebp + 8]
418ff5  mov	ecx, dword ptr [4*eax + 4903424]
418ffc  mov	edx, dword ptr [ebp + 8]
418fff  mov	eax, dword ptr [4*edx + 4903424]
419006  mov	ecx, dword ptr [ecx + 12]
419009  mov	edx, dword ptr [eax + 20]
41900c  mov	eax, dword ptr [ebp - 12]
41900f  mov	eax, dword ptr [eax + 16]
419012  mov	cl, byte ptr [ecx + edx + 5]
419016  mov	byte ptr [eax + 242], cl
41901c  jmp	0x41904e <.text+0x1804e>
41901e  jmp	0x41904e <.text+0x1804e>
419020  mov	edx, dword ptr [ebp + 8]
419023  mov	eax, dword ptr [4*edx + 4903424]
41902a  mov	ecx, dword ptr [ebp + 8]
41902d  mov	edx, dword ptr [4*ecx + 4903424]
419034  mov	eax, dword ptr [eax + 12]
419037  mov	ecx, dword ptr [edx + 20]
41903a  xor	edx, edx
41903c  mov	dl, byte ptr [eax + ecx + 3]
419040  push	edx
419041  push	4752748   ;; " * 게임 서버: 정의되지 않은 패킷 입니다. (%d)\n"
419046  call	0x477069 <.text+0x76069>
41904b  add	esp, 8
41904e  mov	eax, dword ptr [ebp - 8]
