; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 서버 0x03(다른 서버로): 주소 4(빅엔디언으로 읽음 → 바이트 거꾸로) · 포트 2 · 입장권(1바이트 길이) → 끊고 다시 접속 → 0x10 + 입장권 그대로
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- STransferServer 읽기
6445a0: push	esi
6445a1: push	ebp
6445a2: push	esi
6445a3: mov	esi, ecx
6445a5: mov	ebp, dword ptr [esp + 16]
6445a9: mov	ecx, ebp
6445ab: call	0x63b640
6445b0: mov	dword ptr [esi + 16], eax
6445b3: mov	ecx, ebp
6445b5: call	0x63b5e0
6445ba: mov	word ptr [esi + 20], ax
6445be: lea	edx, [esi + 28]
6445c1: push	edx
6445c2: mov	ecx, ebp
6445c4: call	0x63b690
6445c9: mov	dword ptr [esi + 24], eax
6445cc: add	esp, 4
6445cf: pop	ebp
6445d0: pop	esi
6445d1: ret	12

; ---- 처리: 다시 접속(명령 4) 기다린 뒤 0x10 보내기
522c64: push	edi
522c65: push	esi
522c66: push	ebp
522c67: push	ebx
522c68: sub	esp, 1300
522c6e: mov	ebp, dword ptr [esp + 1320]
522c75: mov	eax, dword ptr [7556680]
522c7a: mov	dl, byte ptr [eax + 142]
522c80: cmp	dl, 5
522c83: je	0x522cda
522c85: mov	dword ptr [esp], 16
522c8c: lea	eax, [esp + 8]
522c90: mov	dword ptr [esp + 4], eax
522c94: call	0x5fe020
522c99: lea	edi, [esp + 9]
522c9d: lea	esi, [esp + 1032]
522ca4: mov	ecx, dword ptr [ebp + 24]
522ca7: rep		movsb	byte ptr es:[edi], byte ptr [esi]
522ca9: mov	ebp, dword ptr [ebp + 24]
522cac: lea	esi, [ebp + 1]
522caf: mov	byte ptr [esp + ebp + 9], 0
522cb4: mov	ecx, dword ptr [7556672]
522cba: lea	edx, [esp + 8]
522cbe: movsx	eax, si
522cc1: push	eax
522cc2: push	edx
522cc3: call	0x5fdf00
522cc8: mov	eax, 1
522ccd: add	esp, 1300
522cd3: pop	ebx
522cd4: pop	ebp
522cd5: pop	esi
522cd6: pop	edi
522cd7: ret	4
522cda: mov	byte ptr [esp], 0
522cde: call	0x604760
522ce3: mov	ebx, dword ptr [ebp + 16]
522ce6: mov	dword ptr [esp + 1292], ebx
522ced: movzx	ebx, word ptr [ebp + 20]
522cf1: lea	edi, [esp + 1032]
522cf8: lea	esi, [ebp + 28]
522cfb: mov	ecx, dword ptr [ebp + 24]
522cfe: rep		movsb	byte ptr es:[edi], byte ptr [esi]
522d00: mov	eax, dword ptr [ebp + 24]
522d03: mov	byte ptr [esp + eax + 1032], 0
522d0b: mov	ecx, dword ptr [7556672]
522d11: push	ebx
522d12: mov	ebx, dword ptr [esp + 1296]
522d19: push	ebx
522d1a: call	0x5fded0
522d1f: mov	ecx, dword ptr [7556672]
522d25: lea	eax, [esp + 1288]
522d2c: push	eax
522d2d: call	0x5fdeb0
522d32: mov	ecx, dword ptr [7556672]
522d38: push	eax
522d39: call	0x6253f0
522d3e: jmp	0x522c85

; ---- 명령 4: 소켓 닫고 받기 상태 초기화(순번·열쇠는 그대로) → 0x5fff40 → 1초 쉼
6017b4: push	ebx
6017b5: mov	ebx, ecx
6017b7: mov	ecx, dword ptr [ebx + 295116]
6017bd: mov	edx, dword ptr [7556704]
6017c3: xor	eax, eax
6017c5: push	eax
6017c6: push	eax
6017c7: push	edx
6017c8: push	ecx
6017c9: call	0x652576    ; ws!WSAAsyncSelect
6017ce: mov	ecx, ebx
6017d0: call	0x601464
6017d5: movzx	edx, word ptr [esp + 12]
6017da: mov	eax, dword ptr [esp + 8]
6017de: xor	ecx, ecx
6017e0: mov	dword ptr [ebx + 295040], ecx
6017e6: mov	dword ptr [ebx + 295044], ecx
6017ec: mov	dword ptr [ebx + 360660], ecx
6017f2: mov	byte ptr [ebx + 491737], cl
6017f8: mov	byte ptr [ebx + 491738], cl
6017fe: mov	byte ptr [ebx + 491739], cl
601804: mov	ecx, dword ptr [7556680]
60180a: mov	dword ptr [ecx + 661], eax
601810: mov	eax, dword ptr [7556680]
601815: mov	word ptr [eax + 1056], dx
60181c: mov	ecx, ebx
60181e: call	0x5fff40
601823: push	1000
601828: call	dword ptr [8665208]    ; KERNEL32.dll!Sleep
60182e: pop	ebx
60182f: ret	8
