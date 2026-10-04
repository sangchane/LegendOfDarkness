; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 서버 패킷 클래스 58개: 생성자가 opcode 를 넣는다(예: SActionDelay = 0x3F), 공장이 opcode → 클래스, 화면들은 0x63b800(opcode 읽기)로 가른다
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- SActionDelay 생성자: push 63 → 기본 생성자 0x63b7d0
63d180: push	esi
63d181: mov	esi, ecx
63d183: push	63
63d185: mov	ecx, esi
63d187: call	0x63b7d0
63d18c: mov	dword ptr [esi], 9086564    ; "Pï¿½c"
63d192: mov	eax, esi
63d194: pop	esi
63d195: ret

; ---- 읽기: 바이트·바이트·dword(빅엔디언)
63d1a0: push	edi
63d1a1: push	esi
63d1a2: push	esi
63d1a3: mov	edi, ecx
63d1a5: mov	esi, dword ptr [esp + 16]
63d1a9: mov	ecx, esi
63d1ab: call	0x63b5c0
63d1b0: mov	byte ptr [edi + 16], al
63d1b3: mov	ecx, esi
63d1b5: call	0x63b5c0
63d1ba: mov	byte ptr [edi + 17], al
63d1bd: mov	ecx, esi
63d1bf: call	0x63b640
63d1c4: mov	dword ptr [edi + 20], eax
63d1c7: add	esp, 4
63d1ca: pop	esi
63d1cb: pop	edi
63d1cc: ret	12

; ---- 기본 생성자: opcode 를 +12 에
63b7d0: push	ebp
63b7d1: mov	ebp, ecx
63b7d3: mov	ecx, ebp
63b7d5: call	0x51cd90
63b7da: mov	edx, dword ptr [esp + 8]
63b7de: mov	dword ptr [ebp], 9085572
63b7e5: mov	dword ptr [ebp + 12], edx
63b7e8: mov	eax, ebp
63b7ea: pop	ebp
63b7eb: ret	4

; ---- opcode 읽기
63b800: mov	eax, dword ptr [ecx + 12]
63b803: ret

; ---- 공장: opcode < 256 이면 표 +24 에서 만들 함수를 꺼냄
63ccd0: push	ebp
63ccd1: mov	ebp, esp
63ccd3: push	-1
63ccd5: push	6540752
63ccda: mov	eax, dword ptr fs:[0]
63cce0: push	eax
63cce1: mov	dword ptr fs:[0], esp
63cce8: sub	esp, 48
63cceb: mov	dword ptr [ebp - 16], esp
63ccee: mov	dword ptr [ebp - 36], edi
63ccf1: mov	dword ptr [ebp - 32], esi
63ccf4: mov	dword ptr [ebp - 28], ebx
63ccf7: mov	edx, dword ptr [ebp + 8]
63ccfa: cmp	edx, 256
63cd00: jge	0x63cd15
63cd02: mov	eax, dword ptr [ecx + 4*edx + 24]
63cd06: mov	dword ptr [ebp - 24], eax
63cd09: mov	dword ptr [ecx + 4*edx + 24], 0
63cd11: test	eax, eax
63cd13: jne	0x63cd2f
63cd15: mov	eax, dword ptr [ecx + 16]
63cd18: sub	eax, dword ptr [ecx + 12]
63cd1b: sar	eax, 2
63cd1e: cmp	edx, eax
63cd20: jb	0x63cda5
63cd26: xor	eax, eax
63cd28: mov	dword ptr [ebp - 24], eax
63cd2b: test	eax, eax
63cd2d: je	0x63cd7d
63cd2f: mov	eax, dword ptr [ebp + 16]
63cd32: mov	edx, dword ptr [ebp + 12]
63cd35: lea	ecx, [ebp - 60]
63cd38: push	eax
63cd39: push	edx
63cd3a: call	0x63cde0
63cd3f: mov	dword ptr [ebp - 4], 0
63cd46: add	dword ptr [ebp - 40], 1
63cd4a: lea	ecx, [ebp - 60]
63cd4d: mov	eax, dword ptr [ebp + 16]
63cd50: push	eax
63cd51: mov	edx, dword ptr [ebp + 12]
63cd54: push	edx
63cd55: push	ecx
63cd56: mov	ecx, dword ptr [ebp - 24]
63cd59: mov	ebx, dword ptr [ecx]
63cd5b: mov	eax, dword ptr [ebx + 8]
63cd5e: call	eax

; ---- 세계 화면: 원본 0x2F, 그다음 객체 opcode 0x08…
547230: push	ebp
547231: mov	ebp, esp
547233: push	-1
547235: push	5537120
54723a: mov	eax, dword ptr fs:[0]
547240: push	eax
547241: mov	dword ptr fs:[0], esp
547248: sub	esp, 80
54724b: mov	dword ptr [ebp - 16], esp
54724e: mov	dword ptr [ebp - 52], edi
547251: mov	dword ptr [ebp - 48], esi
547254: mov	dword ptr [ebp - 56], ebx
547257: mov	edi, ecx
547259: mov	eax, dword ptr [ebp + 8]
54725c: mov	ebx, dword ptr [eax + 20]
54725f: mov	dword ptr [ebp - 32], ebx
547262: mov	edx, dword ptr [eax + 28]
547265: mov	dword ptr [ebp - 28], edx
547268: test	ebx, ebx
54726a: je	0x547278
54726c: mov	esi, ebx
54726e: mov	al, byte ptr [esi]
547270: cmp	al, 47
547272: je	0x547d31
547278: mov	eax, edx
54727a: test	eax, eax
54727c: je	0x5473a0
547282: mov	ecx, eax
547284: call	0x63b800
547289: cmp	eax, 8
54728c: je	0x54737a
