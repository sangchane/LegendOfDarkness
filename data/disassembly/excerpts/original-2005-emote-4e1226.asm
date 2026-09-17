; 감정표현 (2005 = 5.99 클라이언트 Legend.exe) — 몸 동작 9~17 · 23~44 를 머리 위 표정·말풍선(emot01.epf)으로
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe (md5 347dc381…, 원작 lodr.exe 안의 것과 같음)
; 도구: llvm-objdump -d --x86-asm-syntax=intel, 오프셋은 10진수
;
; 0x4e1104: 하던 일이 없고([+520]==0) 방향 [+516] 이 1·2(동·남, 앞모습)일 때만 표 0x869880(=8820864) 을 찾는다.
; 표 한 줄 64바이트: +0 단축키(0x1031 …, 음수면 끝) · +4 클라이언트 번호 0~35 · +8 몸 동작 번호(-1 이면 앞 줄 + 1)
;   · +12 첫 칸(1부터, -1 이면 앞 줄에서 이어서) · +16 칸 수(그림 수 + 1) · +20 칸마다 ms · +24 채팅 글("^^" "ㅠㅠ" …)
; 찾으면 상태 6, [+562] = 첫 칸, 타이머(0x1000008, ms, 칸 수).
; 표 값 (첫 칸, 칸 수, ms): 9~15 → (1~7, 2, 1500) · 16 → (8, 3, 1000) · 17 → (10, 3, 1000) · 18·19 → (-1, 1, 1000)
;   23 → (12, 2, 1500) · 24~26 → (-1, 2, 1500) · 27 → (-1, 4, 500) · 28~41 → (-1, 2, 1500, 34 만 번호를 다시 적음)
;   42·43 → (-1, 4, 500) · 44 → (-1, 5, 500). 첫 칸 -1 은 앞 줄 첫 칸 + 앞 줄 칸 수 - 1.
; → 0부터 센 칸: 9~15 → 0~6 · 16 → 7~8 · 17 → 9~10 · 23~26 → 11~14 · 27 → 15~17 · 28~41 → 18~31 · 42 → 32~34 · 43 → 35~37 · 44 → 38~41

  4e1104: mov eax, dword ptr [esi + 520]
  4e110a: test eax, eax
  4e110c: jne 0x4e1124 <.text+0xba124>
  4e110e: mov al, byte ptr [esi + 516]
  4e1114: cmp al, 1
  4e1116: je 0x4e1226 <.text+0xba226>
  4e111c: cmp al, 2
  4e111e: je 0x4e1226 <.text+0xba226>
  4e1124: cmp ebx, 128
  ...
  4e1226: mov edx, 8820864
  4e122b: xor ecx, ecx
  4e122d: xor eax, eax
  4e122f: mov edi, dword ptr [8820864]
  4e1235: test edi, edi
  4e1237: jl 0x4e1124 <.text+0xba124>
  4e123d: mov dword ptr [esp + 4], esi
  4e1241: mov esi, dword ptr [edx + 8]
  4e1244: test esi, esi
  4e1246: jl 0x4e1274 <.text+0xba274>
  4e1248: mov eax, esi
  4e124a: mov edi, dword ptr [edx + 12]
  4e124d: test edi, edi
  4e124f: jl 0x4e1253 <.text+0xba253>
  4e1251: mov ecx, edi
  4e1253: cmp ebx, esi
  4e1255: je 0x4e1280 <.text+0xba280>
  4e1257: mov edi, dword ptr [edx + 16]
  4e125a: lea ecx, [edi + ecx - 1]
  4e125e: add eax, 1
  4e1261: mov esi, dword ptr [edx + 64]
  4e1264: add edx, 64
  4e1267: test esi, esi
  4e1269: jge 0x4e1241 <.text+0xba241>
  4e126b: mov esi, dword ptr [esp + 4]
  4e126f: jmp 0x4e1124 <.text+0xba124>
  4e1274: mov esi, eax
  4e1276: jmp 0x4e124a <.text+0xba24a>
  4e1278: nop
  4e1279: lea esi, [esi + eiz]
  4e1280: mov esi, dword ptr [esp + 4]
  4e1284: c7 86 08 02 00 00 06 00 00 00 mov dword ptr [esi + 520], 6
  4e128e: xor eax, eax
  4e1290: mov byte ptr [esi + 524], al
  4e1296: mov word ptr [esi + 562], cx
  4e129d: mov ecx, dword ptr [7556692]
  4e12a3: mov edi, dword ptr [edx + 20]
  4e12a6: mov edx, dword ptr [edx + 16]
  4e12a9: push edx
  4e12aa: push edi
  4e12ab: push eax
  4e12ac: push 16777224
  4e12b1: push esi
  4e12b2: call 0x4ac5b0 <.text+0x855b0>
  4e12b7: jmp 0x4e1124 <.text+0xba124>
  ...
