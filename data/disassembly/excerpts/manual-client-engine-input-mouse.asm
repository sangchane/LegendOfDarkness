; 마우스 (2005 = 5.99 클라이언트 Legend.exe) — 좌표 자르기, 자체 더블클릭, 사람 클릭 끄기(UserClickMode), 오른쪽 버튼 걷기
; 출처: ~/Downloads/5.99 클라이언트/Legend.exe · 맥 objdump, 오프셋 10진수
;
; 0x4eac12: 마우스 메시지(512 이동·513 왼쪽 누름·514 뗌·516 오른쪽 누름·517 뗌·522 휠)의 x 를 0~639 로 자른다. 515(왼쪽 더블클릭)는 받지 않는다.
; 0x4ae7f0 왼쪽 누름: 직전 누름이 남아 있고([+36]==1) 시간 차 < [+32](생성자 0x4aded8 에서 1000ms) 이고 |dx|+|dy| ≤ 2 이면
;   사건 종류 2(더블클릭), 아니면 1(클릭). 뗌=3, 오른쪽 누름=4·오른쪽 더블=5, 이동=0, 키=8. 사건 +24 = 버튼·수식키 비트
;   (1 Alt · 2 Ctrl · 4 Shift · 16 왼쪽 누름 · 32 오른쪽 누름). 0x4adee6 ShowCursor(0) — 커서는 게임이 그린다(mouse.epf).
; 0x51c65d 물체 클릭: 물체 종류 [+428]==1(사람)이고 Legend.cfg UserClickMode([cfg+0x53C]) > 0 이면 맞지 않은 것으로 친다.
; 0x51c6f2: 내 캐릭터를 (Shift 없이) 클릭 → 0x4c9510(0x2D 패킷, 내 정보). 0x51c7be: Ctrl+오른쪽 → 그 물체로 길찾기 걷기(0x5428d0).
; 0x53ffdf 지도: 이동 사건이고 오른쪽 버튼이 눌린 채(32)·Shift 아님이면 커서 칸까지 길찾아(0x540420) 걷기 시작(0x540c04).

  4aded8: mov dword ptr [esi + 32], 1000
  4adedf: mov byte ptr [esi + 36], al
  4adee2: mov dword ptr [esi + 48], eax
  4adee5: push eax
  4adee6: call dword ptr [8665756]   ;; ShowCursor
  ...
  4ae97b: mov edx, edi
  4ae97d: mov ecx, dword ptr [edx + 48]
  4ae980: neg ecx
  4ae982: add ecx, esi
  4ae984: cmp ecx, dword ptr [edx + 32]
  4ae987: jae 0x4aea33 <.text+0x87a33>
  4ae98d: mov ecx, dword ptr [edx + 20]
  4ae990: mov ebx, ecx
  4ae992: sub ebx, dword ptr [edx + 44]
  4ae995: mov dword ptr [ebp - 24], ebx
  4ae998: test ebx, ebx
  4ae99a: jle 0x4aea4a <.text+0x87a4a>
  4ae9a0: mov edx, edi
  4ae9a2: mov ebx, dword ptr [edx + 16]
  4ae9a5: mov edi, ebx
  4ae9a7: sub edi, dword ptr [edx + 40]
  4ae9aa: mov dword ptr [ebp - 28], edi
  4ae9ad: test edi, edi
  4ae9af: jle 0x4aea3e <.text+0x87a3e>
  4ae9b5: mov edx, dword ptr [ebp - 24]
  4ae9b8: add edx, dword ptr [ebp - 28]
  4ae9bb: cmp edx, 2
  4ae9be: jg 0x4ae857 <.text+0x87857>
  4ae9c4: mov edx, dword ptr [ebp - 32]
  4ae9c7: mov dword ptr [edx + 48], 0
  4ae9ce: mov byte ptr [ebp - 200], 2
  ...
  4eac12: movzx eax, bp
  4eac15: shr ebp, 16
  4eac18: mov dword ptr [esp + 20], ebp
  4eac1c: test eax, eax
  4eac1e: jl 0x4eac37 <.text+0xc3c37>
  4eac20: cmp eax, 640
  4eac25: jl 0x4eac31 <.text+0xc3c31>
  4eac27: mov dword ptr [esp + 24], 639
  4eac2f: jmp 0x4eac3f <.text+0xc3c3f>
  4eac31: mov dword ptr [esp + 24], eax
  4eac35: jmp 0x4eac3f <.text+0xc3c3f>
  4eac37: mov dword ptr [esp + 24], 0
  4eac3f: mov eax, dword ptr [7556684]
  ...
  51c65d: mov al, byte ptr [ebp + 428]
  51c663: cmp al, 1
  51c665: jne 0x51c683 <.text+0xf5683>
  51c667: mov edx, dword ptr [7556680]
  51c66d: mov esi, dword ptr [edx + 1340]
  51c673: test esi, esi
  51c675: jle 0x51c683 <.text+0xf5683>
  51c677: xor eax, eax
  51c679: add esp, 60
  51c67c: pop ebx
  51c67d: pop ebp
  51c67e: pop esi
  51c67f: pop edi
  51c680: ret 4
  ...
  51c6f2: jne 0x51c7a6 <.text+0xf57a6>
  51c6f8: mov ebp, dword ptr [esp + 80]
  51c6fc: mov al, byte ptr [ebp + 24]
  51c6ff: test al, 4
  51c701: jne 0x51c7b2 <.text+0xf57b2>
  51c707: mov ecx, dword ptr [6847168]
  51c70d: call 0x5e0800 <.text+0x1b9800>
  51c712: movzx edx, al
  51c715: test edx, edx
  51c717: jne 0x51c792 <.text+0xf5792>
  51c719: mov ecx, dword ptr [7556644]
  51c71f: call 0x4c9510 <.text+0xa2510>
  ...
  53ffd2: mov eax, dword ptr [7556636]
  53ffd7: test eax, eax
  53ffd9: je 0x54034c <.text+0x11934c>
  53ffdf: mov al, byte ptr [esi + 24]
  53ffe2: test al, 32
  53ffe4: je 0x53fef7 <.text+0x118ef7>
  53ffea: test al, 4
  53ffec: jne 0x53fef7 <.text+0x118ef7>
  53fff2: mov dword ptr [ebp - 84], ebx
