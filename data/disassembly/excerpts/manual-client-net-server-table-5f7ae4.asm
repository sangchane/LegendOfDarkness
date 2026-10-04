; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 서버 주소: 명령줄 → (없으면) 기본 210.101.85.25:2610 → mServer2.tbl/mServer.tbl 고른 칸. 표 글자는 두 바이트씩 자리바꿈
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- 명령줄: 따옴표 뒤가 숫자면 점 IP, 아니면 호스트 이름
46e711: call	dword ptr [8665444]    ; KERNEL32.dll!GetCommandLineA
46e717: mov	esi, eax
46e719: push	34
46e71b: push	esi
46e71c: call	0x6702b0
46e721: add	esp, 8
46e724: test	eax, eax
46e726: je	0x46eb2f
46e72c: lea	edx, [esi + 1]
46e72f: push	34
46e731: push	edx
46e732: call	0x6702b0
46e737: mov	ebx, eax
46e739: add	esp, 8
46e73c: add	ebx, 1
46e73f: test	ebx, ebx
46e741: je	0x46e8c4
46e747: mov	edi, esi
46e749: xor	eax, eax
46e74b: mov	dh, byte ptr [edi]
46e74d: mov	ecx, edi
46e74f: test	dh, dh
46e751: je	0x46e75f
46e753: add	ecx, 1
46e756: add	eax, 1
46e759: mov	dl, byte ptr [ecx]
46e75b: test	dl, dl
46e75d: jne	0x46e753
46e75f: neg	esi
46e761: add	esi, ebx
46e763: add	esi, 5
46e766: cmp	eax, esi
46e768: jle	0x46e8c4
46e76e: lea	esi, [ebx + 1]
46e771: mov	dword ptr [ebp - 28], esi
46e774: mov	dl, byte ptr [ebx + 1]
46e777: cmp	dl, 48
46e77a: jl	0x46e7bc
46e77c: cmp	dl, 57
46e77f: jg	0x46e7bc
46e781: xor	edi, edi
46e783: xor	ebx, ebx
46e785: push	esp
46e786: push	esi
46e787: call	0x67011a
46e78c: add	esp, 8
46e78f: mov	edx, dword ptr [ebp - 444]
46e795: mov	byte ptr [ebx + edx + 661], al
46e79c: push	46
46e79e: push	esi
46e79f: call	0x6702b0
46e7a4: mov	esi, eax
46e7a6: add	esp, 8
46e7a9: add	esi, 1
46e7ac: add	edi, 1
46e7af: movsx	ebx, di
46e7b2: cmp	ebx, 4
46e7b5: jl	0x46e785
46e7b7: jmp	0x46e85d

; ---- " 포트" 가 없으면 포트 23
46e85d: push	32
46e85f: mov	eax, dword ptr [ebp - 28]
46e862: push	eax
46e863: call	0x6702b0
46e868: add	esp, 8
46e86b: test	eax, eax
46e86d: je	0x46e899
46e86f: add	eax, 1
46e872: push	esp
46e873: push	eax
46e874: call	0x67011a
46e879: add	esp, 8
46e87c: mov	edi, dword ptr [ebp - 444]
46e882: movzx	eax, ax
46e885: test	eax, eax
46e887: je	0x46e89f
46e889: mov	word ptr [edi + 1056], ax
46e890: mov	byte ptr [edi + 1061], 0
46e897: jmp	0x46e8b2
46e899: mov	edi, dword ptr [ebp - 444]
46e89f: mov	eax, 23
46e8a4: mov	word ptr [edi + 1056], ax
46e8ab: mov	byte ptr [edi + 1061], 1
46e8b2: mov	byte ptr [edi + 1060], 1
46e8b9: mov	byte ptr [edi + 142], 5
46e8c0: mov	dl, 1
46e8c2: jmp	0x46e90a
46e8c4: mov	eax, dword ptr [ebp - 444]
46e8ca: add	eax, 661
46e8cf: push	25
46e8d1: push	85
46e8d3: push	101
46e8d5: push	210
46e8da: push	8728608    ; "%c%c%c%c"
46e8df: push	eax
46e8e0: call	0x66ee00
46e8e5: add	esp, 24
46e8e8: mov	edi, dword ptr [ebp - 444]
46e8ee: mov	esi, 2610
46e8f3: mov	word ptr [edi + 1056], si
46e8fa: mov	al, 0
46e8fc: mov	byte ptr [edi + 1060], al
46e902: mov	byte ptr [edi + 1061], al
46e908: mov	dl, 0

; ---- 명령줄이 없으면 서버 표 [+1358] 칸의 IP·포트
46e912: movzx	ebx, byte ptr [edi + 1358]
46e919: cmp	ebx, 255
46e91f: je	0x46ea95

