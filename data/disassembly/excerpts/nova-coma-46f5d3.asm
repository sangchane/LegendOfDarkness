; 혼수(빈사) — set_state · set_coma · coma_delay · 1초 처리
; 출처: ~/Downloads/5.99 서버팩/Novaonline.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)

; ── set_state 0x446e42 — 부른 캐릭터의 보이는 상태 칸 +0xF0 에 첫 인자, 주변에 모습 다시 보내기(0x462ec1)
  446e42: push ebp
  446e43: mov ebp, esp
  446e45: sub esp, 8
  446e56: mov eax, dword ptr [ebp + 8]
  446e59: mov ecx, dword ptr [eax + 20]
  446e5c: cmp dword ptr [4*ecx + 4903424], 0
  446e64: je 0x446e7b <.text+0x45e7b>
  446e66: mov edx, dword ptr [ebp + 8]
  446e69: mov eax, dword ptr [edx + 20]
  446e6c: mov ecx, dword ptr [4*eax + 4903424]
  446e73: mov edx, dword ptr [ecx + 32]
  446e76: mov dword ptr [ebp - 8], edx
  446e79: jmp 0x446e82 <.text+0x45e82>
  446e7b: mov dword ptr [ebp - 8], 0
  446e82: mov eax, dword ptr [ebp - 8]
  446e85: mov dword ptr [ebp - 4], eax
  446e88: mov ecx, dword ptr [ebp + 8]
  446e8b: mov edx, dword ptr [ecx]
  446e8d: mov eax, dword ptr [ebp + 8]
  446e90: mov ecx, dword ptr [eax + 4]
  446e93: add ecx, 2
  446e96: imul ecx, ecx, 12
  446e99: mov edx, dword ptr [edx + 12]
  446e9c: add edx, ecx
  446e9e: push edx
  446e9f: mov eax, dword ptr [ebp + 8]
  446ea2: push eax
  446ea3: call 0x43d110 <.text+0x3c110>
  446ea8: add esp, 8
  446eab: mov ecx, dword ptr [ebp - 4]
  446eae: mov edx, dword ptr [ecx + 16]
  446eb1: mov byte ptr [edx + 240], al
  446eb7: mov eax, dword ptr [ebp - 4]
  446eba: mov ecx, dword ptr [eax + 4]
  446ebd: push ecx
  446ebe: call 0x462ec1 <.text+0x61ec1>
  446ec3: add esp, 4
  446ec6: xor eax, eax
  446ec8: add esp, 8
  446ecb: cmp ebp, esp
  446ecd: call 0x476da4 <.text+0x75da4>
  446ed2: mov esp, ebp
  446ed4: pop ebp
  446ed5: ret

; ── set_coma 0x444bdf — 캐릭터 +0x6D(혼수)
  444bdf: push ebp
  444be0: mov ebp, esp
  444be2: sub esp, 8
  444bf3: mov eax, dword ptr [ebp + 8]
  444bf6: mov ecx, dword ptr [eax]
  444bf8: mov edx, dword ptr [ebp + 8]
  444bfb: mov eax, dword ptr [edx + 4]
  444bfe: add eax, 2
  444c01: imul eax, eax, 12
  444c04: mov ecx, dword ptr [ecx + 12]
  444c07: add ecx, eax
  444c09: push ecx
  444c0a: mov edx, dword ptr [ebp + 8]
  444c0d: push edx
  444c0e: call 0x43d110 <.text+0x3c110>
  444c13: add esp, 8
  444c16: mov dword ptr [ebp - 4], eax
  444c19: mov eax, dword ptr [ebp - 4]
  444c1c: mov ecx, dword ptr [4*eax + 4903424]
  444c23: mov edx, dword ptr [ecx + 32]
  444c26: mov dword ptr [ebp - 8], edx
  444c29: cmp dword ptr [ebp - 8], 0
  444c2d: je 0x444c5b <.text+0x43c5b>
  444c2f: mov eax, dword ptr [ebp + 8]
  444c32: mov ecx, dword ptr [eax]
  444c34: mov edx, dword ptr [ebp + 8]
  444c37: mov eax, dword ptr [edx + 4]
  444c3a: add eax, 3
  444c3d: imul eax, eax, 12
  444c40: mov ecx, dword ptr [ecx + 12]
  444c43: add ecx, eax
  444c45: push ecx
  444c46: mov edx, dword ptr [ebp + 8]
  444c49: push edx
  444c4a: call 0x43d110 <.text+0x3c110>
  444c4f: add esp, 8
  444c52: mov ecx, dword ptr [ebp - 8]
  444c55: mov edx, dword ptr [ecx + 16]
  444c58: mov byte ptr [edx + 109], al
  444c5b: xor eax, eax
  444c5d: add esp, 8
  444c60: cmp ebp, esp
  444c62: call 0x476da4 <.text+0x75da4>
  444c67: mov esp, ebp
  444c69: pop ebp
  444c6a: ret

