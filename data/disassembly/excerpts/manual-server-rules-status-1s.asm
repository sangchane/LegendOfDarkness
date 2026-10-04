; 상태이상 1초 처리 — Novaonline.exe 0x46daa2 (1000ms 타이머)
; 증명: 접속자마다 C=[P+0x10] 의 남은-초 칸을 1씩 줄이며, 0x458eca(세션, 아이콘, 남은초)=0x3A 상태 아이콘, 0x46b66a(…, 이펙트, …, 속도)=0x29 이펙트를 보낸다.
;  중독 C+0x77(poisen): 이펙트 42 · 아이콘 35 · 매초 체력 −C+0x128(poisen 셋째 인자). "중독 끝."
;   비교는 체력 > 체력×2/100 (같은 칸끼리 — 사실상 체력>0 이면 늘 참). 빼기 뒤 0 아래 검사 없음(unsigned).
;  나르콜리(잠) C+0x76(mobnar_delay): 이펙트 32 · 아이콘 90 · 끝에 C+0x162=0 "나르콜리 끝."
;  동면(얼음) C+0x78(mobsor_delay): 이펙트 40 · 아이콘 50 · 끝에 C+0x164=0 "동면 끝."
;  물약 C+0x88: 이펙트 22 · 아이콘 145 · C+0x86==1 이면 체력, 아니면 마력 += C+0x87 (potion_use)
; 막는 곳(마법 0x459873): 귀신 C+0xF0==1 "귀신은 할 수 없습니다." · 혼수 C+0x15C "죽음의 그림자가 드리웁니다."
;  · 잠 C+0x76 "잠이 쏟아져 옵니다." · 얼음 C+0x78 "몸이 얼어 움직일수 없습니다." — 마법 표 +26(word)≠0 이면 잠·얼음에도 된다.
;  걷기 0x466b4c · 평타 0x4160f7 · 아이템 0x41e23f · 장착 0x41c96e 도 같은 칸을 읽는다.
;
; --- 물약(C+0x88) 1초
  46dd97: mov eax, dword ptr [ebp - 284]
  46dd9d: mov ecx, dword ptr [eax + 16]
; --- 물약(C+0x88) 1초  (아래 [ebp-284]=P, 그 +16 = C 를 다시 읽는 줄은 뺐다)
  46ddb6: mov ecx, dword ptr [eax + 16]
  46ddb9: mov dl, byte ptr [ecx + 136]
  46ddbf: sub dl, 1
  46ddc8: mov ecx, dword ptr [eax + 16]
  46ddcb: mov byte ptr [ecx + 136], dl
  46ddd1: push 75
  46ddd3: push 0
  46ddd5: push 22
  46ddd7: push 0
  46dddf: mov eax, dword ptr [edx + 16]
  46dde2: add eax, 88
  46dde5: push eax
  46dde6: mov ecx, dword ptr [ebp - 288]
  46ddec: mov edx, dword ptr [4*ecx + 4903140]
  46ddf3: push edx
  46ddf4: call 0x46b66a <.text+0x6a66a>
  46de02: mov ecx, dword ptr [eax + 16]
  46de05: mov dl, byte ptr [ecx + 136]
  46de0b: push edx
  46de0c: push 145
  46de11: mov eax, dword ptr [ebp - 288]
  46de17: mov ecx, dword ptr [4*eax + 4903140]
  46de1e: push ecx
  46de1f: call 0x458eca <.text+0x57eca>
  46de2d: mov eax, dword ptr [edx + 16]
  46de30: xor ecx, ecx
  46de32: mov cl, byte ptr [eax + 134]
  46de38: cmp ecx, 1
  46de3b: jne 0x46de70 <.text+0x6ce70>
  46de43: mov eax, dword ptr [edx + 16]
  46de4c: mov edx, dword ptr [ecx + 16]
  46de4f: xor ecx, ecx
  46de51: mov cl, byte ptr [edx + 135]
  46de57: mov edx, dword ptr [eax + 204]
  46de5d: add edx, ecx
  46de65: mov ecx, dword ptr [eax + 16]
  46de68: mov dword ptr [ecx + 204], edx
  46de6e: jmp 0x46dea1 <.text+0x6cea1>
  46de76: mov eax, dword ptr [edx + 16]
  46de7f: mov edx, dword ptr [ecx + 16]
  46de82: xor ecx, ecx
  46de84: mov cl, byte ptr [edx + 135]
  46de8a: mov edx, dword ptr [eax + 208]
  46de90: add edx, ecx
  46de98: mov ecx, dword ptr [eax + 16]
  46de9b: mov dword ptr [ecx + 208], edx
