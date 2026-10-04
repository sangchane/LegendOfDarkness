; Novaonline.exe (5.99 서버) — 괴물 스폰·리스폰
; 0x40b053 스폰 줄 "맵,이름,수" 로더: 수만큼 20바이트 칸(+0 시각 +4 살아있음 +8 템플릿 +0xC/+0x10 목록)을 맵 +0x78 목록에 단다.
;   자리 = (rand%맵너비 +0x18, rand%맵높이 +0x1A), 막힌 칸(0x466a9e)이면 다시 굴림. 방향 = rand%4. 생성 0x422a73.
; 0x426a16 (1000ms 타이머, 0x47668d 등록): 죽은 칸(+4==0)이고 죽은시각 + 템플릿 젠타임(+0x44, 초) < time(0) 이면 같은 방식으로 다시 세운다.
40b209: mov ecx, dword ptr [ebp - 28]
40b20c: xor edx, edx
40b20e: mov dx, word ptr [ecx + 24]
40b212: mov ecx, edx
40b214: cdq
40b215: idiv ecx
40b217: mov word ptr [ebp - 4], dx
40b21b: call 0x476d82 <.text+0x75d82>
40b220: mov edx, dword ptr [ebp - 28]
40b223: xor ecx, ecx
40b225: mov cx, word ptr [edx + 26]
40b229: cdq
40b22a: idiv ecx
40b22c: mov word ptr [ebp - 2], dx
40b230: mov edx, dword ptr [ebp - 28]
40b233: xor eax, eax
40b235: mov ax, word ptr [edx + 24]
40b239: mov ecx, dword ptr [ebp - 2]
40b23c: and ecx, 65535
40b242: imul eax, ecx
40b245: mov edx, dword ptr [ebp - 4]
40b248: and edx, 65535
40b24e: add eax, edx
40b250: mov dword ptr [ebp - 16], eax
40b253: mov eax, dword ptr [ebp - 28]
; ...
40b2dc: push 0
40b2de: mov eax, dword ptr [ebp - 12]
40b2e1: push eax
40b2e2: mov ecx, dword ptr [ebp - 12]
40b2e5: add ecx, 4
40b2e8: push ecx
40b2e9: mov edx, dword ptr [ebp - 24]
40b2ec: push edx
40b2ed: call 0x476d82 <.text+0x75d82>
40b2f2: and eax, 2147483651
40b2f7: jns 0x40b2fe <.text+0xa2fe>
40b2f9: dec eax
40b2fa: or eax, -4
40b2fd: inc eax
40b2fe: push eax
40b2ff: lea eax, [ebp - 8]
40b302: push eax
40b303: call 0x422a73 <.text+0x21a73>
; ---- 리스폰 1초 타이머 0x426a16
426a59: mov eax, dword ptr [ebp - 12]
426a5c: xor ecx, ecx
426a5e: mov cl, byte ptr [eax + 4]
426a61: test ecx, ecx
426a63: jne 0x426bb5 <.text+0x25bb5>
426a69: mov edx, dword ptr [ebp - 12]
426a6c: mov eax, dword ptr [edx + 8]
426a6f: mov ecx, dword ptr [ebp - 12]
426a72: mov esi, dword ptr [ecx]
426a74: add esi, dword ptr [eax + 68]
426a77: push 0
426a79: call 0x477bae <.text+0x76bae>
426a81: cmp esi, eax
426a83: jge 0x426bb5 <.text+0x25bb5>
426a89: mov edx, dword ptr [ebp - 12]
426a8c: mov byte ptr [edx + 4], 1
426a90: push 0
426a92: call 0x477bae <.text+0x76bae>
426a9a: mov ecx, dword ptr [ebp - 12]
426a9d: mov dword ptr [ecx], eax
; ... (같은 자리 굴림) ...
426b7d: call 0x476d82 <.text+0x75d82>
426b82: and eax, 2147483651
426b87: jns 0x426b8e <.text+0x25b8e>
426b89: dec eax
426b8a: or eax, -4
426b8d: inc eax
426b8e: mov dword ptr [ebp - 8], eax
426b91: push 0
426b93: mov eax, dword ptr [ebp - 12]
426b96: push eax
426b97: mov ecx, dword ptr [ebp - 12]
426b9a: add ecx, 4
426b9d: push ecx
426b9e: mov edx, dword ptr [ebp - 12]
426ba1: mov eax, dword ptr [edx + 8]
426ba4: push eax
426ba5: mov ecx, dword ptr [ebp - 8]
426ba8: push ecx
426ba9: lea edx, [ebp - 24]
426bac: push edx
426bad: call 0x422a73 <.text+0x21a73>
