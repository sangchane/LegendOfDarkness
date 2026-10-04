; 원작 2005 Legend.exe — 벽 통과 판정(SOTP.DAT) · 벽 그리기 깃발
; SOTP.DAT(ia.dat 안, 벽 번호당 1바이트)를 표[1..] 에 두 번 읽는다: 아랫 4비트만 남긴 표(0x766fe8)=막힘, 윗 4비트 표(0x766fe0)=그리기 깃발.
; 칸 판정 0x53e2f0: 왼벽·오른벽 번호(10000 은 없음=0)로 표를 OR → 0x0F(=01|02|04|08, 0x6a2280) 와 같으면 1(사방 막힘), 아니면 0. 실제 걷기는 아래 0x540860 의 방향별 비트 검사.
; 범위 밖은 4, 그 칸의 사람·괴물은 2·3·6·7 로 따로 돌려준다. 벽 그리기: 윗비트 0x80 → 모드 109, 0x40 → 모드 3.

; SOTP.DAT 읽기 1 — 윗 4비트만 남김(and 0xF0)
53d7a7  call 0x4bdb80
53d7ac  push 8897152   ;; "SOTP.DAT"
53d7b1  mov ecx, eax
53d7b3  call 0x4bd600
53d7b8  mov ebx, eax
53d7ba  call 0x4bdb80
53d7bf  push ebx
53d7c0  mov ecx, eax
53d7c2  call 0x4bdb60
53d7c7  mov dword ptr [7761892], eax
53d7cc  mov ecx, dword ptr [6847040]
53d7d2  add eax, 1
53d7d5  push eax
53d7d6  call 0x554690
53d7db  mov dword ptr [7761888], eax
53d7e0  call 0x4bdb80
53d7e5  mov esi, dword ptr [7761888]
53d7eb  add esi, 1
53d7ee  mov edx, dword ptr [7761892]
53d7f4  push edx
53d7f5  push esi
53d7f6  push ebx
53d7f7  mov ecx, eax
53d7f9  call 0x4bd800
53d7fe  call 0x4bdb80
53d803  push ebx
53d804  mov ecx, eax
53d806  call 0x4bd7d0
53d80b  xor ecx, ecx
53d80d  mov eax, dword ptr [7761892]
53d812  add eax, 1
53d815  test eax, eax
53d817  jle 0x53d435
53d81d  mov eax, dword ptr [7761888]
53d822  and byte ptr [eax + ecx], -16
53d826  add ecx, 1
53d829  mov edx, dword ptr [7761892]
53d82f  add edx, 1
53d832  cmp ecx, edx
53d834  jl 0x53d81d
53d836  jmp 0x53d435

; SOTP.DAT 읽기 2 — 아랫 4비트만 남김(and 0x0F)
53d83b  call 0x4bdb80
53d840  push 8897152   ;; "SOTP.DAT"
53d845  mov ecx, eax
53d847  call 0x4bd600
53d84c  mov ebx, eax
53d84e  call 0x4bdb80
53d853  push ebx
53d854  mov ecx, eax
53d856  call 0x4bdb60
53d85b  mov dword ptr [7761900], eax
53d860  mov ecx, dword ptr [6847040]
53d866  add eax, 1
53d869  push eax
53d86a  call 0x554690
53d86f  mov dword ptr [7761896], eax
53d874  call 0x4bdb80
53d879  mov esi, dword ptr [7761896]
53d87f  add esi, 1
53d882  mov edx, dword ptr [7761900]
53d888  push edx
53d889  push esi
53d88a  push ebx
53d88b  mov ecx, eax
53d88d  call 0x4bd800
53d892  call 0x4bdb80
53d897  push ebx
53d898  mov ecx, eax
53d89a  call 0x4bd7d0
53d89f  xor ecx, ecx
53d8a1  mov eax, dword ptr [7761900]
53d8a6  add eax, 1
53d8a9  test eax, eax
53d8ab  jle 0x53d428
53d8b1  mov eax, dword ptr [7761896]
53d8b6  and byte ptr [eax + ecx], 15
53d8ba  add ecx, 1
53d8bd  mov edx, dword ptr [7761900]
53d8c3  add edx, 1
53d8c6  cmp ecx, edx
53d8c8  jl 0x53d8b1
53d8ca  jmp 0x53d428

