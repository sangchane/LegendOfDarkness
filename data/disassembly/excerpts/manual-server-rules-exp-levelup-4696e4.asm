; Novaonline.exe (5.99 서버) — 경험치 주기·레벨업 0x4696e4  (오프셋은 10진수: +188=0xBC, +106=0x6A …)
; P = [4*id+0x4AD200]+0x20 (접속자), C = [P+0x10] (캐릭터)
; exp 배수 순서: 서버배율[0x4AD0C4](>=2 일 때) → exp_per(C+0x150 ==1|3 이면 ×C+0x14E) → 귀걸이(C+0x188) 별의귀걸이 ×6/5 · 퀸셰어의귀걸이 ×3/2
;   → 상태 C+0xF0 가 0·2·6 아니면 안 줌 → 맵 [C+0x58]+0x2A ×2 · 맵+0x2C 면 승급(C+0x7E)0 ÷4, 아니면 ÷2 → |rand()%100| 30~32 면 ×2 + 이펙트 341
; 레벨<99: C+0xBC(보유경험치) += exp, ≥ 표[직업 C+0x7D][레벨+1] 이면 레벨+1, 포인트 C+0x6C += 표.byte4(=2),
;   기본최대체력 C+0xB0 += 콘(C+0xA3)+30, 기본최대마력 C+0xB4 += 위즈(C+0xA2)+25, 현재체력·마력 = 최대(P+0x90/P+0x94), 이펙트 79, __LEVEL_SCRIPT__
; 레벨≥99: 넘침(32비트) 아니면 C+0xBC += exp+extra, 넘치면 "더 이상 경험치가 오르지 않습니다."
4696e4: push ebp
; 3% 두 배
469909: cmp dword ptr [ebp - 12], 30
46990d: jl 0x46993e <.text+0x6893e>
46990f: cmp dword ptr [ebp - 12], 32
469913: jg 0x46993e <.text+0x6893e>
469915: mov eax, dword ptr [ebp + 12]
469918: shl eax
46991a: mov dword ptr [ebp + 12], eax
46991d: push 100
46991f: push 0
469921: push 341
469926: push 0
469928: mov ecx, dword ptr [ebp - 8]
46992b: mov edx, dword ptr [ecx + 16]
46992e: add edx, 88
469931: push edx
469932: mov eax, dword ptr [ebp + 8]
469935: push eax
469936: call 0x46b66a <.text+0x6a66a>
; 레벨<99 → 더하고 레벨업 판정
46993e: mov ecx, dword ptr [ebp - 8]
469941: mov edx, dword ptr [ecx + 16]
469944: xor eax, eax
469946: mov al, byte ptr [edx + 106]
469949: cmp eax, 99
46994c: jge 0x469b4a <.text+0x68b4a>
469952: mov ecx, dword ptr [ebp - 8]
469955: mov edx, dword ptr [ecx + 16]
469958: mov eax, dword ptr [edx + 188]
46995e: add eax, dword ptr [ebp + 12]
469961: mov ecx, dword ptr [ebp - 8]
469964: mov edx, dword ptr [ecx + 16]
469967: mov dword ptr [edx + 188], eax
46996d: mov eax, dword ptr [ebp - 8]
469970: mov ecx, dword ptr [eax + 16]
469973: xor edx, edx
469975: mov dl, byte ptr [ecx + 125]
469978: imul edx, edx, 800
46997e: mov eax, dword ptr [ebp - 8]
469981: mov ecx, dword ptr [eax + 16]
469984: xor eax, eax
469986: mov al, byte ptr [ecx + 106]
469989: lea ecx, [edx + 8*eax + 5168232]
469990: mov dword ptr [ebp - 16], ecx
469993: mov edx, dword ptr [ebp + 12]
469996: push edx
469997: push 4787860   ; "경험치가 %lu 올랐습니다"
46999c: push 4837472
4699a1: call 0x4779c0 <.text+0x769c0>
4699a9: push 4837472
4699ae: push 3
4699b0: mov eax, dword ptr [ebp + 8]
4699b3: push eax
4699b4: call 0x45cecb <.text+0x5becb>
4699bc: mov ecx, dword ptr [ebp - 8]
4699bf: mov edx, dword ptr [ecx + 16]
4699c2: mov eax, dword ptr [ebp - 16]
4699c5: mov ecx, dword ptr [edx + 188]
4699cb: cmp ecx, dword ptr [eax]
4699cd: jb 0x469b45 <.text+0x68b45>
4699d3: mov edx, dword ptr [ebp - 8]
4699d6: mov eax, dword ptr [edx + 16]
4699d9: mov ecx, dword ptr [eax + 188]
4699df: mov edx, dword ptr [ebp - 16]
4699e2: add ecx, dword ptr [edx]
4699e4: mov eax, dword ptr [ebp - 8]
4699e7: mov edx, dword ptr [eax + 16]
4699ea: mov dword ptr [edx + 188], ecx
4699f0: mov eax, dword ptr [ebp - 8]
4699f3: mov ecx, dword ptr [eax + 16]
4699f6: mov edx, dword ptr [ebp - 16]
4699f9: mov eax, dword ptr [ecx + 188]
4699ff: sub eax, dword ptr [edx]
469a01: mov ecx, dword ptr [ebp - 8]
469a04: mov edx, dword ptr [ecx + 16]
469a07: mov dword ptr [edx + 188], eax
469a0d: mov eax, dword ptr [ebp - 8]
469a10: mov ecx, dword ptr [eax + 16]
469a13: mov dl, byte ptr [ecx + 106]
469a16: add dl, 1
469a19: mov eax, dword ptr [ebp - 8]
469a1c: mov ecx, dword ptr [eax + 16]
469a1f: mov byte ptr [ecx + 106], dl
469a22: mov edx, dword ptr [ebp - 8]
469a25: mov eax, dword ptr [edx + 16]
469a28: mov ecx, dword ptr [ebp - 16]
469a2b: mov dl, byte ptr [eax + 108]
469a2e: add dl, byte ptr [ecx + 4]
469a31: mov eax, dword ptr [ebp - 8]
469a34: mov ecx, dword ptr [eax + 16]
469a37: mov byte ptr [ecx + 108], dl
469a3a: mov edx, dword ptr [ebp - 8]
; 체력·마력 증가
469a46: mov eax, dword ptr [ebp - 8]
469a49: mov ecx, dword ptr [eax + 16]
469a4c: mov edx, dword ptr [ebp - 8]
469a4f: mov eax, dword ptr [edx + 16]
469a52: xor edx, edx
469a54: mov dl, byte ptr [eax + 163]
469a5a: mov eax, dword ptr [ecx + 176]
469a60: lea ecx, [eax + edx + 30]
469a64: mov edx, dword ptr [ebp - 8]
469a67: mov eax, dword ptr [edx + 16]
469a6a: mov dword ptr [eax + 176], ecx
469a70: mov ecx, dword ptr [ebp - 8]
469a73: mov edx, dword ptr [ecx + 16]
469a76: mov eax, dword ptr [ebp - 8]
469a79: mov ecx, dword ptr [eax + 16]
469a7c: xor eax, eax
469a7e: mov al, byte ptr [ecx + 162]
469a84: mov ecx, dword ptr [edx + 180]
469a8a: lea edx, [ecx + eax + 25]
469a8e: mov eax, dword ptr [ebp - 8]
469a91: mov ecx, dword ptr [eax + 16]
469a94: mov dword ptr [ecx + 180], edx
469a9a: mov edx, dword ptr [ebp - 8]
469a9d: mov eax, dword ptr [edx + 16]
469aa0: mov ecx, dword ptr [ebp - 8]
469aa3: mov edx, dword ptr [ecx + 144]
469aa9: mov dword ptr [eax + 204], edx
469aaf: mov eax, dword ptr [ebp - 8]
469ab2: mov ecx, dword ptr [eax + 16]
469ab5: mov edx, dword ptr [ebp - 8]
469ab8: mov eax, dword ptr [edx + 148]
469abe: mov dword ptr [ecx + 208], eax
469ac4: push 4787884   ; "레벨이 올랐습니다!"
; --- 경험치 표 로더 0x402909 (experience.txt: 직업·레벨·포인트·필요경험치, 레벨<100), 표 0x4EDC60 = 직업×800 + 레벨×8 ---
402962: mov eax, dword ptr [ebp + 8]
402965: mov ecx, dword ptr [eax]
402967: push ecx
402968: call 0x477436 <.text+0x76436>
402970: mov esi, eax
402972: imul esi, esi, 800
402978: mov edx, dword ptr [ebp + 8]
40297b: mov eax, dword ptr [edx + 4]
40297e: push eax
40297f: call 0x477436 <.text+0x76436>
402987: lea ecx, [esi + 8*eax + 5168224]
40298e: mov dword ptr [ebp - 4], ecx
402991: mov edx, dword ptr [ebp + 8]
402994: mov eax, dword ptr [edx + 8]
402997: push eax
402998: call 0x477436 <.text+0x76436>
4029a0: mov ecx, dword ptr [ebp - 4]
4029a3: mov byte ptr [ecx + 4], al
4029a6: mov edx, dword ptr [ebp + 8]
4029a9: mov eax, dword ptr [edx + 12]
4029ac: push eax
4029ad: call 0x4773ab <.text+0x763ab>
4029b5: mov ecx, dword ptr [ebp - 4]
4029b8: mov dword ptr [ecx], eax
