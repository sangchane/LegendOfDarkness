; 원작 2005 Legend.exe — 맵 파일 maps\lod%d.map
; 가로·세로는 파일에 없다(서버가 준 값으로 [+2]·[+4] 를 먼저 채운다). 파일 = 가로×세로×6바이트, 한 칸 = u16 3개.
; 칸 (x,y) 위치 = (y×가로 + x)×6. +0 바닥 타일, +2 왼벽, +4 오른벽. 크기가 모자라면 실패(→ 서버에 다시 받음).
; 파일은 읽기·쓰기로 열고(OPEN_ALWAYS) 받은 맵을 다시 써 둔다(0x52bcc0). 0x52bad0 은 맵 검사용 CRC16(표 0x6a1220).

; 칸 버퍼 마련: 가로×세로×6
52ba80  push ebp
52ba81  sub esp, 8
52ba84  mov ebp, ecx
52ba86  movzx edx, word ptr [esp + 20]
52ba8b  movzx eax, word ptr [esp + 16]
52ba90  mov word ptr [ebp + 2], ax
52ba94  mov word ptr [ebp + 4], dx
52ba98  mov ecx, dword ptr [ebp + 12]
52ba9b  test ecx, ecx
52ba9d  je 0x52baaf
52ba9f  mov dword ptr [esp], ecx
52baa2  call 0x66e5a8
52baa7  movzx eax, word ptr [ebp + 2]
52baab  movzx edx, word ptr [ebp + 4]
52baaf  lea ecx, [eax + eax]
52bab2  add ecx, eax
52bab4  add ecx, ecx
52bab6  imul edx, ecx
52bab9  mov dword ptr [esp], edx
52babc  call 0x66e8ca
52bac1  mov dword ptr [ebp + 12], eax
52bac4  add esp, 8
52bac7  pop ebp
52bac8  ret 8

; 바닥(+0) 읽기
52bb00  push esi
52bb01  push ebx
52bb02  mov esi, ecx
52bb04  mov edx, dword ptr [esp + 12]
52bb08  test edx, edx
52bb0a  jl 0x52bb3f
52bb0c  movzx ecx, word ptr [esi + 2]
52bb10  cmp edx, ecx
52bb12  jge 0x52bb3f
52bb14  mov ebx, dword ptr [esp + 16]
52bb18  test ebx, ebx
52bb1a  jl 0x52bb3f
52bb1c  movzx eax, word ptr [esi + 4]
52bb20  cmp ebx, eax
52bb22  jge 0x52bb3f
52bb24  mov eax, dword ptr [esi + 12]
52bb27  test eax, eax
52bb29  je 0x52bb3f
52bb2b  imul ebx, ecx
52bb2e  lea edx, [edx + ebx]
52bb31  lea ecx, [edx + edx]
52bb34  add ecx, edx
52bb36  movzx eax, word ptr [eax + 2*ecx]
52bb3a  pop ebx
52bb3b  pop esi
52bb3c  ret 8
52bb3f  xor eax, eax
52bb41  pop ebx
52bb42  pop esi
52bb43  ret 8

; 왼벽(+2) 읽기 — 오른벽(+4)은 0x52bba0 에서 같은 꼴로 +4
52bb7b  imul ebx, ecx
52bb7e  lea edx, [edx + ebx]
52bb81  lea ecx, [edx + edx]
52bb84  add ecx, edx
52bb86  movzx eax, word ptr [eax + 2*ecx + 2]
52bb8b  pop ebx
52bb8c  pop esi
52bb8d  ret 8

; 파일 열기·크기 검사·읽기
52bbf0  push esi
52bbf1  push ebx
52bbf2  sub esp, 148
52bbf8  mov esi, ecx
52bbfa  movzx eax, word ptr [esp + 160]
52bc02  mov word ptr [esi], ax
52bc05  push 0
52bc07  push 8880736   ;; "maps"
52bc0c  call dword ptr [8665088]   ;; CreateDirectoryA
52bc12  lea eax, [esp + 16]
52bc16  mov dword ptr [esp], eax
52bc19  mov dword ptr [esp + 4], 8880768   ;; "maps\lod%d.map"
52bc21  movzx edx, word ptr [esi]
52bc24  mov dword ptr [esp + 8], edx
52bc28  call 0x66ee00
52bc2d  lea edx, [esp + 16]
52bc31  xor eax, eax
52bc33  push eax
52bc34  push 268435584
52bc39  push 4
52bc3b  push eax
52bc3c  push eax
52bc3d  push 3221225472
52bc42  push edx
52bc43  call dword ptr [8665528]   ;; CreateFileA
52bc49  mov ecx, eax
52bc4b  mov dword ptr [esi + 8], ecx
52bc4e  cmp ecx, -1
52bc51  je 0x52bca1
52bc53  movzx ebx, word ptr [esi + 4]
52bc57  movzx edx, word ptr [esi + 2]
52bc5b  lea eax, [edx + edx]
52bc5e  add eax, edx
52bc60  add eax, eax
52bc62  imul ebx, eax
52bc65  push 0
52bc67  push ecx
52bc68  call dword ptr [8665512]   ;; GetFileSize
52bc6e  cmp eax, ebx
52bc70  jae 0x52bc7f
52bc72  xor eax, eax
52bc74  add esp, 148
52bc7a  pop ebx
52bc7b  pop esi
52bc7c  ret 4
52bc7f  mov edx, dword ptr [esi + 8]
52bc82  mov eax, dword ptr [esi + 12]
52bc85  lea esi, [esp + 144]
52bc8c  push 0
52bc8e  push esi
52bc8f  push ebx
52bc90  push eax
52bc91  push edx
52bc92  call dword ptr [8665516]   ;; ReadFile
52bc98  cmp ebx, dword ptr [esp + 144]
52bc9f  je 0x52bcae
52bca1  xor eax, eax
52bca3  add esp, 148
52bca9  pop ebx
52bcaa  pop esi
52bcab  ret 4
52bcae  mov eax, 1
52bcb3  add esp, 148
52bcb9  pop ebx
52bcba  pop esi
52bcbb  ret 4

; CRC16 한 바이트
52bad0  movzx edx, word ptr [esp + 8]
52bad5  movzx ecx, byte ptr [esp + 4]
52bada  mov eax, edx
52badc  sar eax, 8
52badf  movzx eax, word ptr [2*eax + 6951456]
52bae7  shl edx, 8
52baea  xor eax, edx
52baec  xor eax, ecx
52baee  movzx eax, ax
52baf1  ret 8