; 이동 판정 — 범위·사람·벽
53e323  movsx eax, word ptr [esp + 24]
53e328  test eax, eax
53e32a  jl 0x53e356
53e32c  lea esi, [ebx + 484]
53e332  movzx eax, word ptr [ebx + 486]
53e339  movsx edx, word ptr [esp + 24]
53e33e  cmp edx, eax
53e340  jge 0x53e356
53e342  movsx ebp, word ptr [esp + 28]
53e347  test ebp, ebp
53e349  jl 0x53e356
53e34b  movzx eax, word ptr [ebx + 488]
53e352  cmp ebp, eax
53e354  jl 0x53e365
53e356  mov eax, 4
53e35b  add esp, 4
53e35e  pop ebx
53e35f  pop ebp
53e360  pop esi
53e361  pop edi
53e362  ret 8
53e365  mov ecx, dword ptr [ebx + 624]
53e3c1  push ebp
53e3c2  movsx eax, word ptr [esp + 28]
53e3c7  push eax
53e3c8  mov ecx, esi
53e3ca  call 0x52bb50
53e3cf  mov ebx, eax
53e3d1  push ebp
53e3d2  movsx edx, word ptr [esp + 28]
53e3d7  push edx
53e3d8  mov ecx, esi
53e3da  call 0x52bba0
53e3df  movzx edx, bx
53e3e2  cmp edx, 10000
53e3e8  je 0x53e43a
53e3ea  movzx edx, ax
53e3ed  cmp edx, 10000
53e3f3  je 0x53e436
53e3f5  mov ebp, dword ptr [7761896]
53e3fb  movzx ebx, bx
53e3fe  mov dl, byte ptr [ebx + ebp]
53e401  movzx eax, ax
53e404  or dl, byte ptr [eax + ebp]
53e407  mov cl, byte ptr [6955650]
53e40d  or cl, byte ptr [6955648]
53e413  or cl, byte ptr [6955651]
53e419  or cl, byte ptr [6955649]
53e41f  cmp dl, cl
53e421  jne 0x53e317
53e427  mov eax, 1
53e42c  add esp, 4
53e42f  pop ebx
53e430  pop ebp
53e431  pop esi
53e432  pop edi
53e433  ret 8
53e436  xor eax, eax
53e438  jmp 0x53e3f5
53e43a  xor ebx, ebx
53e43c  jmp 0x53e3ea

; 벽 오브젝트 만들 때 윗비트 깃발 (0x5d3180 안)
5d3288  mov dword ptr [edx + 332], 1
5d3292  push esp
5d3293  mov ecx, dword ptr [ebp - 24]
5d3296  push ecx
5d3297  call 0x53ce50
5d329c  add esp, 8
5d329f  movzx edx, al
5d32a2  test edx, 128
5d32a8  je 0x5d32b9
5d32aa  mov ecx, dword ptr [ebp - 44]
5d32ad  mov dword ptr [ecx + 332], 109
5d32b7  jmp 0x5d32e0
5d32b9  push esp
5d32ba  mov eax, dword ptr [ebp - 24]
5d32bd  push eax
5d32be  call 0x53ce50
5d32c3  add esp, 8
5d32c6  movzx edx, al
5d32c9  test dl, 64
5d32cc  je 0x5d32dd
5d32ce  mov ecx, dword ptr [ebp - 44]
5d32d1  mov dword ptr [ecx + 332], 3
5d32db  jmp 0x5d32e0
5d32dd  mov ecx, dword ptr [ebp - 44]

; ── 걷기 앞칸 판정 0x540860 (내 방향키 걷기 0x5c8d53 가 부름) — 방향별 비트 ──
; 나가는 칸의 왼·오른벽, 들어가는 칸의 왼·오른벽 번호 4개를 모은다(0x5d1800, 벽 오브젝트 [+0x1FA]).
; 나가는 칸 벽은 표 0x6a228c[방향] = 04 08 01 02, 들어가는 칸 벽은 0x6a2280[방향] = 01 02 04 08 과 AND — 하나라도 겹치면 0(못 감).
; 즉 SOTP 아랫 4비트 = 방향별 막힘 비트, 0x0F 는 사방 막힘. 벽 번호가 표 크기 이상이면 검사 생략.

