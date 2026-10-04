; Legend.exe (원작 2005 = 5.99 클라이언트, md5 347dc381…d128) — TCP 접속: socket → setsockopt → connect(주소 +661, 포트 +1056+1058) → WSAAsyncSelect(0x402, FD_READ|FD_CLOSE=33), 실패하면 서버 목록 다음 칸으로
; 맥 objdump --x86-asm-syntax=intel, 오프셋 10진수. 줄 끝 ; 는 가져오기 함수 이름·CP949 문자열을 붙인 것
; 문서: docs/exe-manual/01-client-network.md

; ---- 소켓 만들기 · WSAStartup 1.1 · setsockopt
5fffcb: push	50
5fffcd: call	0x6251b0
5fffd2: lea	eax, [ebp - 1024]
5fffd8: push	eax
5fffd9: push	257
5fffde: call	0x65252e    ; ws!WSAStartup
5fffe3: test	eax, eax
5fffe5: jne	0x6013a9
5fffeb: movzx	eax, word ptr [ebp - 1024]
5ffff2: cmp	eax, 257
5ffff7: jne	0x6002bc
5ffffd: push	0
5fffff: push	1
600001: push	2
600003: call	0x65255e    ; ws!socket
600008: mov	edx, dword ptr [ebp - 28]
60000b: mov	dword ptr [edx + 295116], eax
600011: cmp	eax, -1
600014: je	0x601293
60001a: cmp	eax, -1
60001d: jne	0x60003b
60001f: call	0x65253a    ; ws!WSACleanup
600024: mov	ebx, dword ptr [ebp - 52]
600027: mov	esi, dword ptr [ebp - 44]
60002a: mov	edi, dword ptr [ebp - 48]
60002d: mov	ecx, dword ptr [ebp - 12]
600030: mov	dword ptr fs:[0], ecx
600037: mov	esp, ebp
600039: pop	ebp
60003a: ret
60003b: lea	edx, [ebp - 1176]
600041: push	4
600043: push	edx
600044: push	4097
600049: push	65535
60004e: push	eax
60004f: call	0x652564    ; ws!setsockopt

; ---- 주소·포트 → connect → 대행사(isp, cfg +141) 분기 → WSAAsyncSelect
600054: mov	eax, 2
600059: mov	word ptr [ebp - 612], ax
600060: lea	ecx, [ebp - 608]
600066: mov	edx, dword ptr [7556680]
60006c: add	edx, 661
600072: mov	ebx, dword ptr [edx]
600074: mov	dword ptr [ecx], ebx
600076: mov	esi, dword ptr [7556680]
60007c: movzx	eax, word ptr [esi + 1056]
600083: movzx	edi, word ptr [esi + 1058]
60008a: add	eax, edi
60008c: movzx	edx, ax
60008f: push	edx
600090: call	0x65256a    ; ws!htons
600095: mov	word ptr [ebp - 610], ax
60009c: mov	ecx, dword ptr [7556680]
6000a2: call	0x474030
6000a7: mov	ecx, dword ptr [7556680]
6000ad: call	0x474030
6000b2: movsx	edx, al
6000b5: test	edx, edx
6000b7: jne	0x6000c6
6000b9: mov	eax, dword ptr [6947872]
6000be: test	eax, eax
6000c0: je	0x60018e
6000c6: mov	ecx, dword ptr [7556680]
6000cc: call	0x474030
6000d1: movsx	edx, al
6000d4: cmp	edx, 2
6000d7: je	0x60018e
6000dd: mov	eax, dword ptr [ebp - 28]
6000e0: mov	ecx, dword ptr [eax + 295116]
6000e6: lea	edx, [ebp - 612]
6000ec: push	16
6000ee: push	edx
6000ef: push	ecx
6000f0: call	0x652570    ; ws!connect
6000f5: cmp	eax, -1
6000f8: je	0x601034
6000fe: mov	edi, dword ptr [7556680]
600104: mov	al, byte ptr [edi + 141]
60010a: cmp	al, 2
60010c: je	0x601016
600112: cmp	al, 4
600114: je	0x600ff6
60011a: cmp	al, 3
60011c: je	0x600f6e
600122: cmp	al, 6
600124: je	0x600e44
60012a: cmp	al, 8
60012c: je	0x600c0f
600132: cmp	al, 10
600134: je	0x6006d1
60013a: cmp	al, 11
60013c: je	0x6004b4
600142: cmp	al, 7
600144: je	0x600345
60014a: cmp	al, 9
60014c: je	0x6002da
600152: mov	eax, dword ptr [ebp - 28]
600155: mov	ecx, dword ptr [eax + 295116]
60015b: mov	edx, dword ptr [7556704]
600161: push	33
600163: push	1026
600168: push	edx
600169: push	ecx
60016a: call	0x652576    ; ws!WSAAsyncSelect
60016f: test	eax, eax
600171: jne	0x6002cb
600177: mov	ebx, dword ptr [ebp - 52]

; ---- connect 실패: "Connection failure... retry.." → 서버 목록 [+1358] 다음 칸으로 바꿔 다시
6001ae: mov	eax, dword ptr [6947808]
6001b3: test	eax, eax
6001b5: je	0x6001cc
6001b7: mov	ecx, dword ptr [eax + 5456]
6001bd: push	255
6001c2: push	9038048    ; "Connection failure... retry.."
6001c7: call	0x623e60
6001cc: mov	ecx, dword ptr [7556680]
6001d2: movzx	ebx, byte ptr [ecx + 1357]
6001d9: test	ebx, ebx
6001db: je	0x6001f6
6001dd: movzx	eax, byte ptr [ecx + 1358]
6001e4: add	eax, 1
6001e7: xor	edx, edx
6001e9: idiv	ebx
6001eb: movzx	eax, dl
6001ee: mov	byte ptr [ecx + 1358], al
6001f4: jmp	0x6001ff
6001f6: xor	eax, eax
6001f8: mov	byte ptr [ecx + 1358], 0
6001ff: mov	ecx, dword ptr [ecx + 1360]
600205: test	ecx, ecx
600207: je	0x600297
60020d: lea	ebx, [ebp - 608]
600213: lea	edx, [eax + 4*eax]
600216: add	edx, edx
600218: add	edx, edx
60021a: add	edx, edx
60021c: add	edx, edx
60021e: add	edx, edx
600220: add	edx, edx
600222: add	eax, edx
600224: add	eax, eax
600226: add	eax, eax
600228: lea	eax, [ecx + 8*eax + 4]
60022c: mov	ecx, dword ptr [eax]
60022e: mov	dword ptr [ebx], ecx
