; Novaonline.exe — 괴물이 죽을 때 경험치 나누기 (0x424215 의 끝 부분) · 그룹 0x41a229 · 그룹 어빌 0x41a2cb
; 괴물 인스턴스 M=[arg2+4], 템플릿 T=[M+4]: 경험치 T+0x20, 어빌경험치 T+0x24
; 킬 카운트 C+0x84 += 1. 그룹(P+0x14)이면 그룹 칸 12개(group+0xC+4i, 캐릭터 포인터) 중 죽인 사람과 **같은 맵**([+0x58])인 모두에게
;   경험치 **전부**(나누지 않음) add_exp(id, T+0x20, 0). 레벨차·거리 보정 없음. 그룹 아니면 혼자 add_exp.
; 어빌경험치(T+0x24 ≠0)도 같은 방식(0x41a2cb → 0x46a165).
; 0x419f4b(레벨 비례: 각자 레벨×(exp+1)/레벨합) · 0x41a067(인원 n: exp/n/10×12, 직업4 있고 n≥3 이면 ×17) 은 아무도 부르지 않는다(죽은 코드)
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
; --- 0x41a229 그룹 경험치 (첫 두 바이트 push ebp/mov ebp,esp 가 점프표 뒤라 objdump 가 어긋나게 읽음) ---
41a243: mov eax, dword ptr [ebp + 8]
41a246: mov ecx, dword ptr [4*eax + 4903424]
41a24d: mov edx, dword ptr [ecx + 32]
41a250: mov dword ptr [ebp - 12], edx
41a253: mov eax, dword ptr [ebp - 12]
41a256: mov ecx, dword ptr [eax + 16]
41a259: mov edx, dword ptr [ecx + 88]
41a25c: mov dword ptr [ebp - 20], edx
41a25f: mov eax, dword ptr [ebp - 12]
41a262: mov ecx, dword ptr [eax + 20]
41a265: mov dword ptr [ebp - 8], ecx
41a268: cmp dword ptr [ebp - 8], 0
41a26c: jne 0x41a270 <.text+0x19270>
41a26e: jmp 0x41a2bd <.text+0x192bd>
41a270: mov dword ptr [ebp - 16], 0
41a277: jmp 0x41a282 <.text+0x19282>
41a279: mov edx, dword ptr [ebp - 16]
41a27c: add edx, 1
41a27f: mov dword ptr [ebp - 16], edx
41a282: cmp dword ptr [ebp - 16], 12
41a286: jge 0x41a2bd <.text+0x192bd>
41a288: mov eax, dword ptr [ebp - 16]
41a28b: mov ecx, dword ptr [ebp - 8]
41a28e: mov edx, dword ptr [ecx + 4*eax + 12]
41a292: mov dword ptr [ebp - 4], edx
41a295: cmp dword ptr [ebp - 4], 0
41a299: je 0x41a2bb <.text+0x192bb>
41a29b: mov eax, dword ptr [ebp - 4]
41a29e: mov ecx, dword ptr [eax + 88]
41a2a1: cmp ecx, dword ptr [ebp - 20]
41a2a4: jne 0x41a2bb <.text+0x192bb>
41a2a6: push 0
41a2a8: mov edx, dword ptr [ebp + 12]
41a2ab: push edx
41a2ac: mov eax, dword ptr [ebp - 4]
41a2af: mov ecx, dword ptr [eax + 4]
41a2b2: push ecx
41a2b3: call 0x4696e4 <.text+0x686e4>
41a2bb: jmp 0x41a279 <.text+0x19279>
41a2c7: mov esp, ebp
41a2c9: pop ebp
41a2ca: ret
; --- 0x41a067 (안 불림) 인원 나누기 ---
41a1a7: cmp dword ptr [ebp - 36], 1
41a1ab: jne 0x41a1db <.text+0x191db>
41a1ad: cmp dword ptr [ebp - 24], 3
41a1b1: jl 0x41a1db <.text+0x191db>
41a1b3: push 0
41a1b5: mov eax, dword ptr [ebp + 12]
41a1b8: xor edx, edx
41a1ba: div dword ptr [ebp - 24]
41a1bd: xor edx, edx
41a1bf: mov ecx, 10
41a1c4: div ecx
41a1c6: imul eax, eax, 17
41a1c9: push eax
41a1ca: mov edx, dword ptr [ebp - 4]
41a1cd: mov eax, dword ptr [edx + 4]
41a1d0: push eax
41a1d1: call 0x4696e4 <.text+0x686e4>
41a1d9: jmp 0x41a201 <.text+0x19201>
41a1db: push 0
41a1dd: mov eax, dword ptr [ebp + 12]
41a1e0: xor edx, edx
41a1e2: div dword ptr [ebp - 24]
41a1e5: xor edx, edx
41a1e7: mov ecx, 10
41a1ec: div ecx
41a1ee: imul eax, eax, 12
41a1f1: push eax
41a1f2: mov edx, dword ptr [ebp - 4]
41a1f5: mov eax, dword ptr [edx + 4]
41a1f8: push eax
41a1f9: call 0x4696e4 <.text+0x686e4>