; ---- mServer2.tbl 열기 · 첫 두 줄
5f7ae4: push	edi
5f7ae5: push	esi
5f7ae6: push	ebp
5f7ae7: push	ebx
5f7ae8: sub	esp, 28
5f7aeb: mov	ebp, ecx
5f7aed: mov	dword ptr [esp], 9031328    ; "mServer2.tbl"
5f7af4: mov	dword ptr [esp + 4], 8885824
5f7afc: call	0x66ea67
5f7b01: mov	esi, eax
5f7b03: test	esi, esi
5f7b05: je	0x5f7e40
5f7b0b: mov	dword ptr [esp], esi
5f7b0e: mov	dword ptr [esp + 4], 8730336    ; "%d\n"
5f7b16: lea	edx, [esp + 16]
5f7b1a: mov	dword ptr [esp + 8], edx
5f7b1e: call	0x67036c
5f7b23: mov	dl, byte ptr [esp + 16]
5f7b27: mov	byte ptr [ebp], dl
5f7b2a: mov	dword ptr [esp], esi
5f7b2d: mov	dword ptr [esp + 4], 8730336    ; "%d\n"
5f7b35: lea	edi, [esp + 16]
5f7b39: mov	dword ptr [esp + 8], edi
5f7b3d: call	0x67036c
5f7b42: movzx	edx, byte ptr [esp + 16]
5f7b47: mov	byte ptr [ebp + 1], dl

; ---- 칸마다 %d 4줄 + 이름(256)·설명(10000)
5f7bb7: mov	dword ptr [esp], esi
5f7bba: mov	dword ptr [esp + 4], 8730336    ; "%d\n"
5f7bc2: add	eax, edi
5f7bc4: mov	dword ptr [esp + 8], eax
5f7bc8: call	0x67036c
5f7bcd: mov	dword ptr [esp], esi
5f7bd0: mov	dword ptr [esp + 4], 8730336    ; "%d\n"
5f7bd8: mov	edx, dword ptr [ebp + 4]
5f7bdb: lea	edi, [edx + 8*ebx + 4]
5f7bdf: mov	dword ptr [esp + 8], edi
5f7be3: call	0x67036c
5f7be8: mov	dword ptr [esp], esi
5f7beb: mov	dword ptr [esp + 4], 8730336    ; "%d\n"
5f7bf3: mov	edx, dword ptr [ebp + 4]
5f7bf6: lea	edi, [edx + 8*ebx + 8]
5f7bfa: mov	dword ptr [esp + 8], edi
5f7bfe: call	0x67036c
5f7c03: mov	dword ptr [esp], esi
5f7c06: mov	dword ptr [esp + 4], 8730336    ; "%d\n"
5f7c0e: mov	edx, dword ptr [ebp + 4]
5f7c11: lea	edi, [edx + 8*ebx + 268]
5f7c18: mov	dword ptr [esp + 8], edi
5f7c1c: call	0x67036c
5f7c21: mov	edx, dword ptr [ebp + 4]
5f7c24: lea	edi, [edx + 8*ebx + 12]
5f7c28: mov	dword ptr [esp], edi
5f7c2b: mov	dword ptr [esp + 4], 256
5f7c33: mov	dword ptr [esp + 8], esi
5f7c37: call	0x67174b
5f7c3c: mov	edx, dword ptr [ebp + 4]
5f7c3f: lea	edi, [edx + 8*ebx + 269]
5f7c46: mov	dword ptr [esp], edi
5f7c49: mov	dword ptr [esp + 4], 10000
5f7c51: mov	dword ptr [esp + 8], esi
5f7c55: call	0x67174b
5f7c5a: mov	edx, dword ptr [ebp + 4]
5f7c5d: lea	edi, [edx + 8*ebx + 12]
5f7c61: mov	dword ptr [esp], edi
5f7c64: mov	dword ptr [esp + 4], 10
5f7c6c: call	0x6702b0
5f7c71: test	eax, eax
5f7c73: jne	0x5f7e30

; ---- 글자 풀기: 1·2, 3·4 … 바이트 자리바꿈
5f7d08: mov	esi, 2
5f7d0d: mov	eax, dword ptr [ebp + 4]
5f7d10: lea	edi, [eax + ebx + 12]
5f7d14: xor	eax, eax
5f7d16: mov	dh, byte ptr [edi]
5f7d18: mov	ecx, edi
5f7d1a: test	dh, dh
5f7d1c: je	0x5f7d2a
5f7d1e: add	ecx, 1
5f7d21: add	eax, 1
5f7d24: mov	dl, byte ptr [ecx]
5f7d26: test	dl, dl
5f7d28: jne	0x5f7d1e
5f7d2a: cmp	eax, 2
5f7d2d: jbe	0x5f7d79
5f7d2f: mov	dword ptr [esp + 20], ebx
5f7d33: mov	ebx, dword ptr [ebp + 4]
5f7d36: mov	ecx, dword ptr [esp + 20]
5f7d3a: add	ebx, ecx
5f7d3c: mov	al, byte ptr [ebx + esi + 11]
5f7d40: mov	dl, byte ptr [ebx + esi + 12]
5f7d44: mov	byte ptr [ebx + esi + 11], dl
5f7d48: mov	edi, dword ptr [ebp + 4]
5f7d4b: add	edi, ecx
5f7d4d: mov	byte ptr [esi + edi + 12], al
5f7d51: mov	eax, dword ptr [ebp + 4]
5f7d54: lea	edi, [eax + ecx + 12]
5f7d58: xor	eax, eax
5f7d5a: mov	dh, byte ptr [edi]
5f7d5c: mov	ecx, edi
5f7d5e: test	dh, dh
5f7d60: je	0x5f7d6e
5f7d62: add	ecx, 1
5f7d65: add	eax, 1
5f7d68: mov	dl, byte ptr [ecx]
5f7d6a: test	dl, dl
5f7d6c: jne	0x5f7d62
5f7d6e: add	esi, 2
5f7d71: cmp	esi, eax
5f7d73: jb	0x5f7d33
5f7d75: mov	ebx, dword ptr [esp + 20]
