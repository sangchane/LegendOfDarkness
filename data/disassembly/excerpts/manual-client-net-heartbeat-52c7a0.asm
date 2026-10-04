; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 서버 0x3B(2바이트) → 클라이언트 0x45 로 두 바이트를 뒤바꿔 답(CRC16 표 0x6a1220 을 쓰지만 결과는 자리바꿈)
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- 세계 화면 패킷 함수: 원본 첫 바이트 0x3B·0x42
547831: je	0x547845
547833: mov	al, byte ptr [eax]
547835: cmp	al, 59
547837: je	0x547bdb
54783d: cmp	al, 66
54783f: je	0x547bb8

; ---- 계산과 0x45 보내기(길이 3)
52c7a0: push	ebp
52c7a1: push	ebx
52c7a2: sub	esp, 12
52c7a5: mov	ebx, ecx
52c7a7: mov	eax, dword ptr [esp + 24]
52c7ab: lea	edx, [eax + 1]
52c7ae: mov	dword ptr [esp], edx
52c7b1: call	0x5fe090
52c7b6: movzx	ecx, ax
52c7b9: movzx	edx, cl
52c7bc: movzx	eax, word ptr [6951456]
52c7c3: xor	ebp, ebp
52c7c5: xor	eax, ebp
52c7c7: xor	eax, edx
52c7c9: movzx	eax, ax
52c7cc: sar	ecx, 8
52c7cf: mov	edx, eax
52c7d1: sar	edx, 8
52c7d4: movzx	edx, word ptr [2*edx + 6951456]
52c7dc: shl	eax, 8
52c7df: xor	edx, eax
52c7e1: xor	edx, ecx
52c7e3: movzx	eax, dx
52c7e6: push	eax
52c7e7: mov	ecx, ebx
52c7e9: call	0x52c804
52c7ee: mov	eax, 1
52c7f3: add	esp, 12
52c7f6: pop	ebx
52c7f7: pop	ebp
52c7f8: ret	4
52c800: mov	eax, dword ptr [esp + 4]
52c804: sub	esp, 28
52c807: mov	dword ptr [esp], 69
52c80e: lea	eax, [esp + 8]
52c812: mov	dword ptr [esp + 4], eax
52c816: call	0x5fe020
52c81b: movzx	eax, word ptr [esp + 32]
52c820: mov	dword ptr [esp], eax
52c823: lea	edx, [esp + 9]
52c827: mov	dword ptr [esp + 4], edx
52c82b: call	0x601d40
52c830: mov	byte ptr [esp + 11], 0
52c835: mov	ecx, dword ptr [7556672]
52c83b: lea	eax, [esp + 8]
52c83f: push	3
52c841: push	eax
52c842: call	0x5fdf00
52c847: add	esp, 28
