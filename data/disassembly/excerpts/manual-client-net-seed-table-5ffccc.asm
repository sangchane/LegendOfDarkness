; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 씨앗 표 10종(명령 12, 씨앗 0~9) — 하데스 SecurityProvider 와 같은 식
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- 씨앗별 256칸 표를 0x6b3340 에 (바이트 4번 반복한 dword)
5ffccc: xor	ecx, ecx
5ffcce: mov	esi, esi
5ffcd0: cmp	ebx, 9
5ffcd3: ja	0x5ffe70
5ffcd9: mov	eax, dword ptr [4*ebx + 9034912]
5ffce0: jmp	eax
5ffcf0: mov	eax, ecx
5ffcf2: lea	edx, [ecx + 1]
5ffcf5: jmp	0x5ffe73
5ffd00: test	cl, 1
5ffd03: je	0x5ffd10
5ffd05: mov	eax, 4294967295
5ffd0a: jmp	0x5ffd15
5ffd10: mov	eax, 1
5ffd15: lea	edx, [ecx + 1]
5ffd18: mov	esi, edx
5ffd1a: shr	esi, 31
5ffd1d: lea	edi, [ecx + esi + 1]
5ffd21: sar	edi
5ffd23: imul	eax, edi
5ffd26: add	eax, 128
5ffd2b: jmp	0x5ffe73
5ffd30: mov	eax, ecx
5ffd32: neg	eax
5ffd34: add	eax, 255
5ffd39: lea	edx, [ecx + 1]
5ffd3c: jmp	0x5ffe73
5ffd50: test	cl, 1
5ffd53: je	0x5ffd60
5ffd55: mov	eax, 4294967295
5ffd5a: jmp	0x5ffd65
5ffd60: mov	eax, 1
5ffd65: mov	edx, ecx
5ffd67: neg	edx
5ffd69: add	edx, 255
5ffd6f: shr	edx, 31
5ffd72: sub	edx, ecx
5ffd74: add	edx, 255
5ffd7a: sar	edx
5ffd7c: imul	eax, edx
5ffd7f: add	eax, 128
5ffd84: lea	edx, [ecx + 1]
5ffd87: jmp	0x5ffe73
5ffd90: mov	eax, ecx
5ffd92: sar	eax, 3
5ffd95: shr	eax, 28
5ffd98: add	eax, ecx
5ffd9a: sar	eax, 4
5ffd9d: imul	eax, eax
5ffda0: lea	edx, [ecx + 1]
5ffda3: jmp	0x5ffe73
5ffdb0: lea	eax, [ecx + ecx]
5ffdb3: cdq
5ffdb4: xor	eax, edx
5ffdb6: sub	eax, edx
5ffdb8: and	eax, 255
5ffdbd: xor	eax, edx
5ffdbf: sub	eax, edx
5ffdc1: lea	edx, [ecx + 1]
5ffdc4: jmp	0x5ffe73
5ffdd0: lea	eax, [ecx + ecx]
5ffdd3: cdq
5ffdd4: xor	eax, edx
5ffdd6: sub	eax, edx
5ffdd8: and	eax, 255
5ffddd: xor	eax, edx
5ffddf: sub	eax, edx
5ffde1: neg	eax
5ffde3: add	eax, 255
5ffde8: lea	edx, [ecx + 1]
5ffdeb: jmp	0x5ffe73
5ffdf0: cmp	ecx, 127
5ffdf3: jg	0x5ffe10
5ffdf5: lea	eax, [ecx + ecx]
5ffdf8: neg	eax
5ffdfa: add	eax, 255
5ffdff: lea	edx, [ecx + 1]
5ffe02: jmp	0x5ffe73
5ffe07: mov	esi, esi
5ffe10: lea	eax, [ecx + ecx - 256]
5ffe17: lea	edx, [ecx + 1]
5ffe1a: jmp	0x5ffe73
5ffe20: cmp	ecx, 127
5ffe23: jg	0x5ffe30
5ffe25: lea	eax, [ecx + ecx]
5ffe28: lea	edx, [ecx + 1]
5ffe2b: jmp	0x5ffe73
5ffe30: lea	eax, [ecx + ecx]
5ffe33: neg	eax
5ffe35: add	eax, 511
5ffe3a: lea	edx, [ecx + 1]
5ffe3d: jmp	0x5ffe73
5ffe40: lea	esi, [ecx - 128]
5ffe43: sar	esi, 2
5ffe46: shr	esi, 29
5ffe49: lea	eax, [ecx + esi - 128]
5ffe4d: sar	eax, 3
5ffe50: imul	eax, eax
5ffe53: cdq
5ffe54: xor	eax, edx
5ffe56: sub	eax, edx
5ffe58: and	eax, 255
5ffe5d: xor	eax, edx
5ffe5f: sub	eax, edx
5ffe61: neg	eax
5ffe63: add	eax, 255
5ffe68: lea	edx, [ecx + 1]
5ffe6b: jmp	0x5ffe73
5ffe70: lea	edx, [ecx + 1]
5ffe73: mov	esi, eax
5ffe75: shl	esi, 8
5ffe78: or	esi, eax
5ffe7a: mov	eax, esi
5ffe7c: shl	eax, 16
5ffe7f: or	eax, esi
5ffe81: mov	dword ptr [4*ecx + 7025472], eax
5ffe88: mov	ecx, edx
5ffe8a: cmp	edx, 256
5ffe90: jl	0x5ffcd0