; ── coma_delay 0x4539ac — 캐릭터 +0xF1(남은 초) · +0x15C=1
  4539ac: push ebp
  4539ad: mov ebp, esp
  4539af: sub esp, 8
  4539c0: mov eax, dword ptr [ebp + 8]
  4539c3: mov ecx, dword ptr [eax]
  4539c5: mov edx, dword ptr [ebp + 8]
  4539c8: mov eax, dword ptr [edx + 4]
  4539cb: add eax, 2
  4539ce: imul eax, eax, 12
  4539d1: mov ecx, dword ptr [ecx + 12]
  4539d4: add ecx, eax
  4539d6: push ecx
  4539d7: mov edx, dword ptr [ebp + 8]
  4539da: push edx
  4539db: call 0x43d110 <.text+0x3c110>
  4539e0: add esp, 8
  4539e3: mov dword ptr [ebp - 4], eax
  4539e6: mov eax, dword ptr [ebp - 4]
  4539e9: mov ecx, dword ptr [4*eax + 4903424]
  4539f0: mov edx, dword ptr [ecx + 32]
  4539f3: mov dword ptr [ebp - 8], edx
  4539f6: mov eax, dword ptr [ebp + 8]
  4539f9: mov ecx, dword ptr [eax]
  4539fb: mov edx, dword ptr [ebp + 8]
  4539fe: mov eax, dword ptr [edx + 4]
  453a01: add eax, 3
  453a04: imul eax, eax, 12
  453a07: mov ecx, dword ptr [ecx + 12]
  453a0a: add ecx, eax
  453a0c: push ecx
  453a0d: mov edx, dword ptr [ebp + 8]
  453a10: push edx
  453a11: call 0x43d110 <.text+0x3c110>
  453a16: add esp, 8
  453a19: mov ecx, dword ptr [ebp - 8]
  453a1c: mov edx, dword ptr [ecx + 16]
  453a1f: mov byte ptr [edx + 241], al
  453a25: mov eax, dword ptr [ebp - 8]
  453a28: mov ecx, dword ptr [eax + 16]
  453a2b: mov word ptr [ecx + 348], 1
  453a34: xor eax, eax
  453a36: add esp, 8
  453a39: cmp ebp, esp
  453a3b: call 0x476da4 <.text+0x75da4>
  453a40: mov esp, ebp
  453a42: pop ebp
  453a43: ret

