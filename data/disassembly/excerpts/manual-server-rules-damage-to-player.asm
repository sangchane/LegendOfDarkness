; 괴물 → 사람 · 사람 → 사람 피해 — 괴물 AI 0x424bb4 · 사람 피해 받기 0x415341 · char_damaged 0x44e4d5
; 출처: ~/Downloads/5.99 서버팩/Novaonline.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
; P = 사람 객체, C = [P+0x10], M = 괴물 하나, T = [M+4] 템플릿
;
; 괴물 평타(0x425d4c): d = 0x415173(0, T+0x30 최소공격력, T+0x34 최대공격력, P 바이트+0x8C 받는 사람 AC 합, 1)
;   = 최소 + (rand×rand×rand) mod (최대−최소) → AC 적용(0x4150f2, 음수 AC 1점당 −0.9%)
;   속성: 0x415cff(d, M+0x65 괴물 공격속성, 허리띠[C+0x1A0].방어속성) — 공격속성 1~5 면 ×1.3.
;   M+0x65 는 생성 때 0 이면 1~4 중 무작위(0x422bc5)라 사실상 괴물 평타는 늘 ×1.3. 받는 쪽 속성은 안 본다
;   치명타 없음 · 명중 판정 없음. 앞칸(방향 0~3)에 목표가 없으면 돌아서기만(0x420ef0)
;   M+0x3A(잠)·M+0x42 이면 동작도 안 하고 끝, M+0x3C 이면 동작만 하고 피해 없음
; 사람 피해 받기 0x415341(id, ?, d): C 바이트+0x62(쉬기)·word+0x136(숨기) 풀림
;   C+0x14A(portris)·+0x148(defens)·+0x144(immortal) → d=0 · C+0x134(horrama) → d/2
;   C 바이트+0x76(나르콜리 잠) → d×2 후 풀림 · C+0xCC(체력) −= d · 체력%=C+0xCC/P+0x90×100 → 0x414e70
;   체력 0 → 죽음 스크립트([0x4edc50])가 있으면 그것, 없으면 0x415202(경험치 C+0xD4 의 1% 잃고 유령 C+0xF0=1)
;   (0x4156a7 은 +0x146(dell)·+0x144 만, 0x4159f0 은 막는 칸 없이 같은 처리)
; 사람 평타로 사람(0x416961): 평타 원값·치명타·오기 그대로, AC 적용 없이 0x415341
; char_damaged(대상, d): 같은 그룹이면 무시 · C+0x15E ×1.5 · C+0x160 ×값 · 치명타 ×2 ·
;   방어: 0x415173(P+0xA0 공격수정, d, d, 대상 C+0xC8 기본 AC(장비 뺀 값), 1) — C+0x15A 면 20% 건너뜀
;   대상 C 바이트+0x71(reply 반사): rand%100 ≤10 맞음 · 11~90 막음(피해 0) · 91~ 공격자에게 되돌림
  ...
  425d4c: push 1
  425d4e: mov edx, dword ptr [ebp - 84]
  425d51: mov al, byte ptr [edx + 140]
  425d57: push eax
  425d58: mov ecx, dword ptr [ebp - 8]
  425d5b: mov edx, dword ptr [ecx + 4]
  425d5e: mov eax, dword ptr [edx + 52]
  425d61: push eax
  425d62: mov ecx, dword ptr [ebp - 8]
  425d65: mov edx, dword ptr [ecx + 4]
  425d68: mov eax, dword ptr [edx + 48]
  425d6b: push eax
  425d6c: push 0
  425d6e: call 0x415173 <.text+0x14173>
  425d73: add esp, 20
  425d76: mov dword ptr [ebp - 68], eax
  425d79: mov ecx, dword ptr [ebp - 8]
  425d7c: movsx edx, byte ptr [ecx + 101]
  425d80: mov dword ptr [ebp - 76], edx
  425d83: mov eax, dword ptr [ebp - 84]
  425d86: mov ecx, dword ptr [eax + 16]
  425d89: cmp dword ptr [ecx + 416], 0
  425d90: jne 0x425d9b <.text+0x24d9b>
  425d92: mov dword ptr [ebp - 88], 0
  425d99: jmp 0x425db1 <.text+0x24db1>
  425d9b: mov edx, dword ptr [ebp - 84]
  425d9e: mov eax, dword ptr [edx + 16]
  425da1: mov ecx, dword ptr [eax + 416]
  425da7: mov edx, dword ptr [ecx + 12]
  425daa: movsx eax, byte ptr [edx + 102]
  425dae: mov dword ptr [ebp - 88], eax
  425db1: mov ecx, dword ptr [ebp - 88]
  425db4: mov dword ptr [ebp - 4], ecx
  425db7: mov edx, dword ptr [ebp - 4]
  425dba: push edx
  425dbb: mov eax, dword ptr [ebp - 76]
  425dbe: push eax
  425dbf: mov ecx, dword ptr [ebp - 68]
  425dc2: push ecx
  425dc3: call 0x415cff <.text+0x14cff>
  425dc8: add esp, 12
  425dcb: mov dword ptr [ebp - 68], eax
  ...
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
  425e6c: add esp, 12
  ...
  422bc2: mov eax, dword ptr [ebp - 8]
  422bc5: movsx ecx, byte ptr [eax + 101]
  422bc9: test ecx, ecx
  422bcb: jne 0x422bee <.text+0x21bee>
  422bcd: call 0x476d82 <.text+0x75d82>
  422bd2: cdq
  422bd3: mov ecx, 5
  422bd8: idiv ecx
  422bda: mov dword ptr [ebp - 12], edx
  422bdd: cmp dword ptr [ebp - 12], 0
  422be1: jne 0x422be5 <.text+0x21be5>
  422be3: jmp 0x422bcd <.text+0x21bcd>
  422be5: mov edx, dword ptr [ebp - 8]
  422be8: mov al, byte ptr [ebp - 12]
  422beb: mov byte ptr [edx + 101], al
  422bee: mov ecx, dword ptr [ebp - 8]
  422bf1: movsx edx, byte ptr [ecx + 100]
  422bf5: test edx, edx
  422bf7: jne 0x422c1a <.text+0x21c1a>
  422bf9: call 0x476d82 <.text+0x75d82>
  422bfe: cdq
  422bff: mov ecx, 5
  422c04: idiv ecx
  422c06: mov dword ptr [ebp - 12], edx
  422c09: cmp dword ptr [ebp - 12], 0
  422c0d: jne 0x422c11 <.text+0x21c11>
  422c0f: jmp 0x422bf9 <.text+0x21bf9>
  422c11: mov edx, dword ptr [ebp - 8]
  422c14: mov al, byte ptr [ebp - 12]
  422c17: mov byte ptr [edx + 100], al
  ...
  415376: mov eax, dword ptr [ebp + 8]
  415379: mov ecx, dword ptr [4*eax + 4903424]
  415380: mov edx, dword ptr [ecx + 32]
  415383: mov dword ptr [ebp - 264], edx
  415389: mov eax, dword ptr [ebp - 264]
  41538f: mov ecx, dword ptr [eax + 16]
  ...
  4154a9: mov ax, word ptr [edx + 330]
  4154b0: test eax, eax
  4154b2: je 0x4154bb <.text+0x144bb>
  4154b4: mov dword ptr [ebp + 16], 0
  4154bb: mov ecx, dword ptr [ebp - 264]
  4154c1: mov edx, dword ptr [ecx + 16]
  4154c4: xor eax, eax
  4154c6: mov ax, word ptr [edx + 328]
  4154cd: test eax, eax
  4154cf: je 0x4154d8 <.text+0x144d8>
  4154d1: mov dword ptr [ebp + 16], 0
  4154d8: mov ecx, dword ptr [ebp - 264]
  4154de: mov edx, dword ptr [ecx + 16]
  4154e1: xor eax, eax
  4154e3: mov ax, word ptr [edx + 324]
  4154ea: test eax, eax
  4154ec: je 0x4154f5 <.text+0x144f5>
  4154ee: mov dword ptr [ebp + 16], 0
  4154f5: mov ecx, dword ptr [ebp - 264]
  4154fb: mov edx, dword ptr [ecx + 16]
  4154fe: xor eax, eax
  415500: mov ax, word ptr [edx + 308]
  415507: test eax, eax
  415509: je 0x415513 <.text+0x14513>
  41550b: mov ecx, dword ptr [ebp + 16]
  41550e: shr ecx
  415510: mov dword ptr [ebp + 16], ecx
  415513: mov edx, dword ptr [ebp - 264]
  415519: mov eax, dword ptr [edx + 16]
  41551c: xor ecx, ecx
  41551e: mov cl, byte ptr [eax + 118]
  415521: test ecx, ecx
  415523: je 0x41556d <.text+0x1456d>
  415525: mov edx, dword ptr [ebp + 16]
  415528: shl edx
  41552a: mov dword ptr [ebp + 16], edx
  41552d: mov eax, dword ptr [ebp - 264]
  415533: mov ecx, dword ptr [eax + 16]
  415536: mov byte ptr [ecx + 118], 0
  41553a: mov edx, dword ptr [ebp - 264]
  415540: mov eax, dword ptr [edx + 16]
  415543: mov word ptr [eax + 354], 0
  41554c: mov ecx, dword ptr [ebp - 264]
  415552: mov edx, dword ptr [ecx + 16]
  415555: mov al, byte ptr [edx + 118]
  415558: push eax
  415559: push 90
  41555b: mov ecx, dword ptr [ebp - 264]
  415561: mov edx, dword ptr [ecx + 4]
  415564: push edx
  415565: call 0x458eca <.text+0x57eca>
  41556a: add esp, 12
  41556d: mov eax, dword ptr [ebp - 264]
  415573: mov ecx, dword ptr [eax + 16]
  415576: mov edx, dword ptr [ebp + 16]
  415579: cmp edx, dword ptr [ecx + 204]
  41557f: jbe 0x4155a0 <.text+0x145a0>
  415581: mov eax, dword ptr [ebp - 264]
  415587: mov ecx, dword ptr [eax + 16]
  41558a: mov dword ptr [ecx + 204], 0
  415594: mov dword ptr [ebp - 260], 0
  41559e: jmp 0x41561b <.text+0x1461b>
  4155a0: mov edx, dword ptr [ebp - 264]
  4155a6: mov eax, dword ptr [edx + 16]
  4155a9: mov ecx, dword ptr [eax + 204]
  4155af: sub ecx, dword ptr [ebp + 16]
  4155b2: mov edx, dword ptr [ebp - 264]
  4155b8: mov eax, dword ptr [edx + 16]
  4155bb: mov dword ptr [eax + 204], ecx
  4155c1: mov ecx, dword ptr [ebp - 264]
  4155c7: mov edx, dword ptr [ecx + 16]
  4155ca: mov eax, dword ptr [edx + 204]
  4155d0: mov dword ptr [ebp - 280], eax
  4155d6: mov dword ptr [ebp - 276], 0
  4155e0: fild qword ptr [ebp - 280]
  4155e6: mov ecx, dword ptr [ebp - 264]
  4155ec: mov edx, dword ptr [ecx + 144]
  4155f2: mov dword ptr [ebp - 288], edx
  4155f8: mov dword ptr [ebp - 284], 0
  415602: fild qword ptr [ebp - 288]
  415608: fdivp st(1), st
  41560a: fmul qword ptr [4723368]
  415610: call 0x4783d4 <.text+0x773d4>
  415615: mov dword ptr [ebp - 260], eax
  ...
  415653: mov eax, dword ptr [ebp - 264]
  415659: mov ecx, dword ptr [eax + 16]
  41565c: cmp dword ptr [ecx + 204], 0
  415663: jne 0x415695 <.text+0x14695>
  415665: cmp dword ptr [5168208], 0
  41566c: je 0x415689 <.text+0x14689>
  41566e: push 0
  415670: mov edx, dword ptr [ebp + 8]
  415673: push edx
  415674: push 0
  415676: mov eax, dword ptr [5168208]
  41567b: mov ecx, dword ptr [eax + 20]
  41567e: push ecx
  41567f: call 0x43e65f <.text+0x3d65f>
  415684: add esp, 16
  415687: jmp 0x415695 <.text+0x14695>
  415689: mov edx, dword ptr [ebp + 8]
  41568c: push edx
  41568d: call 0x415202 <.text+0x14202>
  415692: add esp, 4
  ...
  415202: push ebp
  415203: mov ebp, esp
  415205: sub esp, 280
  41520b: push edi
  41520c: lea edi, [ebp - 280]
  415212: mov ecx, 70
  41521e: mov byte ptr [ebp - 256], 0
  415225: mov ecx, 63
  41522a: xor eax, eax
  41522c: lea edi, [ebp - 255]
  415234: stosw word ptr es:[edi], ax
  415236: stosb byte ptr es:[edi], al
  415237: mov eax, dword ptr [ebp + 8]
  41523a: mov ecx, dword ptr [4*eax + 4903424]
  415241: mov edx, dword ptr [ecx + 32]
  415244: mov dword ptr [ebp - 264], edx
  41524a: mov eax, dword ptr [ebp - 264]
  415250: mov ecx, dword ptr [eax + 16]
  415253: mov edx, dword ptr [ecx + 212]
  415259: mov dword ptr [ebp - 280], edx
  41525f: mov dword ptr [ebp - 276], 0
  415269: fild qword ptr [ebp - 280]
  41526f: fdiv qword ptr [4723368]
  415275: call 0x4783d4 <.text+0x773d4>
  ...
  416961: mov eax, dword ptr [ebp - 28]
  416964: mov esi, dword ptr [eax + 16]
  416967: call 0x476d82 <.text+0x75d82>
  41696c: cdq
  41696d: mov ecx, 10
  416972: idiv ecx
  416974: mov esi, dword ptr [esi + 140]
  41697a: add esi, edx
  41697c: call 0x476d82 <.text+0x75d82>
  416981: cdq
  416982: mov ecx, 10
  416987: idiv ecx
  416989: sub esi, edx
  41698b: mov dword ptr [ebp - 32], esi
  41698e: call 0x476d82 <.text+0x75d82>
  416993: cdq
  416994: mov ecx, 100
  416999: idiv ecx
  41699b: mov eax, dword ptr [ebp - 28]
  41699e: mov ecx, dword ptr [eax + 16]
  4169a1: cmp edx, dword ptr [ecx + 156]
  4169a7: ja 0x4169b1 <.text+0x159b1>
  4169a9: mov edx, dword ptr [ebp - 32]
  4169ac: shl edx
  4169ae: mov dword ptr [ebp - 32], edx
  4169b1: mov eax, dword ptr [ebp - 28]
  4169b4: mov ecx, dword ptr [eax + 16]
  4169b7: xor edx, edx
  4169b9: mov dl, byte ptr [ecx + 116]
  4169bc: test edx, edx
  4169be: je 0x4169d4 <.text+0x159d4>
  4169c0: mov eax, dword ptr [ebp - 28]
  4169c3: mov ecx, dword ptr [eax + 144]
  4169c9: shr ecx, 3
  4169cc: mov edx, dword ptr [ebp - 32]
  4169cf: add edx, ecx
  4169d1: mov dword ptr [ebp - 32], edx
  4169d4: mov eax, dword ptr [ebp - 32]
  4169d7: push eax
  4169d8: push 255
  4169dd: mov ecx, dword ptr [ebp - 24]
  4169e0: mov edx, dword ptr [ecx + 4]
  4169e3: push edx
  4169e4: call 0x415341 <.text+0x14341>
  4169e9: add esp, 12
  ...
  44e605: mov eax, dword ptr [ebp - 24]
  44e608: mov ecx, dword ptr [eax + 16]
  44e60b: xor edx, edx
  44e60d: mov dx, word ptr [ecx + 350]
  44e614: cmp edx, 1
  44e617: jne 0x44e624 <.text+0x4d624>
  44e619: mov eax, dword ptr [ebp - 12]
  44e61c: imul eax, eax, 3
  44e61f: shr eax
  44e621: mov dword ptr [ebp - 12], eax
  44e624: mov ecx, dword ptr [ebp - 24]
  44e627: mov edx, dword ptr [ecx + 16]
  44e62a: xor eax, eax
  44e62c: mov ax, word ptr [edx + 352]
  44e633: test eax, eax
  44e635: je 0x44e64f <.text+0x4d64f>
  44e637: mov ecx, dword ptr [ebp - 24]
  44e63a: mov edx, dword ptr [ecx + 16]
  44e63d: xor eax, eax
  44e63f: mov ax, word ptr [edx + 352]
  44e646: mov ecx, dword ptr [ebp - 12]
  44e649: imul ecx, eax
  44e64c: mov dword ptr [ebp - 12], ecx
  44e64f: call 0x476d82 <.text+0x75d82>
  44e654: cdq
  44e655: mov ecx, 100
  44e65a: idiv ecx
  44e65c: mov eax, dword ptr [ebp - 24]
  44e65f: mov ecx, dword ptr [eax + 16]
  44e662: cmp edx, dword ptr [ecx + 156]
  44e668: ja 0x44e672 <.text+0x4d672>
  44e66a: mov edx, dword ptr [ebp - 12]
  44e66d: shl edx
  44e66f: mov dword ptr [ebp - 12], edx
  44e672: mov eax, dword ptr [ebp - 24]
  44e675: mov ecx, dword ptr [eax + 16]
  44e678: xor edx, edx
  44e67a: mov dx, word ptr [ecx + 346]
  44e681: test edx, edx
  44e683: je 0x44e6aa <.text+0x4d6aa>
  44e685: mov eax, dword ptr [ebp - 24]
  44e688: mov ecx, dword ptr [eax + 16]
  44e68b: xor edx, edx
  44e68d: mov dx, word ptr [ecx + 346]
  44e694: test edx, edx
  44e696: je 0x44e6d8 <.text+0x4d6d8>
  44e698: call 0x476d82 <.text+0x75d82>
  44e69d: cdq
  44e69e: mov ecx, 100
  44e6a3: idiv ecx
  44e6a5: cmp edx, 80
  44e6a8: jge 0x44e6d8 <.text+0x4d6d8>
  44e6aa: push 1
  44e6ac: mov edx, dword ptr [ebp - 4]
  44e6af: mov eax, dword ptr [edx + 16]
  44e6b2: mov cl, byte ptr [eax + 200]
  44e6b8: push ecx
  44e6b9: mov edx, dword ptr [ebp - 12]
  44e6bc: push edx
  44e6bd: mov eax, dword ptr [ebp - 12]
  44e6c0: push eax
  44e6c1: mov ecx, dword ptr [ebp - 24]
  44e6c4: xor edx, edx
  44e6c6: mov dl, byte ptr [ecx + 160]
  44e6cc: push edx
  44e6cd: call 0x415173 <.text+0x14173>
  44e6d2: add esp, 20
  44e6d5: mov dword ptr [ebp - 12], eax
  44e6d8: mov eax, dword ptr [ebp - 4]
  44e6db: mov ecx, dword ptr [eax + 16]
  44e6de: xor edx, edx
  44e6e0: mov dl, byte ptr [ecx + 113]
  44e6e3: test edx, edx
  44e6e5: je 0x44e70e <.text+0x4d70e>
  44e6e7: cmp dword ptr [ebp - 16], 10
  44e6eb: jle 0x44e70e <.text+0x4d70e>
  44e6ed: cmp dword ptr [ebp - 16], 90
  44e6f1: jle 0x44e70a <.text+0x4d70a>
  44e6f3: mov eax, dword ptr [ebp - 12]
  44e6f6: push eax
  44e6f7: mov ecx, dword ptr [ebp - 12]
  44e6fa: push ecx
  44e6fb: mov edx, dword ptr [ebp - 24]
  44e6fe: mov eax, dword ptr [edx + 4]
  44e701: push eax
  44e702: call 0x4156a7 <.text+0x146a7>
  44e707: add esp, 12
  44e70a: xor eax, eax
  44e70c: jmp 0x44e727 <.text+0x4d727>
  44e70e: mov ecx, dword ptr [ebp - 12]
  44e711: push ecx
  44e712: mov edx, dword ptr [ebp - 12]
  44e715: push edx
  44e716: mov eax, dword ptr [ebp - 4]
  44e719: mov ecx, dword ptr [eax + 4]
  44e71c: push ecx
  44e71d: call 0x415341 <.text+0x14341>
  44e722: add esp, 12
  44e725: xor eax, eax
