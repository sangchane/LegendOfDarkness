; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 기본 열쇠 "NexonInc." · 0x00 이 바꾸는 열쇠(명령 11 → 0x6023d4)
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- 처음 열쇠 = "NexonInc." 9바이트를 4번 이어 붙임
5fdc74: mov	esi, 7839776
5fdc79: mov	edi, 9036384    ; "NexonInc."
5fdc7e: mov	dl, byte ptr [edi]
5fdc80: add	edi, 1
5fdc83: mov	byte ptr [esi], dl
5fdc85: add	esi, 1
5fdc88: test	dl, dl
5fdc8a: jne	0x5fdc7e
5fdc8c: mov	edx, dword ptr [ebp + 8]
5fdc8f: mov	eax, dword ptr [ebp - 44]
5fdc92: mov	dword ptr [eax + 116], edx
5fdc95: mov	edi, 7839776
5fdc9a: xor	eax, eax
5fdc9c: mov	dh, byte ptr [edi]
5fdc9e: mov	ecx, edi
5fdca0: test	dh, dh
5fdca2: je	0x5fdcb0
5fdca4: add	ecx, 1
5fdca7: add	eax, 1
5fdcaa: mov	dl, byte ptr [ecx]
5fdcac: test	dl, dl
5fdcae: jne	0x5fdca4
5fdcb0: mov	dword ptr [7839788], eax
5fdcb5: mov	edi, 7839792
5fdcba: mov	esi, 7839776
5fdcbf: mov	ecx, eax
5fdcc1: rep		movsb	byte ptr es:[edi], byte ptr [esi]
5fdcc3: mov	ecx, dword ptr [7839788]
5fdcc9: lea	edi, [ecx + 7839792]
5fdccf: mov	esi, 7839776
5fdcd4: rep		movsb	byte ptr es:[edi], byte ptr [esi]
5fdcd6: mov	ecx, dword ptr [7839788]
5fdcdc: lea	edi, [ecx + ecx + 7839792]
5fdce3: mov	esi, 7839776
5fdce8: rep		movsb	byte ptr es:[edi], byte ptr [esi]
5fdcea: mov	ecx, dword ptr [7839788]
5fdcf0: lea	edi, [ecx + 2*ecx + 7839792]
5fdcf7: mov	esi, 7839776
5fdcfc: rep		movsb	byte ptr es:[edi], byte ptr [esi]

; ---- 서버가 준 열쇠로 바꿈
6023d4: push	edi
6023d5: push	ebx
6023d6: push	esi
6023d7: mov	eax, dword ptr [esp + 20]
6023db: mov	ebx, dword ptr [esp + 16]
6023df: mov	edi, dword ptr [6847040]
6023e5: push	ebx
6023e6: push	eax
6023e7: push	7839776
6023ec: mov	ecx, edi
6023ee: call	0x554970
6023f3: mov	dword ptr [7839788], ebx
6023f9: push	ebx
6023fa: push	7839776
6023ff: push	7839792
602404: mov	ecx, edi
602406: call	0x554970
60240b: mov	eax, dword ptr [7839788]
602410: lea	edx, [eax + 7839792]
602416: push	eax
602417: push	7839776
60241c: push	edx
60241d: mov	ecx, edi
60241f: call	0x554970
602424: mov	eax, dword ptr [7839788]
602429: lea	edx, [eax + eax + 7839792]
602430: push	eax
602431: push	7839776
602436: push	edx
602437: mov	ecx, edi
602439: call	0x554970
60243e: mov	eax, dword ptr [7839788]
602443: lea	edx, [eax + 2*eax + 7839792]
60244a: push	eax
60244b: push	7839776
602450: push	edx
602451: mov	ecx, edi
602453: call	0x554970
602458: add	esp, 4
60245b: pop	ebx
60245c: pop	edi
60245d: ret	8
