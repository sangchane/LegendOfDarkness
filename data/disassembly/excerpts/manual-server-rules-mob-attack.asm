; Novaonline.exe (5.99 서버) — 괴물 추적·공격·기술 (0x4258a4 중 사람 표적 부분)
; 놓치는 조건: 다른 맵, |dx|>16 또는 |dy|>14, 캐릭터 상태(+0xF0)≥1 → 0x42597c 가 +0x18·+0x1C 를 지운다.
; 옆칸(0x45c310 방향 0~3)에 표적: 그쪽을 안 보면 돌기만, 보면 몸동작(1,속도20) + 피해 0x415173(최소+0x30,최대+0x34) → 속성 0x415cff → 0x415341.
;   mob+0x3A(잠)·+0x42 면 공격 안 함, +0x3C 면 동작만 하고 피해 없음.
; 멀면 50% 로 세로 먼저/가로 먼저 한 칸 다가감(길찾기 없음), 막히면 빈 방향 무작위.
; 기술: +0x3E/+0x50/+0x3A/+0x42 없고, 침묵(+0x44)이면 템플릿 면역(+0x3C)이 있을 때만,  |rand%100| ≤ 템플릿 +0x9C 이면 스크립트 +0xA4 실행.
425ca0: mov edx, dword ptr [ebp - 36]
425ca3: mov eax, dword ptr [ebp - 64]
425ca6: mov ecx, dword ptr [edx]
425ca8: cmp ecx, dword ptr [eax]
425caa: jne 0x425d03 <.text+0x24d03>
425cac: mov edx, dword ptr [ebp - 36]
425caf: xor eax, eax
425cb1: mov ax, word ptr [edx + 4]
425cb5: mov ecx, dword ptr [ebp - 64]
425cb8: xor edx, edx
425cba: mov dx, word ptr [ecx + 4]
425cbe: sub eax, edx
425cc0: push eax
425cc1: call 0x4783fb <.text+0x773fb>
425cc9: cmp eax, 16
425ccc: jg 0x425d03 <.text+0x24d03>
425cce: mov eax, dword ptr [ebp - 36]
425cd1: xor ecx, ecx
425cd3: mov cx, word ptr [eax + 6]
425cd7: mov edx, dword ptr [ebp - 64]
425cda: xor eax, eax
425cdc: mov ax, word ptr [edx + 6]
425ce0: sub ecx, eax
425ce2: push ecx
425ce3: call 0x4783fb <.text+0x773fb>
425ceb: cmp eax, 14
425cee: jg 0x425d03 <.text+0x24d03>
425cf0: mov ecx, dword ptr [ebp - 84]
425cf3: mov edx, dword ptr [ecx + 16]
425cf6: xor eax, eax
425cf8: mov al, byte ptr [edx + 240]
425cfe: cmp eax, 1
425d01: jl 0x425d08 <.text+0x24d08>
425d03: jmp 0x42597c <.text+0x2497c>
; ...공격
425df8: push 0
425dfa: push 20
425dfc: push 1
425dfe: mov eax, dword ptr [ebp + 8]
425e01: push eax
425e02: call 0x420fe2 <.text+0x1ffe2>
425e0a: push 255
425e0f: push 2
425e11: push 100
425e13: mov ecx, dword ptr [ebp - 8]
425e16: mov edx, dword ptr [ecx + 28]
425e19: push edx
425e1a: mov eax, dword ptr [ebp - 8]
425e1d: mov ecx, dword ptr [eax + 28]
425e20: push ecx
425e21: call 0x414f21 <.text+0x13f21>
425e29: mov edx, dword ptr [ebp - 8]
425e2c: xor eax, eax
425e2e: mov ax, word ptr [edx + 58]
425e32: test eax, eax
425e34: je 0x425e3b <.text+0x24e3b>
425e36: jmp 0x426563 <.text+0x25563>
425e3b: mov ecx, dword ptr [ebp - 8]
425e3e: xor edx, edx
425e40: mov dx, word ptr [ecx + 66]
425e44: test edx, edx
425e46: je 0x425e4d <.text+0x24e4d>
425e48: jmp 0x426563 <.text+0x25563>
425e4d: mov eax, dword ptr [ebp - 8]
425e50: xor ecx, ecx
425e52: mov cx, word ptr [eax + 60]
425e56: test ecx, ecx
425e58: jne 0x425e6f <.text+0x24e6f>
425e5a: mov edx, dword ptr [ebp - 68]
425e5d: push edx
425e5e: push 0
425e60: mov eax, dword ptr [ebp - 8]
425e63: mov ecx, dword ptr [eax + 28]
425e66: push ecx
425e67: call 0x415341 <.text+0x14341>
425e6f: jmp 0x4263fd <.text+0x253fd>
; ---- 기술 굴림
42641b: mov eax, dword ptr [ebp - 8]
42641e: xor ecx, ecx
426420: mov cx, word ptr [eax + 62]
426424: test ecx, ecx
426426: je 0x42642d <.text+0x2542d>
426428: jmp 0x426563 <.text+0x25563>
42642d: mov edx, dword ptr [ebp - 8]
426430: xor eax, eax
426432: mov ax, word ptr [edx + 80]
426436: test eax, eax
426438: je 0x42643f <.text+0x2543f>
42643a: jmp 0x426563 <.text+0x25563>
42643f: mov ecx, dword ptr [ebp - 8]
426442: xor edx, edx
426444: mov dx, word ptr [ecx + 58]
426448: test edx, edx
42644a: je 0x426451 <.text+0x25451>
42644c: jmp 0x426563 <.text+0x25563>
426451: mov eax, dword ptr [ebp - 8]
426454: xor ecx, ecx
426456: mov cx, word ptr [eax + 66]
42645a: test ecx, ecx
42645c: je 0x426463 <.text+0x25463>
42645e: jmp 0x426563 <.text+0x25563>
426463: mov edx, dword ptr [ebp - 8]
426466: xor eax, eax
426468: mov ax, word ptr [edx + 68]
42646c: test eax, eax
42646e: je 0x42647e <.text+0x2547e>
426470: mov ecx, dword ptr [ebp - 32]
426473: cmp dword ptr [ecx + 60], 0
426477: jne 0x42647e <.text+0x2547e>
426479: jmp 0x426563 <.text+0x25563>
42647e: mov edx, dword ptr [ebp - 32]
426481: cmp dword ptr [edx + 164], 0
426488: je 0x426563 <.text+0x25563>
42648e: call 0x476d82 <.text+0x75d82>
426493: cdq
426494: mov ecx, 100
426499: idiv ecx
42649b: push edx
42649c: call 0x4783fb <.text+0x773fb>
4264a4: mov edx, dword ptr [ebp - 32]
4264a7: xor ecx, ecx
4264a9: mov cx, word ptr [edx + 156]
4264b0: cmp eax, ecx
4264b2: jg 0x426563 <.text+0x25563>
4264b8: cmp dword ptr [ebp - 60], 0
