; item_drop 명령(죽을 때) 0x46f941 — 금전: r=20..49, 바닥에 (금전×r/100)×2 를 떨구고 금전 0. 가방 0~34칸: 떨굼여부(+0x94) 0 이면 안 떨굼, 아니면 1/10 사라짐·9/10 바닥(겹친 것은 rand%개수). 장비 13칸: 칸마다 1/10 확률 + 떨굼여부 1 이면 벗겨서 떨굼
; 출처: Novaonline.exe (5.99 서버팩, md5 d36015ac…aa71) · macOS objdump -d --x86-asm-syntax=intel · 오프셋은 10진수
; ;; 뒤는 문자열(CP949)·가져온 함수 이름 주석

; ---- 0x46f95d-0x46fb57 ----
46f95d  mov	dword ptr [ebp - 4], 0
46f964  mov	dword ptr [ebp - 8], 0
46f96b  mov	eax, dword ptr [ebp + 8]
46f96e  mov	ecx, dword ptr [4*eax + 4903424]
46f975  mov	edx, dword ptr [ecx + 32]
46f978  mov	dword ptr [ebp - 12], edx
46f97b  cmp	dword ptr [ebp - 4], 20
46f97f  jae	0x46f993 <.text+0x6e993>
46f981  call	0x476d82 <.text+0x75d82>
46f986  cdq
46f987  mov	ecx, 50
46f98c  idiv	ecx
46f98e  mov	dword ptr [ebp - 4], edx
46f991  jmp	0x46f97b <.text+0x6e97b>
46f993  mov	edx, dword ptr [ebp - 12]
46f996  mov	eax, dword ptr [edx + 16]
46f999  cmp	dword ptr [eax + 224], 0
46f9a0  je	0x46f9e7 <.text+0x6e9e7>
46f9a2  mov	ecx, dword ptr [ebp - 12]
46f9a5  mov	edx, dword ptr [ecx + 4]
46f9a8  push	edx
46f9a9  mov	eax, dword ptr [ebp - 12]
46f9ac  mov	ecx, dword ptr [eax + 16]
46f9af  mov	eax, dword ptr [ecx + 224]
46f9b5  imul	eax, dword ptr [ebp - 4]
46f9b9  xor	edx, edx
46f9bb  mov	ecx, 100
46f9c0  div	ecx
46f9c2  shl	eax
46f9c4  push	eax
46f9c5  mov	edx, dword ptr [ebp - 12]
46f9c8  mov	eax, dword ptr [edx + 16]
46f9cb  add	eax, 88
46f9ce  push	eax
46f9cf  call	0x422635 <.text+0x21635>
46f9d4  add	esp, 12
46f9d7  mov	ecx, dword ptr [ebp - 12]
46f9da  mov	edx, dword ptr [ecx + 16]
46f9dd  00	mov	dword ptr [edx + 224], 0
46f9e7  mov	dword ptr [ebp - 16], 0
46f9ee  jmp	0x46f9f9 <.text+0x6e9f9>
46f9f0  mov	eax, dword ptr [ebp - 16]
46f9f3  add	eax, 1
46f9f6  mov	dword ptr [ebp - 16], eax
46f9f9  cmp	dword ptr [ebp - 16], 35
46f9fd  jae	0x46fada <.text+0x6eada>
46fa03  mov	ecx, dword ptr [ebp - 12]
46fa06  mov	edx, dword ptr [ecx + 16]
46fa09  mov	eax, dword ptr [ebp - 16]
46fa0c  mov	ecx, dword ptr [edx + 4*eax + 432]
46fa13  mov	dword ptr [ebp - 20], ecx
46fa16  cmp	dword ptr [ebp - 20], 0
46fa1a  je	0x46fad5 <.text+0x6ead5>
46fa20  mov	edx, dword ptr [ebp - 20]
46fa23  mov	eax, dword ptr [edx + 12]
46fa26  xor	ecx, ecx
46fa28  mov	cl, byte ptr [eax + 148]
46fa2e  test	ecx, ecx
46fa30  jne	0x46fa45 <.text+0x6ea45>
46fa32  mov	edx, dword ptr [ebp - 20]
46fa35  mov	eax, dword ptr [edx + 12]
46fa38  mov	ecx, dword ptr [eax]
46fa3a  push	ecx
46fa3b  call	0x477504 <.text+0x76504>
46fa40  add	esp, 4
46fa43  jmp	0x46f9f0 <.text+0x6e9f0>
46fa45  call	0x476d82 <.text+0x75d82>
46fa4a  cdq
46fa4b  mov	ecx, 10
46fa50  idiv	ecx
46fa52  mov	dword ptr [ebp - 4], edx
46fa55  cmp	dword ptr [ebp - 4], 1
46fa59  je	0x46fab9 <.text+0x6eab9>
46fa5b  mov	edx, dword ptr [ebp - 20]
46fa5e  mov	eax, dword ptr [edx + 12]
46fa61  xor	ecx, ecx
46fa63  mov	cl, byte ptr [eax + 16]
46fa66  cmp	ecx, 1
46fa69  jl	0x46fa96 <.text+0x6ea96>
46fa6b  mov	edx, dword ptr [ebp - 20]
46fa6e  xor	eax, eax
46fa70  mov	al, byte ptr [edx + 8]
46fa73  cmp	eax, 1
46fa76  jle	0x46fa96 <.text+0x6ea96>
46fa78  call	0x476d82 <.text+0x75d82>
46fa7d  mov	ecx, dword ptr [ebp - 20]
46fa80  xor	edx, edx
46fa82  mov	dl, byte ptr [ecx + 8]
46fa85  mov	ecx, edx
46fa87  cdq
46fa88  idiv	ecx
46fa8a  mov	dword ptr [ebp - 8], edx
46fa8d  mov	edx, dword ptr [ebp - 20]
46fa90  mov	al, byte ptr [ebp - 8]
46fa93  mov	byte ptr [edx + 8], al
46fa96  mov	ecx, dword ptr [ebp - 12]
46fa99  mov	edx, dword ptr [ecx + 4]
46fa9c  push	edx
46fa9d  push	1
46fa9f  push	0
46faa1  push	0
46faa3  mov	eax, dword ptr [ebp - 20]
46faa6  push	eax
46faa7  mov	ecx, dword ptr [ebp - 12]
46faaa  mov	edx, dword ptr [ecx + 16]
46faad  add	edx, 88
46fab0  push	edx
46fab1  call	0x4227e9 <.text+0x217e9>
46fab6  add	esp, 24
46fab9  mov	eax, dword ptr [ebp - 20]
46fabc  mov	cl, byte ptr [eax + 8]
46fabf  push	ecx
46fac0  mov	edx, dword ptr [ebp - 20]
46fac3  mov	eax, dword ptr [edx + 12]
46fac6  push	eax
46fac7  push	12
46fac9  mov	ecx, dword ptr [ebp + 8]
46facc  push	ecx
46facd  call	0x41b460 <.text+0x1a460>
46fad2  add	esp, 16
46fad5  jmp	0x46f9f0 <.text+0x6e9f0>
46fada  mov	dword ptr [ebp - 16], 0
46fae1  jmp	0x46faec <.text+0x6eaec>
46fae3  mov	edx, dword ptr [ebp - 16]
46fae6  add	edx, 1
46fae9  mov	dword ptr [ebp - 16], edx
46faec  cmp	dword ptr [ebp - 16], 12
46faf0  ja	0x46fbd2 <.text+0x6ebd2>
46faf6  call	0x476d82 <.text+0x75d82>
46fafb  cdq
46fafc  mov	ecx, 10
46fb01  idiv	ecx
46fb03  mov	dword ptr [ebp - 4], edx
46fb06  cmp	dword ptr [ebp - 4], 9
46fb0a  jne	0x46fbcd <.text+0x6ebcd>
46fb10  mov	edx, dword ptr [ebp - 12]
46fb13  mov	eax, dword ptr [edx + 16]
46fb16  mov	ecx, dword ptr [ebp - 16]
46fb19  cmp	dword ptr [eax + 4*ecx + 376], 0
46fb21  je	0x46fbcd <.text+0x6ebcd>
46fb27  mov	edx, dword ptr [ebp - 12]
46fb2a  mov	eax, dword ptr [edx + 16]
46fb2d  mov	ecx, dword ptr [ebp - 16]
46fb30  mov	edx, dword ptr [eax + 4*ecx + 376]
46fb37  mov	eax, dword ptr [edx + 12]
46fb3a  xor	ecx, ecx
46fb3c  mov	cl, byte ptr [eax + 148]
46fb42  cmp	ecx, 1
46fb45  je	0x46fb49 <.text+0x6eb49>
46fb47  jmp	0x46fae3 <.text+0x6eae3>
46fb49  mov	edx, dword ptr [ebp - 16]
46fb4c  add	edx, 1
46fb4f  push	edx
46fb50  mov	eax, dword ptr [ebp - 12]
46fb53  mov	ecx, dword ptr [eax + 4]
46fb56  push	ecx
46fb57  call	0x41c55f <.text+0x1b55f>
