; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 서버 0x00(인사): 0 = 정상(서버목록 CRC·씨앗·열쇠), 1 = 메시지, 2 = 패치. 원본 버퍼 첫 바이트로 가른다
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- 패킷 이벤트: +20 원본 버퍼, +28 패킷 객체. 0x00·0x02 는 원본으로
521900: push	esi
521901: push	ebp
521902: push	ebx
521903: mov	ebp, ecx
521905: mov	eax, dword ptr [esp + 16]
521909: mov	esi, dword ptr [eax + 20]
52190c: mov	ebx, dword ptr [eax + 28]
52190f: test	ebx, ebx
521911: je	0x52192d
521913: mov	ecx, ebx
521915: call	0x63b800
52191a: cmp	eax, 10
52191d: jne	0x52192d
52191f: push	ebx
521920: mov	ecx, ebp
521922: call	0x521a14
521927: pop	ebx
521928: pop	ebp
521929: pop	esi
52192a: ret	4
52192d: test	esi, esi
52192f: je	0x521943
521931: mov	al, byte ptr [esi]
521933: test	al, al
521935: je	0x5219db
52193b: cmp	al, 2
52193d: je	0x5219ee

; ---- 종류 1 = 메시지 창
521a8d: mov	eax, dword ptr [ebp + 8]
521a90: mov	al, byte ptr [eax + 1]
521a93: test	al, al
521a95: je	0x521b34
521a9b: cmp	al, 1
521a9d: jne	0x521ae3
521a9f: push	0
521aa1: push	1604
521aa6: call	0x51cdb0
521aab: mov	dword ptr [ebp - 40], eax
521aae: add	esp, 8
521ab1: test	eax, eax
521ab3: je	0x521b16
521ab5: mov	dword ptr [ebp - 4], 2
521abc: push	esp
521abd: push	7
521abf: call	0x4ff8e0
521ac4: add	esp, 8
521ac7: push	0
521ac9: push	-1
521acb: push	15
521acd: mov	edx, dword ptr [ebp - 36]
521ad0: push	edx
521ad1: push	eax
521ad2: mov	ecx, dword ptr [ebp - 40]
521ad5: call	0x4877c0
521ada: mov	dword ptr [ebp - 4], 4294967295
521ae1: jmp	0x521b16
521ae3: cmp	al, 2
521ae5: jne	0x521b16

; ---- 종류 0: CRC(4) · 씨앗(1) → 명령 12 · 열쇠 길이(1)+열쇠 → 명령 11
521b34: mov	eax, dword ptr [ebp + 8]
521b37: lea	edx, [eax + 2]
521b3a: push	esp
521b3b: push	edx
521b3c: call	0x5fe0d0
521b41: mov	dword ptr [ebp - 40], eax
521b44: add	esp, 8
521b47: mov	edx, dword ptr [ebp + 8]
521b4a: lea	ecx, [edx + 6]
521b4d: push	esp
521b4e: push	ecx
521b4f: call	0x5fe080
521b54: add	esp, 8
521b57: movzx	edx, al
521b5a: mov	ecx, dword ptr [7556672]
521b60: push	edx
521b61: call	0x5fe000
521b66: mov	eax, dword ptr [ebp + 8]
521b69: lea	edx, [eax + 7]
521b6c: push	esp
521b6d: push	edx
521b6e: call	0x5fe080
521b73: add	esp, 8
521b76: mov	ecx, dword ptr [7556672]
521b7c: movzx	edx, al
521b7f: mov	esi, dword ptr [ebp + 8]
521b85: push	edi
521b86: push	edx
521b87: call	0x5fdfc0

; ---- 내 서버목록 CRC 와 다르면 서버 고르기 창을 "목록 새로 받기"로
521c7c: lea	eax, [ebp - 2100]
521c82: push	esp
521c83: push	esi
521c84: push	eax
521c85: push	0
521c87: call	0x652910
521c8c: add	esp, 16
521c8f: mov	esi, dword ptr [6864864]
521c95: mov	dl, byte ptr [esi + 1357]
521c9b: test	dl, dl
521c9d: jbe	0x521ca6
521c9f: mov	edx, dword ptr [ebp - 40]
521ca2: cmp	edx, eax
521ca4: je	0x521cdd
521ca6: push	0
521ca8: push	9996
521cad: call	0x51cdb0
521cb2: add	esp, 8
521cb5: test	eax, eax
521cb7: je	0x521b16
521cbd: mov	dword ptr [ebp - 4], 0
521cc4: push	1
521cc6: mov	edx, dword ptr [ebp - 36]
521cc9: push	edx
521cca: mov	ecx, eax
521ccc: call	0x5f8e00
521cd1: mov	dword ptr [ebp - 4], 4294967295
521cd8: jmp	0x521b16