; --- 중독(C+0x77) 매초 피해
  46e9ea: mov edx, dword ptr [ecx + 16]
  46e9ed: mov al, byte ptr [edx + 119]
  46e9f0: push eax
  46e9f1: push 35
  46e9f3: mov ecx, dword ptr [ebp - 288]
  46e9f9: mov edx, dword ptr [4*ecx + 4903140]
  46ea00: push edx
  46ea01: call 0x458eca <.text+0x57eca>
  46ea0f: mov ecx, dword ptr [eax + 16]
  46ea18: mov eax, dword ptr [edx + 16]
  46ea1b: mov eax, dword ptr [eax + 204]
  46ea21: xor edx, edx
  46ea23: mov esi, 100
  46ea28: div esi
  46ea2a: shl eax
  46ea2c: cmp dword ptr [ecx + 204], eax
  46ea32: jbe 0x46ea61 <.text+0x6da61>
  46ea3a: mov edx, dword ptr [ecx + 16]
  46ea43: mov ecx, dword ptr [eax + 16]
  46ea46: mov edx, dword ptr [edx + 204]
  46ea4c: sub edx, dword ptr [ecx + 296]
  46ea58: mov ecx, dword ptr [eax + 16]
  46ea5b: mov dword ptr [ecx + 204], edx
  46ea67: push edx
  46ea68: call 0x45df11 <.text+0x5cf11>
  46ea76: mov ecx, dword ptr [eax + 16]
  46ea79: xor edx, edx
  46ea7b: mov dl, byte ptr [ecx + 119]
  46ea7e: test edx, edx
  46ea80: jne 0x46ea9f <.text+0x6da9f>
  46ea82: push 4790108   ; "중독 끝."
  46ea87: push 3
  46ea89: mov eax, dword ptr [ebp - 288]
  46ea8f: mov ecx, dword ptr [4*eax + 4903140]
  46ea96: push ecx
  46ea97: call 0x45cecb <.text+0x5becb>
; --- 0x459873 마법 막기: 잠·얼음
  459ac4: mov edx, dword ptr [ebp - 32]
  459ac7: mov eax, dword ptr [edx + 16]
  459aca: xor ecx, ecx
  459acc: mov cl, byte ptr [eax + 118]
  459acf: test ecx, ecx
  459ad1: je 0x459afb <.text+0x58afb>
  459ad3: mov edx, dword ptr [ebp - 8]
  459ad6: xor eax, eax
  459ad8: mov ax, word ptr [edx + 26]
  459adc: test eax, eax
  459ade: jne 0x459afb <.text+0x58afb>
  459ae0: push 4783348   ; "잠이 쏟아져 옵니다."
  459ae5: push 3
  459ae7: mov ecx, dword ptr [ebp - 32]
  459aea: mov edx, dword ptr [ecx + 4]
  459aed: push edx
  459aee: call 0x45cecb <.text+0x5becb>
  459af6: jmp 0x459e67 <.text+0x58e67>
  459afb: mov eax, dword ptr [ebp - 32]
  459afe: mov ecx, dword ptr [eax + 16]
  459b01: xor edx, edx
  459b03: mov dl, byte ptr [ecx + 120]
  459b06: test edx, edx
  459b08: je 0x459b32 <.text+0x58b32>
  459b0a: mov eax, dword ptr [ebp - 8]
  459b0d: xor ecx, ecx
  459b0f: mov cx, word ptr [eax + 26]
  459b13: test ecx, ecx
  459b15: jne 0x459b32 <.text+0x58b32>
  459b17: push 4783368   ; "몸이 얼어 움직일수 없습니다."
  459b1c: push 3
