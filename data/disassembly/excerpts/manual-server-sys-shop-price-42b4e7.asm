; 상점 값 — 사기: 판매가격(item+0x98) × 개수 를 금전(char+0xE0)에서 뺀다(0x42b4e7). 팔기: 판매가격 / 2.0(상수 0x4812c0) 버림 × 개수(0x42bed4). 수리: (100 - 버림(지금내구/최대내구(+0x20) × 100.0)) × 수리가격(word +0x92), 수리여부(+0x90) 0 이면 못 고침(0x4319c0)
; 출처: Novaonline.exe (5.99 서버팩, md5 d36015ac…aa71) · macOS objdump -d --x86-asm-syntax=intel · 오프셋은 10진수
; ;; 뒤는 문자열(CP949)·가져온 함수 이름 주석

; ---- 0x42b4dc-0x42b530 ----
42b4d9  add	eax, 356
42b4de  push	eax
42b4df  call	0x4773ab <.text+0x763ab>
42b4e4  add	esp, 4
42b4e7  mov	ecx, dword ptr [edi + 152]
42b4ed  imul	ecx, eax
42b4f0  mov	edx, dword ptr [esi + 224]
42b4f6  sub	edx, ecx
42b4f8  mov	eax, dword ptr [ebp + 8]
42b4fb  mov	ecx, dword ptr [eax + 16]
42b4fe  mov	dword ptr [ecx + 224], edx
42b504  jmp	0x42b52d <.text+0x2a52d>
42b506  mov	edx, dword ptr [ebp + 8]
42b509  mov	eax, dword ptr [edx + 16]
42b50c  mov	ecx, dword ptr [ebp + 8]
42b50f  mov	edx, dword ptr [ecx + 620]
42b515  mov	eax, dword ptr [eax + 224]
42b51b  sub	eax, dword ptr [edx + 152]
42b521  mov	ecx, dword ptr [ebp + 8]
42b524  mov	edx, dword ptr [ecx + 16]
42b527  mov	dword ptr [edx + 224], eax
42b52d  mov	eax, dword ptr [ebp + 8]
42b530  push	eax

; ---- 0x42becb-0x42bf66 ----
42becb  mov	eax, dword ptr [ebp + 8]
42bece  mov	ecx, dword ptr [eax + 620]
42bed4  mov	edx, dword ptr [ecx + 152]
42beda  mov	dword ptr [ebp - 288], edx
42bee0  00	mov	dword ptr [ebp - 284], 0
42beea  fild	qword ptr [ebp - 288]
42bef0  fdiv	qword ptr [4723392]
42bef6  call	0x4783d4 <.text+0x773d4>
42befb  mov	dword ptr [ebp - 272], eax
42bf01  mov	eax, dword ptr [ebp + 8]
42bf04  xor	ecx, ecx
42bf06  mov	cl, byte ptr [eax + 628]
42bf0c  mov	edx, dword ptr [ebp - 272]
42bf12  imul	edx, ecx
42bf15  mov	dword ptr [ebp - 272], edx
42bf1b  mov	eax, dword ptr [ebp + 8]
42bf1e  mov	ecx, dword ptr [eax + 16]
42bf21  mov	edx, dword ptr [ecx + 224]
42bf27  xor	eax, eax
42bf29  mov	dword ptr [ebp - 296], edx
42bf2f  mov	dword ptr [ebp - 292], eax
42bf35  cmp	dword ptr [ebp - 292], 1
42bf3c  jl	0x42bf66 <.text+0x2af66>
42bf3e  jg	0x42bf4c <.text+0x2af4c>
42bf40  e2	cmp	dword ptr [ebp - 296], 3805032704
42bf4a  jb	0x42bf66 <.text+0x2af66>
42bf4c  push	0
42bf4e  push	0
42bf50  push	4758084   ;; "돈을 너무 많이 가지고 계시네요"
42bf55  mov	ecx, dword ptr [ebp + 8]
42bf58  push	ecx
42bf59  call	0x42ab01 <.text+0x29b01>
42bf5e  add	esp, 16
42bf61  jmp	0x42c107 <.text+0x2b107>
42bf66  mov	edx, dword ptr [ebp + 8]

; ---- 0x4319c0-0x431a56 ----
4319c0  mov	eax, dword ptr [ebp - 528]
4319c6  mov	ecx, dword ptr [eax + 12]
4319c9  xor	edx, edx
4319cb  mov	dl, byte ptr [ecx + 144]
4319d1  test	edx, edx
4319d3  je	0x431b0d <.text+0x30b0d>
4319d9  mov	eax, dword ptr [ebp + 8]
4319dc  00	mov	dword ptr [eax + 616], 8
4319e6  mov	ecx, dword ptr [ebp - 528]
4319ec  mov	edx, dword ptr [ecx + 4]
4319ef  mov	dword ptr [ebp - 548], edx
4319f5  00	mov	dword ptr [ebp - 544], 0
4319ff  fild	qword ptr [ebp - 548]
431a05  mov	eax, dword ptr [ebp - 528]
431a0b  mov	ecx, dword ptr [eax + 12]
431a0e  mov	edx, dword ptr [ecx + 32]
431a11  mov	dword ptr [ebp - 556], edx
431a17  00	mov	dword ptr [ebp - 552], 0
431a21  fild	qword ptr [ebp - 556]
431a27  fdivp	st(1), st
431a29  fmul	qword ptr [4723368]
431a2f  call	0x4783d4 <.text+0x773d4>
431a34  mov	ecx, 100
431a39  sub	ecx, eax
431a3b  mov	edx, dword ptr [ebp - 528]
431a41  mov	eax, dword ptr [edx + 12]
431a44  xor	edx, edx
431a46  mov	dx, word ptr [eax + 146]
431a4d  imul	ecx, edx
431a50  mov	dword ptr [ebp - 520], ecx
431a56  cmp	dword ptr [ebp - 520], 0