; 겹치기 0x4e77e4: 감정 칸([esp+220]) 이 있으면 emot01.epf 칸-1 을 읽는다. 얼굴 장식 C(+24) 가 30·31·32 면 emot04·03·02.
; 자리: 가로 x+55-[0x86b514](=28) · 세로 y+10. 그리는 때: 순서표 값이 4(첫 H)일 때 그 앞(0x4e7ca7 → 0x4e82a1).
  4e7b5d: movsx eax, word ptr [esp + 220]
  4e7b65: test eax, eax
  4e7b67: je 0x4e7c14 <.text+0xc0c14>
  4e7b6d: mov eax, dword ptr [esp + 196]
  4e7b74: movzx eax, word ptr [eax + 24]
  4e7b78: cmp eax, 32
  4e7b7b: je 0x4e8257 <.text+0xc1257>
  4e7b81: cmp eax, 31
  4e7b84: je 0x4e820d <.text+0xc120d>
  4e7b8a: cmp eax, 30
  4e7b8d: je 0x4e81c3 <.text+0xc11c3>
  4e7b93: mov ebp, dword ptr [6846784]
  4e7b99: movsx esi, word ptr [esp + 220]
  4e7ba1: lea edi, [esi - 1]
  4e7ba4: movsx eax, di
  4e7ba7: mov dword ptr [esp + 160], eax
  4e7bae: call 0x4bddb0 <.text+0x96db0>
  4e7bb3: lea esi, [esp + 24]
  4e7bb7: push 0
  4e7bb9: push eax
  4e7bba: push esi
  4e7bbb: mov edx, dword ptr [esp + 172]
  4e7bc2: push edx
  4e7bc3: push 8829760
  4e7bc8: mov ecx, ebp
  4e7bca: call 0x4e8854 <.text+0xc1854>
  4e7bcf: mov esi, dword ptr [esp + 36]
  4e7bd3: mov ecx, dword ptr [esp + 32]
  4e7bd7: mov edi, dword ptr [esp + 44]
  4e7bdb: mov ebp, dword ptr [esp + 40]
  4e7bdf: mov eax, dword ptr [esp + 224]
  4e7be6: lea edx, [eax + 55]
  4e7be9: sub edx, dword ptr [8828180]
  4e7bef: mov eax, dword ptr [esp + 228]
  4e7bf6: lea ecx, [ecx + eax + 10]
  4e7bfa: mov dword ptr [esp + 92], ecx
  4e7bfe: add esi, edx
  4e7c00: mov dword ptr [esp + 96], esi
  4e7c04: lea eax, [ebp + eax + 10]
  4e7c08: mov dword ptr [esp + 100], eax
  4e7c0c: add edi, edx
  4e7c0e: mov dword ptr [esp + 104], edi
  4e7c12: mov dl, 1
  4e7c98: cmp al, 11
  4e7c9a: je 0x4e8410 <.text+0xc1410>
  4e7ca0: mov al, byte ptr [esp + 144]
  4e7ca7: cmp al, 4
  4e7ca9: je 0x4e82a1 <.text+0xc12a1>
