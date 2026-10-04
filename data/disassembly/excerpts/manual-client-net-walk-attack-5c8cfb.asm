; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 걷기·돌기·평타 보내기 조건: 방향이 다르면 0x11 만, 같고 갈 수 있으면 0x06(방향, 걸음 번호), 걷는 중에는 마지막 칸에서만 다음 걸음 예약. 평타 0x13 은 100ms 넘게 지났을 때만
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- 걷기 요청 판정
5c8cfb: movsx	ebp, byte ptr [esp + 24]
5c8d00: mov	edi, dword ptr [esi + 520]
5c8d06: cmp	edi, 1
5c8d09: je	0x5c8d86
5c8d0b: test	edi, edi
5c8d0d: jne	0x5c8d7d
5c8d0f: movsx	eax, byte ptr [esi + 516]
5c8d16: cmp	ebp, eax
5c8d18: je	0x5c8d3e
5c8d1a: push	ebp
5c8d1b: mov	ecx, esi
5c8d1d: call	0x5c8e04
5c8d22: push	ebp
5c8d23: mov	ecx, esi
5c8d25: call	0x51bfd0
5c8d2a: mov	ecx, dword ptr [7556660]
5c8d30: call	0x540c00
5c8d35: add	esp, 8
5c8d38: pop	ebp
5c8d39: pop	esi
5c8d3a: pop	edi
5c8d3b: ret	4
5c8d3e: mov	ecx, dword ptr [esi + 5196]
5c8d44: mov	ebp, dword ptr [esi + 436]
5c8d4a: mov	edx, dword ptr [esi + 440]
5c8d50: push	eax
5c8d51: push	edx
5c8d52: push	ebp
5c8d53: call	0x540860
5c8d58: movzx	edx, al
5c8d5b: test	edx, edx
5c8d5d: je	0x5c8d7d
5c8d5f: movsx	eax, byte ptr [esi + 516]
5c8d66: push	eax
5c8d67: mov	ecx, esi
5c8d69: call	0x5c8e34
5c8d6e: movsx	eax, byte ptr [esi + 516]
5c8d75: push	eax
5c8d76: mov	ecx, esi
5c8d78: call	0x5c8eb4
5c8d7d: add	esp, 8
5c8d80: pop	ebp
5c8d81: pop	esi
5c8d82: pop	edi
5c8d83: ret	4
5c8d86: movsx	edx, byte ptr [esi + 524]
5c8d8d: movsx	eax, word ptr [esi + 548]
5c8d94: add	eax, -1
5c8d97: cmp	edx, eax
5c8d99: jne	0x5c8d0b
5c8d9f: mov	ecx, dword ptr [esi + 5196]
5c8da5: mov	edi, dword ptr [esi + 436]
5c8dab: mov	edx, dword ptr [esi + 440]
5c8db1: movsx	eax, byte ptr [esi + 516]
5c8db8: push	eax
5c8db9: push	edx
5c8dba: push	edi
5c8dbb: call	0x540860
5c8dc0: movzx	edx, al
5c8dc3: test	edx, edx
5c8dc5: je	0x5c8ded
5c8dc7: movsx	eax, byte ptr [esi + 516]
5c8dce: cmp	ebp, eax
5c8dd0: je	0x5c8ddd
5c8dd2: mov	edi, dword ptr [esi + 520]
5c8dd8: jmp	0x5c8d0b
5c8ddd: mov	byte ptr [esi + 67072], 1

