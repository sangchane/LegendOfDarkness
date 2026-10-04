; 메인 루프와 타이머 — Novaonline.exe (5.99 서버)
; 증명: main(0x47692f) 은 init(0x4764fa) 뒤 끝없이 [타이머 돌리기 0x412a7f → select 0x41068f → 보내기 0x410ab4 → Sleep(1)] 을 돈다.
;  타이머 등록 0x412980(fn, 1회여부, 주기ms, 인자): 노드 [+0]fn [+4]1회 [+8]다음시각=GetTickCount+주기 [+0xC]주기 [+0x18]인자.
;  돌리기 0x412a7f: GetTickCount 가 [+8] 을 넘으면 — 반복 타이머는 [+8]=지금+주기 로 다시 잡고(밀린 만큼 따라잡지 않는다) fn 호출, 1회 타이머는 호출 뒤 목록에서 뺀다.
;  select 대기 시간 = 등록된 주기 중 가장 짧은 것([0x487cbc], 10ms) (0x4106ac).
; 출처: objdump -d --x86-asm-syntax=intel, 오프셋 10진수. IAT: [4722768]=GetTickCount [4722744]=Sleep
;
; --- main
  47692f: push ebp
  476930: mov ebp, esp
  476932: push esi
  476933: call 0x4764fa <.text+0x754fa>
  476938: mov eax, 1
  47693d: test eax, eax
  47693f: je 0x476963 <.text+0x75963>
  476941: call 0x412a7f <.text+0x11a7f>
  476946: call 0x41068f <.text+0xf68f>
  47694b: call 0x410ab4 <.text+0xfab4>
  476950: mov esi, esp
  476952: push 1
  476954: call dword ptr [4722744]
  476961: jmp 0x476938 <.text+0x75938>
; --- 타이머 등록(일부): push 0 · 주기 · 0 · 함수주소 → call 0x412980
  476656: push 0
  476658: push 60
  47665a: push 0
  47665c: push 4677841
  476661: call 0x412980 <.text+0x11980>
  476666: add esp, 16
  476669: push 0
  47666b: push 250
  476670: push 0
  476672: push 4352018
  476677: call 0x412980 <.text+0x11980>
  47667c: add esp, 16
  47667f: push 0
  476681: push 1000
  476686: push 0
  476688: push 4352534
  47668d: call 0x412980 <.text+0x11980>
  476692: add esp, 16
  476695: push 0
  476697: push 10
  476699: push 0
  47669b: push 4642885
  4766a0: call 0x412980 <.text+0x11980>
; --- 0x412980: 노드 채우기
  4129e7: mov ecx, dword ptr [ebp - 4]
  4129ea: mov edx, dword ptr [ebp + 8]
  4129ed: mov dword ptr [ecx], edx
  4129ef: mov eax, dword ptr [ebp - 4]
  4129f2: mov ecx, dword ptr [ebp + 16]
  4129f5: mov dword ptr [eax + 12], ecx
  4129f8: mov esi, esp
  4129fa: call dword ptr [4722768]
  412a07: add eax, dword ptr [ebp + 16]
  412a0a: mov edx, dword ptr [ebp - 4]
  412a0d: mov dword ptr [edx + 8], eax
  412a10: mov eax, dword ptr [ebp - 4]
  412a13: mov ecx, dword ptr [ebp + 12]
  412a16: mov dword ptr [eax + 4], ecx
  412a19: mov edx, dword ptr [ebp - 4]
  412a1c: mov eax, dword ptr [ebp + 20]
  412a1f: mov dword ptr [edx + 24], eax
; --- 0x412a7f: 때가 된 타이머 부르기
  412aad: mov esi, esp
  412aaf: call dword ptr [4722768]
  412abc: mov ecx, dword ptr [ebp - 8]
  412abf: cmp dword ptr [ecx + 8], eax
  412ac2: jae 0x412bd2 <.text+0x11bd2>
  412ac8: mov edx, dword ptr [ebp - 8]
  412acb: cmp dword ptr [edx + 4], 0
  412acf: je 0x412b9f <.text+0x11b9f>
  412ad5: mov esi, esp
  412ad7: mov eax, dword ptr [ebp - 8]
  412ada: mov ecx, dword ptr [eax + 24]
  412add: push ecx
  412ade: mov edx, dword ptr [ebp - 8]
  412ae1: call dword ptr [edx]
  412ae3: add esp, 4
  412b9f: mov esi, esp
  412ba1: call dword ptr [4722768]
  412bae: mov ecx, dword ptr [ebp - 8]
  412bb1: add eax, dword ptr [ecx + 12]
  412bb4: mov edx, dword ptr [ebp - 8]
  412bb7: mov dword ptr [edx + 8], eax
  412bba: mov esi, esp
  412bbc: mov eax, dword ptr [ebp - 8]
  412bbf: mov ecx, dword ptr [eax + 24]
  412bc2: push ecx
  412bc3: mov edx, dword ptr [ebp - 8]
  412bc6: call dword ptr [edx]
  412bc8: add esp, 4
; --- 0x41068f: select 대기 시간 = [4750524] ms
  4106ac: mov eax, dword ptr [4750524]
  4106b1: xor edx, edx
  4106b3: mov ecx, 1000
  4106b8: div ecx
  4106ba: mov dword ptr [ebp - 304], eax
  4106c0: mov eax, dword ptr [4750524]
  4106c5: xor edx, edx
  4106c7: mov ecx, 1000
  4106cc: div ecx
  4106ce: imul edx, edx, 1000
  4106d4: mov dword ptr [ebp - 300], edx
