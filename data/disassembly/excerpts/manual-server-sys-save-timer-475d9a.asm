; 저장 — 15초 타이머 0x475d9a 가 접속자마다 0x4077d0(Save/User/<아이디>/ 8파일 + Save/map_reg.txt)을 부른다. 30분 타이머 0x475d35 는 랭킹 8파일
; 출처: Novaonline.exe (5.99 서버팩, md5 d36015ac…aa71) · macOS objdump -d --x86-asm-syntax=intel · 오프셋은 10진수
; ;; 뒤는 문자열(CP949)·가져온 함수 이름 주석

; ---- 0x475d35-0x475e57 ----
475d35  push	ebp
475d36  mov	ebp, esp
475d38  call	0x473ecb <.text+0x72ecb>
475d3d  mov	eax, dword ptr [ebp + 8]
475d40  push	eax
475d41  call	0x47420e <.text+0x7320e>
475d46  add	esp, 4
475d49  mov	ecx, dword ptr [ebp + 8]
475d4c  push	ecx
475d4d  call	0x4745ee <.text+0x735ee>
475d52  add	esp, 4
475d55  mov	edx, dword ptr [ebp + 8]
475d58  push	edx
475d59  call	0x4749c1 <.text+0x739c1>
475d5e  add	esp, 4
475d61  mov	eax, dword ptr [ebp + 8]
475d64  push	eax
475d65  call	0x474da5 <.text+0x73da5>
475d6a  add	esp, 4
475d6d  mov	ecx, dword ptr [ebp + 8]
475d70  push	ecx
475d71  call	0x475189 <.text+0x74189>
475d76  add	esp, 4
475d79  mov	edx, dword ptr [ebp + 8]
475d7c  push	edx
475d7d  call	0x47556d <.text+0x7456d>
475d82  add	esp, 4
475d85  mov	eax, dword ptr [ebp + 8]
475d88  push	eax
475d89  call	0x475951 <.text+0x74951>
475d8e  add	esp, 4
475d91  cmp	ebp, esp
475d98  pop	ebp
475d99  ret
475d9a  push	ebp
475d9b  mov	ebp, esp
475d9d  sub	esp, 16
475da0  mov	eax, 3435973836
475da5  mov	dword ptr [ebp - 16], eax
475da8  mov	dword ptr [ebp - 12], eax
475dab  mov	dword ptr [ebp - 8], eax
475dae  mov	dword ptr [ebp - 4], eax
475db1  mov	dword ptr [ebp - 16], 1
475db8  cmp	dword ptr [ebp + 8], 0
475dbc  je	0x475dd1 <.text+0x74dd1>
475dbe  mov	eax, dword ptr [4903116]
475dc3  push	eax
475dc4  push	4795684   ;; "저장완료 현재접속중인 유저는 %d 명 입니다、 \n"
475dc9  call	0x477069 <.text+0x76069>
475dce  add	esp, 8
475dd1  push	14
475dd3  call	0x40f9ef <.text+0xe9ef>
475dd8  add	esp, 4
475ddb  mov	dword ptr [ebp - 12], 0
475de2  jmp	0x475ded <.text+0x74ded>
475de4  mov	ecx, dword ptr [ebp - 12]
475de7  add	ecx, 1
475dea  mov	dword ptr [ebp - 12], ecx
475ded  mov	edx, dword ptr [ebp - 12]
475df0  cmp	edx, dword ptr [4903136]
475df6  jge	0x475e4a <.text+0x74e4a>
475df8  mov	eax, dword ptr [ebp - 12]
475dfb  mov	ecx, dword ptr [4*eax + 4903140]
475e02  mov	edx, dword ptr [4*ecx + 4903424]
475e09  mov	dword ptr [ebp - 4], edx
475e0c  cmp	dword ptr [ebp - 4], 0
475e10  je	0x475e48 <.text+0x74e48>
475e12  mov	eax, dword ptr [ebp - 4]
475e15  xor	ecx, ecx
475e17  mov	cl, byte ptr [eax + 61]
475e1a  test	ecx, ecx
475e1c  je	0x475e48 <.text+0x74e48>
475e1e  mov	edx, dword ptr [ebp - 4]
475e21  xor	eax, eax
475e23  mov	al, byte ptr [edx + 60]
475e26  test	eax, eax
475e28  je	0x475e48 <.text+0x74e48>
475e2a  mov	ecx, dword ptr [ebp - 4]
475e2d  mov	edx, dword ptr [ecx + 32]
475e30  mov	dword ptr [ebp - 8], edx
475e33  cmp	dword ptr [ebp - 8], 0
475e37  je	0x475e48 <.text+0x74e48>
475e39  mov	eax, dword ptr [ebp - 8]
475e3c  mov	ecx, dword ptr [eax + 16]
475e3f  push	ecx
475e40  call	0x4077d0 <.text+0x67d0>
475e45  add	esp, 4
475e48  jmp	0x475de4 <.text+0x74de4>
475e4a  add	esp, 16
475e4d  cmp	ebp, esp
475e54  mov	esp, ebp
475e56  pop	ebp
475e57  ret
