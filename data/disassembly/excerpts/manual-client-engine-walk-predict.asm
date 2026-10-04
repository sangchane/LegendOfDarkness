; manual-client-engine-walk-predict — 원작 2005(=5.99) Legend.exe, 내 캐릭터(UserPane) 걷기 예측
; 방향키 → 앞칸 비었으면 0x06(방향, 걸음번호) 보내고 **바로** 걷기 시작(0x5c8eb4). 응답을 기다리지 않는다. 미응답 수 +0x10602 를 +1.
; 0x0B 응답: 미응답 수 -1, 서버 좌표(+0x18 x, +0x14 y)를 따로 적어 둘 뿐 내 그림을 되돌리지 않는다.
; 방향이 4(=움직이지 않음)로 오고 내 칸과 다르면 0x38(새로고침 요청)을 보낸다 → 서버가 0x04 로 자리를 다시 주면 그때 순간이동(0x5c9f84).
; --- 방향키 같은 방향: 앞칸 검사 → 0x06 보내기 → 바로 걷기
5c8d53  call 0x540860 <.text+0x119860>
5c8d58  movzx edx, al
5c8d5b  test edx, edx
5c8d5d  je 0x5c8d7d <.text+0x1a1d7d>
5c8d5f  movsx eax, byte ptr [esi + 516]
5c8d66  push eax
5c8d67  mov ecx, esi
5c8d69  call 0x5c8e34 <.text+0x1a1e34>
5c8d6e  movsx eax, byte ptr [esi + 516]
5c8d75  push eax
5c8d76  mov ecx, esi
5c8d78  call 0x5c8eb4 <.text+0x1a1eb4>
; --- 0x06 보내기: [06 방향 걸음번호], 보낸 시각 기록, 미응답 수 +1
5c8e34  push ebx
5c8e35  sub esp, 24
5c8e38  mov ebx, ecx
5c8e3a  mov al, byte ptr [esp + 32]
5c8e3e  mov byte ptr [esp + 8], 6
5c8e43  mov byte ptr [esp + 9], al
5c8e47  movzx edx, byte ptr [ebx + 67064]
5c8e4e  add edx, 1
5c8e51  mov byte ptr [ebx + 67064], dl
5c8e57  movzx eax, dl
5c8e5a  mov dword ptr [esp], eax
5c8e5d  lea eax, [esp + 10]
5c8e61  mov dword ptr [esp + 4], eax
5c8e65  call 0x5fe020 <.text+0x1d7020>
5c8e6a  call dword ptr [8666000]   ;; timeGetTime
5c8e70  mov dword ptr [ebx + 67068], eax
5c8e76  mov byte ptr [esp + 11], 0
5c8e7b  mov ecx, dword ptr [7556672]
5c8e81  lea edx, [esp + 8]
5c8e85  push 3
5c8e87  push edx
5c8e88  call 0x5fdf00 <.text+0x1d6f00>
5c8e8d  mov byte ptr [ebx + 67073], 0
5c8e94  mov al, byte ptr [ebx + 67074]
5c8e9a  add al, 1
5c8e9c  mov byte ptr [ebx + 67074], al
5c8ea2  add esp, 24
5c8ea5  pop ebx
5c8ea6  ret 4
5c8eb0  mov eax, dword ptr [esp + 4]
; --- 0x0B 처리
5c9fe4  push esi
5c9fe5  push ebp
5c9fe6  push ebx
5c9fe7  sub esp, 16
5c9fea  mov esi, ecx
5c9fec  mov edx, dword ptr [esp + 32]
5c9ff0  mov al, byte ptr [edx + 16]
5c9ff3  mov byte ptr [esp + 12], al
5c9ff7  movsx ebx, word ptr [edx + 18]
5c9ffb  movsx ebp, word ptr [edx + 20]
5c9fff  call dword ptr [8666000]   ;; timeGetTime
5ca005  mov dword ptr [esi + 67076], eax
5ca00b  mov dword ptr [esi + 67084], ebx
5ca011  mov dword ptr [esi + 67080], ebp
5ca017  mov al, byte ptr [esi + 67074]
5ca01d  test al, al
5ca01f  jbe 0x5ca029 <.text+0x1a3029>
5ca021  dec al
5ca023  mov byte ptr [esi + 67074], al
5ca029  mov byte ptr [esi + 67073], 0
5ca030  mov al, byte ptr [esi + 67064]
5ca036  mov byte ptr [esi + 67065], al
5ca03c  call dword ptr [8666000]   ;; timeGetTime
5ca042  sub eax, dword ptr [esi + 67068]
5ca048  mov dword ptr [esi + 67120], eax
5ca04e  mov al, byte ptr [esp + 12]
5ca052  cmp al, 4
5ca054  je 0x5ca09f <.text+0x1a309f>
5ca056  mov byte ptr [esp], al
5ca059  call 0x540820 <.text+0x119820>
5ca05e  add dword ptr [esi + 67084], edx
5ca064  add dword ptr [esi + 67080], eax
5ca06a  mov al, byte ptr [esi + 67074]
5ca070  test al, al
5ca072  jne 0x5ca07b <.text+0x1a307b>
5ca074  mov byte ptr [esi + 67073], 1
5ca07b  mov ecx, dword ptr [6865472]
5ca081  test ecx, ecx
5ca083  je 0x5ca08a <.text+0x1a308a>
5ca085  call 0x474e90 <.text+0x4de90>
5ca08a  mov ecx, esi
5ca08c  call 0x5c9334 <.text+0x1a2334>
5ca091  mov eax, 1
5ca096  add esp, 16
5ca099  pop ebx
5ca09a  pop ebp
5ca09b  pop esi
5ca09c  ret 4
5ca09f  lea eax, [esi + 88]
5ca0a2  push eax
5ca0a3  mov edx, dword ptr [esi]
5ca0a5  mov eax, dword ptr [edx + 136]
5ca0ab  mov ecx, esi
5ca0ad  call eax
5ca0af  mov eax, dword ptr [esi + 436]
5ca0b5  cmp ebx, eax
5ca0b7  jne 0x5ca0d0 <.text+0x1a30d0>
5ca0b9  mov ebx, dword ptr [esi + 440]
5ca0bf  cmp ebp, ebx
5ca0c1  jne 0x5ca0d0 <.text+0x1a30d0>
5ca0c3  mov edx, dword ptr [esi + 520]
; --- 0x38 새로고침 요청 (위치가 어긋났을 때)
5c89b4  sub esp, 140
5c89ba  mov dword ptr [esp], 56
5c89c1  lea eax, [esp + 8]
5c89c5  mov dword ptr [esp + 4], eax
5c89c9  call 0x5fe020 <.text+0x1d7020>
5c89ce  mov byte ptr [esp + 9], 0
5c89d3  mov ecx, dword ptr [7556672]
5c89d9  lea eax, [esp + 8]
5c89dd  push 1
5c89df  push eax
5c89e0  call 0x5fdf00 <.text+0x1d6f00>
; --- 0x04 내 위치: 그 자리로 바로 옮김
5c9f84  push edi
5c9f85  push esi
5c9f86  push ebx
5c9f87  mov esi, ecx
5c9f89  mov eax, dword ptr [esp + 16]
5c9f8d  movsx edi, word ptr [eax + 16]
5c9f91  movsx ebx, word ptr [eax + 18]
5c9f95  mov dl, 0
5c9f97  mov byte ptr [esi + 67064], dl
5c9f9d  mov byte ptr [esi + 67065], dl
5c9fa3  push ebx
5c9fa4  push edi
5c9fa5  mov ecx, esi
5c9fa7  call 0x5c9884 <.text+0x1a2884>
5c9fac  mov dword ptr [esi + 67084], edi
