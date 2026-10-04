; 자연 회복(21초)·포만도(120초) — Novaonline.exe
; 증명(체력 쪽만 발췌, 마력은 0x46d3a2~0x46d4de 가 같은 식을 +0xA2·+0x94·+0xD0 으로):
;  P = 접속자 객체([세션+0x20]), C = [P+0x10] 캐릭터 칸.
;  건너뜀: 귀신(C+0xF0==1) · 포만도 C+0xDC==0.
;  v = (P+0x90 최대체력 / 100) × (C+0xA3 콘 / 4.3) → 최소 최대체력의 15%, 최대 25%(콘 ≥108 이면 바로 25%)
;  휴식(C+0x62, set_rest) 이면 v = (v>>1)×3 (×1.5). 체력 C+0xCC += v + P+0x98(장비 재생력 합, item+0x5C "재생력"). 최대에서 자른다.
;  상수: [0x4812a8]=100.0 · [0x4812e0]=4.3
; 포만도 0x46d8e1(120초): C+0xDC 를 1 줄이고 50·25 가 되는 순간 경고. 0 이면 체력 −(C+0xB0 기본체력/100)×5 (체력이 그보다 클 때만).
;
; --- 0x46d1a5 회복
  46d1fa: mov edx, dword ptr [ebp - 8]
; --- 0x46d1a5 회복(체력)
  46d22d: mov edx, dword ptr [ebp - 12]
  46d230: mov eax, dword ptr [edx + 16]
  46d233: xor ecx, ecx
  46d235: mov cl, byte ptr [eax + 240]
  46d23b: cmp ecx, 1
  46d23e: je 0x46d548 <.text+0x6c548>
  46d244: mov edx, dword ptr [ebp - 12]
  46d247: mov eax, dword ptr [edx + 16]
  46d24a: movsx ecx, byte ptr [eax + 220]
  46d251: test ecx, ecx
  46d253: jne 0x46d25a <.text+0x6c25a>
  46d255: jmp 0x46d1c4 <.text+0x6c1c4>
  46d25a: mov edx, dword ptr [ebp - 12]
  46d25d: mov eax, dword ptr [edx + 16]
  46d260: xor ecx, ecx
  46d262: mov cl, byte ptr [eax + 163]
  46d268: test ecx, ecx
  46d26a: je 0x46d2ac <.text+0x6c2ac>
  46d26c: mov edx, dword ptr [ebp - 12]
  46d26f: mov eax, dword ptr [edx + 144]
  46d275: mov dword ptr [ebp - 24], eax
  46d278: mov dword ptr [ebp - 20], 0
  46d27f: fild qword ptr [ebp - 24]
  46d282: fdiv qword ptr [4723368]
  46d288: mov ecx, dword ptr [ebp - 12]
  46d28b: mov edx, dword ptr [ecx + 16]
  46d28e: xor eax, eax
  46d290: mov al, byte ptr [edx + 163]
  46d296: mov dword ptr [ebp - 28], eax
  46d299: fild dword ptr [ebp - 28]
  46d29c: fdiv qword ptr [4723424]
  46d2a2: fmulp st(1), st
  46d2a4: call 0x4783d4 <.text+0x773d4>
  46d2a9: mov dword ptr [ebp - 4], eax
  46d2ac: mov ecx, dword ptr [ebp - 12]
  46d2af: mov edx, dword ptr [ecx + 144]
  46d2b5: mov dword ptr [ebp - 36], edx
  46d2b8: mov dword ptr [ebp - 32], 0
  46d2bf: fild qword ptr [ebp - 36]
  46d2c2: fdiv qword ptr [4723368]
  46d2c8: call 0x4783d4 <.text+0x773d4>
  46d2cd: imul eax, eax, 15
  46d2d0: cmp dword ptr [ebp - 4], eax
  46d2d3: jae 0x46d2fc <.text+0x6c2fc>
  ...
  46d31d: imul eax, eax, 25
  46d320: cmp dword ptr [ebp - 4], eax
  46d323: ja 0x46d338 <.text+0x6c338>
  46d325: mov ecx, dword ptr [ebp - 12]
  46d328: mov edx, dword ptr [ecx + 16]
  46d32b: xor eax, eax
  46d32d: mov al, byte ptr [edx + 163]
  46d333: cmp eax, 108
  46d336: jl 0x46d35f <.text+0x6c35f>
  ...
  46d35f: mov eax, dword ptr [ebp - 12]
  46d362: mov ecx, dword ptr [eax + 16]
  46d365: xor edx, edx
  46d367: mov dl, byte ptr [ecx + 98]
  46d36a: test edx, edx
  46d36c: je 0x46d379 <.text+0x6c379>
  46d36e: mov eax, dword ptr [ebp - 4]
  46d371: shr eax
  46d373: imul eax, eax, 3
  46d376: mov dword ptr [ebp - 4], eax
  46d379: mov ecx, dword ptr [ebp - 12]
  46d37c: mov edx, dword ptr [ecx + 16]
  46d37f: mov eax, dword ptr [ebp - 12]
  46d382: mov ecx, dword ptr [ebp - 4]
  46d385: add ecx, dword ptr [eax + 152]
  46d38b: mov edx, dword ptr [edx + 204]
  46d391: add edx, ecx
  46d393: mov eax, dword ptr [ebp - 12]
  46d396: mov ecx, dword ptr [eax + 16]
  46d399: mov dword ptr [ecx + 204], edx