; ---- 0x11 돌기 · 0x06 걷기(걸음 번호 +67064, 보낸 시각 +67068, 미확인 걸음 +67074)
5c8e00: mov	eax, dword ptr [esp + 4]
5c8e04: sub	esp, 12
5c8e07: mov	al, byte ptr [esp + 16]
5c8e0b: mov	byte ptr [esp], 17
5c8e0f: mov	byte ptr [esp + 1], al
5c8e13: mov	byte ptr [esp + 2], 0
5c8e18: mov	ecx, dword ptr [7556672]
5c8e1e: lea	edx, [esp]
5c8e21: push	2
5c8e23: push	edx
5c8e24: call	0x5fdf00
5c8e29: add	esp, 12
5c8e2c: ret	4
5c8e30: mov	eax, dword ptr [esp + 4]
5c8e34: push	ebx
5c8e35: sub	esp, 24
5c8e38: mov	ebx, ecx
5c8e3a: mov	al, byte ptr [esp + 32]
5c8e3e: mov	byte ptr [esp + 8], 6
5c8e43: mov	byte ptr [esp + 9], al
5c8e47: movzx	edx, byte ptr [ebx + 67064]
5c8e4e: add	edx, 1
5c8e51: mov	byte ptr [ebx + 67064], dl
5c8e57: movzx	eax, dl
5c8e5a: mov	dword ptr [esp], eax
5c8e5d: lea	eax, [esp + 10]
5c8e61: mov	dword ptr [esp + 4], eax
5c8e65: call	0x5fe020
5c8e6a: call	dword ptr [8666000]    ; WINMM.dll!timeGetTime
5c8e70: mov	dword ptr [ebx + 67068], eax
5c8e76: mov	byte ptr [esp + 11], 0
5c8e7b: mov	ecx, dword ptr [7556672]
5c8e81: lea	edx, [esp + 8]
5c8e85: push	3
5c8e87: push	edx
5c8e88: call	0x5fdf00
5c8e8d: mov	byte ptr [ebx + 67073], 0
5c8e94: mov	al, byte ptr [ebx + 67074]
5c8e9a: add	al, 1
5c8e9c: mov	byte ptr [ebx + 67074], al

; ---- 서버 0x0B(내 걸음 확인): 미확인 -1, 왕복 시간 +67120
5c9fe4: push	esi
5c9fe5: push	ebp
5c9fe6: push	ebx
5c9fe7: sub	esp, 16
5c9fea: mov	esi, ecx
5c9fec: mov	edx, dword ptr [esp + 32]
5c9ff0: mov	al, byte ptr [edx + 16]
5c9ff3: mov	byte ptr [esp + 12], al
5c9ff7: movsx	ebx, word ptr [edx + 18]
5c9ffb: movsx	ebp, word ptr [edx + 20]
5c9fff: call	dword ptr [8666000]    ; WINMM.dll!timeGetTime
5ca005: mov	dword ptr [esi + 67076], eax
5ca00b: mov	dword ptr [esi + 67084], ebx
5ca011: mov	dword ptr [esi + 67080], ebp
5ca017: mov	al, byte ptr [esi + 67074]
5ca01d: test	al, al
5ca01f: jbe	0x5ca029
5ca021: dec	al
5ca023: mov	byte ptr [esi + 67074], al
5ca029: mov	byte ptr [esi + 67073], 0
5ca030: mov	al, byte ptr [esi + 67064]
5ca036: mov	byte ptr [esi + 67065], al
5ca03c: call	dword ptr [8666000]    ; WINMM.dll!timeGetTime
5ca042: sub	eax, dword ptr [esi + 67068]
5ca048: mov	dword ptr [esi + 67120], eax
5ca04e: mov	al, byte ptr [esp + 12]
5ca052: cmp	al, 4
5ca054: je	0x5ca09f
5ca056: mov	byte ptr [esp], al
5ca059: call	0x540820
5ca05e: add	dword ptr [esi + 67084], edx
5ca064: add	dword ptr [esi + 67080], eax
5ca06a: mov	al, byte ptr [esi + 67074]
5ca070: test	al, al
5ca072: jne	0x5ca07b
5ca074: mov	byte ptr [esi + 67073], 1

; ---- 평타: timeGetTime - 마지막 > 100 이면 0x13
5c92c0: mov	eax, dword ptr [esp + 4]
5c92c4: push	edi
5c92c5: mov	edi, ecx
5c92c7: call	dword ptr [8666000]    ; WINMM.dll!timeGetTime
5c92cd: sub	eax, dword ptr [edi + 67060]
5c92d3: cmp	eax, 100
5c92d6: jbe	0x5c92eb
5c92d8: mov	ecx, edi
5c92da: call	0x5c9304
5c92df: call	dword ptr [8666000]    ; WINMM.dll!timeGetTime
5c92e5: mov	dword ptr [edi + 67060], eax
5c92eb: mov	ecx, edi
5c92ed: call	0x5c9334
5c92f2: pop	edi
5c92f3: ret
5c9300: mov	eax, dword ptr [esp + 4]
5c9304: sub	esp, 12
5c9307: mov	byte ptr [esp], 19
5c930b: mov	byte ptr [esp + 1], 0
5c9310: mov	ecx, dword ptr [7556672]
5c9316: lea	eax, [esp]
5c9319: push	1
5c931b: push	eax
5c931c: call	0x5fdf00
5c9321: add	esp, 12
5c9324: ret
