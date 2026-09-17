; 사람 몸 동작의 칸 구간 (2005 = 5.99 클라이언트 Legend.exe) — 평타 1 · 손 들기 6 · 키스 21 · 손 흔들기 22
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe (md5 347dc381…, 원작 lodr.exe 안의 것과 같음)
; 도구: llvm-objdump -d --x86-asm-syntax=intel, 오프셋은 10진수
;
; 초기값: [+540] 평타 시작 0 · [+550] 칸 수 3 · [+544] 동작 6 시작 0 · [+554] 칸 수 2 · [+548] 서기 칸 5
;         [+650] 동작 21 시작 2 · [+652] 칸 수 3 · [+654] 동작 22 시작 6 · [+656] 칸 수 3  (eax=0, dx=3, bx=2, cx=6, di=5)
; 저장된 칸 수 = 그림 수 + 1. [+524] 는 걸음(0 이면 01 파일 서기 칸 [+548]×앞뒤).
; 칸 = 시작 + (칸 수 − 1) × 앞뒤(al: 0 등 · 1 앞) + 걸음 − 1. [ebp] 에 파일 종류를 적는다(2 = 03 파일).
; 동작 22 는 0x4e1020 이 속도를 3 으로 나눈다.

  4e364f: xor eax, eax
  4e3651: mov byte ptr [ebp + 648], al
  4e3657: mov edx, 24
  4e365c: mov word ptr [ebp + 536], dx
  4e3663: mov word ptr [ebp + 538], ax
  4e366a: mov edi, 5
  4e366f: mov word ptr [ebp + 548], di
  4e3676: mov word ptr [ebp + 540], ax
  4e367d: mov edx, 3
  4e3682: mov word ptr [ebp + 550], dx
  4e3689: mov esi, 4
  4e368e: mov word ptr [ebp + 542], si
  4e3695: mov word ptr [ebp + 552], dx
  4e369c: mov word ptr [ebp + 544], ax
  4e36a3: mov ebx, 2
  4e36a8: mov word ptr [ebp + 554], bx
  4e36af: mov word ptr [ebp + 650], bx
  4e36b6: mov word ptr [ebp + 652], dx
  4e36bd: mov ecx, 6
  4e36c2: mov word ptr [ebp + 654], cx
  4e36c9: mov word ptr [ebp + 656], dx
  4e36d0: mov dword ptr [ebp + 508], eax
  ...
; 평타(상태 3)
  4e2383: movzx ebp, word ptr [ebx + 540]
  4e238a: movsx edi, word ptr [ebx + 550]
  4e2391: add edi, -1
  4e2394: movsx edx, al
  4e2397: imul edi, edx
  4e239a: add ebp, edi
  4e239c: movsx esi, byte ptr [ebx + 524]
  4e23a3: lea ebx, [ebp + esi - 1]
  ...
; 동작 6(상태 4)
  4e23e8: movzx ebp, word ptr [ebx + 544]
  4e23ef: movsx edi, word ptr [ebx + 554]
  4e23f6: add edi, -1
  4e23f9: movsx edx, al
  4e23fc: imul edi, edx
  4e23ff: add ebp, edi
  4e2401: movsx esi, byte ptr [ebx + 524]
  4e2408: lea ebx, [ebp + esi - 1]
  ...
; 동작 21(상태 7)
  4e24c5: movzx ebp, word ptr [ebx + 650]
  4e24cc: movsx edi, word ptr [ebx + 652]
  4e24d3: add edi, -1
  4e24d6: movsx edx, al
  4e24d9: imul edi, edx
  4e24dc: add ebp, edi
  4e24de: movsx esi, byte ptr [ebx + 524]
  4e24e5: lea ebx, [ebp + esi - 1]
  ...
; 동작 22(상태 8) — 파일 종류 2(03)
  4e2526: mov byte ptr [ebp], 2
  4e252a: movzx ebp, word ptr [ebx + 654]
  4e2531: movsx edi, word ptr [ebx + 656]
  4e2538: add edi, -1
  4e253b: movsx edx, al
  4e253e: imul edi, edx
  4e2541: add ebp, edi
  4e2543: movsx esi, byte ptr [ebx + 524]
  4e254a: lea ebx, [ebp + esi - 1]
  ...
; 0x4e78f4: 겹치기에서 파일 종류가 2 면 0x4e84e0 으로 — 부위 0(S 뒤 방패)·9(W)·12(P)·10(S 앞 방패)는 건너뛴다
  4e78ec: movsx eax, byte ptr [esp + 200]
  4e78f4: cmp eax, 2
  4e78f7: je 0x4e84e0 <.text+0xc14e0>
  4e84e0: mov eax, dword ptr [esp + 108]
  4e84e4: movsx esi, byte ptr [eax]
  4e84e7: test esi, esi
  4e84e9: je 0x4e7b09 <.text+0xc0b09>
  4e84ef: cmp esi, 9
  4e84f2: je 0x4e7b09 <.text+0xc0b09>
  4e84f8: cmp esi, 12
  4e84fb: je 0x4e7b09 <.text+0xc0b09>
  4e8501: cmp esi, 10
  4e8504: je 0x4e7b09 <.text+0xc0b09>
  4e850a: jmp 0x4e7904 <.text+0xc0904>
