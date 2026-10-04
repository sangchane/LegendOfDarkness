; Novaonline.exe (5.99 서버) — 괴물 죽음·금화·드롭
; 0x424215(사람이 괴물을 침) 끝: 처치 수 char+0x84 +1, 그룹이면 0x41a229/0x41a2cb, 아니면 0x4696e4/0x46a165 (경험치·어빌 경험치 = 템플릿 +0x20/+0x24),
;   __MOB_KILL__ 스크립트, mob+0x50 = 30 (10ms 타이머 0x46f760 이 줄여 0 이 되면 0x423f16 → 300ms 뒤 치움).
; 0x423f16: 골드(+0x2C)≠0 이고 |rand%100| ≤ 골드확률(+0x9E) 이면 그 칸에 금화 더미(0x422635, 금액 그대로).
;   타입 4·5 가 아니면 0x423c00 드롭. 그다음 스폰칸 살아있음=0, 시각=time(0) (리스폰 기준).
; 0x423c00: 20% 로 리쿠리쿠(내구 ÷2). 무리마다(+0x50 드롭아이템/1, +0x58 2, +0x60 3, +0x68 4, +0x70 5) 줄 순서로 |rand%100| < 확률 인 첫 줄 하나만 떨군다.
424658: cmp dword ptr [ebp - 516], 0
42465f: jne 0x42475b <.text+0x2375b>
424665: mov edx, dword ptr [ebp - 1024]
42466b: mov eax, dword ptr [edx + 16]
42466e: mov cl, byte ptr [eax + 132]
424674: add cl, 1
424677: mov edx, dword ptr [ebp - 1024]
42467d: mov eax, dword ptr [edx + 16]
424680: mov byte ptr [eax + 132], cl
424686: mov ecx, dword ptr [ebp - 1024]
42468c: cmp dword ptr [ecx + 20], 0
424690: je 0x4246d5 <.text+0x236d5>
424692: mov edx, dword ptr [ebp - 512]
424698: mov eax, dword ptr [edx + 4]
42469b: mov ecx, dword ptr [eax + 32]
42469e: push ecx
42469f: mov edx, dword ptr [ebp + 8]
4246a2: push edx
4246a3: call 0x41a229 <.text+0x19229>
4246ab: mov eax, dword ptr [ebp - 512]
4246b1: mov ecx, dword ptr [eax + 4]
4246b4: cmp dword ptr [ecx + 36], 0
4246b8: je 0x4246d3 <.text+0x236d3>
4246ba: mov edx, dword ptr [ebp - 512]
4246c0: mov eax, dword ptr [edx + 4]
4246c3: mov ecx, dword ptr [eax + 36]
4246c6: push ecx
4246c7: mov edx, dword ptr [ebp + 8]
4246ca: push edx
4246cb: call 0x41a2cb <.text+0x192cb>
4246d3: jmp 0x424718 <.text+0x23718>
4246d5: push 0
4246d7: mov eax, dword ptr [ebp - 512]
4246dd: mov ecx, dword ptr [eax + 4]
4246e0: mov edx, dword ptr [ecx + 32]
4246e3: push edx
4246e4: mov eax, dword ptr [ebp + 8]
4246e7: push eax
4246e8: call 0x4696e4 <.text+0x686e4>
4246f0: mov ecx, dword ptr [ebp - 512]
4246f6: mov edx, dword ptr [ecx + 4]
4246f9: cmp dword ptr [edx + 36], 0
4246fd: je 0x424718 <.text+0x23718>
4246ff: mov eax, dword ptr [ebp - 512]
424705: mov ecx, dword ptr [eax + 4]
424708: mov edx, dword ptr [ecx + 36]
42470b: push edx
42470c: mov eax, dword ptr [ebp + 8]
42470f: push eax
424710: call 0x46a165 <.text+0x69165>
424718: mov ecx, dword ptr [ebp - 512]
42471e: mov edx, dword ptr [ecx + 4]
424721: mov eax, dword ptr [ebp - 1024]
424727: mov ecx, dword ptr [eax + 16]
42472a: mov edx, dword ptr [edx]
42472c: mov dword ptr [ecx + 48], edx
42472f: push 0
424731: mov eax, dword ptr [ebp - 1024]
424737: mov ecx, dword ptr [eax + 4]
42473a: push ecx
42473b: push 0
42473d: mov edx, dword ptr [ebp - 508]
424743: mov eax, dword ptr [edx + 20]
424746: push eax
424747: call 0x43e65f <.text+0x3d65f>
42474f: mov ecx, dword ptr [ebp - 512]
424755: mov word ptr [ecx + 80], 30
42475b: pop edi
; ---- 0x423f16 금화
423f4a: mov edx, dword ptr [ebp - 4]
423f4d: mov eax, dword ptr [edx + 4]
423f50: cmp dword ptr [eax + 44], 0
423f54: je 0x423f9a <.text+0x22f9a>
423f56: call 0x476d82 <.text+0x75d82>
423f5b: cdq
423f5c: mov ecx, 100
423f61: idiv ecx
423f63: push edx
423f64: call 0x4783fb <.text+0x773fb>
423f6c: mov edx, dword ptr [ebp - 4]
423f6f: mov ecx, dword ptr [edx + 4]
423f72: xor edx, edx
423f74: mov dx, word ptr [ecx + 158]
423f7b: cmp eax, edx
423f7d: jg 0x423f9a <.text+0x22f9a>
423f7f: push 0
423f81: mov eax, dword ptr [ebp - 4]
423f84: mov ecx, dword ptr [eax + 4]
423f87: mov edx, dword ptr [ecx + 44]
423f8a: push edx
423f8b: mov eax, dword ptr [ebp + 8]
423f8e: add eax, 8
423f91: push eax
423f92: call 0x422635 <.text+0x21635>
423f9a: mov ecx, dword ptr [ebp - 4]
423f9d: mov edx, dword ptr [ecx + 4]
423fa0: movsx eax, byte ptr [edx + 12]
423fa4: cmp eax, 4
423fa7: je 0x423fcf <.text+0x22fcf>
423fa9: mov ecx, dword ptr [ebp - 4]
423fac: mov edx, dword ptr [ecx + 4]
423faf: movsx eax, byte ptr [edx + 12]
423fb3: cmp eax, 5
423fb6: je 0x423fcf <.text+0x22fcf>
423fb8: mov ecx, dword ptr [ebp + 8]
423fbb: movsx edx, byte ptr [ecx]
423fbe: cmp edx, 1
423fc1: jne 0x423fcf <.text+0x22fcf>
423fc3: mov eax, dword ptr [ebp + 8]
423fc6: push eax
423fc7: call 0x423c00 <.text+0x22c00>
423fcf: mov ecx, dword ptr [ebp - 4]
423fd2: cmp dword ptr [ecx + 104], 0
423fd6: je 0x423fe1 <.text+0x22fe1>
423fd8: mov edx, dword ptr [ebp - 4]
423fdb: mov eax, dword ptr [edx + 104]
423fde: mov byte ptr [eax], 0
423fe1: mov ecx, dword ptr [ebp - 4]
; ---- 0x423c00 리쿠리쿠 20% · 첫 무리
423c38: call 0x476d82 <.text+0x75d82>
423c3d: cdq
423c3e: mov ecx, 100
423c43: idiv ecx
423c45: push edx
423c46: call 0x4783fb <.text+0x773fb>
423c4e: cmp eax, 20
423c51: jge 0x423caa <.text+0x22caa>
423c53: mov dword ptr [ebp - 20], 0
423c5a: mov byte ptr [ebp - 12], 1
423c5e: push 4757048   ; "리쿠리쿠"
423c63: call 0x40ede1 <.text+0xdde1>
423c6b: mov dword ptr [ebp - 8], eax
423c6e: mov edx, dword ptr [ebp - 8]
423c71: mov eax, dword ptr [edx + 32]
423c74: mov dword ptr [ebp - 32], eax
423c77: mov dword ptr [ebp - 28], 0
423c7e: fild qword ptr [ebp - 32]
423c81: fdiv qword ptr [4723392]
423c87: call 0x4783d4 <.text+0x773d4>
423c8c: mov dword ptr [ebp - 16], eax
423c8f: push 0
423c91: push 0
423c93: push 0
423c95: push 0
423c97: lea ecx, [ebp - 20]
423c9a: push ecx
423c9b: mov edx, dword ptr [ebp + 8]
423c9e: add edx, 8
423ca1: push edx
423ca2: call 0x4227e9 <.text+0x217e9>
423caa: cmp dword ptr [ebp - 24], 0
423cae: je 0x423d18 <.text+0x22d18>
423cb0: call 0x476d82 <.text+0x75d82>
423cb5: cdq
423cb6: mov ecx, 100
423cbb: idiv ecx
423cbd: push edx
423cbe: call 0x4783fb <.text+0x773fb>
423cc6: mov edx, dword ptr [ebp - 24]
423cc9: xor ecx, ecx
423ccb: mov cl, byte ptr [edx + 20]
423cce: cmp eax, ecx
423cd0: jge 0x423d0d <.text+0x22d0d>
423cd2: mov dword ptr [ebp - 20], 0
423cd9: mov edx, dword ptr [ebp - 24]
423cdc: mov eax, dword ptr [edx]
423cde: mov ecx, dword ptr [eax + 32]
423ce1: mov dword ptr [ebp - 16], ecx
423ce4: mov byte ptr [ebp - 12], 2
423ce8: mov edx, dword ptr [ebp - 24]
423ceb: mov eax, dword ptr [edx]
423ced: mov dword ptr [ebp - 8], eax
423cf0: push 0
423cf2: push 0
423cf4: push 0
423cf6: push 0
423cf8: lea ecx, [ebp - 20]
423cfb: push ecx
423cfc: mov edx, dword ptr [ebp + 8]
423cff: add edx, 8
423d02: push edx
423d03: call 0x4227e9 <.text+0x217e9>
423d0b: jmp 0x423d18 <.text+0x22d18>
423d0d: mov eax, dword ptr [ebp - 24]
423d10: mov ecx, dword ptr [eax + 36]
423d13: mov dword ptr [ebp - 24], ecx
423d16: jmp 0x423caa <.text+0x22caa>
423d18: mov edx, dword ptr [ebp - 4]
