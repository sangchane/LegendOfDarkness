; 사람 → 괴물 피해 — 평타 0x4160f7 · 피해 넣기 0x424215 · 방어력 0x4150f2 · 범위+방어 0x415173 · 속성 0x415cff
; 출처: ~/Downloads/5.99 서버팩/Novaonline.exe (objdump -d --x86-asm-syntax=intel, 오프셋은 10진수)
; P = [0x4ad200+4*id]+0x20 (사람 객체), C = [P+0x10] (캐릭터 칸), M = 괴물 하나(+4 = 템플릿 T)
;
; 평타 원값  : d = C+0x8C(공격력) + rand%10 − rand%10        (0x4166b1~0x4166d5)
; 치명타     : rand%100 ≤ C+0x9C 이면 d×2  (≤ 이라 실제 확률은 치명타+1 %) (0x4166eb ja)
; 오기       : C 바이트+0x74(damage_per_delay) 가 켜져 있으면 d += P+0x90(최대체력) >> 3 (0x416703)
; 명중/회피  : 판정 없음 — 평타는 늘 맞는다
; 0x424215(id, M, 소리?, d):
;   d = 0x415173(P+0xA0 공격수정, d, d, M+0x00 괴물 방어력, 1) = d + 공격수정 → 방어력 적용
;   방어력 0x4150f2: AC≠0 이면 d += trunc(d × AC × (AC>0 ? 0.01 : 0.009))   (상수 0x481298=0.01, 0x4812a0=0.009)
;   속성 0x415cff(d, 목걸이[C+0x18C].공격속성(item+0x65), M+0x64): 공격 쪽 속성이 1~5 면 d×13/10. 셋째 인자(받는 쪽 속성)는 읽지 않는다
;   C+0x15A(방어무시) 켜짐 + rand%100≥80 → d = 처음 값(공격수정·방어·속성 전)
;   C+0x15E(속성강화) ==1 → d×3/2 · C+0x160(슈페이아움) ≠0 → d×값 · M+0x3A(잠) → d×2 후 잠 깸
;   T+0x88(보호데미지) → d=1 · M+0x48 ≠0 → d=0 · M+0x10(체력) −= d (0 에서 멈춤)
;   T+0x89(데미지체크) → "%s: d" 알림 · T+0x8A(비타체크) → "%s: 남은체력" 알림
;   체력 0 → C 바이트+0x84(잡은 수)++ · 그룹이면 0x41a229/0x41a2cb, 아니면 0x4696e4/0x46a165 로 T+0x20 경험치 · T+0x24 어빌 경험치
  ...
  4166ab: mov edx, dword ptr [ebp - 28]
  4166ae: mov esi, dword ptr [edx + 16]
  4166b1: call 0x476d82 <.text+0x75d82>
  4166b6: cdq
  4166b7: mov ecx, 10
  4166bc: idiv ecx
  4166be: mov esi, dword ptr [esi + 140]
  4166c4: add esi, edx
  4166c6: call 0x476d82 <.text+0x75d82>
  4166cb: cdq
  4166cc: mov ecx, 10
  4166d1: idiv ecx
  4166d3: sub esi, edx
  4166d5: mov dword ptr [ebp - 32], esi
  4166d8: call 0x476d82 <.text+0x75d82>
  4166dd: cdq
  4166de: mov ecx, 100
  4166e3: idiv ecx
  4166e5: mov eax, dword ptr [ebp - 28]
  4166e8: mov ecx, dword ptr [eax + 16]
  4166eb: cmp edx, dword ptr [ecx + 156]
  4166f1: ja 0x4166fb <.text+0x156fb>
  4166f3: mov edx, dword ptr [ebp - 32]
  4166f6: shl edx
  4166f8: mov dword ptr [ebp - 32], edx
  4166fb: mov eax, dword ptr [ebp - 28]
  4166fe: mov ecx, dword ptr [eax + 16]
  416701: xor edx, edx
  416703: mov dl, byte ptr [ecx + 116]
  416706: test edx, edx
  416708: je 0x41671e <.text+0x1571e>
  41670a: mov eax, dword ptr [ebp - 28]
  41670d: mov ecx, dword ptr [eax + 144]
  416713: shr ecx, 3
  416716: mov edx, dword ptr [ebp - 32]
  416719: add edx, ecx
  41671b: mov dword ptr [ebp - 32], edx
  41671e: mov eax, dword ptr [ebp - 32]
  416721: push eax
  416722: push 255
  416727: mov ecx, dword ptr [ebp - 48]
  41672a: push ecx
  41672b: mov edx, dword ptr [ebp + 8]
  41672e: push edx
  41672f: call 0x424215 <.text+0x23215>
  416734: add esp, 16
  416737: jmp 0x4167c5 <.text+0x157c5>
  41673c: mov eax, dword ptr [ebp - 28]
  ...
  4150f2: push ebp
  4150f3: mov ebp, esp
  4150f5: sub esp, 24
  4150fd: mov dword ptr [ebp - 24], eax
  415100: mov dword ptr [ebp - 20], eax
  415103: mov dword ptr [ebp - 16], eax
  415106: mov dword ptr [ebp - 12], eax
  415109: mov dword ptr [ebp - 8], eax
  41510c: mov dword ptr [ebp - 4], eax
  41510f: movsx eax, byte ptr [ebp + 12]
  415113: test eax, eax
  415115: je 0x415168 <.text+0x14168>
  415117: movsx ecx, byte ptr [ebp + 12]
  41511b: mov dword ptr [ebp - 12], ecx
  41511e: fild dword ptr [ebp - 12]
  415121: fmul qword ptr [4723360]
  415127: fstp qword ptr [ebp - 8]
  41512a: movsx edx, byte ptr [ebp + 12]
  41512e: test edx, edx
  415130: jle 0x415145 <.text+0x14145>
  415132: movsx eax, byte ptr [ebp + 12]
  415136: mov dword ptr [ebp - 16], eax
  415139: fild dword ptr [ebp - 16]
  41513c: fmul qword ptr [4723352]
  415142: fstp qword ptr [ebp - 8]
  415145: mov ecx, dword ptr [ebp + 8]
  415148: mov dword ptr [ebp - 24], ecx
  41514b: mov dword ptr [ebp - 20], 0
  415152: fild qword ptr [ebp - 24]
  415155: fmul qword ptr [ebp - 8]
  415158: fst qword ptr [ebp - 8]
  41515b: call 0x4783d4 <.text+0x773d4>
  415160: mov edx, dword ptr [ebp + 8]
  415163: add edx, eax
  415165: mov dword ptr [ebp + 8], edx
  415168: mov eax, dword ptr [ebp + 8]
  41516b: imul eax, dword ptr [ebp + 16]
  41516f: mov esp, ebp
  415171: pop ebp
  415172: ret
  ...
  415173: push ebp
  415174: mov ebp, esp
  415176: push ecx
  415177: push esi
  41517f: cmp dword ptr [ebp + 16], 0
  415183: jne 0x41518e <.text+0x1418e>
  415185: mov eax, dword ptr [ebp + 12]
  415188: add eax, 1
  41518b: mov dword ptr [ebp + 16], eax
  41518e: mov ecx, dword ptr [ebp + 12]
  415191: cmp ecx, dword ptr [ebp + 16]
  415194: jb 0x41519f <.text+0x1419f>
  415196: mov edx, dword ptr [ebp + 12]
  415199: add edx, 1
  41519c: mov dword ptr [ebp + 16], edx
  41519f: mov eax, dword ptr [ebp + 8]
  4151a2: mov dword ptr [ebp - 4], eax
  4151a5: mov ecx, dword ptr [ebp - 4]
  4151a8: add ecx, dword ptr [ebp + 12]
  4151ab: mov dword ptr [ebp - 4], ecx
  4151ae: call 0x476d82 <.text+0x75d82>
  4151b3: mov esi, eax
  4151b5: call 0x476d82 <.text+0x75d82>
  4151ba: imul esi, eax
  4151bd: call 0x476d82 <.text+0x75d82>
  4151c2: imul esi, eax
  4151c5: mov ecx, dword ptr [ebp + 16]
  4151c8: sub ecx, dword ptr [ebp + 12]
  4151cb: mov eax, esi
  4151cd: xor edx, edx
  4151cf: div ecx
  4151d1: mov eax, dword ptr [ebp - 4]
  4151d4: add eax, edx
  4151d6: mov dword ptr [ebp - 4], eax
  4151d9: mov ecx, dword ptr [ebp + 24]
  4151dc: push ecx
  4151dd: mov dl, byte ptr [ebp + 20]
  4151e0: push edx
  4151e1: mov eax, dword ptr [ebp - 4]
  4151e4: push eax
  4151e5: call 0x4150f2 <.text+0x140f2>
  4151ea: add esp, 12
  4151ed: mov dword ptr [ebp - 4], eax
  ...
  415cff: push ebp
  415d00: mov ebp, esp
  415d02: cmp dword ptr [ebp + 12], 0
  415d06: jne 0x415d0e <.text+0x14d0e>
  415d08: mov eax, dword ptr [ebp + 8]
  415d0b: mov dword ptr [ebp + 8], eax
  415d0e: cmp dword ptr [ebp + 12], 1
  415d12: jne 0x415d25 <.text+0x14d25>
  415d14: mov eax, dword ptr [ebp + 8]
  415d17: imul eax, eax, 13
  415d1a: cdq
  415d1b: mov ecx, 10
  415d20: idiv ecx
  415d22: mov dword ptr [ebp + 8], eax
  415d25: cmp dword ptr [ebp + 12], 2
  415d29: jne 0x415d3c <.text+0x14d3c>
  415d2b: mov eax, dword ptr [ebp + 8]
  415d2e: imul eax, eax, 13
  415d31: cdq
  415d32: mov ecx, 10
  415d37: idiv ecx
  415d39: mov dword ptr [ebp + 8], eax
  415d3c: cmp dword ptr [ebp + 12], 3
  415d40: jne 0x415d53 <.text+0x14d53>
  415d42: mov eax, dword ptr [ebp + 8]
  415d45: imul eax, eax, 13
  415d48: cdq
  415d49: mov ecx, 10
  415d4e: idiv ecx
  415d50: mov dword ptr [ebp + 8], eax
  415d53: cmp dword ptr [ebp + 12], 4
  415d57: jne 0x415d6a <.text+0x14d6a>
  415d59: mov eax, dword ptr [ebp + 8]
  415d5c: imul eax, eax, 13
  415d5f: cdq
  415d60: mov ecx, 10
  415d65: idiv ecx
  415d67: mov dword ptr [ebp + 8], eax
  415d6a: cmp dword ptr [ebp + 12], 5
  415d6e: jne 0x415d81 <.text+0x14d81>
  415d70: mov eax, dword ptr [ebp + 8]
  415d73: imul eax, eax, 13
  415d76: cdq
  415d77: mov ecx, 10
  415d7c: idiv ecx
  415d7e: mov dword ptr [ebp + 8], eax
  415d81: mov eax, dword ptr [ebp + 8]
  415d84: pop ebp
  415d85: ret
  ...
  42432e: mov ecx, dword ptr [ebp - 1024]
  424334: mov edx, dword ptr [ecx + 16]
  424337: mov eax, dword ptr [edx + 156]
  42433d: mov dword ptr [ebp - 1020], eax
  424343: push 1
  424345: mov ecx, dword ptr [ebp - 512]
  42434b: mov dl, byte ptr [ecx]
  42434d: push edx
  42434e: mov eax, dword ptr [ebp + 20]
  424351: push eax
  424352: mov ecx, dword ptr [ebp + 20]
  424355: push ecx
  424356: mov edx, dword ptr [ebp - 1024]
  42435c: xor eax, eax
  42435e: mov al, byte ptr [edx + 160]
  424364: push eax
  424365: call 0x415173 <.text+0x14173>
  42436a: add esp, 20
  42436d: mov dword ptr [ebp + 20], eax
  424370: mov ecx, dword ptr [ebp - 512]
  424376: movsx edx, byte ptr [ecx + 100]
  42437a: mov dword ptr [ebp - 504], edx
  424380: mov eax, dword ptr [ebp - 1024]
  424386: mov ecx, dword ptr [eax + 16]
  424389: cmp dword ptr [ecx + 396], 0
  424390: jne 0x42439e <.text+0x2339e>
  424392: mov dword ptr [ebp - 1036], 0
  42439c: jmp 0x4243ba <.text+0x233ba>
  42439e: mov edx, dword ptr [ebp - 1024]
  4243a4: mov eax, dword ptr [edx + 16]
  4243a7: mov ecx, dword ptr [eax + 396]
  4243ad: mov edx, dword ptr [ecx + 12]
  4243b0: movsx eax, byte ptr [edx + 101]
  4243b4: mov dword ptr [ebp - 1036], eax
  4243ba: mov ecx, dword ptr [ebp - 1036]
  4243c0: mov dword ptr [ebp - 1032], ecx
  4243c6: mov edx, dword ptr [ebp - 504]
  4243cc: push edx
  4243cd: mov eax, dword ptr [ebp - 1032]
  4243d3: push eax
  4243d4: mov ecx, dword ptr [ebp + 20]
  4243d7: push ecx
  4243d8: call 0x415cff <.text+0x14cff>
  4243dd: add esp, 12
  4243e0: mov dword ptr [ebp + 20], eax
  4243e3: mov edx, dword ptr [ebp - 1024]
  4243e9: mov eax, dword ptr [edx + 16]
  4243ec: xor ecx, ecx
  4243ee: mov cx, word ptr [eax + 346]
  4243f5: test ecx, ecx
  4243f7: je 0x424414 <.text+0x23414>
  4243f9: call 0x476d82 <.text+0x75d82>
  4243fe: cdq
  4243ff: mov ecx, 100
  424404: idiv ecx
  424406: cmp edx, 80
  424409: jl 0x424414 <.text+0x23414>
  42440b: mov edx, dword ptr [ebp - 1028]
  424411: mov dword ptr [ebp + 20], edx
  424414: mov eax, dword ptr [ebp - 1024]
  42441a: mov ecx, dword ptr [eax + 16]
  42441d: xor edx, edx
  42441f: mov dx, word ptr [ecx + 350]
  424426: cmp edx, 1
  424429: jne 0x424436 <.text+0x23436>
  42442b: mov eax, dword ptr [ebp + 20]
  42442e: imul eax, eax, 3
  424431: shr eax
  424433: mov dword ptr [ebp + 20], eax
  424436: mov ecx, dword ptr [ebp - 1024]
  42443c: mov edx, dword ptr [ecx + 16]
  42443f: xor eax, eax
  424441: mov ax, word ptr [edx + 352]
  424448: test eax, eax
  42444a: je 0x424467 <.text+0x23467>
  42444c: mov ecx, dword ptr [ebp - 1024]
  424452: mov edx, dword ptr [ecx + 16]
  424455: xor eax, eax
  424457: mov ax, word ptr [edx + 352]
  42445e: mov ecx, dword ptr [ebp + 20]
  424461: imul ecx, eax
  424464: mov dword ptr [ebp + 20], ecx
  424467: mov edx, dword ptr [ebp - 512]
  42446d: xor eax, eax
  42446f: mov ax, word ptr [edx + 58]
  424473: test eax, eax
  424475: je 0x42447f <.text+0x2347f>
  424477: mov ecx, dword ptr [ebp + 20]
  42447a: shl ecx
  42447c: mov dword ptr [ebp + 20], ecx
  42447f: mov edx, dword ptr [ebp - 512]
  424485: mov eax, dword ptr [edx + 4]
  424488: movsx ecx, byte ptr [eax + 136]
  42448f: test ecx, ecx
  424491: je 0x42449a <.text+0x2349a>
  424493: mov dword ptr [ebp + 20], 1
  ...
  4244a2: mov ax, word ptr [edx + 72]
  4244a6: test eax, eax
  4244a8: je 0x4244b1 <.text+0x234b1>
  4244aa: mov dword ptr [ebp + 20], 0
  4244b1: mov ecx, dword ptr [ebp + 20]
  ...
  4244d5: mov eax, dword ptr [ebp - 512]
  4244db: mov ecx, dword ptr [eax + 4]
  4244de: movsx edx, byte ptr [ecx + 137]
  4244e5: test edx, edx
  4244e7: je 0x424508 <.text+0x23508>
  ...
  424532: mov ecx, dword ptr [ebp - 512]
  424538: mov edx, dword ptr [ecx + 4]
  42453b: movsx eax, byte ptr [edx + 138]
  424542: test eax, eax
  424544: je 0x424565 <.text+0x23565>
  ...
  424565: mov edx, dword ptr [ebp - 512]
  42456b: mov eax, dword ptr [ebp + 20]
  42456e: cmp eax, dword ptr [edx + 16]
  424571: jbe 0x42458f <.text+0x2358f>
  424573: mov ecx, dword ptr [ebp - 512]
  424579: mov dword ptr [ecx + 16], 0
  424580: mov dword ptr [ebp - 516], 0
  42458a: jmp 0x424619 <.text+0x23619>
  42458f: mov edx, dword ptr [ebp - 512]
  424595: mov eax, dword ptr [edx + 16]
  424598: sub eax, dword ptr [ebp + 20]
  42459b: mov ecx, dword ptr [ebp - 512]
  4245a1: mov dword ptr [ecx + 16], eax
  ...
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
  4246a8: add esp, 8
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
  4246d0: add esp, 8
  4246d3: jmp 0x424718 <.text+0x23718>
  4246d5: push 0
  4246d7: mov eax, dword ptr [ebp - 512]
  4246dd: mov ecx, dword ptr [eax + 4]
  4246e0: mov edx, dword ptr [ecx + 32]
  4246e3: push edx
  4246e4: mov eax, dword ptr [ebp + 8]
  4246e7: push eax
  4246e8: call 0x4696e4 <.text+0x686e4>