; 벽 번호 4개 모으기
540957  xor edi, edi
540959  xor eax, eax
54095b  mov dword ptr [esp + 20], eax
54095f  xor eax, eax
540961  mov dword ptr [esp + 16], eax
540965  xor eax, eax
540967  mov dword ptr [esp + 12], eax
54096b  mov ecx, dword ptr [esi + 624]
540971  push 1
540973  mov eax, dword ptr [esp + 56]
540977  push eax
540978  mov eax, dword ptr [esp + 56]
54097c  push eax
54097d  call 0x5d1800
540982  test eax, eax
540984  je 0x5409a4
540986  mov ecx, dword ptr [esi + 624]
54098c  push 1
54098e  mov edi, dword ptr [esp + 56]
540992  push edi
540993  mov edi, dword ptr [esp + 56]
540997  push edi
540998  call 0x5d1800
54099d  movzx edi, word ptr [eax + 506]
5409a4  mov ecx, dword ptr [esi + 624]
5409aa  push 0
5409ac  mov eax, dword ptr [esp + 56]
5409b0  push eax
5409b1  mov eax, dword ptr [esp + 56]
5409b5  push eax
5409b6  call 0x5d1800
5409bb  test eax, eax
5409bd  je 0x5409e1
5409bf  mov ecx, dword ptr [esi + 624]
5409c5  push 0
5409c7  mov eax, dword ptr [esp + 56]
5409cb  push eax
5409cc  mov eax, dword ptr [esp + 56]
5409d0  push eax
5409d1  call 0x5d1800
5409d6  movzx eax, word ptr [eax + 506]
5409dd  mov dword ptr [esp + 20], eax
5409e1  mov ecx, dword ptr [esi + 624]
5409e7  push 1
5409e9  push ebx
5409ea  push ebp
5409eb  call 0x5d1800
5409f0  test eax, eax
5409f2  je 0x540a0e
5409f4  mov ecx, dword ptr [esi + 624]
5409fa  push 1
5409fc  push ebx
5409fd  push ebp
5409fe  call 0x5d1800
540a03  movzx eax, word ptr [eax + 506]
540a0a  mov dword ptr [esp + 16], eax
540a0e  mov ecx, dword ptr [esi + 624]
540a14  push 0
540a16  push ebx
540a17  push ebp
540a18  call 0x5d1800
540a1d  test eax, eax
540a1f  je 0x540a3b
540a21  mov ecx, dword ptr [esi + 624]
540a27  push 0
540a29  push ebx
540a2a  push ebp
540a2b  call 0x5d1800
540a30  movzx edx, word ptr [eax + 506]
540a37  mov dword ptr [esp + 12], edx
540a3b  mov edx, dword ptr [7761900]

; 방향 비트 AND
540a3b  mov edx, dword ptr [7761900]
540a41  cmp edx, edi
540a43  jge 0x540a69
540a45  mov eax, dword ptr [esp + 20]
540a49  cmp edx, eax
540a4b  jge 0x540a69
540a4d  mov eax, dword ptr [esp + 16]
540a51  cmp edx, eax
540a53  jge 0x540a69
540a55  mov eax, dword ptr [esp + 12]
540a59  cmp edx, eax
540a5b  jge 0x540a69
540a5d  xor eax, eax
540a5f  add esp, 28
540a62  pop ebx
540a63  pop ebp
540a64  pop esi
540a65  pop edi
540a66  ret 12
540a69  mov edx, dword ptr [7761896]
540a6f  movsx eax, byte ptr [esp + 56]
540a74  mov al, byte ptr [eax + 6955660]
540a7a  mov cl, byte ptr [edi + edx]
540a7d  test al, cl
540a7f  jne 0x540ab0
540a81  mov ebp, dword ptr [esp + 20]
540a85  mov cl, byte ptr [ebp + edx]
540a89  test al, cl
540a8b  jne 0x540ab0
540a8d  movsx eax, byte ptr [esp + 56]
540a92  mov al, byte ptr [eax + 6955648]
540a98  mov ebp, dword ptr [esp + 16]
540a9c  mov cl, byte ptr [ebp + edx]
540aa0  test al, cl
540aa2  jne 0x540ab0
540aa4  mov ebp, dword ptr [esp + 12]
540aa8  mov dl, byte ptr [ebp + edx]
540aac  test al, dl
540aae  je 0x540af3
540ab0  xor eax, eax
540ab2  add esp, 28
540ab5  pop ebx
540ab6  pop ebp
540ab7  pop esi
540ab8  pop edi
540ab9  ret 12