; --- 0x46d8e1 포만도
  46d986: movsx ecx, byte ptr [eax + 220]
  46d98d: test ecx, ecx
  46d98f: je 0x46da0e <.text+0x6ca0e>
  46d991: mov edx, dword ptr [ebp - 12]
  46d994: mov eax, dword ptr [edx + 16]
  46d997: movsx ecx, byte ptr [eax + 220]
  46d99e: sub ecx, 1
  46d9a1: mov edx, dword ptr [ebp - 12]
  46d9a4: mov eax, dword ptr [edx + 16]
  46d9a7: mov byte ptr [eax + 220], cl
  46d9ad: mov ecx, dword ptr [ebp - 12]
  46d9b0: push ecx
  46d9b1: call 0x45df11 <.text+0x5cf11>
  46d9b9: mov edx, dword ptr [ebp - 12]
  46d9bc: mov eax, dword ptr [edx + 16]
  46d9bf: movsx ecx, byte ptr [eax + 220]
  46d9c6: cmp ecx, 50
  46d9c9: jne 0x46d9e1 <.text+0x6c9e1>
  46d9cb: push 4789848   ; "허기에 시달려 물리, 마법공격력이 줄어듭니다. 1단계"
  46d9d0: push 3
  46d9d2: mov edx, dword ptr [ebp - 12]
  46d9d5: mov eax, dword ptr [edx + 4]
  46d9d8: push eax
  46d9d9: call 0x45cecb <.text+0x5becb>
  ...
  46da0e: mov eax, dword ptr [ebp - 12]
  46da11: mov ecx, dword ptr [eax + 16]
  46da14: mov edx, dword ptr [ebp - 12]
  46da17: mov eax, dword ptr [edx + 16]
  46da1a: mov eax, dword ptr [eax + 176]
  46da20: xor edx, edx
  46da22: mov esi, 100
  46da27: div esi
  46da29: imul eax, eax, 5
  46da2c: cmp dword ptr [ecx + 204], eax
  46da32: jbe 0x46da7e <.text+0x6ca7e>
  46da34: mov ecx, dword ptr [ebp - 12]
  46da37: mov ecx, dword ptr [ecx + 16]
  46da3a: mov edx, dword ptr [ebp - 12]
  46da3d: mov eax, dword ptr [edx + 16]
  46da40: mov eax, dword ptr [eax + 176]
  46da46: xor edx, edx
  46da48: mov esi, 100
  46da4d: div esi
  46da4f: imul eax, eax, 5
  46da52: mov ecx, dword ptr [ecx + 204]
  46da58: sub ecx, eax
  46da5a: mov edx, dword ptr [ebp - 12]
  46da5d: mov eax, dword ptr [edx + 16]
  46da60: mov dword ptr [eax + 204], ecx
  46da66: push 4789952   ; "허기에 시달려 체력이 줄어듭니다."