; ── 0x462ec1 — 상태 칸에 따라 모습 다시 보내기(2 는 따로)
  462ec1: push ebp
  462ec2: mov ebp, esp
  462ec4: sub esp, 8
  462ed5: mov eax, dword ptr [ebp + 8]
  462ed8: mov ecx, dword ptr [4*eax + 4903424]
  462edf: mov edx, dword ptr [ecx + 32]
  462ee2: mov dword ptr [ebp - 8], edx
  462ee5: mov eax, dword ptr [ebp - 8]
  462ee8: mov ecx, dword ptr [eax + 16]
  462eeb: add ecx, 88
  462eee: mov dword ptr [ebp - 4], ecx
  462ef1: mov edx, dword ptr [ebp - 8]
  462ef4: mov eax, dword ptr [edx + 16]
  462ef7: xor ecx, ecx
  462ef9: mov cl, byte ptr [eax + 240]
  462eff: cmp ecx, 2
  462f02: jne 0x462fb3 <.text+0x61fb3>
  462f08: mov edx, dword ptr [ebp + 8]
  462f0b: push edx
  462f0c: call 0x462362 <.text+0x61362>
  462f11: add esp, 4
  462f14: mov eax, dword ptr [4903104]
  462f19: push eax
  462f1a: push 4837472
  462f1f: mov ecx, dword ptr [ebp + 8]
  462f22: mov edx, dword ptr [4*ecx + 4903424]
  462f29: mov eax, dword ptr [ebp + 8]
  462f2c: mov ecx, dword ptr [4*eax + 4903424]
  462f33: mov edx, dword ptr [edx + 4]
  462f36: add edx, dword ptr [ecx + 8]
  462f39: push edx
  462f3a: call 0x476a40 <.text+0x75a40>
  462f3f: add esp, 12
  462f42: mov eax, dword ptr [ebp + 8]
  462f45: push eax
  462f46: call 0x410470 <.text+0xf470>
  462f4b: add esp, 4
  462f4e: mov ecx, dword ptr [ebp - 8]
  462f51: mov edx, dword ptr [ecx + 16]
  462f54: mov byte ptr [edx + 240], 6
  462f5b: mov eax, dword ptr [ebp + 8]
  462f5e: push eax
  462f5f: call 0x462362 <.text+0x61362>
  462f64: add esp, 4
  462f67: mov ecx, dword ptr [4903104]
  462f6d: push ecx
  462f6e: push 4837472
  462f73: mov edx, dword ptr [ebp + 8]
  462f76: mov eax, dword ptr [4*edx + 4903424]
  462f7d: mov ecx, dword ptr [ebp + 8]
  462f80: mov edx, dword ptr [4*ecx + 4903424]
  462f87: mov eax, dword ptr [eax + 4]
  462f8a: add eax, dword ptr [edx + 8]
  462f8d: push eax
  462f8e: call 0x476a40 <.text+0x75a40>
  462f93: add esp, 12
  462f96: push 4
  462f98: mov ecx, dword ptr [ebp + 8]
  462f9b: push ecx
  462f9c: call 0x416fa9 <.text+0x15fa9>
  462fa1: add esp, 8
  462fa4: mov edx, dword ptr [ebp - 8]
  462fa7: mov eax, dword ptr [edx + 16]
  462faa: mov byte ptr [eax + 240], 2
  462fb1: jmp 0x462ff6 <.text+0x61ff6>
  462fb3: mov ecx, dword ptr [ebp + 8]
  462fb6: push ecx
  462fb7: call 0x462362 <.text+0x61362>
  462fbc: add esp, 4
  462fbf: mov edx, dword ptr [ebp - 4]
  462fc2: push edx
  462fc3: call 0x41774b <.text+0x1674b>
  462fc8: add esp, 4
  462fcb: mov eax, dword ptr [ebp - 8]
  462fce: mov ecx, dword ptr [eax + 16]
  462fd1: xor edx, edx
  462fd3: mov dl, byte ptr [ecx + 240]
  462fd9: cmp edx, 4
  462fdc: jne 0x462ff6 <.text+0x61ff6>
  462fde: mov eax, dword ptr [ebp + 8]
  462fe1: push eax
  462fe2: call 0x462213 <.text+0x61213>
  462fe7: add esp, 4
  462fea: mov ecx, dword ptr [ebp - 4]
  462fed: push ecx
  462fee: call 0x41774b <.text+0x1674b>
  462ff3: add esp, 4
  462ff6: add esp, 8
  462ff9: cmp ebp, esp
  462ffb: call 0x476da4 <.text+0x75da4>
  463000: mov esp, ebp
  463002: pop ebp
  463003: ret

