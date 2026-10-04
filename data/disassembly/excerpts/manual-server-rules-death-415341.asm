; Novaonline.exe — 사람이 피해를 받고 죽을 때 0x415341 (괴물→사람; 0x4156a7·0x4159f0 은 C+0x148 대신 0x146 등 칸만 다른 쌍둥이)
; 피해 전: 휴식 C+0x62 해제 · 숨기 C+0x136 해제(바디 남16/여32) · 무적류 C+0x14A(portris)·0x148(defens)·0x144(immortal) 면 피해 0
;   · 호라마 C+0x134 면 ÷2 · 나르콜리(잠) C+0x76 면 ×2 하고 깸
; HP C+0xCC -= 피해(아래로는 0), 막대 % = HP/최대(P+0x90)×100. HP 0 이면 __SCRIPT_DEAD__ 가 있으면 그것을, 없으면 0x415202(char_dead)
; char_dead 0x415202: 경험치(C+0xD4) 1% 깎기(남은 게 1% 이하면 그대로), 바디 C+0xA7 = 성별(C+0x60)?48:64, 상태 C+0xF0 = 1(유령)
4154a9: mov ax, word ptr [edx + 330]
4154a9: mov ax, word ptr [edx + 330]
4154b4: mov dword ptr [ebp + 16], 0
4154bb: mov ecx, dword ptr [ebp - 264]
4154c1: mov edx, dword ptr [ecx + 16]
4154c6: mov ax, word ptr [edx + 328]
4154d1: mov dword ptr [ebp + 16], 0
4154d8: mov ecx, dword ptr [ebp - 264]
4154de: mov edx, dword ptr [ecx + 16]
4154e3: mov ax, word ptr [edx + 324]
4154ee: mov dword ptr [ebp + 16], 0
4154f5: mov ecx, dword ptr [ebp - 264]
4154fb: mov edx, dword ptr [ecx + 16]
415500: mov ax, word ptr [edx + 308]
41550b: mov ecx, dword ptr [ebp + 16]
41550e: shr ecx
415510: mov dword ptr [ebp + 16], ecx
415513: mov edx, dword ptr [ebp - 264]
415519: mov eax, dword ptr [edx + 16]
41551e: mov cl, byte ptr [eax + 118]
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
41555b: mov ecx, dword ptr [ebp - 264]
415561: mov edx, dword ptr [ecx + 4]
41556d: mov eax, dword ptr [ebp - 264]
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
415687: jmp 0x415695 <.text+0x14695>
415689: mov edx, dword ptr [ebp + 8]
41568c: push edx
41568d: call 0x415202 <.text+0x14202>
415695: pop edi
; --- char_dead 0x415202 ---
415244: mov dword ptr [ebp - 264], edx
41524a: mov eax, dword ptr [ebp - 264]
415250: mov ecx, dword ptr [eax + 16]
415253: mov edx, dword ptr [ecx + 212]
415259: mov dword ptr [ebp - 280], edx
41525f: mov dword ptr [ebp - 276], 0
415269: fild qword ptr [ebp - 280]
41526f: fdiv qword ptr [4723368]
415275: call 0x4783d4 <.text+0x773d4>
41527a: mov dword ptr [ebp - 260], eax
415280: mov eax, dword ptr [ebp - 264]
415286: mov ecx, dword ptr [eax + 16]
415289: mov edx, dword ptr [ecx + 212]
41528f: cmp edx, dword ptr [ebp - 260]
415295: jbe 0x4152bd <.text+0x142bd>
415297: mov eax, dword ptr [ebp - 264]
41529d: mov ecx, dword ptr [eax + 16]
4152a0: mov edx, dword ptr [ecx + 212]
4152a6: sub edx, dword ptr [ebp - 260]
4152ac: mov eax, dword ptr [ebp - 264]
4152b2: mov ecx, dword ptr [eax + 16]
4152b5: mov dword ptr [ecx + 212], edx
4152bb: jmp 0x4152d0 <.text+0x142d0>
4152bd: mov edx, dword ptr [ebp - 264]
4152c3: mov eax, dword ptr [edx + 16]
4152c6: mov dword ptr [eax + 212], 0
4152d0: mov ecx, dword ptr [ebp - 264]
4152d6: mov edx, dword ptr [ecx + 16]
4152d9: xor eax, eax
4152db: mov al, byte ptr [edx + 96]
4152de: test eax, eax
4152e0: je 0x4152f4 <.text+0x142f4>
4152e2: mov ecx, dword ptr [ebp - 264]
4152e8: mov edx, dword ptr [ecx + 16]
4152eb: mov byte ptr [edx + 167], 48
4152f2: jmp 0x415304 <.text+0x14304>
4152f4: mov eax, dword ptr [ebp - 264]
4152fa: mov ecx, dword ptr [eax + 16]
4152fd: mov byte ptr [ecx + 167], 64
415304: mov edx, dword ptr [ebp - 264]
41530a: mov eax, dword ptr [edx + 16]
41530d: mov byte ptr [eax + 240], 1
; --- 땅의 물건 0x46cc67 (500초 타이머) ---
46ccc9: mov edx, dword ptr [ebp - 8]
46cccc: mov esi, dword ptr [edx + 24]
46cccf: add esi, dword ptr [5165644]
46ccd5: push 0
46ccd7: call 0x477bae <.text+0x76bae>
46ccdf: cmp esi, eax
46cce1: jge 0x46ce3e <.text+0x6be3e>
46ce47: mov edx, dword ptr [ebp - 8]
46ce4a: mov esi, dword ptr [edx + 24]
46ce4d: add esi, dword ptr [5165648]
46ce53: push 0
46ce55: call 0x477bae <.text+0x76bae>
46ce5d: cmp esi, eax
46ce5f: jge 0x46ce7b <.text+0x6be7b>
46ce61: push 0
46ce63: call 0x477bae <.text+0x76bae>
46ce6b: mov ecx, dword ptr [ebp - 8]
46ce6e: mov dword ptr [ecx + 24], eax
46ce71: mov edx, dword ptr [ebp - 8]
46ce74: mov dword ptr [edx + 20], 0
