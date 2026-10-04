; 체력·마력·방어력·능력치 칸 — 재계산 0x45d4be 앞부분 · get_ac/set_ac · 괴물 생성 때 칸 복사 0x422a73
; 출처: ~/Downloads/5.99 서버팩/Novaonline.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
; P = [0x4ad200+4*id]+0x20 (사람 객체, 계산값), C = [P+0x10] (캐릭터 칸, 저장값). 재계산 인자 = P
;   P 바이트+0x8C AC 합   = C word+0xC8(기본 AC, set_ac) + Σ장비 item+0x43(방어력)      get_ac 는 P+0x8C 를 읽는다
;   P 바이트+0x9F         = Σ item+0x62(명중수정) — 상태 패킷(0x45f346)에만 실린다
;   P 바이트+0xA0         = Σ item+0x63(공격수정) — 피해에 그대로 더한다(0x415173 첫 인자)
;   P +0x90 최대체력      = C+0xB0(기본체력) + Σ(item+0x48 체력변화 + P+0x90/100 × item+0x60 체력변화%)  get_basevita
;   P +0x94 최대마력      = C+0xB4 + Σ(item+0x54 + P+0x94/100 × item+0x61)                             get_basemana
;   P +0x98               = Σ item+0x5C(재생력)
;   P 바이트+0xA3~0xA7 힘·인트·위즈·콘·덱스 = C+0xA0~0xA4 + item+0x68·0x6A·0x6C·0x72·0x70
;   C +0xCC 체력 · C +0xD0 마력 (get_vita/get_mana, set_vita/set_mana)
; 괴물 하나 M (생성 0x422a73): M+0x00 = T+0x0D 방어력 · M+0x08·+0x10 = T+0x18 체력(최대·현재) · M+0x14 = T+0x1C 마력
;   M+0x65 = T+0x8D 공격속성 · M+0x64 = T+0x8E 방어속성, 0 이면 각각 rand%5 를 0 이 아닐 때까지 → 1~4
  ...
  45d4d6: mov eax, dword ptr [ebp + 8]
  45d4d9: mov ecx, dword ptr [eax + 4]
  45d4dc: mov dword ptr [ebp - 4], ecx
  45d4df: mov edx, dword ptr [ebp + 8]
  45d4e2: mov byte ptr [edx + 159], 0
  45d4e9: mov eax, dword ptr [ebp + 8]
  45d4ec: mov ecx, dword ptr [eax + 16]
  45d4ef: mov edx, dword ptr [ebp + 8]
  45d4f2: mov al, byte ptr [ecx + 200]
  45d4f8: mov byte ptr [edx + 140], al
  45d4fe: mov ecx, dword ptr [ebp + 8]
  45d501: mov byte ptr [ecx + 160], 0
  45d508: mov edx, dword ptr [ebp + 8]
  45d50b: mov dword ptr [edx + 152], 0
  45d515: mov eax, dword ptr [ebp + 8]
  45d518: mov ecx, dword ptr [eax + 16]
  45d51b: mov edx, dword ptr [ebp + 8]
  45d51e: mov eax, dword ptr [ecx + 176]
  45d524: mov dword ptr [edx + 144], eax
  45d52a: mov ecx, dword ptr [ebp + 8]
  45d52d: mov edx, dword ptr [ecx + 16]
  45d530: mov eax, dword ptr [ebp + 8]
  45d533: mov ecx, dword ptr [edx + 180]
  45d539: mov dword ptr [eax + 148], ecx
  45d53f: mov edx, dword ptr [ebp + 8]
  45d542: mov eax, dword ptr [edx + 16]
  45d545: mov ecx, dword ptr [ebp + 8]
  45d548: mov dl, byte ptr [eax + 160]
  45d54e: mov byte ptr [ecx + 163], dl
  45d554: mov eax, dword ptr [ebp + 8]
  45d557: mov ecx, dword ptr [eax + 16]
  45d55a: mov edx, dword ptr [ebp + 8]
  45d55d: mov al, byte ptr [ecx + 161]
  45d563: mov byte ptr [edx + 164], al
  45d569: mov ecx, dword ptr [ebp + 8]
  45d56c: mov edx, dword ptr [ecx + 16]
  45d56f: mov eax, dword ptr [ebp + 8]
  45d572: mov cl, byte ptr [edx + 162]
  45d578: mov byte ptr [eax + 165], cl
  45d57e: mov edx, dword ptr [ebp + 8]
  45d581: mov eax, dword ptr [edx + 16]
  45d584: mov ecx, dword ptr [ebp + 8]
  45d587: mov dl, byte ptr [eax + 163]
  45d58d: mov byte ptr [ecx + 166], dl
  45d593: mov eax, dword ptr [ebp + 8]
  45d596: mov ecx, dword ptr [eax + 16]
  45d599: mov edx, dword ptr [ebp + 8]
  45d59c: mov al, byte ptr [ecx + 164]
  45d5a2: mov byte ptr [edx + 167], al
  ...
  45d5f4: mov edx, dword ptr [ebp + 8]
  45d5f7: mov eax, dword ptr [edx + 16]
  45d5fa: mov ecx, dword ptr [ebp - 12]
  45d5fd: mov edx, dword ptr [eax + 4*ecx + 376]
  45d604: mov dword ptr [ebp - 16], edx
  45d607: cmp dword ptr [ebp - 16], 0
  45d60b: je 0x45d89e <.text+0x5c89e>
  45d611: mov eax, dword ptr [ebp - 16]
  45d614: mov ecx, dword ptr [eax + 12]
  45d617: mov edx, dword ptr [ebp + 8]
  45d61a: mov al, byte ptr [edx + 140]
  45d620: add al, byte ptr [ecx + 67]
  45d623: mov ecx, dword ptr [ebp + 8]
  45d626: mov byte ptr [ecx + 140], al
  45d62c: mov edx, dword ptr [ebp - 16]
  45d62f: mov eax, dword ptr [edx + 12]
  45d632: mov ecx, dword ptr [ebp + 8]
  45d635: mov dl, byte ptr [ecx + 159]
  45d63b: add dl, byte ptr [eax + 98]
  45d63e: mov eax, dword ptr [ebp + 8]
  45d641: mov byte ptr [eax + 159], dl
  45d647: mov ecx, dword ptr [ebp - 16]
  45d64a: mov edx, dword ptr [ecx + 12]
  45d64d: mov eax, dword ptr [ebp + 8]
  45d650: mov cl, byte ptr [eax + 160]
  45d656: add cl, byte ptr [edx + 99]
  45d659: mov edx, dword ptr [ebp + 8]
  45d65c: mov byte ptr [edx + 160], cl
  ...
  45d704: mov eax, dword ptr [ebp - 16]
  45d707: mov ecx, dword ptr [eax + 12]
  45d70a: mov edx, dword ptr [ebp + 8]
  45d70d: mov eax, dword ptr [edx + 144]
  45d713: xor edx, edx
  45d715: mov esi, 100
  45d71a: div esi
  45d71c: mov edx, dword ptr [ebp - 16]
  45d71f: mov edx, dword ptr [edx + 12]
  45d722: movsx edx, byte ptr [edx + 96]
  45d726: imul eax, edx
  45d729: mov ecx, dword ptr [ecx + 72]
  45d72c: add ecx, eax
  45d72e: mov edx, dword ptr [ebp + 8]
  45d731: mov eax, dword ptr [edx + 144]
  45d737: add eax, ecx
  45d739: mov ecx, dword ptr [ebp + 8]
  45d73c: mov dword ptr [ecx + 144], eax
  ...
  443d37: mov edx, dword ptr [ecx + 32]
  443d3a: mov dword ptr [ebp - 8], edx
  443d3d: jmp 0x443d46 <.text+0x42d46>
  443d3f: mov dword ptr [ebp - 8], 0
  443d46: mov eax, dword ptr [ebp - 8]
  443d49: mov dword ptr [ebp - 4], eax
  443d4c: mov ecx, dword ptr [ebp - 4]
  443d4f: movsx edx, byte ptr [ecx + 140]
  ...
  443de0: mov ecx, dword ptr [ebp - 4]
  443de3: mov edx, dword ptr [ecx + 16]
  443de6: mov word ptr [edx + 200], ax
  ...
  4442b8: mov edx, dword ptr [ecx + 32]
  4442bb: mov dword ptr [ebp - 8], edx
  4442be: jmp 0x4442c7 <.text+0x432c7>
  4442c0: mov dword ptr [ebp - 8], 0
  4442c7: mov eax, dword ptr [ebp - 8]
  4442ca: mov dword ptr [ebp - 4], eax
  4442cd: mov ecx, dword ptr [ebp - 4]
  4442d0: mov edx, dword ptr [ecx + 144]
  ...
  4445f9: push ebp
  4445fa: mov ebp, esp
  4445fc: sub esp, 8
  44460d: mov eax, dword ptr [ebp + 8]
  444610: mov ecx, dword ptr [eax]
  444612: mov edx, dword ptr [ebp + 8]
  444615: mov eax, dword ptr [edx + 4]
  444618: add eax, 2
  44461b: imul eax, eax, 12
  44461e: mov ecx, dword ptr [ecx + 12]
  444621: add ecx, eax
  444623: push ecx
  444624: mov edx, dword ptr [ebp + 8]
  444627: push edx
  444628: call 0x43d110 <.text+0x3c110>
  44462d: add esp, 8
  444630: mov dword ptr [ebp - 4], eax
  444633: mov eax, dword ptr [ebp - 4]
  444636: mov ecx, dword ptr [4*eax + 4903424]
  44463d: mov edx, dword ptr [ecx + 32]
  444640: mov dword ptr [ebp - 8], edx
  444643: cmp dword ptr [ebp - 8], 0
  444647: je 0x444666 <.text+0x43666>
  444649: mov eax, dword ptr [ebp - 8]
  44464c: mov ecx, dword ptr [eax + 16]
  44464f: mov edx, dword ptr [ecx + 204]
  444655: push edx
  ...
  422b2c: mov ecx, dword ptr [ebp - 8]
  422b2f: mov edx, dword ptr [ebp + 16]
  422b32: mov al, byte ptr [edx + 13]
  422b35: mov byte ptr [ecx], al
  422b37: mov ecx, dword ptr [ebp - 8]
  422b3a: mov edx, dword ptr [ebp + 16]
  422b3d: mov eax, dword ptr [edx + 24]
  422b40: mov dword ptr [ecx + 8], eax
  422b43: mov ecx, dword ptr [ebp - 8]
  422b46: mov edx, dword ptr [ebp + 16]
  422b49: mov eax, dword ptr [edx + 24]
  422b4c: mov dword ptr [ecx + 16], eax
  422b4f: mov ecx, dword ptr [ebp - 8]
  422b52: mov edx, dword ptr [ebp + 16]
  422b55: mov eax, dword ptr [edx + 28]
  422b58: mov dword ptr [ecx + 20], eax
  422b5b: mov ecx, dword ptr [ebp - 8]
  422b5e: mov edx, dword ptr [ebp + 16]
  422b61: mov al, byte ptr [edx + 141]
  422b67: mov byte ptr [ecx + 101], al
  422b6a: mov ecx, dword ptr [ebp - 8]
  422b6d: mov edx, dword ptr [ebp + 16]
  422b70: mov al, byte ptr [edx + 142]
  422b76: mov byte ptr [ecx + 100], al
  422b79: mov ecx, dword ptr [ebp - 8]
  422b7c: mov word ptr [ecx + 82], 0
  422b82: mov edx, dword ptr [ebp - 8]
  422b85: mov word ptr [edx + 76], 0
  422b8b: mov eax, dword ptr [ebp - 8]
  422b8e: mov dword ptr [eax + 88], 0
  422b95: mov ecx, dword ptr [ebp - 8]
  422b98: mov word ptr [ecx + 84], 0
  422b9e: mov edx, dword ptr [ebp - 8]
  422ba1: mov word ptr [edx + 58], 0
  422ba7: mov eax, dword ptr [ebp - 8]
  422baa: mov word ptr [eax + 70], 0
  422bb0: mov ecx, dword ptr [ebp - 8]
  422bb3: mov word ptr [ecx + 54], 0
  422bb9: mov edx, dword ptr [ebp - 8]
  422bbc: mov word ptr [edx + 56], 1
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