; ── 0x46f5d3 — 1초마다: +0xF1 을 1 줄이고 그림 24(0x46b66a)·아이콘 89(0x458eca), 0 이 되면 메시지 3 "끝" 과 스크립트 __COMA_END__(0x4917a8)
  46f5d3: push ebp
  46f5d4: mov ebp, esp
  46f5d6: sub esp, 20
  46f5de: mov dword ptr [ebp - 20], eax
  46f5e1: mov dword ptr [ebp - 16], eax
  46f5e4: mov dword ptr [ebp - 12], eax
  46f5e7: mov dword ptr [ebp - 8], eax
  46f5ea: mov dword ptr [ebp - 4], eax
  46f5ed: push 4790184
  46f5f2: call 0x40ef76 <.text+0xdf76>
  46f5f7: add esp, 4
  46f5fa: mov dword ptr [ebp - 20], eax
  46f5fd: mov dword ptr [ebp - 16], 0
  46f604: jmp 0x46f60f <.text+0x6e60f>
  46f606: mov eax, dword ptr [ebp - 16]
  46f609: add eax, 1
  46f60c: mov dword ptr [ebp - 16], eax
  46f60f: mov ecx, dword ptr [ebp - 16]
  46f612: cmp ecx, dword ptr [4903136]
  46f618: jae 0x46f752 <.text+0x6e752>
  46f61e: mov edx, dword ptr [ebp - 16]
  46f621: mov eax, dword ptr [4*edx + 4903140]
  46f628: mov ecx, dword ptr [4*eax + 4903424]
  46f62f: mov dword ptr [ebp - 8], ecx
  46f632: cmp dword ptr [ebp - 8], 0
  46f636: je 0x46f74d <.text+0x6e74d>
  46f63c: mov edx, dword ptr [ebp - 8]
  46f63f: xor eax, eax
  46f641: mov al, byte ptr [edx + 61]
  46f644: test eax, eax
  46f646: je 0x46f74d <.text+0x6e74d>
  46f64c: mov ecx, dword ptr [ebp - 8]
  46f64f: xor edx, edx
  46f651: mov dl, byte ptr [ecx + 60]
  46f654: test edx, edx
  46f656: je 0x46f74d <.text+0x6e74d>
  46f65c: mov eax, dword ptr [ebp - 8]
  46f65f: mov ecx, dword ptr [eax + 32]
  46f662: mov dword ptr [ebp - 12], ecx
  46f665: cmp dword ptr [ebp - 12], 0
  46f669: je 0x46f74d <.text+0x6e74d>
  46f66f: mov edx, dword ptr [ebp - 12]
  46f672: mov eax, dword ptr [edx + 16]
  46f675: xor ecx, ecx
  46f677: mov cl, byte ptr [eax + 241]
  46f67d: test ecx, ecx
  46f67f: je 0x46f73e <.text+0x6e73e>
  46f685: mov edx, dword ptr [ebp - 12]
  46f688: mov eax, dword ptr [edx + 16]
  46f68b: mov cl, byte ptr [eax + 241]
  46f691: sub cl, 1
  46f694: mov edx, dword ptr [ebp - 12]
  46f697: mov eax, dword ptr [edx + 16]
  46f69a: mov byte ptr [eax + 241], cl
  46f6a0: push 75
  46f6a2: push 0
  46f6a4: push 24
  46f6a6: push 0
  46f6a8: mov ecx, dword ptr [ebp - 12]
  46f6ab: mov edx, dword ptr [ecx + 16]
  46f6ae: add edx, 88
  46f6b1: push edx
  46f6b2: mov eax, dword ptr [ebp - 16]
  46f6b5: mov ecx, dword ptr [4*eax + 4903140]
  46f6bc: push ecx
  46f6bd: call 0x46b66a <.text+0x6a66a>
  46f6c2: add esp, 24
  46f6c5: mov edx, dword ptr [ebp - 12]
  46f6c8: mov eax, dword ptr [edx + 16]
  46f6cb: mov cl, byte ptr [eax + 241]
  46f6d1: push ecx
  46f6d2: push 89
  46f6d4: mov edx, dword ptr [ebp - 16]
  46f6d7: mov eax, dword ptr [4*edx + 4903140]
  46f6de: push eax
  46f6df: call 0x458eca <.text+0x57eca>
  46f6e4: add esp, 12
  46f6e7: mov ecx, dword ptr [ebp - 12]
  46f6ea: mov edx, dword ptr [ecx + 16]
  46f6ed: xor eax, eax
  46f6ef: mov al, byte ptr [edx + 241]
  46f6f5: test eax, eax
  46f6f7: jne 0x46f73c <.text+0x6e73c>
  46f6f9: mov ecx, dword ptr [ebp - 12]
  46f6fc: mov edx, dword ptr [ecx + 16]
  46f6ff: mov word ptr [edx + 348], 0
  46f708: push 4790200
  46f70d: push 3
  46f70f: mov eax, dword ptr [ebp - 16]
  46f712: mov ecx, dword ptr [4*eax + 4903140]
  46f719: push ecx
  46f71a: call 0x45cecb <.text+0x5becb>
  46f71f: add esp, 12
  46f722: push 0
  46f724: mov edx, dword ptr [ebp - 12]
  46f727: mov eax, dword ptr [edx + 4]
  46f72a: push eax
  46f72b: push 0
  46f72d: mov ecx, dword ptr [ebp - 20]
  46f730: mov edx, dword ptr [ecx + 20]
  46f733: push edx
  46f734: call 0x43e65f <.text+0x3d65f>
  46f739: add esp, 16
  46f73c: jmp 0x46f74d <.text+0x6e74d>
  46f73e: mov eax, dword ptr [ebp - 12]
  46f741: mov ecx, dword ptr [eax + 16]
  46f744: mov word ptr [ecx + 348], 0
  46f74d: jmp 0x46f606 <.text+0x6e606>
  46f752: add esp, 20
  46f755: cmp ebp, esp
  46f757: call 0x476da4 <.text+0x75da4>
  46f75c: mov esp, ebp
  46f75e: pop ebp
  46f75f: ret
