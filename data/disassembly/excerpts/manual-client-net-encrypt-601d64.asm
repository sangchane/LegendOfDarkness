; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 암호화: [opcode][순번][몸통 XOR], 순번 0x77a060 은 보낼 때마다 +1, 키 = 4바이트씩 겹친 9글자 열쇠 표 0x779f30, 씨앗 표 0x6b3340
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- opcode 그대로, 순번 붙이고 +1, 1단계: 열쇠 표와 XOR
601d64: push	edi
601d65: push	esi
601d66: push	ebp
601d67: push	ebx
601d68: sub	esp, 108
601d6b: mov	ebx, dword ptr [esp + 136]
601d72: mov	ebp, dword ptr [esp + 132]
601d79: mov	esi, dword ptr [esp + 128]
601d80: mov	cl, byte ptr [esi]
601d82: mov	byte ptr [ebx], cl
601d84: movzx	eax, byte ptr [7839840]
601d8b: mov	dword ptr [esp + 100], eax
601d8f: mov	dl, al
601d91: add	dl, 1
601d94: mov	byte ptr [7839840], dl
601d9a: mov	byte ptr [ebx + 1], al
601d9d: lea	ecx, [ebp - 1]
601da0: test	ecx, ecx
601da2: jle	0x6022c2
601da8: lea	ebp, [esi + 1]
601dab: mov	edi, dword ptr [esp + 136]
601db2: lea	eax, [edi + 2]
601db5: mov	dword ptr [esp + 104], eax
601db9: mov	edi, dword ptr [7839788]
601dbf: mov	dword ptr [esp + 76], eax
601dc3: mov	ebx, ecx
601dc5: shr	ebx, 2
601dc8: xor	esi, esi
601dca: test	ebx, ebx
601dcc: jle	0x601ec4
601dd2: cmp	ebx, 5
601dd5: jl	0x601e7c
601ddb: lea	edx, [ebx - 5]
601dde: lea	eax, [esi + 1]
601de1: mov	dword ptr [esp + 80], eax
601de5: lea	eax, [esi + 2]
601de8: mov	dword ptr [esp + 84], eax
601dec: lea	eax, [esi + 3]
601def: mov	dword ptr [esp + 60], ecx
601df3: mov	dword ptr [esp + 92], eax
601df7: mov	dword ptr [esp + 96], edx
601dfb: mov	dword ptr [esp + 64], ebx
601dff: mov	ecx, dword ptr [esp + 76]
601e03: mov	ebx, dword ptr [ebp]
601e06: mov	eax, esi
601e08: cdq
601e09: idiv	edi
601e0b: xor	ebx, dword ptr [4*edx + 7839792]
601e12: mov	dword ptr [ecx], ebx
601e14: mov	ebx, dword ptr [ebp + 4]

; ---- 2단계: 9바이트 덩어리 번호 b ≠ 순번이면 씨앗표[b] 와 XOR
601f2a: mov	ebp, dword ptr [esp + 132]
601f31: lea	eax, [ebp - 2]
601f34: cdq
601f35: idiv	edi
601f37: mov	ebp, eax
601f39: add	ebp, 1
601f3c: xor	edx, edx
601f3e: mov	dword ptr [esp + 68], edx
601f42: test	ebp, ebp
601f44: jle	0x602132
601f4a: mov	eax, edi
601f4c: shr	eax, 2
601f4f: mov	edx, edi
601f51: and	edx, 3
601f54: lea	esi, [eax - 5]
601f57: mov	dword ptr [esp + 96], esi
601f5b: mov	esi, edi
601f5d: imul	esi, dword ptr [esp + 68]
601f62: mov	dword ptr [esp + 92], edx
601f66: mov	dword ptr [esp + 88], eax
601f6a: mov	dword ptr [esp + 60], ecx
601f6e: mov	dword ptr [esp + 64], ebx
601f72: mov	dword ptr [esp + 56], edi
601f76: mov	ebx, dword ptr [esp + 68]
601f7a: movzx	ecx, bl
601f7d: mov	edx, dword ptr [esp + 100]
601f81: cmp	edx, ecx
601f83: je	0x60211b
601f89: mov	edi, dword ptr [esp + 136]
601f94: mov	dword ptr [esp + 80], edi
601f98: add	ecx, ecx
601f9a: add	ecx, ecx
601f9c: xor	eax, eax

; ---- 3단계: 몸통 전체를 씨앗표[순번] 과 XOR
602132: mov	eax, dword ptr [esp + 100]
602136: add	eax, eax
602138: add	eax, eax
60213a: mov	dword ptr [esp + 100], eax
60213e: mov	ebp, dword ptr [esp + 104]
602142: xor	eax, eax
602144: test	ebx, ebx
602146: jle	0x602242
60214c: cmp	ebx, 5
60214f: jl	0x60220c
602155: lea	esi, [ebx - 5]
602158: lea	edx, [eax + 1]
60215b: lea	edi, [eax + 2]
60215e: mov	dword ptr [esp + 84], edi
602162: lea	edi, [eax + 3]
602165: mov	dword ptr [esp + 52], edi
602169: mov	dword ptr [esp + 48], edx
60216d: mov	dword ptr [esp + 56], esi
602171: mov	dword ptr [esp + 60], ecx
602175: mov	dword ptr [esp + 64], ebx
602179: mov	ebx, eax
60217b: mov	esi, ebp
60217d: mov	ecx, dword ptr [esp + 100]
602181: mov	edi, dword ptr [ebp]

; ---- 길이 +1 돌려줌
6022a2: mov	ecx, dword ptr [esp + 132]
6022a9: mov	edx, dword ptr [esp + 136]
6022b0: mov	byte ptr [ecx + edx + 1], 0
6022b5: lea	eax, [ecx + 1]
6022b8: add	esp, 108
6022bb: pop	ebx
6022bc: pop	ebp
6022bd: pop	esi
6022be: pop	edi
6022bf: ret	12
