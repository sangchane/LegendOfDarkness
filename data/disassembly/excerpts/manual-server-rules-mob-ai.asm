; Novaonline.exe (5.99 서버) — 괴물 행동(AI)
; 0x426812 (250ms 타이머): 맵마다 괴물(객체 종류 1)의 다음행동시각 mob+0x70 이 GetTickCount 를 지났으면 +0x70 = 지금 + 템플릿 속도(+0x10 ms).
;   템플릿 타입(+0x0C) 0: 표적찾기 후 50% 배회(쫓지 않음) / 1: 어그로(mob+0x18)면 추적·공격, 아니면 표적찾기+배회(선공)
;   2: 어그로면 추적·공격, 아니면 배회만(비선공 — 맞으면 0x4242ab 가 +0x18=1, +0x1C=때린 사람).  mob+0x40(딜루메니) 이 있으면 쫓지 않고 배회.
; 0x426572 표적찾기: 같은 맵·화면 안(0x416b63, 괴물 기준 ±9칸 상자)의 접속자 중 점수 = max(1,|dx|)×max(1,|dy|) 가 가장 작은 사람.
;   캐릭터 상태(+0xF0) 1 이면 어그로 안 됨, 2 이면 템플릿 하이드체크(+0x40)가 있을 때만.
426875: call dword ptr [4722768]
426882: mov ecx, dword ptr [ebp - 4]
426885: cmp dword ptr [ecx + 112], eax
426888: jae 0x42698b <.text+0x2598b>
426890: call dword ptr [4722768]
42689d: mov edx, dword ptr [ebp - 8]
4268a0: mov ecx, dword ptr [edx + 16]
4268a3: add ecx, eax
4268a5: mov edx, dword ptr [ebp - 4]
4268a8: mov dword ptr [edx + 112], ecx
4268ab: mov eax, dword ptr [ebp - 8]
4268ae: mov cl, byte ptr [eax + 12]
4268b1: mov byte ptr [ebp - 20], cl
4268b4: cmp byte ptr [ebp - 20], 0
4268b8: je 0x4268cf <.text+0x258cf>
4268ba: cmp byte ptr [ebp - 20], 1
4268be: je 0x426901 <.text+0x25901>
4268c0: cmp byte ptr [ebp - 20], 2
4268c4: je 0x42694d <.text+0x2594d>
4268ca: jmp 0x42698b <.text+0x2598b>
4268cf: mov edx, dword ptr [ebp - 16]
4268d2: push edx
4268d3: call 0x426572 <.text+0x25572>
4268db: call 0x476d82 <.text+0x75d82>
4268e0: and eax, 2147483649
4268e5: jns 0x4268ec <.text+0x258ec>
4268e7: dec eax
4268e8: or eax, -2
4268eb: inc eax
4268ec: test eax, eax
4268ee: je 0x4268fc <.text+0x258fc>
4268f0: mov eax, dword ptr [ebp - 16]
4268f3: push eax
4268f4: call 0x424b16 <.text+0x23b16>
4268fc: jmp 0x42698b <.text+0x2598b>
426901: mov ecx, dword ptr [ebp - 4]
426904: cmp dword ptr [ecx + 24], 0
426908: je 0x426933 <.text+0x25933>
42690a: mov edx, dword ptr [ebp - 4]
42690d: xor eax, eax
42690f: mov ax, word ptr [edx + 64]
426913: test eax, eax
426915: jne 0x426925 <.text+0x25925>
426917: mov ecx, dword ptr [ebp - 16]
42691a: push ecx
42691b: call 0x4258a4 <.text+0x248a4>
426923: jmp 0x426931 <.text+0x25931>
426925: mov edx, dword ptr [ebp - 16]
426928: push edx
426929: call 0x424b16 <.text+0x23b16>
426931: jmp 0x42694b <.text+0x2594b>
426933: mov eax, dword ptr [ebp - 16]
426936: push eax
426937: call 0x426572 <.text+0x25572>
42693f: mov ecx, dword ptr [ebp - 16]
426942: push ecx
426943: call 0x424b16 <.text+0x23b16>
42694b: jmp 0x42698b <.text+0x2598b>
42694d: mov edx, dword ptr [ebp - 4]
426950: cmp dword ptr [edx + 24], 0
426954: je 0x42697f <.text+0x2597f>
426956: mov eax, dword ptr [ebp - 4]
426959: xor ecx, ecx
42695b: mov cx, word ptr [eax + 64]
42695f: test ecx, ecx
426961: jne 0x426971 <.text+0x25971>
426963: mov edx, dword ptr [ebp - 16]
426966: push edx
426967: call 0x4258a4 <.text+0x248a4>
42696f: jmp 0x42697d <.text+0x2597d>
426971: mov eax, dword ptr [ebp - 16]
426974: push eax
426975: call 0x424b16 <.text+0x23b16>
42697d: jmp 0x42698b <.text+0x2598b>
42697f: mov ecx, dword ptr [ebp - 16]
426982: push ecx
426983: call 0x424b16 <.text+0x23b16>
42698b: mov edx, dword ptr [ebp - 16]
; ---- 표적찾기 0x426572: 점수
4266b2: mov eax, dword ptr [ebp + 8]
4266b5: xor ecx, ecx
4266b7: mov cx, word ptr [eax + 12]
4266bb: mov edx, dword ptr [ebp - 16]
4266be: mov eax, dword ptr [edx + 16]
4266c1: xor edx, edx
4266c3: mov dx, word ptr [eax + 92]
4266c7: sub ecx, edx
4266c9: push ecx
4266ca: call 0x4783fb <.text+0x773fb>
4266d2: mov dword ptr [ebp - 24], eax
4266d5: mov eax, dword ptr [ebp + 8]
4266d8: xor ecx, ecx
4266da: mov cx, word ptr [eax + 14]
4266de: mov edx, dword ptr [ebp - 16]
4266e1: mov eax, dword ptr [edx + 16]
4266e4: xor edx, edx
4266e6: mov dx, word ptr [eax + 94]
4266ea: sub ecx, edx
4266ec: push ecx
4266ed: call 0x4783fb <.text+0x773fb>
4266f5: mov dword ptr [ebp - 28], eax
4266f8: cmp dword ptr [ebp - 24], 0
4266fc: jne 0x426705 <.text+0x25705>
4266fe: mov dword ptr [ebp - 24], 1
426705: cmp dword ptr [ebp - 28], 0
426709: jne 0x426712 <.text+0x25712>
42670b: mov dword ptr [ebp - 28], 1
426712: mov eax, dword ptr [ebp - 24]
426715: imul eax, dword ptr [ebp - 28]
426719: mov dword ptr [ebp - 8], eax
42671c: mov ecx, dword ptr [ebp - 32]
42671f: cmp ecx, dword ptr [ebp - 8]
426722: jle 0x426744 <.text+0x25744>
426724: mov edx, dword ptr [ebp - 8]
426727: mov dword ptr [ebp - 32], edx
42672a: mov eax, dword ptr [ebp - 4]
42672d: mov ecx, dword ptr [ebp - 20]
426730: mov edx, dword ptr [4*ecx + 4903140]
426737: mov dword ptr [eax + 28], edx
42673a: mov eax, dword ptr [ebp - 4]
42673d: mov dword ptr [eax + 24], 1
426744: jmp 0x4267ff <.text+0x257ff>
