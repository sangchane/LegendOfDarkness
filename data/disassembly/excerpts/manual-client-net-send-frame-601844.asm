; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 보내기 틀: 0x00·0x10·0x48 은 암호화 안 함, 나머지는 0x601d64 로 암호화 → AA 길이(2, 빅엔디언) 몸통
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- 글자 모드(+491741, 전화 접속)가 아니면 아래로
601844: push	edi
601845: push	esi
601846: push	ebp
601847: push	ebx
601848: sub	esp, 316
60184e: mov	ebp, ecx
601850: movsx	ebx, word ptr [esp + 340]
601858: mov	edx, dword ptr [esp + 336]
60185f: mov	al, byte ptr [ebp + 491741]
601865: test	al, al
601867: je	0x601a20
60186d: test	edx, edx
60186f: je	0x601ac2
601875: test	ebx, ebx
601877: jle	0x601ac2
60187d: mov	al, byte ptr [edx]
60187f: test	al, al
601881: je	0x60188b
601883: cmp	al, 72
601885: je	0x60188b
601887: cmp	al, 16
601889: jne	0x60188f
60188b: mov	esi, edx
60188d: jmp	0x6018a2
60188f: lea	esi, [ebp + 426200]
601895: push	esi
601896: push	ebx
601897: push	edx
601898: mov	ecx, ebp
60189a: call	0x601d64
60189f: movsx	ebx, ax

; ---- 바이너리 모드: 0xAA + 길이 + 몸통을 send
601a20: test	edx, edx
601a22: je	0x601ac2
601a28: test	ebx, ebx
601a2a: jle	0x601ac2
601a30: mov	esi, dword ptr [ebp + 295116]
601a36: cmp	esi, -1
601a39: je	0x601ac2
601a3f: mov	al, byte ptr [edx]
601a41: test	al, al
601a43: je	0x601a4d
601a45: cmp	al, 72
601a47: je	0x601a4d
601a49: cmp	al, 16
601a4b: jne	0x601a51
601a4d: mov	esi, edx
601a4f: jmp	0x601a63
601a51: lea	esi, [ebp + 426200]
601a57: push	esi
601a58: push	ebx
601a59: push	edx
601a5a: mov	ecx, ebp
601a5c: call	0x601d64
601a61: mov	ebx, eax
601a63: lea	edx, [ebx + 3]
601a66: mov	dword ptr [esp + 280], edx
601a6d: mov	ecx, dword ptr [ebp + 491744]
601a73: push	edx
601a74: call	0x613720
601a79: mov	dword ptr [esp + 284], eax
601a80: mov	byte ptr [eax], -86
601a83: mov	byte ptr [eax + 1], 0
601a87: lea	edx, [eax + 1]
601a8a: mov	eax, ebx
601a8c: call	0x601d48
601a91: mov	edx, dword ptr [esp + 284]
601a98: lea	edi, [edx + 3]
601a9b: mov	ecx, ebx
601a9d: rep		movsb	byte ptr es:[edi], byte ptr [esi]
601a9f: mov	eax, dword ptr [ebp + 295116]
601aa5: push	0
601aa7: mov	ebx, dword ptr [esp + 284]
601aae: push	ebx
601aaf: mov	edx, dword ptr [esp + 292]
601ab6: push	edx
601ab7: push	eax
601ab8: call	0x65257c    ; ws!send
601abd: cmp	eax, -1
601ac0: jne	0x601acf
601ac2: add	esp, 316
601ac8: pop	ebx
601ac9: pop	ebp
601aca: pop	esi
601acb: pop	edi
601acc: ret	8
601acf: mov	ecx, dword ptr [ebp + 491744]

; ---- 길이 2바이트를 큰 자리부터
601d40: mov	eax, dword ptr [esp + 4]
601d44: mov	edx, dword ptr [esp + 8]
601d48: mov	ecx, eax
601d4a: shr	ecx, 8
601d4d: mov	byte ptr [edx], cl
601d4f: mov	byte ptr [edx + 1], al
601d52: mov	byte ptr [edx + 2], 0
601d56: ret
