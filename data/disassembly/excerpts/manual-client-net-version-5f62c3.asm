; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — 버전 패킷 0x00: 버전 = 문자열 자원 2번 "599"(또는 6번 "501") 와 Version.nfo 중 큰 값, 뒤에 "LK"
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- 문자열 자원 → atoi
5f62c3: mov	edx, dword ptr [7556708]
5f62c9: lea	eax, [esp + 656]
5f62d0: push	100
5f62d2: push	eax
5f62d3: push	2
5f62d5: push	edx
5f62d6: call	dword ptr [8665680]    ; USER32.dll!LoadStringA
5f62dc: lea	eax, [esp + 656]
5f62e3: mov	dword ptr [esp], eax
5f62e6: call	0x67011a
5f62eb: mov	word ptr [7754168], ax
5f62f1: lea	edx, [esp + 656]
5f62f8: mov	dword ptr [esp], edx
5f62fb: call	0x67011a
5f6300: mov	word ptr [7754172], ax

; ---- 00 | 버전(2) | 4C 4B 를 길이 5로 보냄
5f63ae: lea	eax, [esp + 529]
5f63b5: mov	dword ptr [esp], 0
5f63bc: mov	dword ptr [esp + 4], eax
5f63c0: call	0x601d40
5f63c5: jmp	0x5f63e1
5f63c7: lea	edx, [esp + 529]
5f63ce: movsx	eax, word ptr [7754172]
5f63d5: mov	dword ptr [esp], eax
5f63d8: mov	dword ptr [esp + 4], edx
5f63dc: call	0x601d40
5f63e1: mov	byte ptr [esp + 531], 76
5f63e9: mov	byte ptr [esp + 532], 75
5f63f1: mov	ecx, dword ptr [7556672]
5f63f7: lea	eax, [esp + 528]
5f63fe: push	5
5f6400: push	eax
5f6401: call	0x5fdf00
