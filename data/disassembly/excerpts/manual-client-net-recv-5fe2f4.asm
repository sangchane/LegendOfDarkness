; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 받기: 머리 바이트는 확인 없이 건너뜀, 길이 2바이트 → 몸통 모으기 → 0x00·0x40·0x03 은 복호화 안 함 → 0x4afad0 으로 화면 스레드에
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- 상태 [+491737] 0=머리 기다림, 1=길이·몸통
5fe2f4: push	ebp
5fe2f5: sub	esp, 2080
5fe2fb: mov	ebp, ecx
5fe2fd: mov	al, byte ptr [ebp + 491741]
5fe303: test	al, al
5fe305: jne	0x5fe4ba
5fe30b: mov	al, byte ptr [ebp + 491740]
5fe311: test	al, al
5fe313: je	0x5fe33e
5fe315: mov	edx, dword ptr [ebp + 295116]
5fe31b: lea	eax, [esp]
5fe31e: push	0
5fe320: push	1000
5fe325: push	eax
5fe326: push	edx
5fe327: call	0x652558    ; ws!recv
5fe32c: mov	ecx, dword ptr [ebp + 116]
5fe32f: lea	edx, [esp]
5fe332: push	eax
5fe333: push	edx
5fe334: call	0x4af970
5fe339: jmp	0x5fe5f9
5fe33e: lea	eax, [esp + 2008]
5fe345: push	eax
5fe346: mov	ecx, ebp
5fe348: call	0x5ff244
5fe34d: movzx	edx, al
5fe350: test	edx, edx
5fe352: je	0x5fe5f9
5fe358: mov	dword ptr [esp + 2068], esi
5fe35f: mov	dword ptr [esp + 2064], edi
5fe366: mov	al, byte ptr [ebp + 491737]
5fe36c: test	al, al
5fe36e: jne	0x5fe390
5fe370: mov	byte ptr [ebp + 491737], 1
5fe377: xor	eax, eax
5fe379: mov	dword ptr [ebp + 360660], eax
5fe37f: mov	dword ptr [ebp + 295048], eax
5fe385: jmp	0x5fe45c
5fe390: mov	edx, dword ptr [ebp + 360660]
5fe396: test	edx, edx
5fe398: jne	0x5fe3c0
5fe39a: movzx	eax, byte ptr [esp + 2008]
5fe3a2: shl	eax, 8
5fe3a5: or	dword ptr [ebp + 295048], eax
5fe3ab: add	edx, 1
5fe3ae: mov	dword ptr [ebp + 360660], edx
5fe3b4: jmp	0x5fe45c
5fe3c0: cmp	edx, 1
5fe3c3: je	0x5fe7c0
5fe3c9: mov	al, byte ptr [esp + 2008]
5fe3d0: mov	byte ptr [edx + ebp + 295119], al
5fe3d7: mov	esi, dword ptr [ebp + 360660]
5fe3dd: lea	edx, [esi + 1]
5fe3e0: mov	dword ptr [ebp + 360660], edx
5fe3e6: mov	eax, dword ptr [ebp + 295048]
5fe3ec: add	esi, -1
5fe3ef: cmp	esi, eax
5fe3f1: jne	0x5fe45c
5fe3f3: mov	dl, 0
5fe3f5: mov	byte ptr [eax + ebp + 295121], dl
5fe3fc: mov	byte ptr [ebp + 491737], dl
5fe402: mov	edi, dword ptr [ebp + 295048]
5fe408: test	edi, edi
5fe40a: jle	0x5fe45c
5fe40c: mov	dl, byte ptr [ebp + 295121]
5fe412: test	dl, dl
5fe414: je	0x5fe420
5fe416: cmp	dl, 64
5fe419: je	0x5fe420
5fe41b: cmp	dl, 3
5fe41e: jne	0x5fe430
5fe420: lea	esi, [ebp + 295121]
5fe426: jmp	0x5fe448
5fe430: lea	edx, [ebp + 295121]
5fe436: lea	esi, [ebp + 426200]
5fe43c: push	esi
5fe43d: push	edi
5fe43e: push	edx
5fe43f: mov	ecx, ebp
5fe441: call	0x5fed04
5fe446: mov	edi, eax
5fe448: test	esi, esi
5fe44a: je	0x5fe45c
5fe44c: call	dword ptr [8666000]    ; WINMM.dll!timeGetTime
5fe452: mov	ecx, dword ptr [ebp + 116]
5fe455: push	edi
5fe456: push	esi
5fe457: call	0x4afad0

; ---- recv 98304 바이트, 0 이면 연결 끊김 처리
5fe710: mov	edx, dword ptr [ebp + 295116]
5fe716: cmp	edx, -1
5fe719: je	0x5fe7a0
5fe71f: mov	eax, dword ptr [ebp + 120]
5fe722: push	0
5fe724: push	98304
5fe729: push	eax
5fe72a: push	edx
5fe72b: call	0x652558    ; ws!recv
5fe730: cmp	eax, -1
5fe733: je	0x5fe642
5fe739: test	eax, eax
5fe73b: je	0x5fe770
5fe73d: add	eax, -1
5fe740: mov	dword ptr [ebp + 295044], eax
  5fe746: c7 85 80 80 04 00 01 00 00 00	mov	dword ptr [ebp + 295040], 1
5fe750: mov	esi, dword ptr [ebp + 120]
5fe753: mov	al, byte ptr [esi]
5fe755: mov	byte ptr [esp + 2008], al
5fe75c: jmp	0x5fe49d
5fe770: mov	dword ptr [ebp + 295044], eax
5fe776: mov	eax, dword ptr [ebp + 295116]
5fe77c: push	eax
5fe77d: call	0x65254c    ; ws!closesocket
  5fe782: c7 85 cc 80 04 00 ff ff ff ff	mov	dword ptr [ebp + 295116], 4294967295
5fe78c: mov	byte ptr [ebp + 295112], 0
5fe793: jmp	0x5fe642

; ---- 창 메시지 0x402: FD_READ(1) → 받기, FD_CLOSE(32) → 0x5fe100
50e643: movzx	eax, di
50e646: cmp	eax, 1
50e649: jne	0x50e662
50e64b: mov	ecx, dword ptr [7556672]
50e651: call	0x5fdef0
50e656: xor	eax, eax
50e658: add	esp, 20
50e65b: pop	ebx
50e65c: pop	ebp
50e65d: pop	esi
50e65e: pop	edi
50e65f: ret	16
50e662: cmp	eax, 32
50e665: jne	0x50e672
50e667: mov	ecx, dword ptr [7556672]
50e66d: call	0x5fe100
